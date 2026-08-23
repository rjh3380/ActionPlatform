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
    private readonly ILogger<App> _logger;
    private readonly IMessageNotifier _notifier;
    private readonly ActionLoop _actionLoop;

    public App(ILogger<App> logger, IMessageNotifier notifier, ActionLoop actionLoop)
    {
        _logger = logger;
        _notifier = notifier;
        _actionLoop = actionLoop;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("应用启动，IOC 容器装配完成");

        // Debug 级别日志：验证 nlog.config 级别过滤（minlevel=Info，不应写入日志文件）
        _logger.LogDebug("这是一条 Debug 日志，不应出现在日志文件中");

        await _notifier.SendAsync("ActionPlatform 启动通知", "IOC / 日志 / 消息服务已就绪。", CancellationToken.None);

        // 启动 Action 调度循环：常驻轮询所有已启用 Action（间隔日志演示每 5 秒触发一次）
        await _actionLoop.StartAsync(CancellationToken.None);
        _logger.LogInformation("Action 调度循环已启动，按任意键退出...");

        // 阻塞保持进程存活，让调度循环持续运行（退出前停止循环）
        Console.ReadKey();

        await _actionLoop.StopAsync();
        _logger.LogInformation("Action 调度循环已停止，应用退出");
    }
}
