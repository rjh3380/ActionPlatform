## Context

ActionPlatform 是 .NET 8 控制台应用（`ActionPlatform` 入口 + `ActionPlatform.Core` 类库），已具备 IOC（`ServiceManager` 静态服务定位器 + 构造器注入）、日志（NLog）、消息（`IMessageNotifier` 飞书）与数据服务（`IStockDataService` 同花顺）基础设施。目前所有能力都是「方法形态」——业务想要定时/周期性地做某件事，需要自己写循环。

本次新增 Action 机制：定义 `ActionBase` 抽象基类（触发模式 + 判断方法 + 干活方法），由统一调度循环在程序启动后轮询所有已启用的 Action，条件满足即执行。用户已确认定时触发语义为**每日定时**（每天指定时刻触发一次，次日重新生效）。

## Goals / Non-Goals

**Goals:**

- 提供 `ActionBase` 抽象基类：启用状态、两种触发模式（间隔/每日定时）、判断方法（`CanExecuteAsync`，基类默认按模式判断、子类可重写）、干活方法（`ExecuteAsync`，子类实现）
- 提供统一调度循环 `ActionLoop`：程序启动时启动，轮询所有已启用 Action；条件满足执行；防重入（同一 Action 执行中不重复触发）；异常隔离（单个 Action 失败不影响其他）
- 与既有模式一致：DI 注册（`AddActionLoop` / `AddAction<T>`），`App.RunAsync` 中启动
- 子类只需继承 `ActionBase` + 声明模式参数 + 实现干活方法，即可被循环自动调度

**Non-Goals:**

- 不做持久化调度（重启后当天定时已触发过的状态不记忆）
- 不做跨 Action 并行执行（v1 串行，后续可并行化）
- 不做 Cron 表达式/多时区支持（固定进程本地时区）
- 不做分布式锁/多实例部署支持（单进程控制台应用）
- 不改变既有服务（StockData/Messaging/Logging）的任何行为

## Decisions

### D1: `ActionBase` 形态 —— 判断方法 virtual（基类默认实现），干活方法 abstract（子类必须实现）

```csharp
public enum TriggerMode { Interval, Scheduled }   // 间隔触发 / 每日定时触发

public abstract class ActionBase
{
    public abstract string Name { get; }                    // 唯一标识（日志用）
    public bool IsEnabled { get; set; } = true;             // 是否启用，轮询时跳过未启用的
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Interval;
    public TimeSpan Interval { get; set; }                  // 间隔模式：触发间隔
    public TimeOnly ScheduledTime { get; set; }             // 定时模式：每日触发时刻

    // 判断方法：基类按模式完成默认判断，子类可 override 叠加额外条件（如「仅交易日」）
    protected virtual Task<bool> CanExecuteAsync(DateTimeOffset now, CancellationToken ct);

    // 干活方法：基类定义，子类必须实现（「可在子类里进行重写」的强制形式）
    protected abstract Task ExecuteAsync(CancellationToken ct);
}
```

- 判断逻辑（默认实现）：间隔模式 = `now - LastTriggeredAt >= Interval`；定时模式 = `now` 的本地时刻 ≥ `ScheduledTime` 且当日未执行过（比较 `LastTriggeredAt` 日期）。防重入标志 `IsRunning` 参与判断：执行中一律不触发。
- 子类可重写判断方法：先 `await base.CanExecuteAsync(...)` 再叠加业务条件（如交易日判断），实现「判断在基类完成、子类可扩展」。
- 备选 A：判断方法也 abstract —— 每种模式判断逻辑每个子类各写一遍，与「判断在 ActionBase 里面完成」的诉求相悖。
- 备选 B：干活方法 virtual + 空默认 —— 允许子类不实现，属于 footgun，abstract 更安全。

### D2: 触发状态（LastTriggeredAt / IsRunning）内聚在 `ActionBase`

循环每次轮询读取 `CanExecuteAsync`，触发后调用 `MarkExecuted()`（内部更新 `LastTriggeredAt` 并置 `IsRunning`），执行结束（无论成败）调用 `MarkCompleted()`（清除 `IsRunning`）。状态字段为 `internal` 可见性 + `protected` 读取，子类可在重写的判断方法中读取，但不允许子类直接改写执行状态。

- 备选：状态放循环侧字典 —— 子类重写判断方法时读不到「上次执行时间」，扩展判断（如「距上次执行 ≥ 2 小时」）无法实现。

### D3: `ActionLoop` 调度循环 —— 单线程轮询 + 串行执行

