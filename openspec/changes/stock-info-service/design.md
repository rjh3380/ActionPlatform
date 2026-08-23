## Context

ActionPlatform 是 .NET 8 控制台应用（`ActionPlatform` 入口 + `ActionPlatform.Core` 类库），已具备 IOC（Microsoft.Extensions.DependencyInjection）、日志（NLog）、消息（EasyNotice 飞书等）基础设施，配置走 `appsettings.json` + `appsettings.Local.json`（后者 gitignore、不入库）覆盖机制。

本次新增股票数据服务：以**方法库**形式封装同花顺 Financial-API（`https://fuyao.aicubes.cn`，`X-api-key` 认证），方法返回值即数据。用户明确：不做落盘、不接定时、仅提供方法。数据范围（用户选定）：行情/K线、元信息、集合竞价（含流通市值）、涨跌停/异动/热榜/龙虎榜。

## Goals / Non-Goals

**Goals:**

- 提供 `IStockDataService` 服务接口与强类型 DTO，覆盖选定数据范围的全部端点，与 Messaging/Logging 服务形态一致
- 统一 `ApiResponse` 信封解析：`code = 0` 返回强类型数据，`code != 0` 抛异常
- 通过 DI 注册（`AddStockDataService`，typed HttpClient），ApiKey 从配置读取（`FinancialApi:ApiKey`），支持 `appsettings.Local.json` 覆盖
- 方法形态贴合调用方：构造器注入 `IStockDataService`，直接 `await stockData.GetLimitUpPoolAsync(...)` 拿数据

**Non-Goals:**

- 不做数据落盘、缓存、定时任务（用户明确不需要）
- 不做财务/估值、指数、基金端点（未选，接口后续可扩展）
- 不做全市场 Parquet 导出（依赖登录态 Cookie + S3 预签名链接，返回文件而非数据）
- 不做股票基础信息/总市值（上游文档标注「敬请期待」）
- 不引入第三方 HTTP/JSON 库（HttpClient + System.Text.Json 已足够）

## Decisions

### D1: 服务形态 —— `IStockDataService` / `StockDataService`，typed HttpClient 注册

与既有 Messaging（`IMessageNotifier`）/ Logging 服务形态一致：公开服务接口 `IStockDataService`（`GetXXXAsync(...)` 方法组），实现类 `StockDataService`，通过 `AddStockDataService(IConfiguration)` 注册到 DI，消费方构造器注入后直接调用方法取数据。

`AddStockDataService` 中调用 `services.AddHttpClient<IStockDataService, StockDataService>()`，由 DI 管理 HttpClient 生命周期（自动轮换 DNS、连接复用），控制台应用需新增 `Microsoft.Extensions.Http` 包引用。

- 备选 A：手动 `new HttpClient` —— 连接泄漏、无生命周期管理，不可取。
- 备选 B：直接注册 HttpClient 单例 —— 丢失 typed client 的强类型语义与默认超时策略。
- 备选 C：只暴露静态方法/无接口 —— 无法 DI 注入、无法测试替身，与项目既有服务形态不一致。

### D2: 统一 `ApiResponse<T>` 信封 + 失败抛 `FinancialApiException`

所有端点返回 `{code, message, request_id, data}`（HTTP 恒为 200）。`StockDataService` 内部统一 `GetDataAsync<T>(path, query)`：请求 → 反序列化信封 → `code != 0` 抛 `FinancialApiException(code, message, request_id)` → 返回 `data`。

- `data` 为 null 或缺失时抛异常（数据缺失属于异常）。
- 反序列化失败（字段改名/上游变更）抛 `JsonException` 包装异常，便于诊断。

- 备选：返回信封对象让调用方判 code —— 调用方代码冗余；「方法返回值就是数据」的需求直接否决此形态。

### D3: 模型按端点独立命名，与上游字段名一一对应

每个端点一个 `data` 模型 + 一个 `item` 模型（如 `SnapshotData`/`PriceSnapshotItem`、`LimitUpPoolData`/`LimitUpPoolItem`），属性名与上游 JSON 字段完全一致（`price_change_ratio_pct`、`continue_day_cnt` 等），配合 `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`。字段类型参照上游文档：number → `decimal`（金额/价格），integer → `int`，时间戳 → `long`。

- 复用通用模型（如 `TickerItem` 在检索/列表共用）合理，跨端点同名字段不强行合并。

### D4: 分页端点封装为「一次性取全」与「手动分页」双形态

