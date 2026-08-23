## ADDED Requirements

### Requirement: Action 基类与触发模式
系统 SHALL 提供 `ActionBase` 抽象基类，统一封装 Action 的启用状态（`IsEnabled`）、触发模式与执行状态。系统 SHALL 支持两种触发模式：间隔触发（每间隔指定毫秒数触发一次）与每日定时触发（每天本地时区指定时刻触发一次，当天只触发一次）。基类 SHALL 提供判断方法（`CanExecuteAsync`，默认按触发模式完成判断）与干活方法（`ExecuteAsync`，子类必须实现），子类 SHALL 可重写判断方法以叠加业务条件。基类 SHALL 维护上次执行时间与执行中标志，执行中不参与触发判断。

#### Scenario: 间隔触发判断
- **WHEN** 间隔模式 Action 距离上次执行已超过其间隔时长
- **THEN** 判断方法返回可执行，执行完成后更新上次执行时间；未到间隔时长时判断方法返回不可执行

#### Scenario: 每日定时触发判断
- **WHEN** 定时模式 Action 的本地时间到达指定时刻且当日尚未执行过
- **THEN** 判断方法返回可执行，当日不再重复触发；跨天（次日）再次到达指定时刻时重新可执行

#### Scenario: 子类重写判断方法
- **WHEN** 子类重写 `CanExecuteAsync`，先调用基类默认判断并叠加业务条件（如仅交易日执行）
- **THEN** 按叠加后的条件决定是否执行，基类默认判断逻辑仍然生效

#### Scenario: 未启用跳过
- **WHEN** Action 的 `IsEnabled` 为 false
- **THEN** 调度循环跳过该 Action，不执行其判断与干活方法

#### Scenario: 执行中不重复触发
- **WHEN** Action 的干活方法正在执行（尚未返回）且触发条件再次满足
- **THEN** 不重复触发，待本次执行结束后按触发模式重新计算下一次触发

### Requirement: 调度循环
系统 SHALL 提供统一调度循环 `ActionLoop`：程序启动时启动，常驻轮询所有已注册的 Action，对每个已启用 Action 执行判断方法，条件满足时执行其干活方法。循环 SHALL 按可配置的轮询间隔（默认 100ms）周期检查。单个 Action 的判断或干活方法抛出异常时，循环 SHALL 记录日志并继续轮询其他 Action，不得中断整个循环。

#### Scenario: 启动后轮询执行
- **WHEN** 调度循环启动且存在已启用、条件满足的 Action
- **THEN** 循环执行该 Action 的干活方法，并在后续轮询中继续检查所有 Action

#### Scenario: 单 Action 异常不影响其他
- **WHEN** 某个 Action 的干活方法或判断方法抛出异常
- **THEN** 循环记录该 Action 的异常日志，其他 Action 的检查与执行不受影响，循环持续运行

### Requirement: 注册与启动
系统 SHALL 提供 DI 注册扩展：`AddActionLoop()` 注册调度循环单例，`AddAction<T>()` 注册 Action 子类（T 继承 `ActionBase`）。系统 SHALL 支持注册多个 Action，调度循环通过构造函数注入自动收集全部已注册 Action。系统 SHALL 在应用启动流程（`App.RunAsync`）中启动调度循环，并在应用退出前停止循环。

#### Scenario: 多 Action 自动收集
- **WHEN** 应用通过 `AddAction<T1>()`、`AddAction<T2>()` 注册多个 Action 并调用 `AddActionLoop()`
- **THEN** 调度循环收集全部已注册 Action，按各自的触发模式轮询判断与执行

#### Scenario: 应用启动时启动循环
- **WHEN** 应用启动执行 `App.RunAsync`
- **THEN** 调度循环随之启动，常驻运行直至应用退出（退出前停止）
