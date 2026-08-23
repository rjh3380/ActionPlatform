namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>标的检索/列表响应 data（timestamp + item）。</summary>
public class TickerListData
{
    public long? Timestamp { get; set; }
    public List<TickerItem> Item { get; set; } = new();
}

/// <summary>标的信息（检索与列表共用）。</summary>
public class TickerItem
{
    /// <summary>带交易所后缀的完整 thscode，如 600519.SH。</summary>
    public string? Thscode { get; set; }

    /// <summary>纯代码，如 600519。</summary>
    public string? Ticker { get; set; }

    /// <summary>展示名称。</summary>
    public string? Name { get; set; }

    /// <summary>交易所后缀（SH / SZ / BJ），无后缀指数为 null。</summary>
    public string? Exchange { get; set; }

    /// <summary>资产类别（a-share / a-share-index / forex / fund-*）。</summary>
    public string? AssetType { get; set; }

    /// <summary>币种代码。</summary>
    public string? Currency { get; set; }
}