```csharp
public sealed class ActionLoop
{
    // 构造器注入 ILogger<ActionLoop> 与 IEnumerable<ActionBase>（DI 自动收集所有已注册 Action）
    public Task StartAsync(CancellationToken ct);   // Task.Run 常驻循环
    public Task StopAsync();                        // 取消循环
}
```

- 循环体：每轮按快照遍历所有 Action → `IsEnabled` 跳过 → `CanExecuteAsync` 通过则 `await ExecuteAsync`（包 try/catch）→ 全部检查完后 `await Task.Delay(PollInterval)`。
- 轮询间隔默认 100ms（`PollInterval` 可配置，构造器参数），对「间隔触发毫秒级精度」足够，进程内检查开销可忽略。
- 串行执行保证状态无竞争；`IsRunning` 防重入使后续并行化无需改动状态模型。
- 异常隔离：每个 Action 的 `ExecuteAsync` / `CanExecuteAsync` 各自 try/catch，异常记 `LogError`（含 Action Name）后循环继续。
- 备选：每 Action 一条独立循环 Task —— 实现复杂、状态同步成本高，v1 不需要。
- 备选：System.Threading.Timer 每 Action 一个 —— 与「统一循环轮询判断」的诉求不符，且定时触发（每日时刻）用 Timer 实现繁琐。

### D4: DI 注册 —— `AddActionLoop()` + `AddAction<T>()`

```csharp
public static IServiceCollection AddActionLoop(this IServiceCollection services)
{
    services.AddSingleton<ActionLoop>();
    return services;
}
public static IServiceCollection AddAction<T>(this IServiceCollection services)
    where T : ActionBase
    => services.AddSingleton<ActionBase, T>();   // 多个 T 注册后 IEnumerable<ActionBase> 自动收集
```

- 控制台应用手动装配容器（`ServiceManager.InitService` 扫描 `typeof(App).Assembly`），既有机制已把 `ActionPlatform` 程序集中的类型注册进容器；Action 子类放哪个程序集，就在哪侧调用 `AddAction<T>` 显式注册。
- 生命周期：`ActionLoop` 与 Action 均为单例（循环常驻，Action 内可缓存状态，如上次行情）。
- 启动：`App.RunAsync` 中 `await _actionLoop.StartAsync(ct)`（当前 App 是演示代码，改为启动循环）；退出前 `StopAsync()`。
- 备选：托管 IHostedService —— 项目是控制台应用，无 Host 宿主，与既有 ServiceManager 模式不一致。

### D5: 定时触发的语义边界

- 每日定时：`ScheduledTime` 为本地时区时刻；当天已触发过则不再触发（`LastTriggeredAt` 日期 == 今天）；跨天（零点后）重置，次日到点再次触发。
- 重启语义：触发状态在内存中，进程重启后丢失——若重启时当日已过触发时刻，会再次触发一次。可接受（进程内调度器不做持久化，文档注明）。
- 时刻精度：轮询方式最多偏差一个 `PollInterval`（100ms），对「到点执行」类动作足够。

## Risks / Trade-offs

- **定时触发状态不持久化（重启重复触发）** → 设计决策明确接受；若后续需要，可在 `ActionBase` 加持久化钩子，不在本次范围。
- **串行执行下，长耗时 Action 会推迟其他 Action** → v1 明确接受（可预期、无竞争）；`IsRunning` 模型已为并行化留好接口，需要时在循环内对每个 Action 独立启动执行任务即可。
- **子类 `ExecuteAsync` 死循环/不返回** → 由调用方控制，循环被阻塞（串行模型固有）；文档注明建议子类控制单次执行时长。
- **判断方法抛异常** → 循环内 try/catch 隔离，记日志后继续轮询其他 Action。
- **轮询延迟偏差** → 最大一个 `PollInterval`（100ms），文档注明。

## Migration Plan

- 纯新增：`ActionPlatform.Core/Services/Actions/`（`TriggerMode.cs`、`ActionBase.cs`、`ActionLoop.cs`、`ActionServiceExtensions.cs`）
- `App.RunAsync` 中调用 `ServiceManager` 获取 `ActionLoop` 并启动（演示逻辑保留在启动前，不删除既有能力）
- 无破坏性变更；回滚 = 移除 `AddActionLoop`/`AddAction<T>` 调用与启动语句

## Open Questions

- 首个示范 Action 是否需要？（用于验证机制，例如「每日 9:25 推送竞价 Top10」—— 复用 `IStockDataService`，顺带打通数据服务 → 消息服务 → Action 全链路）—— 默认做一个最小演示 Action 验证机制，用户在 tasks 阶段可调整。
