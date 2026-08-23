using ActionPlatform.Core.Services.Messaging;
using Microsoft.Extensions.Logging;

namespace ActionPlatform;

/// <summary>
/// 应用演示类：演示 IOC 构造函数注入与日志、消息服务的组合使用。
/// </summary>
internal sealed class App
{
    private readonly ILogger<App> _logger;
    private readonly IMessageNotifier _notifier;

    public App(ILogger<App> logger, IMessageNotifier notifier)
    {
        _logger = logger;
        _notifier = notifier;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("应用启动，IOC 容器装配完成");

        // Debug 级别日志：验证 nlog.config 级别过滤（minlevel=Info，不应写入日志文件）
        _logger.LogDebug("这是一条 Debug 日志，不应出现在日志文件中");

        await _notifier.SendAsync("ActionPlatform 启动通知", "IOC / 日志 / 消息服务已就绪。", CancellationToken.None);

        _logger.LogInformation("演示消息处理完成，应用即将退出");
    }
}