涨停/跌停/炸板池用 `page/size`、标的列表与全市场快照用 `limit/offset`：

- 提供带游标语义的方法（`page`/`size` 入参、返回分页信息），同时提供便捷重载 `GetAllLimitUpPoolAsync(dateMs)` 内部循环取全（自动处理 `total`/`pages`）。
- 调用方控制粒度：方法库形态下两种都要，避免「想要全量还得自己写循环」。

### D5: Options 绑定 `FinancialApi` 配置节

`FinancialApiOptions { BaseUrl = "https://fuyao.aicubes.cn", ApiKey, TimeoutSeconds = 30 }`，`AddStockDataService` 中 `services.Configure<FinancialApiOptions>(config.GetSection("FinancialApi"))`。ApiKey 为空时服务仍注册，调用时抛异常（spec 已固化该场景）。`appsettings.json` 放占位节（ApiKey 空字符串），真实 Key 填 `appsettings.Local.json`。

### D6: 时间参数统一毫秒戳，提供日期辅助方法

上游要求毫秒 Unix 时间戳（Asia/Shanghai）。公开方法接受 `long startMs/endMs`（与上游对齐），另提供 `FinancialApiDateHelpers`（或静态方法）做 `DateTimeOffset` ↔ 毫秒戳、`yyyy-MM-dd` ↔ 毫秒戳转换（Asia/Shanghai 时区，遇夏令时差异时按固定 +08:00 处理——中国无夏令时）。

### D7: 并发与限流

涨停池等分页取全时串行循环（每页 200 条，涨停池通常 1–2 页，量小）；历史 K 线/复权因子为单只请求，由调用方决定并发。客户端不做内置限流——上游未披露明确 QPS 限额，方法库形态下由调用方掌控节奏。

## Risks / Trade-offs

- **上游字段变更导致反序列化失败** → 统一信封解析处捕获并抛出带 JSON 上下文的异常；模型层集中在一个目录，易排查。
- **全市场快照分页全量拉取量大**（~5500 只，每页 100 → 55 页） → 不提供「全量取全」重载，只暴露分页方法，由调用方决定；文档注释中标注页数与成本。
- **ApiKey 泄露** → 仅读配置，不写日志、不输出；`appsettings.Local.json` 已 gitignore。
- **竞价数据仅终态（final）完整**（live 阶段数据可能不完整） → 默认 `stage=final`，文档注释说明。
- **HTTP 恒为 200，业务错误走 code** → 统一在解析层校验 code，避免调用方遗漏。
- **上游无总市值端点**（基础信息「敬请期待」） → 以竞价快照 `float_market_cap`（流通市值）覆盖市值类需求，文档中注明。

## Migration Plan

- 新增 Core 项目引用 `Microsoft.Extensions.Http`（10.0.11，与既有 Microsoft.Extensions 系列版本一致）
- `appsettings.json` 增加 `FinancialApi` 占位节；用户向 `appsettings.Local.json` 追加真实 ApiKey
- 无破坏性变更；回滚 = 移除 AddStockDataService 调用与占位配置

## Open Questions

- 是否需要 `IEnumerable` 批量 K 线便捷方法（给定 thscode 列表循环取 K 线）？——默认不做，调用方自行循环（K 线单只端点限制）。
- 涨停池「取全」重载的日期默认值：默认取服务器今日（省略 `date_ms`），符合上游语义。

### D8: 全市场竞价扫描 + 上游限流处理（实测验证）

竞价快照端点要求显式传 `thscodes`，不支持全市场分页。全市场扫描实现：代码表（`ListTickersAsync` 分页取全，约 5500 只）→ 每批 100 个（`BatchSize`，参照估值端点 100 token 上限保守取值）→ 串行拉取（每批间 100ms 节流）。

**实测限流**：上游存在滑动窗口配额限流，瞬时请求过多返回 HTTP 429 `{"code":429,"message":"request limit exceeded"}`。实测串行 56 批（~4.8 req/s）在配额窗口内可全部成功；连续大流量测试会触发 429 并需等待配额恢复。

- 处理：`GetDataAsync` 统一对 HTTP 429 指数退避重试（2s/4s/8s，最多 3 次）；串行扫描默认节流。
- 并发：`GetAllAuctionSnapshotsAsync(maxConcurrency > 1)` 由调用方显式开启，重试兜底。
- 备选：无重试直接抛异常 —— 全市场扫描（56 次请求）在配额紧张的时段必失败，不可接受。
