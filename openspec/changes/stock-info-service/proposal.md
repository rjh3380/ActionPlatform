## Why

ActionPlatform 目前只有日志与消息推送能力，无法获取任何金融市场数据。业务需要以编程方式获取 A 股行情、K 线、涨停池、集合竞价等数据（同花顺 Financial-API，`https://fuyao.aicubes.cn`），并以方法库形式暴露——方法返回值即数据，后续业务逻辑直接调用，不做落盘、不接入定时。

## What Changes

- 在 `ActionPlatform.Core` 新增股票数据服务（`Services/StockData/`）：
  - `IStockDataService` / `StockDataService`：同花顺 Financial-API HTTP 服务，`X-api-key` 认证，统一 `ApiResponse` 信封解析，强类型返回数据（与 Messaging/Logging 服务形态一致，DI 注入使用）
  - 方法组（按用户选择的第一批范围）：
    - **行情 / K 线**：行情快照（批量 thscodes / 全市场分页）、历史日 K（单只，支持复权）、复权因子事件流
    - **元信息**：标的检索（search）、标的列表（list，分页）、交易日历
    - **竞价数据**：A 股集合竞价快照（竞价价格/涨幅/成交额/量比/未匹配量/**流通市值**）、短线风向标竞价基准（竞价涨幅 + 高开/放量等标签）
    - **涨跌停 / 异动 / 龙虎榜**：涨停池、跌停池、炸板池、连板天梯、个股异动原因（列表 / 按标的）、飙升榜、热股榜（当前 / 历史 / 排名趋势）、龙虎榜（全部 / 机构 / 游资）
  - 配置：`FinancialApi` 配置节（`BaseUrl`、`ApiKey`、`TimeoutSeconds`），ApiKey 走既有 `appsettings.Local.json` 覆盖机制，不入库
  - DI 注册：`AddStockDataService()` 扩展方法
- 不实现（本次范围外，文档中亦标注「敬请期待」）：股票基础信息（总市值等）、个股所属指数查询
- 不实现：全市场 Parquet 导出（依赖登录态 Cookie + S3 预签名链接，返回文件而非数据，与「方法返回数据」的形态不符）
- 不实现：财务 / 估值、指数、基金端点（用户未选，架构可扩展）

## Capabilities

### New Capabilities

- `stock-data`: 同花顺 Financial-API 数据获取方法库——行情快照、历史 K 线、复权因子、标的检索/列表、交易日历、集合竞价、涨跌停池、连板天梯、异动原因、热榜、龙虎榜

### Modified Capabilities

<!-- 无 -->

## Impact

- **代码**：`ActionPlatform.Core/Services/StockData/` 新增（接口、实现、模型 DTO、Options、DI 扩展）；`ActionPlatform.Core.csproj` 增加 `Microsoft.Extensions.Http` 包（typed HttpClient）与 System.Text.Json 支持（net8.0 内置）
- **配置**：`appsettings.json` 增加 `FinancialApi` 占位节（空 ApiKey）；`appsettings.Local.json` 由用户自行填写真实 ApiKey（已被 .gitignore 排除）
- **行为**：ApiKey 未配置时调用方法抛异常/警告并跳过（不阻塞其他能力）
- **对外**：无第三方包新增（除 Microsoft.Extensions.Http）；不改变既有日志/消息行为
