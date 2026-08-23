## Why

平台目前只有「服务方法」形态（如 `IStockDataService` 提供数据、`IMessageNotifier` 发送通知），业务要自动化执行（如定时获取行情、监控涨跌停、触发推送）只能自己写循环或依赖外部调度。缺少一个统一的「Action」机制：把一段业务动作定义为可插拔的子类，由平台统一负责启用判断与触发执行，业务方只需关心「判断」与「干活」两个方法。

## What Changes

- 新增抽象基类 `ActionBase`：统一封装触发模式（间隔触发 / 每日定时触发）、启用状态、**判断方法**（`CanExecuteAsync`，基类按触发模式完成默认判断，子类可重写）与**干活方法**（`ExecuteAsync`，基类定义、子类必须实现）
- 新增调度循环 `ActionLoop`：程序启动时启动，循环轮询所有已启用的 Action；`CanExecuteAsync` 返回 true 时执行 `ExecuteAsync`；同一 Action 执行中不重复触发；单个 Action 异常不影响其他 Action 继续
- 两种触发模式（子类声明，判断逻辑由基类完成）：
  - **间隔触发**：每间隔指定毫秒数触发一次
  - **定时触发**：每日到指定时刻触发一次（用户已确认：每日重复，非一次性）
- 新增 DI 注册扩展 `AddActionLoop`，与既有 ServiceManager / 构造器注入模式一致；`App.RunAsync` 中启动循环

## Capabilities

### New Capabilities
- `action-framework`: Action 抽象基类（触发模式、判断/干活方法）、统一调度循环（轮询已启用 Action、条件满足即执行、防重入与异常隔离）、DI 注册

### Modified Capabilities
<!-- 无既有 spec 行为变更 -->

## Impact

- **代码**：`ActionPlatform.Core` 新增 `Services/Actions/` 目录（`ActionBase`、`TriggerMode`、`ActionLoop`、DI 扩展）；`ActionPlatform/App.cs` 启动循环
- **API**：新增公共类型与注册方法，纯新增、无破坏性变更；既有服务（StockData/Messaging/Logging）不受影响
- **依赖**：无新增 NuGet 包（`Microsoft.Extensions.*` 既有引用已足够）
- **配置**：无需新增配置节（Action 属性在子类代码中声明）
