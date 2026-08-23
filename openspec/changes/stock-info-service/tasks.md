## 1. 项目准备

- [x] 1.1 `ActionPlatform.Core.csproj` 添加 `Microsoft.Extensions.Http` 10.0.11 包引用（与既有 Microsoft.Extensions.* 版本一致）
- [x] 1.2 `appsettings.json` 添加 `FinancialApi` 占位配置节（`BaseUrl`、空 `ApiKey`、`TimeoutSeconds`），确认被 copy 到输出目录

## 2. 基础设施

- [x] 2.1 创建 `Services/StockData/FinancialApiOptions.cs`：`BaseUrl`（默认 `https://fuyao.aicubes.cn`）、`ApiKey`、`TimeoutSeconds`（默认 30）
- [x] 2.2 创建 `Services/StockData/FinancialApiException.cs`：含 `Code`、`Message`、`RequestId`，继承 `Exception`
- [x] 2.3 创建信封与通用模型：`ApiResponse<T>`（code/message/request_id/data）、`TickerItem`（thscode/ticker/name/exchange/asset_type/currency）
- [x] 2.4 创建 `Services/StockData/IStockDataService.cs` 服务接口（全部方法签名）
- [x] 2.5 创建 `Services/StockData/StockDataService.cs` 实现：内部 `GetDataAsync<T>(path, query)` 统一走信封解析（非 0 code 抛 `FinancialApiException`、data 缺失抛异常、反序列化失败包装）；ApiKey 为空时调用抛异常
- [x] 2.6 创建 `Services/StockData/StockDataServiceExtensions.cs`：`AddStockDataService(IConfiguration)`，`Configure<FinancialApiOptions>` + `AddHttpClient<IStockDataService, StockDataService>`（设置 TimeoutSeconds）
- [x] 2.7 创建日期辅助 `FinancialApiDateHelper`：`DateTimeOffset` ↔ 毫秒戳、`yyyy-MM-dd` ↔ 毫秒戳（Asia/Shanghai 固定 +08:00）

## 3. 行情 / K 线端点

- [x] 3.1 模型：`SnapshotData`/`PriceSnapshotItem`（last_price、price_change、price_change_ratio_pct、OHLC、prev_price、volume、turnover）
- [x] 3.2 模型：`HistoricalData`/`PriceBarItem`（date_ms、OHLC、volume、turnover）
- [x] 3.3 模型：`AdjustmentFactorsData`/`AdjustmentFactorItem`（ticker、ex_date_ms、dividend_per_share、per_share_bonus）
- [x] 3.4 实现 `GetSnapshotAsync(thscodes, limit, offset)`：thscodes 与分页二选一；返回 `SnapshotData`
- [x] 3.5 实现 `GetHistoricalKLineAsync(thscode, startMs, endMs, adjust="forward")`：返回 `HistoricalData`
- [x] 3.6 实现 `GetAdjustmentFactorsAsync(thscode, from?, to?)`：返回 `AdjustmentFactorsData`

## 4. 元信息端点

- [x] 4.1 模型：`TickerListData`（timestamp、item[] = TickerItem）
- [x] 4.2 实现 `SearchTickersAsync(q, exchange?, assetType?, limit=10)`：返回 `TickerItem` 列表
- [x] 4.3 实现 `ListTickersAsync(exchange="SH,SZ", assetType?, limit=1000, offset)`：返回 `TickerListData`
- [x] 4.4 模型：`TradingDaysData`/`TradingDayItem`（date_ms、date）；实现 `GetTradingDaysAsync()`：返回交易日列表

## 5. 集合竞价端点

- [x] 5.1 模型：`AuctionSnapshotData`/`AuctionSnapshotItem`（auction_price、auction_pct、auction_volume、auction_amount、auction_unmatched、auction_turnover_pct、auction_yesterday_ratio_pct、auction_volume_ratio、pre_close_price、open_price、last_price、float_market_cap、name）
- [x] 5.2 实现 `GetAuctionSnapshotAsync(thscodes, stage="final")`：返回 `AuctionSnapshotData`
- [x] 5.3 模型：`ShortTermBenchmarkData`/`ShortTermBenchmarkItem`（auction_pct、tags）；实现 `GetShortTermBenchmarkAsync(date?)`：返回 `ShortTermBenchmarkData`

## 6. 涨跌停 / 异动 / 热榜 / 龙虎榜端点

- [x] 6.1 模型：`LimitUpPoolData`（timestamp、pagination、item[]）+ `LimitUpPoolItem`（name、is_st、is_new、last_price、price_change_ratio_pct、limit_up_time、limit_up_reason、continue_day_text、continue_day_cnt、seal_money、max_seal_money）
- [x] 6.2 实现 `GetLimitUpPoolAsync(dateMs?, page=1, size=50, sortField?, sortDir?)` 与 `GetAllLimitUpPoolAsync(dateMs?)` 取全重载
- [x] 6.3 模型：`LimitDownPoolData`/`LimitDownPoolItem`（first_limit_time、last_limit_time、turnover_ratio_pct 等）；实现 `GetLimitDownPoolAsync(...)` 与取全重载
- [x] 6.4 模型：`LimitBreakPoolData`/`LimitBreakPoolItem`（open_times、turnover_ratio_pct、turnover 等）；实现 `GetLimitBreakPoolAsync(...)` 与取全重载
- [x] 6.5 模型：`LimitUpLadderData`（window、item[]：date + boards{two_board..seven_over}）+ 梯队内模型（thscode、ticker、name、board_num、sign_level、seal_nextday）；实现 `GetLimitUpLadderAsync()`
- [x] 6.6 模型：`AnomalyAnalysisData`/`AnomalyAnalysisItem`（stock_name、analysis_content、keyword_list、thscode、tag_name）；实现 `GetAnomalyAnalysisListAsync(tagCodes?)` 与 `GetAnomalyAnalysisStockAsync(thscodes)`
- [x] 6.7 模型：`RankListData`/`RankListItem`（rank、heat、rank_change、rank_trend）；实现 `GetSkyrocketListAsync(period="day")`、`GetHotStockListAsync(period="day")`、`GetHotStockListHistoryAsync(date)`、`GetHotStockRankTrendAsync(thscode, startDate, endDate)`
- [x] 6.8 模型：`DragonTigerData`（stock_items + hot_money_items 及各自明细模型）；实现 `GetDragonTigerListAsync(boardType="all", date?)`

## 7. 集成与验证

- [x] 7.1 `App.cs`（或 Program.cs 装配处）调用 `AddStockDataService(configuration)` 注册服务
- [x] 7.2 本地验证：`dotnet build` 通过；临时用真实 ApiKey 调通至少一个端点（如交易日历、竞价快照），确认信封解析与强类型返回正确
- [x] 7.3 验证 ApiKey 未配置场景：移除 Local 配置后调用方法抛 `FinancialApiException`，程序其余功能不受影响
- [x] 7.4 确认 appsettings.json 的 `FinancialApi` 占位节不包含任何真实密钥，`appsettings.Local.json` 仍被 gitignore 排除
