namespace ActionPlatform.Core.Services.Actions;

/// <summary>北京时间偏移（A 股交易时间的基准时区，UTC+8，中国无夏令时）。</summary>
public static class ChinaTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    /// <summary>任意时刻 → 北京时间（UTC+8，固定偏移，不依赖运行主机时区）。</summary>
    public static DateTime ToBeijing(DateTimeOffset t) => t.UtcDateTime + Offset;

    /// <summary>任意时刻的北京时间时刻（TimeOnly）。</summary>
    public static TimeOnly ToBeijingTimeOnly(DateTimeOffset t) => TimeOnly.FromDateTime(ToBeijing(t));

    /// <summary>任意时刻的北京日期（DateOnly）。</summary>
    public static DateOnly ToBeijingDate(DateTimeOffset t) => DateOnly.FromDateTime(ToBeijing(t));
}

/// <summary>
/// Action 抽象基类：统一封装触发模式与执行状态。
/// 调度循环 <see cref="ActionLoop"/> 轮询所有已注册 Action，先调用判断方法 <see cref="CanExecuteAsync"/>，
/// 返回 true 时执行干活方法 <see cref="ExecuteAsync"/>。
/// 子类只需声明 <see cref="TriggerMode"/> 及对应参数（<see cref="Interval"/> / <see cref="ScheduledTime"/>）并实现干活方法；
/// 判断方法按需重写（建议先调用 base 叠加业务条件）。
/// </summary>
public abstract class ActionBase
{
    /// <summary>Action 唯一标识（日志用）。</summary>
    public abstract string Name { get; }

    /// <summary>是否启用：false 时调度循环跳过该 Action，不执行判断与干活方法。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>触发模式（默认间隔触发）。</summary>
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Interval;

    /// <summary>间隔触发：两次触发的最小间隔。</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>每日定时触发：每天北京时间指定时刻触发。</summary>
    public TimeOnly ScheduledTime { get; set; }

    /// <summary>是否在周末（北京周六/周日）触发：默认 false —— 定时 Action 面向 A 股交易日，周末不触发。仅 Scheduled 模式生效。</summary>
    public bool RunsOnWeekends { get; set; }

    /// <summary>执行失败后允许下次重试的最小间隔（防止失败后立即忙循环重试；成功执行不受影响）。</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 每日定时模式「今日是否已完成」：true = 今天已确认完成、不应再次触发（跨北京日期自动重置）。
    /// 语义由子类声明而非执行结果推断 —— 子类在 <see cref="ExecuteAsync"/> 中到达业务完成点（如推送成功）时调用
    /// <see cref="MarkDailyDone"/>；执行失败（异常）不置位，当天仍可按 <see cref="RetryDelay"/> 重试。
    /// GitHub Actions 重启链场景下置位会经 <see cref="IDailyFireStateStore"/> 跨进程持久化，重启后的新进程自动恢复该标记。
    /// Interval 模式忽略。
    /// </summary>
    public bool DailyDoneToday => _doneBeijingDate == ChinaTime.ToBeijingDate(DateTimeOffset.Now);

    /// <summary>
    /// 今日（北京时间）是否已成功执行过（进程内通用成功锁，跨北京日期自动重置）。
    /// 由调度循环在 <see cref="ExecuteAsync"/> 正常返回后自动置位 —— 任何定时 Action 即使未调用 <see cref="MarkDailyDone"/>
    /// 也不会在进程内反复触发（防呆兜底：成功一次后当日不再自动重触发，除非显式声明完成/失败重试）。
    /// </summary>
    public bool SucceededToday => _lastSuccessBeijingDate == ChinaTime.ToBeijingDate(DateTimeOffset.Now);

    /// <summary>业务显式完成标记对应的北京日期（null = 尚未完成过；含重启链下从存储恢复的值）。</summary>
    private DateOnly? _doneBeijingDate;

    /// <summary>最近一次成功执行的批准北京日期（null = 尚未成功过）。按批准日而非完成时刻记录，跨 0 点完成不会误锁次日。</summary>
    private DateOnly? _lastSuccessBeijingDate;

    /// <summary>本次执行批准时的北京日期（调度循环调用 <see cref="MarkExecuted"/> 时冻结）；执行完成跨 0 点时按它落锁。</summary>
    private DateOnly? _startedBeijingDate;

    /// <summary>跨进程日锁存储（重启链下由调度循环装配；null = 未装配，仅进程内去重）。</summary>
    private IDailyFireStateStore? _dailyStateStore;

    /// <summary>上次成功执行开始时间（本地时间，间隔模式计时用）。</summary>
    protected DateTimeOffset LastTriggeredAt { get; private set; }

    /// <summary>上次执行失败时间（本地时间）；null 表示最近一次执行未失败。</summary>
    protected DateTimeOffset? LastFailedAt { get; private set; }

    /// <summary>当前是否正在执行（执行中不重复触发）。</summary>
    protected bool IsRunning { get; private set; }

