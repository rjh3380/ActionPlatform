using ActionPlatform.Core.Services.Actions;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Actions;

/// <summary>演示 Action：间隔触发模式，每 5 秒记录一条日志。</summary>
public sealed class IntervalLogAction : ActionBase
{
    private readonly ILogger<IntervalLogAction> _logger;

    public IntervalLogAction(ILogger<IntervalLogAction> logger)
    {
        _logger = logger;
        TriggerMode = TriggerMode.Interval;
        Interval = TimeSpan.FromSeconds(5);
    }

    public override string Name => "间隔日志演示";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[{ActionName}] 间隔触发执行：{Now:HH:mm:ss.fff}", Name, DateTimeOffset.Now);
        await Task.CompletedTask;
    }
}
