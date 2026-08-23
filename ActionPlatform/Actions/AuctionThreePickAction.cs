using System.Text;
using ActionPlatform.Core.Services.Actions;
using ActionPlatform.Core.Services.Messaging;
using ActionPlatform.Core.Services.StockData;
using ActionPlatform.Core.Services.StockData.Models;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Actions;

/// <summary>集合竞价「三一票」评分结果：综合得分（0~1，越高越好）与对应标的。</summary>
public sealed record AuctionPickRank(decimal Score, AuctionSnapshotItem Stock);

/// <summary>
/// 集合竞价「三一票」：每天 09:25 执行。
/// 对全市场竞价快照综合评分：竞价金额（越大越好）与流通市值（越小越好）各占 50%，
/// 取综合得分前三的票，以易读文字表格推送到消息端（代码、名称、行业、现价、竞价金额、换手、竞价涨幅、得分、流通市值）。
/// 注：上游暂未提供个股所属行业（文档标注「敬请期待」），行业列显示占位符。
/// </summary>
public sealed class AuctionThreePickAction : ActionBase
{
    private const int PickCount = 3;
    private const decimal AmountWeight = 0.5m;
    private const decimal CapWeight = 0.5m;

    private readonly IStockDataService _stockData;
    private readonly IMessageNotifier _notifier;
    private readonly ILogger<AuctionThreePickAction> _logger;

    public AuctionThreePickAction(IStockDataService stockData, IMessageNotifier notifier, ILogger<AuctionThreePickAction> logger)
    {
        _stockData = stockData;
        _notifier = notifier;
        _logger = logger;
        TriggerMode = TriggerMode.Scheduled;
        ScheduledTime = new TimeOnly(9, 25);
    }

    public override string Name => "集合竞价三一票";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[{ActionName}] 开始扫描全市场竞价数据", Name);
        var all = await _stockData.GetAllAuctionSnapshotsAsync(cancellationToken: ct);

        var picks = RankTop(all, PickCount);
        if (picks.Count == 0)
        {
            _logger.LogWarning("[{ActionName}] 无可评分标的（无竞价成交额且流通市值的记录）", Name);
            return;
        }

        var table = BuildTable(picks);
        _logger.LogInformation("[{ActionName}] 选出 {Count} 只，推送消息", Name, picks.Count);
        await _notifier.SendAsync("集合竞价三一票（09:25）", table, ct);
    }

    /// <summary>
    /// 综合评分取前 count 只：竞价金额、流通市值各占 50%。
    /// 两项均按 min-max 归一化到 [0,1]：金额越大得分越高；市值越低得分越高（1 - 市值归一化值）。
    /// 仅统计有竞价成交额且流通市值大于 0 的标的（避免无数据标的因「市值低」虚高得分）。
    /// </summary>
    public static List<AuctionPickRank> RankTop(IEnumerable<AuctionSnapshotItem> items, int count)
    {
        var valid = items.Where(s => s.AuctionAmount > 0 && s.FloatMarketCap > 0).ToList();
        if (valid.Count == 0)
        {
            return new List<AuctionPickRank>();
        }

        var maxAmount = valid.Max(s => s.AuctionAmount);
        var minAmount = valid.Min(s => s.AuctionAmount);
        var maxCap = valid.Max(s => s.FloatMarketCap);
        var minCap = valid.Min(s => s.FloatMarketCap);

        return valid
            .Select(s => new AuctionPickRank(
                Score: AmountWeight * NormUp(s.AuctionAmount, minAmount, maxAmount)
                     + CapWeight * (1m - NormUp(s.FloatMarketCap, minCap, maxCap)),
                s))
            .OrderByDescending(x => x.Score)
            .Take(Math.Max(0, count))
            .ToList();

        static decimal NormUp(decimal value, decimal min, decimal max) =>
            max == min ? 1m : (value - min) / (max - min);
    }

    /// <summary>组装易读文字表格（等宽对齐，CJK 字符按 2 列宽计）。</summary>
    public static string BuildTable(IReadOnlyList<AuctionPickRank> picks)
    {
        var header = new[] { "代码", "名称", "行业", "现价", "竞价金额", "实际换手", "竞价涨幅", "得分", "流通市值" };
        var rows = picks.Select(r => new[]
        {
            r.Stock.Thscode ?? "",
            r.Stock.Name ?? "",
            "—", // 上游暂未提供个股行业归属
            r.Stock.LastPrice.ToString("F2"),
            r.Stock.AuctionAmount.ToString("N0"),
            r.Stock.AuctionTurnoverPct.ToString("F2") + "%",
            r.Stock.AuctionPct.ToString("F2") + "%",
            r.Score.ToString("F3"),
            FormatCap(r.Stock.FloatMarketCap),
        }).ToList();

        var widths = new[] { 11, 11, 6, 9, 14, 9, 9, 7, 12 };
        var sb = new StringBuilder();
        AppendRow(header);
        sb.AppendLine(new string('-', widths.Sum()));
        foreach (var row in rows)
        {
            AppendRow(row);
        }
        return sb.ToString().TrimEnd();

        void AppendRow(string[] cells)
        {
            var parts = new string[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                parts[i] = PadCjk(cells[i], widths[i]);
            }
            sb.AppendLine(string.Join(' ', parts));
        }

        static string PadCjk(string value, int width)
        {
            var displayWidth = value.Sum(c => c > 0x7F ? 2 : 1);
            return value + new string(' ', Math.Max(0, width - displayWidth));
        }

        static string FormatCap(decimal cap) =>
            cap >= 1_0000_0000_0000m
                ? (cap / 1_0000_0000_0000m).ToString("F2") + "万亿"
                : (cap / 1_0000_0000m).ToString("F2") + "亿";
    }
}
