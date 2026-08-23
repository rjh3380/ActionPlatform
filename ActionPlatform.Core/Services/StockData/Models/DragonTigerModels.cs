namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>龙虎榜响应 data。</summary>
public class DragonTigerData
{
    public long Timestamp { get; set; }

    /// <summary>榜单类型（all / org / hot_money）。</summary>
    public string? BoardType { get; set; }

    /// <summary>交易日期（yyyy-MM-dd）。</summary>
    public string? TradeDate { get; set; }

    /// <summary>记录总数。</summary>
    public int Count { get; set; }

    /// <summary>股票数量。</summary>
    public int StockCount { get; set; }

    /// <summary>个股明细列表。</summary>
    public List<DragonTigerStockItem> StockItems { get; set; } = new();

    /// <summary>游资明细列表（board_type=hot_money 或 all 时返回）。</summary>
    public List<HotMoneyItem> HotMoneyItems { get; set; } = new();
}

/// <summary>龙虎榜个股明细。</summary>
public class DragonTigerStockItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }

    /// <summary>概念列表。</summary>
    public List<string> ConceptList { get; set; } = new();

    /// <summary>涨跌幅。</summary>
    public decimal Change { get; set; }

    /// <summary>买入额。</summary>
    public decimal BuyValue { get; set; }

    /// <summary>卖出额。</summary>
    public decimal SellValue { get; set; }

    /// <summary>净额。</summary>
    public decimal NetValue { get; set; }

    /// <summary>净额占比。</summary>
    public decimal NetRate { get; set; }

    /// <summary>机构净额。</summary>
    public decimal OrgNetValue { get; set; }

    /// <summary>游资净额。</summary>
    public decimal HotMoneyNetValue { get; set; }

    /// <summary>热度排名。</summary>
    public int HotRank { get; set; }

    /// <summary>上榜天数。</summary>
    public int RangeDays { get; set; }

    /// <summary>涨跌停原因。</summary>
    public string? LimitReason { get; set; }
}

/// <summary>游资明细。</summary>
public class HotMoneyItem
{
    /// <summary>游资名称。</summary>
    public string? Name { get; set; }

    /// <summary>买入额。</summary>
    public decimal Buying { get; set; }

    /// <summary>该游资关联的股票明细列表。</summary>
    public List<HotMoneyRow> Rows { get; set; } = new();
}

/// <summary>游资关联股票明细。</summary>
public class HotMoneyRow
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }
    public List<string> ConceptList { get; set; } = new();
    public decimal Change { get; set; }

    /// <summary>金额。</summary>
    public decimal Amount { get; set; }

    public decimal BuyValue { get; set; }
    public decimal SellValue { get; set; }
    public decimal NetValue { get; set; }
    public decimal NetRate { get; set; }
    public decimal OrgNetValue { get; set; }
    public decimal HotMoneyNetValue { get; set; }

    /// <summary>游资净额占比。</summary>
    public decimal HotMoneyNetRate { get; set; }

    /// <summary>游资单项净额。</summary>
    public decimal HotMoneyItemNetValue { get; set; }

    /// <summary>游资单项净额占比。</summary>
    public decimal HotMoneyItemNetRate { get; set; }

    public int HotRank { get; set; }
    public int RangeDays { get; set; }
}
