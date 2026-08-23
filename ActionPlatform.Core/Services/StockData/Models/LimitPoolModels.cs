namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>涨停/跌停/炸板池通用 data 结构（timestamp + pagination + item）。</summary>
public class LimitPoolData<TItem>
{
    public long Timestamp { get; set; }
    public Pagination? Pagination { get; set; }
    public List<TItem> Item { get; set; } = new();
}

/// <summary>涨停股票池记录。</summary>
public class LimitUpPoolItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }
    public bool IsSt { get; set; }
    public bool IsNew { get; set; }
    public decimal LastPrice { get; set; }

    /// <summary>涨跌幅（百分数原值）。</summary>
    public decimal PriceChangeRatioPct { get; set; }

    /// <summary>涨停时间（HH:mm）。</summary>
    public string? LimitUpTime { get; set; }

    /// <summary>涨停原因。</summary>
    public string? LimitUpReason { get; set; }

    /// <summary>连板天数文本（如「2 连板」）。</summary>
    public string? ContinueDayText { get; set; }

    /// <summary>连板天数。</summary>
    public int ContinueDayCnt { get; set; }

    /// <summary>封单金额。</summary>
    public decimal SealMoney { get; set; }

    /// <summary>最大封单金额。</summary>
    public decimal MaxSealMoney { get; set; }
}

/// <summary>跌停股票池记录。</summary>
public class LimitDownPoolItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }
    public decimal LastPrice { get; set; }
    public decimal PriceChangeRatioPct { get; set; }

    /// <summary>首次跌停时间（上海时区 HH:mm）。</summary>
    public string? FirstLimitTime { get; set; }

    /// <summary>最后跌停时间（上海时区 HH:mm）。</summary>
    public string? LastLimitTime { get; set; }

    /// <summary>换手率。</summary>
    public decimal TurnoverRatioPct { get; set; }
}

/// <summary>炸板（曾涨停后开板）股票池记录。</summary>
public class LimitBreakPoolItem
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }
    public decimal LastPrice { get; set; }
    public decimal PriceChangeRatioPct { get; set; }

    /// <summary>开板次数（非连板天数）。</summary>
    public int OpenTimes { get; set; }

    /// <summary>换手率。</summary>
    public decimal TurnoverRatioPct { get; set; }

    /// <summary>成交额。</summary>
    public decimal Turnover { get; set; }
}

/// <summary>连板天梯响应 data（近 30 个交易日连板梯队矩阵）。</summary>
public class LimitUpLadderData
{
    public long Timestamp { get; set; }
    public LadderWindow? Window { get; set; }

    /// <summary>按日期排列的连板矩阵。</summary>
    public List<LadderItem> Item { get; set; } = new();
}

public class LadderWindow
{
    public int? Length { get; set; }
    public List<string>? DateList { get; set; }

    /// <summary>各梯队封板结构，结构未公开，原样保留。</summary>
    public System.Text.Json.JsonElement? BoardCaps { get; set; }
}

/// <summary>单日连板矩阵。</summary>
public class LadderItem
{
    /// <summary>日期。</summary>
    public string? Date { get; set; }

    /// <summary>各梯队股票（two_board 至 seven_over，每梯队最多 4 只）。</summary>
    public LadderBoards? Boards { get; set; }
}

public class LadderBoards
{
    public List<LadderBoardStock> TwoBoard { get; set; } = new();
    public List<LadderBoardStock> ThreeBoard { get; set; } = new();
    public List<LadderBoardStock> FourBoard { get; set; } = new();
    public List<LadderBoardStock> FiveBoard { get; set; } = new();
    public List<LadderBoardStock> SixBoard { get; set; } = new();
    public List<LadderBoardStock> SevenOver { get; set; } = new();
}

public class LadderBoardStock
{
    public string? Thscode { get; set; }
    public string? Ticker { get; set; }
    public string? Name { get; set; }

    /// <summary>连板天数。</summary>
    public int BoardNum { get; set; }

    /// <summary>标记级别。</summary>
    public string? SignLevel { get; set; }

    /// <summary>次日封板情况（最近交易日为 null）。</summary>
    public string? SealNextday { get; set; }
}
