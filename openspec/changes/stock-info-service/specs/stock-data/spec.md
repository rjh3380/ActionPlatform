## ADDED Requirements

### Requirement: 统一 API 客户端与响应信封
系统 SHALL 提供同花顺 Financial-API HTTP 客户端服务，以 `https://fuyao.aicubes.cn` 为 BaseUrl，所有请求携带 `X-api-key` 请求头；统一解析 `ApiResponse` 信封（`code` / `message` / `request_id` / `data`），`code = 0` 时方法直接返回 `data` 中的强类型数据，`code != 0` 时抛出带 `code` / `message` / `request_id` 的异常。

#### Scenario: 成功响应返回强类型数据
- **WHEN** 调用任一数据方法且上游返回 `code = 0`
- **THEN** 方法返回对应端点 `data` 字段的强类型对象（如快照列表、K 线列表）

#### Scenario: 业务错误抛出异常
- **WHEN** 上游返回非 0 的 `code`（如 1001 参数缺失、2001 认证失败、1002 参数非法、1003 超限）
- **THEN** 抛出 `FinancialApiException`，异常消息包含 `code`、`message` 与 `request_id`

#### Scenario: ApiKey 未配置
- **WHEN** 配置节 `FinancialApi:ApiKey` 为空或缺失
- **THEN** 服务正常注册，但任何数据方法调用时抛出异常，提示 ApiKey 未配置；不阻塞程序其他能力

### Requirement: 行情与 K 线获取
系统 SHALL 提供行情快照、历史日 K 线、复权因子事件流三个获取方法。

- 行情快照：支持逗号分隔 thscodes 批量模式与 `limit`/`offset` 全市场分页模式；返回最新价、涨跌额/幅、OHLC、前收盘、成交量、成交额
- 历史 K 线：单只 thscode，`interval=1d`，`start`/`end` 毫秒时间戳（窗口 ≤ 10 年），支持 `adjust` 复权方式（`none`/`forward`/`backward`，默认 `forward`）；返回 OHLC、成交量、成交额、日期毫秒戳
- 复权因子：单只 thscode，可选 `from`/`to` 日期区间；返回除权除息事件（每股分红、送股比例），按除权日降序

#### Scenario: 批量行情快照
- **WHEN** 调用快照方法并传入 `600519.SH,000001.SZ`
- **THEN** 返回包含两只股票的 `PriceSnapshotItem` 列表，按入参顺序

#### Scenario: 全市场快照分页
- **WHEN** 调用快照方法且不传 thscodes，按 `limit`/`offset` 遍历
- **THEN** 每次返回至多 `limit` 条记录，直到某页不足 `limit` 条终止

#### Scenario: 历史 K 线取数
- **WHEN** 传入单只 thscode 与起止毫秒时间戳
- **THEN** 返回该区间日 K 线列表，含 `date_ms`、OHLC、`volume`、`turnover`

#### Scenario: 复权因子事件流
- **WHEN** 传入单只 thscode（可带日期区间）
- **THEN** 返回 `AdjustmentFactorItem` 列表，按 `ex_date_ms` 降序，含每股现金分红与送股比例

### Requirement: 元信息获取
系统 SHALL 提供标的检索、标的列表、交易日历三个获取方法。

- 标的检索：按 thscode / ticker / 中英文名子串搜索，支持 `exchange`、`asset_type`、`limit` 过滤，返回 `TickerItem`（thscode、ticker、name、exchange、asset_type、currency）
- 标的列表：按交易所与资产类别分页获取代码表，返回结构与检索一致
- 交易日历：无入参，返回近一年交易日序列，含 `date_ms` 与 `yyyyMMdd` 格式日期

#### Scenario: 按名称检索标的
- **WHEN** 调用检索方法并传入 `贵州茅台`
- **THEN** 返回匹配的标的列表，其中包含 `600519.SH` 及其名称

#### Scenario: 分页获取标的列表
- **WHEN** 按 `limit`/`offset` 遍历标的列表
- **THEN** 每次返回至多 `limit` 条，直到空页或不足 `limit` 终止

#### Scenario: 获取交易日历
- **WHEN** 调用交易日历方法
- **THEN** 返回近一年按时间升序的交易日列表

### Requirement: 集合竞价数据获取
系统 SHALL 提供 A 股集合竞价快照与短线风向标竞价基准两个获取方法。

- 竞价快照：逗号分隔 thscodes，`stage` 支持 `live`（实时）与 `final`（终态，默认）；返回竞价价格、竞价涨跌幅、竞价成交量/成交额/未匹配量、竞价换手率、竞价量比、相对昨日成交比例、前收盘/开盘/最新价与**流通市值**
- 短线风向标：可选 `date`（yyyy-MM-dd，默认上海时区当日）；返回竞价涨跌幅与标签列表（如「高开」「放量」）

