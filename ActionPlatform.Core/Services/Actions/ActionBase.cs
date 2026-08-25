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

    /// <summary>每日定时触发：每天本地时区触发时刻（当天只触发一次）。</summary>
    public TimeOnly ScheduledTime { get; set; }

    /// <summary>执行失败后允许下次重试的最小间隔（防止失败后立即忙循环重试；成功执行不受影响）。</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>上次成功执行开始时间（本地时间）；仅成功执行后更新，失败不占用当天触发额度。</summary>
    protected DateTimeOffset LastTriggeredAt { get; private set; }

    /// <summary>上次执行失败时间（本地时间）；null 表示最近一次执行未失败。</summary>
    protected DateTimeOffset? LastFailedAt { get; private set; }

    /// <summary>当前是否正在执行（执行中不重复触发）。</summary>
    protected bool IsRunning { get; private set; }

    /// <summary>
    /// 判断方法：基类按触发模式完成默认判断，子类可重写叠加业务条件（如「仅交易日执行」）。
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

            // 每日定时：北京时间已到指定时刻，且北京日期当日尚未成功执行过（LastTriggeredAt 的北京日期 != 今天）；
            // 执行失败不占用当天额度（LastTriggeredAt 仅在成功后更新），按 RetryDelay 间隔自动重试
            TriggerMode.Scheduled => ChinaTime.ToBeijingTimeOnly(now) >= ScheduledTime
                                     && ChinaTime.ToBeijingDate(LastTriggeredAt) != ChinaTime.ToBeijingDate(now)
                                     && IsRetryAllowed(now),
            _ => false,
        };
        return Task.FromResult(canExecute);
    }

    /// <summary>干活方法：子类必须实现（「可在子类里进行重写」的强制形式）。</summary>
    protected internal abstract Task ExecuteAsync(CancellationToken ct);

    /// <summary>调度循环调用：标记开始执行（置执行中标志；记录时间推迟到成功后，失败不占用触发额度）。</summary>
    internal void MarkExecuted()
    {
        IsRunning = true;
    }

    /// <summary>调度循环调用：标记执行成功（成功才记录执行时间并清除失败标记）。</summary>
    internal void MarkSucceeded()
    {
        LastTriggeredAt = DateTimeOffset.Now;
        LastFailedAt = null;
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
