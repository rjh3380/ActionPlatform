using ActionPlatform.Core.Services.Actions;
using ActionPlatform.Core.Services.Messaging;
using ActionPlatform.Core.Services.StockData;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Actions;

/// <summary>
/// 演示 Action：每日定时触发模式，每天 09:25 拉取全市场竞价金额 Top10 并推送消息（飞书等，取决于 IMessageNotifier 实现）。
/// 展示 Action → IStockDataService → IMessageNotifier 全链路。
/// </summary>
public sealed class AuctionTopPushAction : ActionBase
{
    private readonly IStockDataService _stockData;
    private readonly IMessageNotifier _notifier;
    private readonly ILogger<AuctionTopPushAction> _logger;

    public AuctionTopPushAction(IStockDataService stockData, IMessageNotifier notifier, ILogger<AuctionTopPushAction> logger)
    {
        IsEnabled = false;

        _stockData = stockData;
        _notifier = notifier;
        _logger = logger;
        TriggerMode = TriggerMode.Scheduled;
        ScheduledTime = new TimeOnly(9, 25);
    }

    public override string Name => "竞价 Top10 推送";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[{ActionName}] 开始拉取竞价金额 Top10", Name);
        var top = await _stockData.GetTopAuctionAsync(top: 10, cancellationToken: ct);

        var lines = top.Select((s, i) => $"{i + 1}. {s.Name}（{s.Thscode}）竞价额={s.AuctionAmount:N0} 涨幅={s.AuctionPct}%");
        await _notifier.SendAsync("今日竞价金额 Top10", string.Join("\n", lines), ct);

        _logger.LogInformation("[{ActionName}] 推送完成，共 {Count} 只", Name, top.Count);
    }
}