    /// <summary>
    /// 判断方法：基类按触发模式完成默认判断，子类可重写叠加业务条件。
    /// 定时模式统一按北京时间（UTC+8）判断：CI（GitHub Actions runner 本地时区为 UTC）与本地（东八区）行为一致，
    /// 保证 ScheduledTime（如 09:25 集合竞价）在任何运行环境下都在同一时刻触发。
    /// </summary>
    /// <param name="now">当前时间（调度循环每轮传入）。</param>
    /// <param name="ct">取消令牌。</param>
    protected internal virtual Task<bool> CanExecuteAsync(DateTimeOffset now, CancellationToken ct)
    {
        // 执行中一律不触发（防重入）
        if (IsRunning)
        {
            return Task.FromResult(false);
        }

        var canExecute = TriggerMode switch
        {
            // 间隔触发：距上次成功执行（或启动）已超过间隔时长（时间差与主机时区无关）
            TriggerMode.Interval => now - LastTriggeredAt >= Interval && IsRetryAllowed(now),

            // 每日定时：北京时间已到指定时刻、当日尚未完成、且通过失败退避。
            // 「当日完成」双保险：成功执行一次（_lastSuccessBeijingDate，进程内防呆，任何子类无需额外代码即不会连发）
            // 或子类业务显式声明完成（MarkDailyDone，含重启链跨进程日锁）。均按北京日比较、跨日自动重置。
            // 执行失败不置位，当天按 RetryDelay 间隔自动重试。周末默认不触发（RunsOnWeekends=true 可开启）。
            TriggerMode.Scheduled => ChinaTime.ToBeijingTimeOnly(now) >= ScheduledTime
                                     && IsTradingDay(now)
                                     && _lastSuccessBeijingDate != ChinaTime.ToBeijingDate(now)
                                     && _doneBeijingDate != ChinaTime.ToBeijingDate(now)
                                     && IsRetryAllowed(now),
            _ => false,
        };
        return Task.FromResult(canExecute);
    }

    /// <summary>周末过滤（Scheduled 模式）：北京周六/周日不触发（<see cref="RunsOnWeekends"/> = true 时放行）。</summary>
    private bool IsTradingDay(DateTimeOffset now)
        => RunsOnWeekends || ChinaTime.ToBeijingDate(now).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>干活方法：子类必须实现（「可在子类里进行重写」的强制形式）。</summary>
    protected internal abstract Task ExecuteAsync(CancellationToken ct);

    /// <summary>调度循环调用：标记开始执行（冻结本次批准的北京日期，供成功后按批准日落锁；跨 0 点完成不会误锁次日）。</summary>
    internal void MarkExecuted(DateTimeOffset now)
    {
        IsRunning = true;
        _startedBeijingDate = ChinaTime.ToBeijingDate(now);
    }

    /// <summary>调度循环调用：标记执行成功（间隔模式据此计时；按批准日记录成功锁；清除失败标记）。</summary>
    internal void MarkSucceeded()
    {
        LastTriggeredAt = DateTimeOffset.Now;
        _lastSuccessBeijingDate = _startedBeijingDate ?? ChinaTime.ToBeijingDate(DateTimeOffset.Now);
        LastFailedAt = null;
    }

    /// <summary>
    /// 子类确认「今日工作已完成」后调用（如三一票在推送成功后、或确认当日无数据后）：
    /// 进程内置位（当日不再触发，跨北京日自动重置）+ 跨进程日锁落盘（重启链下去重）。
    /// 按本次执行批准日（<see cref="MarkExecuted"/> 冻结）落锁而非完成时刻，避免跨 0 点完成把次日误记为完成。
    /// 落盘失败不抛出 —— 本次置位已生效；若抛出会被调度循环判为执行失败而再次执行，造成已完成内容重复推送。
    /// </summary>
    protected void MarkDailyDone()
    {
        _doneBeijingDate = _startedBeijingDate ?? ChinaTime.ToBeijingDate(DateTimeOffset.Now);
        try
        {
            _dailyStateStore?.RecordFired(Name, _doneBeijingDate.Value);
        }
        catch (Exception)
        {
            // 跨进程日锁落盘失败：仅影响重启后新进程的去重（最坏当天重复推送一次），不影响本次进程内完成状态
        }
    }

    /// <summary>调度循环装配：注入跨进程日锁存储，并恢复最近完成日期（重启链场景下新进程据此跳过当天已完成任务；昨日记录自然失效）。</summary>
    internal void AttachDailyFireState(IDailyFireStateStore store)
    {
        _dailyStateStore = store;
        _doneBeijingDate = store.LastFiredBeijingDate(Name);
    }

    /// <summary>调度循环调用：标记执行失败（记录失败时间，供 <see cref="RetryDelay"/> 退避）。</summary>
    internal void MarkFailed()
    {
        LastFailedAt = DateTimeOffset.Now;
    }

    /// <summary>调度循环调用：标记执行结束（无论成败）。</summary>
    internal void MarkCompleted()
    {
        IsRunning = false;
    }

    /// <summary>失败退避：最近一次失败距今不足 <see cref="RetryDelay"/> 时不触发（避免失败后每轮轮询立即重试形成忙循环）。</summary>
    private bool IsRetryAllowed(DateTimeOffset now)
        => LastFailedAt is null || now - LastFailedAt >= RetryDelay;
}
