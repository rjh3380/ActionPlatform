## 1. ActionBase 核心

- [x] 1.1 新建 `ActionPlatform.Core/Services/Actions/TriggerMode.cs`：`enum TriggerMode { Interval, Scheduled }`
- [x] 1.2 新建 `ActionBase.cs`：`Name`（abstract）、`IsEnabled`（默认 true）、`TriggerMode`（默认 Interval）、`Interval`（TimeSpan）、`ScheduledTime`（TimeOnly）；内部状态 `LastTriggeredAt` / `IsRunning`；`MarkExecuted()` / `MarkCompleted()`（internal）
- [x] 1.3 实现 `CanExecuteAsync(DateTimeOffset now, CancellationToken ct)`（virtual，默认按模式判断：间隔=距上次 ≥ Interval；定时=本地时刻 ≥ ScheduledTime 且当日未执行；执行中一律 false）
- [x] 1.4 定义 `ExecuteAsync(CancellationToken ct)`（abstract，子类实现）

## 2. ActionLoop 调度循环

- [x] 2.1 新建 `ActionLoop.cs`：构造注入 `ILogger<ActionLoop>` 与 `IEnumerable<ActionBase>`；轮询循环按快照遍历 → 跳过未启用 → `CanExecuteAsync` → `MarkExecuted` + `await ExecuteAsync` + `MarkCompleted`（finally）→ `Task.Delay(PollInterval)`
- [x] 2.2 实现 `StartAsync(ct)` / `StopAsync()` 生命周期（Task.Run 常驻循环，取消令牌停止）
- [x] 2.3 异常隔离：判断与干活方法各自 try/catch，`LogError`（含 Action Name）后循环继续；轮询间隔默认 100ms 可配置

## 3. DI 注册与启动

- [x] 3.1 新建 `ActionServiceExtensions.cs`：`AddActionLoop()` 注册 `ActionLoop` 单例；`AddAction<T>()` 注册 `ActionBase` 单例
- [x] 3.2 `App.cs`：构造注入 `ActionLoop`，`RunAsync` 中 `StartAsync(ct)`，退出前 `StopAsync()`

## 4. 验证

- [x] 4.1 示例间隔 Action（如每 5 秒记录一条日志），验证启动即轮询、间隔触发与日志输出
- [x] 4.2 示例每日定时 Action（如每天 09:25 推送竞价金额 Top10，复用 `IStockDataService` + `IMessageNotifier`），验证定时触发与全链路
- [x] 4.3 行为验证：未启用 Action 不执行；长执行 Action 不重复触发；单 Action 抛异常不影响其他 Action 与循环
