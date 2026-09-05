using ActionPlatform.Core.Services;
using ActionPlatform.Core.Services.Actions;
using ActionPlatform.Core.Services.Messaging;
using ActionPlatform.Core.Services.StockData;
using Microsoft.Extensions.Logging;

namespace ActionPlatform;

/// <summary>
/// 应用演示类：演示 IOC 构造函数注入与日志、消息服务、Action 调度循环的组合使用。
/// </summary>
internal sealed class App
{
    /// <summary>完成感知自退循环的检查间隔（GitHub Actions 重启链：当日工作完成即退出，由下一班调度接力）。</summary>
    private static readonly TimeSpan CompletionCheckInterval = TimeSpan.FromSeconds(15);

    private readonly ILogger<App> _logger;
    private readonly IMessageNotifier _notifier;
    private readonly ActionLoop _actionLoop;
    private readonly IReadOnlyList<ActionBase> _actions;

    public App(ILogger<App> logger, IMessageNotifier notifier, ActionLoop actionLoop,
        IEnumerable<ActionBase> actions)
    {
        _logger = logger;
        _notifier = notifier;
        _actionLoop = actionLoop;
        _actions = actions.ToArray();
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("应用启动，IOC 容器装配完成");

        // Debug 级别日志：验证 nlog.config 级别过滤（minlevel=Info，不应写入日志文件）
        _logger.LogDebug("这是一条 Debug 日志，不应出现在日志文件中");

        // GitHub Actions 重启链每 2 小时重启一班进程，启动通知会造成飞书刷屏 → 链模式下由 workflow 置 SUPPRESS_STARTUP_NOTIFY=1 跳过
        if (Environment.GetEnvironmentVariable("SUPPRESS_STARTUP_NOTIFY") != "1")
        {
            await _notifier.SendAsync("ActionPlatform 启动通知", "IOC / 日志 / 消息服务已就绪。", CancellationToken.None);
        }

        // 启动 Action 调度循环：常驻轮询所有已启用 Action（间隔日志演示每 5 秒触发一次）
        await _actionLoop.StartAsync(CancellationToken.None);
        _logger.LogInformation("Action 调度循环已启动，按任意键退出...");

        // 阻塞保持进程存活，让调度循环持续运行（退出前停止循环）。
        // 交互环境：等按键退出。
        // 非交互环境（CI/输入重定向）：不能使用 Console.ReadKey（无控制台时抛 InvalidOperationException）。
        if (!Console.IsInputRedirected)
        {
            Console.ReadKey();
        }
        else
        {
            await WaitUntilDoneOrHardLimitAsync();
        }

        await _actionLoop.StopAsync();
        _logger.LogInformation("Action 调度循环已停止，应用退出");
    }

    /// <summary>
    /// 非交互（GitHub Actions 重启链）存活策略：本班窗口内已无「仍可能触发」的定时 Action → 自然退出，由下一班调度接力；
    /// 否则等到 APP_RUN_SECONDS 硬限退出（默认持续到进程被外部终止）。
    /// 「仍可能触发」包括：当日已完成/成功过（进程内锁 + 跨进程日锁恢复）、周末不跑（<see cref="ActionBase.RunsOnWeekends"/>）、
    /// 或触发时刻落在本班硬限之外 —— 周末/非交易时段班次据此数秒内退出，不再空转整个运行窗口。
    /// 无已启用定时 Action 时（纯间隔演示）退化为单纯按硬限等待。
    /// </summary>
    private async Task WaitUntilDoneOrHardLimitAsync()
    {
        var runSeconds = int.TryParse(Environment.GetEnvironmentVariable("APP_RUN_SECONDS"), out var s) ? s : 0;
        var deadline = runSeconds > 0 ? (DateTimeOffset?)DateTimeOffset.Now.AddSeconds(runSeconds) : null;
        if (deadline is not null)
        {
            _logger.LogInformation("非交互环境运行：硬限 {Deadline:HH:mm:ss}（{Seconds} 秒）内无待办定时任务则提前退出", deadline, runSeconds);
        }

        while (true)
        {
            var pending = PendingScheduledNames(_actions, DateTimeOffset.Now, deadline);
            if (pending.Count == 0)
            {
                _logger.LogInformation("本班窗口内已无待触发的定时 Action，自然退出（等待 GitHub Actions 下一班调度接力）");
                return;
            }
            var names = string.Join("、", pending);
            if (names != _lastPendingNames)
            {
                _lastPendingNames = names;
                _logger.LogInformation("等待触发定时 Action：{Names}", names);
            }
            if (deadline is { } dl && DateTimeOffset.Now >= dl)
            {
                _logger.LogInformation("到达运行硬限（{Deadline:HH:mm:ss}）仍有待办，自动退出（由下一班调度继续）", dl);
                return;
            }
            await Task.Delay(CompletionCheckInterval);
        }
    }

    /// <summary>上次记录的待触发列表（仅变化时记日志，避免每 15s 刷屏）。</summary>
    private string? _lastPendingNames;

    /// <summary>
    /// 本班窗口内仍可能触发的已启用定时 Action 名称。
    /// 判定：已完成（进程内成功锁 <see cref="ActionBase.SucceededToday"/> 或业务完成 <see cref="ActionBase.DailyDoneToday"/>，含跨进程日锁恢复）→ 排除；
    /// 周末且不允许周末跑（<see cref="ActionBase.RunsOnWeekends"/> = false）→ 今天不会触发 → 排除；
    /// 未到点且硬限也到不了点 → 本班不可能触发 → 排除（如工作日 00:00 班次到不了 09:25、或任何周末班次）。
    /// </summary>
    internal static List<string> PendingScheduledNames(IEnumerable<ActionBase> actions, DateTimeOffset now, DateTimeOffset? hardDeadline)
    {
        var beijingNow = ChinaTime.ToBeijing(now);
        var today = DateOnly.FromDateTime(beijingNow);
        var nowTime = TimeOnly.FromDateTime(beijingNow);
        var windowEnd = hardDeadline is { } dl ? TimeOnly.FromDateTime(ChinaTime.ToBeijing(dl)) : TimeOnly.MaxValue;

        var pending = new List<string>();
        foreach (var a in actions)
        {
            if (!a.IsEnabled || a.TriggerMode != TriggerMode.Scheduled)
            {
                continue;
            }
            if (a.DailyDoneToday || a.SucceededToday)
            {
                continue; // 今日已完成（进程内/跨进程恢复）
            }
            if (!a.RunsOnWeekends && today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue; // 业务今天不跑（周末）
            }
            if (nowTime < a.ScheduledTime && windowEnd < a.ScheduledTime)
            {
                continue; // 未到点且本班硬限前到不了点
            }
            pending.Add(a.Name);
        }
        return pending;
    }
}