#### Scenario: 获取竞价快照
- **WHEN** 调用竞价快照方法并传入 thscodes
- **THEN** 返回 `AuctionSnapshotItem` 列表，含 `auction_price`、`auction_pct`、`auction_amount`、`auction_volume_ratio` 与 `float_market_cap`

#### Scenario: 获取短线风向标
- **WHEN** 调用短线风向标方法（可指定日期）
- **THEN** 返回 `ShortTermBenchmarkItem` 列表，含 `auction_pct` 与 `tags`

### Requirement: 涨跌停 / 异动 / 热榜 / 龙虎榜获取
系统 SHALL 提供涨停池、跌停池、炸板池、连板天梯、个股异动原因、热榜、龙虎榜获取方法。

- 涨停池 / 跌停池 / 炸板池：按交易日（`date_ms` 可选，默认今日）与 `page`/`size` 分页（1–200），支持排序字段与方向；涨停池返回含涨停原因、连板数、封单金额；跌停池返回首次/最后跌停时间、换手率；炸板池返回开板次数、换手率、成交额
- 连板天梯：无入参，返回近 30 个交易日连板梯队矩阵（2 板至 7 板以上，各梯队最多 4 只）
- 个股异动原因：支持按标签过滤的全市场当日列表（`LIMIT_UP`/`LIMIT_DOWN`/`SHARP_RISE`/`SHARP_FALL`/`RAPID_RALLY`/`RAPID_DECLINE`），与按 1–50 个 thscode 批量查询两种形态；返回股票名称、异动原因分析、关键词、标签
- 热榜：飙升榜（`day`/`hour`）、热股榜（当前/历史按日/单只排名趋势）；返回排名、热度值、排名变化与趋势
- 龙虎榜：`board_type` 支持 `all`/`org`/`hot_money`，`date` 可选；返回个股明细（买卖额、净额、机构/游资净额、概念列表、涨跌停原因）与游资明细

#### Scenario: 分页获取涨停池
- **WHEN** 调用涨停池方法并指定 `date_ms` 与 `page`/`size`
- **THEN** 返回该交易日涨停股列表与分页信息（`total`/`pages`），列表项含 `limit_up_reason`、`continue_day_cnt`、`seal_money`

#### Scenario: 获取连板天梯
- **WHEN** 调用连板天梯方法
- **THEN** 返回近 30 个交易日各梯队（two_board 至 seven_over）的股票列表，最近交易日的 `seal_nextday` 为 null

#### Scenario: 按标的查询异动原因
- **WHEN** 调用异动方法并传入 1–50 个 thscode
- **THEN** 返回按输入顺序分组的当日异动原因，含 `analysis_content` 与 `tag_name`

#### Scenario: 获取龙虎榜
- **WHEN** 调用龙虎榜方法并指定 `board_type=hot_money`
- **THEN** 返回该榜单的个股明细与游资明细，含 `buy_value`/`sell_value`/`net_value` 等字段

### Requirement: 全市场竞价扫描与 TopN
系统 SHALL 提供全市场集合竞价扫描方法：内部自动分页拉取全部 A 股代码表（SH/SZ，asset_type=a-share），按每批 100 个 thscode 请求竞价快照，返回所有有竞价数据的股票。系统 SHALL 提供 TopN 方法，按指定竞价字段（竞价成交额/竞价涨跌幅/竞价量比/竞价换手率）降序取前 top 只。系统 SHALL 在串行扫描时对批次间施加节流，并对上游限流响应（HTTP 429）执行指数退避重试（最多 3 次），避免因瞬时请求过多导致全量扫描失败。

#### Scenario: 获取全市场竞价金额 Top10
- **WHEN** 调用 `GetTopAuctionAsync(top: 10, sortField: AuctionSortField.Amount)`
- **THEN** 返回按竞价成交额降序的前 10 只股票，每只含竞价价、竞价涨幅、竞价额、量比与流通市值

#### Scenario: 上游限流时自动重试
- **WHEN** 批量请求中上游返回 HTTP 429（request limit exceeded）
- **THEN** 系统按指数退避（2s/4s/8s）重试，最多 3 次；重试耗尽后抛出 `FinancialApiException` 携带原始 429 信息

#### Scenario: 并发扫描
- **WHEN** 调用 `GetAllAuctionSnapshotsAsync(maxConcurrency: 5)`
- **THEN** 按并发度并行拉取各批次并返回完整结果；429 时同样走退避重试
