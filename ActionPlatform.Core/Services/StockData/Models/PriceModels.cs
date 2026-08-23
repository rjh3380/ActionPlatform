namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>行情快照响应 data。</summary>
public class SnapshotData
{
    /// <summary>数据就绪时间（毫秒）；按 thscodes 显式取数时为 null。</summary>
    public long? Timestamp { get; set; }

    /// <summary>全市场代码表总数（分页模式估算页数用）。</summary>
    public int Total { get; set; }

    public List<PriceSnapshotItem> Item { get; set; } = new();
}

/// <summary>行情快照记录。注意：快照不返回中文名 name。</summary>
public class PriceSnapshotItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }

    /// <summary>最新成交价。</summary>
    public decimal LastPrice { get; set; }

    /// <summary>相对前收盘价的涨跌额。</summary>
    public decimal PriceChange { get; set; }

    /// <summary>涨跌幅（百分比数值，如 1.74 表示 +1.74%）。</summary>
    public decimal PriceChangeRatioPct { get; set; }

    public decimal OpenPrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }

    /// <summary>前收盘价。</summary>
    public decimal PrevPrice { get; set; }

    /// <summary>成交量（股）。</summary>
    public decimal Volume { get; set; }

    /// <summary>成交额（原始货币）。</summary>
    public decimal Turnover { get; set; }
}

/// <summary>历史 K 线响应 data。</summary>
public class HistoricalData
{
    public long Timestamp { get; set; }
    public List<PriceBarItem> Item { get; set; } = new();
}

/// <summary>单根日 K 线。</summary>
public class PriceBarItem
{
    /// <summary>K 线日期（毫秒，Asia/Shanghai 零点）。</summary>
    public long DateMs { get; set; }

    public decimal OpenPrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal ClosePrice { get; set; }

    /// <summary>成交量（股）。</summary>
    public decimal Volume { get; set; }

    /// <summary>成交额（原始货币）。</summary>
    public decimal Turnover { get; set; }
}

/// <summary>复权因子事件流响应 data。</summary>
public class AdjustmentFactorsData
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }

    /// <summary>事件列表，按 ex_date_ms 降序（最新在前）。</summary>
    public List<AdjustmentFactorItem> Item { get; set; } = new();
}

/// <summary>复权事件：现金分红 / 送股。事件类型由两个数值字段隐式区分（dividend &gt; 0 为现金分红，bonus &gt; 0 为送股）。</summary>
public class AdjustmentFactorItem
{
    public string? Ticker { get; set; }

    /// <summary>除权除息日（Asia/Shanghai 00:00 毫秒戳）。</summary>
    public long ExDateMs { get; set; }

    /// <summary>每股现金分红（税前）。</summary>
    public decimal DividendPerShare { get; set; }

    /// <summary>每股送股比例（如 0.1 表示 10 送 1）。</summary>
    public decimal PerShareBonus { get; set; }
}
