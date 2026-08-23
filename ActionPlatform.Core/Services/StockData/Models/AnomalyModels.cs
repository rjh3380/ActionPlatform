namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>个股异动原因响应 data（列表 / 按标的共用结构）。</summary>
public class AnomalyAnalysisData
{
    public long Timestamp { get; set; }
    public List<AnomalyAnalysisItem> Item { get; set; } = new();
}

/// <summary>个股异动原因记录。</summary>
public class AnomalyAnalysisItem
{
    /// <summary>股票名称。</summary>
    public string? StockName { get; set; }

    /// <summary>异动原因分析。</summary>
    public string? AnalysisContent { get; set; }

    /// <summary>关键词列表。</summary>
    public List<string> KeywordList { get; set; } = new();

    public string? Thscode { get; set; }

    /// <summary>异动标签名称。</summary>
    public string? TagName { get; set; }
}
