namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>交易日历响应 data（近一年交易日序列）。</summary>
public class TradingDaysData
{
    public long Timestamp { get; set; }
    public List<TradingDayItem> Item { get; set; } = new();
}

public class TradingDayItem
{
    /// <summary>交易日 Asia/Shanghai 00:00:00 毫秒戳。</summary>
    public long DateMs { get; set; }

    /// <summary>同一交易日 yyyyMMdd 格式（Asia/Shanghai）。</summary>
    public string? Date { get; set; }
}
