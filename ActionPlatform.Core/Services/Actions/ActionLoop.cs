using Microsoft.Extensions.Logging;

namespace ActionPlatform.Core.Services.Actions;

/// <summary>
/// Action 调度循环：程序启动时启动，常驻轮询所有已注册的 Action。
/// 每轮遍历快照：跳过未启用 → 判断方法通过 → 执行干活方法；单个 Action 异常隔离，不影响其他 Action 与循环。
/// 同一 Action 执行中不重复触发（<see cref="ActionBase.IsRunning"/> 防重入）。
/// </summary>
public sealed class ActionLoop
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly ILogger<ActionLoop> _logger;
    private readonly IReadOnlyList<ActionBase> _actions;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    /// <param name="dailyFireState">跨进程日锁存储（重启链下去重用；null = 不装配，定时 Action 仅进程内去重）。装配时各 Action 从文件恢复最近完成日期。</param>
    /// <param name="pollInterval">轮询间隔（默认 100ms）。传入 null 使用默认值。</param>
    public ActionLoop(ILogger<ActionLoop> logger, IEnumerable<ActionBase> actions, IDailyFireStateStore? dailyFireState = null, TimeSpan? pollInterval = null)
    {
        _logger = logger;
        _actions = actions.ToArray();
        _pollInterval = pollInterval ?? DefaultPollInterval;
        if (dailyFireState is not null)
        {
            foreach (var action in _actions)
            {
                action.AttachDailyFireState(dailyFireState);
            }
        }
    }

    /// <summary>启动常驻轮询循环；已启动时重复调用直接返回。</summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_loopTask is not null)
        {
            return Task.CompletedTask;
        }
        _loopTask = Task.Run(() => RunLoopAsync(ct), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>停止循环并等待退出。</summary>
    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 取消导致的退出属于正常停止
            }
        }
        _loopTask = null;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        var token = linked.Token;
        _logger.LogInformation("ActionLoop 已启动：共 {ActionCount} 个 Action，轮询间隔 {PollInterval}", _actions.Count, _pollInterval);
        foreach (var action in _actions)
        {
            var trigger = action.TriggerMode == TriggerMode.Interval
                ? $"间隔 {action.Interval.TotalMilliseconds:0}ms"
                : $"每日 {action.ScheduledTime:HH\\:mm}（北京时间）";
            _logger.LogInformation("Action [{ActionName}]：{Enabled}，{Trigger}", action.Name, action.IsEnabled ? "已启用" : "已禁用", trigger);
        }

        while (!token.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            foreach (var action in _actions)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                // 未启用：跳过，不执行判断与干活方法
                if (!action.IsEnabled)
                {
                    continue;
                }

                bool canExecute;
                try
                {
                    canExecute = await action.CanExecuteAsync(now, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Action [{ActionName}] 判断方法异常，已隔离", action.Name);
                    continue;
                }
                if (!canExecute)
                {
                    continue;
                }

                action.MarkExecuted(now);
                try
                {
                    await action.ExecuteAsync(token).ConfigureAwait(false);
                    action.MarkSucceeded();
                }
                catch (Exception ex)
                {
                    // 失败不占用触发额度（每日定时当天仍可按 RetryDelay 重试），仅记录失败时间用于退避
                    _logger.LogError(ex, "Action [{ActionName}] 执行异常，已隔离", action.Name);
                    action.MarkFailed();
                }
                finally
                {
                    action.MarkCompleted();
                }
            }

            try
            {
                await Task.Delay(_pollInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ActionLoop 已停止");
    }
}
