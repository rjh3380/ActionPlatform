namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>全市场竞价 TopN 排序字段。</summary>
public enum AuctionSortField
{
    /// <summary>竞价成交额（auction_amount）。</summary>
    Amount,

    /// <summary>竞价涨跌幅（auction_pct）。</summary>
    Pct,

    /// <summary>竞价量比（auction_volume_ratio）。</summary>
    VolumeRatio,

    /// <summary>竞价换手率（auction_turnover_pct）。</summary>
    TurnoverPct,
}

/// <summary>A 股集合竞价快照响应 data。</summary>
public class AuctionSnapshotData
{
    /// <summary>接口响应组装时间（毫秒）。</summary>
    public long Timestamp { get; set; }

    /// <summary>集合竞价阶段（live / final）。</summary>
    public string? AuctionPhase { get; set; }

    /// <summary>数据状态（ready 等）。</summary>
    public string? DataStatus { get; set; }

    /// <summary>返回标的数量。</summary>
    public int Total { get; set; }

    public List<AuctionSnapshotItem> Item { get; set; } = new();
}

/// <summary>集合竞价快照记录（含流通市值）。</summary>
public class AuctionSnapshotItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }

    /// <summary>竞价价格。</summary>
    public decimal AuctionPrice { get; set; }

    /// <summary>竞价涨跌幅（百分数原值）。</summary>
    public decimal AuctionPct { get; set; }

    /// <summary>竞价成交量。</summary>
    public decimal AuctionVolume { get; set; }

    /// <summary>竞价成交额。</summary>
    public decimal AuctionAmount { get; set; }

    /// <summary>竞价未匹配量。</summary>
    public decimal AuctionUnmatched { get; set; }

    /// <summary>竞价换手率。</summary>
    public decimal AuctionTurnoverPct { get; set; }

    /// <summary>相对昨日成交量比例。</summary>
    public decimal AuctionYesterdayRatioPct { get; set; }

    /// <summary>竞价量比。</summary>
    public decimal AuctionVolumeRatio { get; set; }

    /// <summary>前收盘价。</summary>
    public decimal PreClosePrice { get; set; }

    /// <summary>开盘价。</summary>
    public decimal OpenPrice { get; set; }

    /// <summary>最新价。</summary>
    public decimal LastPrice { get; set; }

    /// <summary>流通市值。</summary>
    public decimal FloatMarketCap { get; set; }
}

/// <summary>短线风向标竞价基准响应 data。</summary>
public class ShortTermBenchmarkData
{
    public long Timestamp { get; set; }

    /// <summary>最终查询日期（yyyy-MM-dd）。</summary>
    public string? Date { get; set; }

    /// <summary>最终查询日期 Asia/Shanghai 当日零点毫秒戳。</summary>
    public long DateMs { get; set; }

    public List<ShortTermBenchmarkItem> Item { get; set; } = new();
}

/// <summary>短线风向标竞价基准记录。</summary>
public class ShortTermBenchmarkItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }

    /// <summary>集合竞价涨跌幅（百分数原值）。</summary>
    public decimal AuctionPct { get; set; }

    /// <summary>短线风向标标签（如「高开」「放量」）。</summary>
    public List<string> Tags { get; set; } = new();
}
