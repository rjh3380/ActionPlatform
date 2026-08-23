namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>飙升榜 / 热股榜通用 data 结构。</summary>
public class RankListData
{
    public long Timestamp { get; set; }
    public List<RankListItem> Item { get; set; } = new();
}

/// <summary>榜单记录（飙升榜与热股榜字段一致）。</summary>
public class RankListItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }

    /// <summary>排名。</summary>
    public int Rank { get; set; }

    /// <summary>热度值。</summary>
    public decimal Heat { get; set; }

    /// <summary>排名变化。</summary>
    public int RankChange { get; set; }

    /// <summary>排名趋势。</summary>
    public string? RankTrend { get; set; }
}

/// <summary>历史热股榜响应 data。</summary>
public class HotStockHistoryData
{
    /// <summary>日期（yyyy-MM-dd）。</summary>
    public string? Date { get; set; }

    /// <summary>日期毫秒戳。</summary>
    public long DateMs { get; set; }

    public List<HotStockHistoryItem> Item { get; set; } = new();
}

public class HotStockHistoryItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }

    /// <summary>当日排名。</summary>
    public int Rank { get; set; }
}

/// <summary>热股排名走势响应 data。</summary>
public class RankTrendData
{
    /// <summary>起始日 Asia/Shanghai 00:00 毫秒戳。</summary>
    public long Timestamp { get; set; }

    public List<RankTrendItem> Item { get; set; } = new();
}

public class RankTrendItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }

    /// <summary>日期（yyyy-MM-dd）。</summary>
    public string? Date { get; set; }

    /// <summary>日期毫秒戳。</summary>
    public long DateMs { get; set; }

    /// <summary>当日排名。某些日期可能无排名数据，属正常现象。</summary>
    public int Rank { get; set; }
}
