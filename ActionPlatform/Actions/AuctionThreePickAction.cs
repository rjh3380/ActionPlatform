using System.Text;
using ActionPlatform.Core.Services.Actions;
using ActionPlatform.Core.Services.Messaging;
using ActionPlatform.Core.Services.StockData;
using ActionPlatform.Core.Services.StockData.Models;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Actions;

/// <summary>集合竞价「三一票」评分结果：得分 = 竞价金额 ÷ 流通市值 × 100（百分制，越高越好）与对应标的。</summary>
public sealed record AuctionPickRank(decimal Score, AuctionSnapshotItem Stock);

/// <summary>
/// 集合竞价「三一票」：每天 09:25 执行。
/// 对全市场竞价快照综合评分：评分 = 竞价金额 ÷ 流通市值（资金占流通盘比例，百分制），越大评分越高，
/// 取得分前五的票，以飞书富文本卡片推送到消息端（代码、名称、现价、竞价金额、竞价实际换手、竞价涨幅、得分、流通市值）。
/// 注：上游暂未提供个股所属行业（文档标注「敬请期待」），不渲染行业行。
/// </summary>
public sealed class AuctionThreePickAction : ActionBase
{
    private const int PickCount = 5;

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

        var lines = BuildPostLines(picks);
        _logger.LogInformation("[{ActionName}] 选出 {Count} 只，推送富文本消息", Name, picks.Count);
        await _notifier.SendRichTextAsync("集合竞价三一票（09:25）", lines, ct);
    }

    /// <summary>
    /// 组装飞书富文本卡片：每只票一个块（🥇🥈🥉 排名 + 4 行字段），手机端逐行渲染不换行。
    /// 名称/代码/得分标记加粗（当前飞书 webhook 不支持 text 加粗样式，序列化层忽略；
    /// 模型保留标记，未来支持加粗的渠道可直接生效）；无行业数据（上游未提供）时不渲染行业行。
    /// </summary>
    public static IReadOnlyList<RichTextLine> BuildPostLines(IReadOnlyList<AuctionPickRank> picks)
    {
        var medals = new[] { "🥇", "🥈", "🥉", "4️⃣", "5️⃣" };
        var lines = new List<RichTextLine>();
        for (var i = 0; i < picks.Count; i++)
        {
            var rank = picks[i];
            var s = rank.Stock;

            var medal = i < medals.Length ? medals[i] : $"{i + 1}.";
            lines.Add(RichTextLine.Mixed(
                new RichTextSegment($"{medal} {i + 1}. ", Bold: true),
                new RichTextSegment($"{s.Thscode} {s.Name}", Bold: true),
                new RichTextSegment($"    得分 {rank.Score:F2}", Bold: true)));
            lines.Add(RichTextLine.Plain($"现价 {s.LastPrice:F2} ｜ 竞价金额 {FormatAmount(s.AuctionAmount)}"));
            lines.Add(RichTextLine.Plain($"竞价实际换手 {s.AuctionTurnoverPct:F2}% ｜ 竞价涨幅 {s.AuctionPct:F2}%"));
            lines.Add(RichTextLine.Plain($"流通市值 {FormatCap(s.FloatMarketCap)}"));
            lines.Add(RichTextLine.Empty);
        }
        return lines;
    }

    private static string FormatAmount(decimal amount) =>
        amount >= 1_0000_0000m
            ? (amount / 1_0000_0000m).ToString("F2") + "亿"
            : amount.ToString("N0");

    /// <summary>
    /// 综合评分取前 count 只：评分 = 竞价金额 ÷ 流通市值（资金占流通盘比例），越大评分越高。
    /// 换算为百分制（比值 × 100，如金额 3.8 亿 / 市值 254 亿 → 1.50 分）。
    /// 仅统计竞价金额大于 1 亿且流通市值大于 0 的标的（金额门槛过滤低关注标的，市值大于 0 避免除零）。
    /// </summary>
    public static List<AuctionPickRank> RankTop(IEnumerable<AuctionSnapshotItem> items, int count)
    {
        var valid = items.Where(s => s.AuctionAmount > 1_0000_0000m && s.FloatMarketCap > 0).ToList();
        if (valid.Count == 0)
        {
            return new List<AuctionPickRank>();
        }

        return valid
            .Select(s => new AuctionPickRank(
                Score: s.AuctionAmount / s.FloatMarketCap * 100m,
                s))
            .OrderByDescending(x => x.Score)
            .Take(Math.Max(0, count))
            .ToList();
    }

    /// <summary>组装易读文字表格（等宽对齐，CJK 字符按 2 列宽计）。</summary>
    public static string BuildTable(IReadOnlyList<AuctionPickRank> picks)
    {
        var header = new[] { "代码", "名称", "行业", "现价", "竞价金额", "竞价实际换手", "竞价涨幅", "得分", "流通市值" };
        var rows = picks.Select(r => new[]
        {
            r.Stock.Thscode ?? "",
            r.Stock.Name ?? "",
            "—", // 上游暂未提供个股行业归属
            r.Stock.LastPrice.ToString("F2"),
            r.Stock.AuctionAmount.ToString("N0"),
            r.Stock.AuctionTurnoverPct.ToString("F2") + "%",
            r.Stock.AuctionPct.ToString("F2") + "%",
            r.Score.ToString("F2"),
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
    }

    private static string FormatCap(decimal cap) =>
        cap >= 1_0000_0000_0000m
            ? (cap / 1_0000_0000_0000m).ToString("F2") + "万亿"
            : (cap / 1_0000_0000m).ToString("F2") + "亿";
}
