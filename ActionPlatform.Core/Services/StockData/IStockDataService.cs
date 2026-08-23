using ActionPlatform.Core.Services.StockData.Models;

namespace ActionPlatform.Core.Services.StockData;

/// <summary>
/// 同花顺 Financial-API 数据服务（https://fuyao.aicubes.cn）。
/// 方法返回值即数据：code=0 时返回强类型 data，code!=0 时抛 <see cref="FinancialApiException"/>。
/// ApiKey 未配置时任何方法调用均抛 <see cref="FinancialApiException"/>。
/// </summary>
public interface IStockDataService
{
    // ---- 行情 / K 线 ----

    /// <summary>行情快照：thscodes 批量模式（逗号分隔）或全市场分页模式（limit/offset，不传 thscodes）。快照不含中文名。</summary>
    Task<SnapshotData> GetSnapshotAsync(string? thscodes = null, int limit = 100, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>历史日 K 线：单只 thscode，起止为毫秒时间戳（窗口 ≤ 10 年），adjust: none/forward/backward。</summary>
    Task<HistoricalData> GetHistoricalKLineAsync(string thscode, long startMs, long endMs, string adjust = "forward", CancellationToken cancellationToken = default);

    /// <summary>复权因子事件流：单只 thscode，from/to 为 yyyy-MM-dd 可选区间。</summary>
    Task<AdjustmentFactorsData> GetAdjustmentFactorsAsync(string thscode, string? from = null, string? to = null, CancellationToken cancellationToken = default);

    // ---- 元信息 ----

    /// <summary>标的检索：按 thscode / ticker / 中英文名子串，支持 exchange 与 asset_type 过滤。</summary>
    Task<TickerListData> SearchTickersAsync(string q, string? exchange = null, string? assetType = null, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>标的列表：按交易所（逗号分隔）与资产类别分页获取代码表。</summary>
    Task<TickerListData> ListTickersAsync(string exchange = "SH,SZ", string? assetType = null, int limit = 1000, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>交易日历：近一年交易日序列（Asia/Shanghai）。</summary>
    Task<TradingDaysData> GetTradingDaysAsync(CancellationToken cancellationToken = default);

    // ---- 集合竞价 ----

    /// <summary>A 股集合竞价快照：thscodes 逗号分隔，stage: live/final（默认 final，数据完整）。含流通市值 float_market_cap。</summary>
    Task<AuctionSnapshotData> GetAuctionSnapshotAsync(string thscodes, string stage = "final", CancellationToken cancellationToken = default);

    /// <summary>
    /// 全市场集合竞价快照：内部自动拉取全部 A 股代码表并按每批 100 个请求竞价快照，返回所有有竞价数据的股票。
    /// maxConcurrency 为 null 或 ≤1 时串行（约 55 次请求）；设置 &gt;1 时按该并发度并行拉取。
    /// </summary>
    Task<IReadOnlyList<AuctionSnapshotItem>> GetAllAuctionSnapshotsAsync(string stage = "final", int? maxConcurrency = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全市场竞价 TopN：按指定竞价字段（默认竞价成交额）降序取前 top 只。内部复用 <see cref="GetAllAuctionSnapshotsAsync"/>。
    /// </summary>
    Task<IReadOnlyList<AuctionSnapshotItem>> GetTopAuctionAsync(int top = 10, AuctionSortField sortField = AuctionSortField.Amount, string stage = "final", int? maxConcurrency = null, CancellationToken cancellationToken = default);

    /// <summary>短线风向标竞价基准：date 为 yyyy-MM-dd（默认 Asia/Shanghai 当日）。</summary>
    Task<ShortTermBenchmarkData> GetShortTermBenchmarkAsync(string? date = null, CancellationToken cancellationToken = default);

    // ---- 涨跌停 / 异动 / 热榜 / 龙虎榜 ----

    /// <summary>涨停股票池：dateMs 为交易日毫秒戳（省略取今日），page/size 分页（size ≤ 200）。</summary>
    Task<LimitPoolData<LimitUpPoolItem>> GetLimitUpPoolAsync(long? dateMs = null, int page = 1, int size = 50, string? sortField = null, string? sortDir = null, CancellationToken cancellationToken = default);

    /// <summary>涨停股票池取全：内部按分页循环拉取全部记录（size 固定 200）。</summary>
    Task<IReadOnlyList<LimitUpPoolItem>> GetAllLimitUpPoolAsync(long? dateMs = null, CancellationToken cancellationToken = default);

    /// <summary>跌停股票池：分页参数同涨停池；sortField 可选 last_limit_time/first_limit_time/last_price/price_change_ratio_pct/turnover_ratio_pct。</summary>
    Task<LimitPoolData<LimitDownPoolItem>> GetLimitDownPoolAsync(long? dateMs = null, int page = 1, int size = 50, string? sortField = null, string? sortDir = null, CancellationToken cancellationToken = default);

    /// <summary>跌停股票池取全。</summary>
    Task<IReadOnlyList<LimitDownPoolItem>> GetAllLimitDownPoolAsync(long? dateMs = null, CancellationToken cancellationToken = default);

    /// <summary>炸板股票池：sortField 可选 price_change_ratio_pct/open_times/last_price/turnover_ratio_pct/turnover。</summary>
    Task<LimitPoolData<LimitBreakPoolItem>> GetLimitBreakPoolAsync(long? dateMs = null, int page = 1, int size = 50, string? sortField = null, string? sortDir = null, CancellationToken cancellationToken = default);

    /// <summary>炸板股票池取全。</summary>
    Task<IReadOnlyList<LimitBreakPoolItem>> GetAllLimitBreakPoolAsync(long? dateMs = null, CancellationToken cancellationToken = default);

    /// <summary>连板天梯：近 30 个交易日连板梯队矩阵（固定窗口，无入参）。</summary>
    Task<LimitUpLadderData> GetLimitUpLadderAsync(CancellationToken cancellationToken = default);

    /// <summary>个股异动原因（当日全市场列表）：tagCodes 逗号分隔（LIMIT_UP/LIMIT_DOWN/SHARP_RISE/SHARP_FALL/RAPID_RALLY/RAPID_DECLINE），空则全量。</summary>
    Task<AnomalyAnalysisData> GetAnomalyAnalysisListAsync(string? tagCodes = null, CancellationToken cancellationToken = default);

    /// <summary>个股异动原因（按标的）：thscodes 逗号分隔，1–50 个。</summary>
    Task<AnomalyAnalysisData> GetAnomalyAnalysisStockAsync(string thscodes, CancellationToken cancellationToken = default);

    /// <summary>飙升榜：period: day/hour。</summary>
    Task<RankListData> GetSkyrocketListAsync(string period = "day", CancellationToken cancellationToken = default);

    /// <summary>热股榜：period: day（24 小时榜）/hour。</summary>
    Task<RankListData> GetHotStockListAsync(string period = "day", CancellationToken cancellationToken = default);

    /// <summary>历史热股榜：date 为 yyyy-MM-dd（需在服务器最近一年窗口内）。</summary>
    Task<HotStockHistoryData> GetHotStockListHistoryAsync(string date, CancellationToken cancellationToken = default);

    /// <summary>热股排名趋势：单只 thscode 在日期区间内的热榜排名走势（区间 ≤ 1 年）。</summary>
    Task<RankTrendData> GetHotStockRankTrendAsync(string thscode, string startDate, string endDate, CancellationToken cancellationToken = default);

    /// <summary>龙虎榜：boardType: all/org/hot_money；date 为 yyyy-MM-dd（省略取最新可用交易日）。</summary>
    Task<DragonTigerData> GetDragonTigerListAsync(string boardType = "all", string? date = null, CancellationToken cancellationToken = default);
}
