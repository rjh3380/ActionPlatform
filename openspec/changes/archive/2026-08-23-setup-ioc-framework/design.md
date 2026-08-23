## Context

项目为空白 .NET 8 解决方案，含两个项目：

- `ActionPlatform`（控制台 Exe）：当前仅输出 "Hello, World!"，无任何服务装配。
- `ActionPlatform.Core`（类库）：只有空 `Services` 目录，无包引用、无既有规范。

目标是为后续业务开发打地基：官方 `Microsoft.Extensions.DependencyInjection` 作为 IOC 容器，日志用 NLog（经 `NLog.Extensions.Logging` 接入 `Microsoft.Extensions.Logging` 抽象），消息通知用 EasyNotice 开源库（支持钉钉/企业微信/飞书/邮件四渠道，按渠道分包：`EasyNotice.Core`、`EasyNotice.Email`、`EasyNotice.Dingtalk`、`EasyNotice.Feishu`、`EasyNotice.Weixin`）。

## Goals / Non-Goals

**Goals:**

- 搭建基于官方 Microsoft.Extensions.DependencyInjection 的 IOC 容器，统一服务注册与解析
- 提供日志基础服务：NLog 提供程序 + `Microsoft.Extensions.Logging` 抽象，`nlog.config` 驱动
- 提供消息基础服务：EasyNotice 注册 + `ActionPlatform.Core` 中的统一门面接口 `IMessageNotifier`
- 配置驱动：渠道参数从 `appsettings.json` 读取，不硬编码
- 最小可运行演示：`Program.cs` 装配容器并演示注入日志与消息

**Non-Goals:**

- 不引入 Web/ASP.NET Core 框架与 Host 构建器（保持控制台显式装配，便于理解容器行为）
- 不引入第三方 DI 容器（Autofac 等）
- 不做消息队列、事件总线（EasyNotice 是通知发送，非消息队列）
- 不做配置加密、密钥管理服务（WebHook 密钥仅从配置文件读取）

## Decisions

### D1: 使用官方 Microsoft.Extensions.DependencyInjection，不引入 Autofac

官方 DI 已覆盖本阶段全部需求（构造注入、生命周期、容器释放），零额外心智负担；后续若需高级特性（按名注册、属性注入、AOP）可再评估。

- 备选：Autofac —— 功能更强但属于额外依赖，骨架阶段不必要。

### D2: 日志走 Microsoft.Extensions.Logging 抽象，NLog 作为提供程序

业务代码只注入 `ILogger<T>`，通过 `NLog.Extensions.Logging` 的 `AddNLog()` 将 NLog 附加为提供程序；日志目标/规则全部在 `nlog.config` 声明，`<CopyToOutputDirectory>` 保证部署时携带。

- 备选：业务代码直接使用 `NLog.LogManager.GetCurrentClassLogger()` —— 与抽象层耦合，后续换提供程序（如 Serilog）需改全部业务代码。

### D3: 消息服务用门面接口 IMessageNotifier 封装 EasyNotice

EasyNotice 本身按渠道暴露 `IDingtalkProvider`、`IWeixinProvider`、`IFeishuProvider`、`IEmailProvider` 等接口，业务直接依赖这些接口会使渠道耦合进业务代码。在 `ActionPlatform.Core` 定义：

```csharp
public interface IMessageNotifier
{
    Task SendAsync(string title, string content, CancellationToken ct = default);
    Task SendAsync(string title, Exception exception, CancellationToken ct = default);
}
```

实现 `EasyNoticeMessageNotifier` 注入各渠道 Provider 并转发；未启用任何渠道时记警告并安全返回。

- 备选：业务直接注入 EasyNotice 各渠道 Provider —— 接口分散、更换实现需改业务；门面接口集中且可测试（便于 mock）。

### D4: 配置读取使用 Microsoft.Extensions.Configuration

控制台无内置配置，引入 `Microsoft.Extensions.Configuration.Json` + `Microsoft.Extensions.Configuration.Binder`，从 `appsettings.json` 读取 `EasyNotice` 渠道配置段（`Enable`/`WebHook`/`Secret`），有渠道启用才调用对应的 `UseDingTalk`/`UseWeixin`/`UseFeishu`/`UseEmail`。

### D5: 注册扩展方法统一放在 ActionPlatform.Core

`AddInfrastructure(IServiceCollection)`（含日志）、`AddMessagingService(IServiceCollection, IConfiguration)`（EasyNotice + IMessageNotifier）等扩展方法定义在 Core 项目中，`Program.cs` 只做装配与启动。依赖方向：`ActionPlatform → ActionPlatform.Core`。

### D6: 渠道采用配置化启用，已启用钉钉 + 飞书 + 企业微信

渠道按 `Enable` 标志配置化启用；当前已实现钉钉、飞书、企业微信三渠道（飞书为实际验证渠道），邮件按同一模式可随时追加（各渠道包按需引用）。真实 WebHook/Secret 通过 `appsettings.Local.json` 本地覆盖提供（已 gitignore，不入库），`appsettings.json` 仅保留占位配置。

## Risks / Trade-offs

- **EasyNotice 渠道包版本与 net8.0 兼容性** → 实施时以 NuGet 最新稳定版为准，并优先验证 `AddEasyNotice` 编译与运行；遇不兼容降级到已验证版本并记录。
- **EasyNotice.Feishu 2.1.4 的 SendAsync 参数丢失 bug**（实测）：`SendAsync(title, message)` 内部构造 `new TextMessage(title, ...)`，message 参数被忽略，飞书仅收到标题。→ 门面层 workaround：将 `title + content` 拼接后作为第一个参数传入（见 `EasyNoticeMessageNotifier` 注释），异常重载（`SendAsync(title, exception)`）无此问题。升级渠道包后需复查此 workaround。
- **WebHook 密钥入库风险** → `appsettings.json` 列入 `.gitignore` 提交模板（或提供 `appsettings.Development.json`），实施任务中明确提醒。
- **未配置渠道时静默失败** → 门面实现统一记录警告日志，避免业务方误以为已发送；`messaging` spec 已固化为场景。
- **NLog 配置缺失导致启动异常** → 实施时先验证 `nlog.config` 复制到输出目录；加载失败时记录明确错误。
- **Console 项目无 Host 的日志/配置生命周期** → 手动管理：启动时初始化，退出时 `LogManager.Shutdown()` 与 `ServiceProvider.Dispose()`。

## Migration Plan

全新项目，无既有代码迁移。回滚策略：移除包引用、删除新增文件、还原 `Program.cs` 即可。

## Open Questions

- 是否需要邮件/飞书渠道首发启用？（默认仅钉钉 + 企业微信，其他按需追加）
- EasyNotice 具体版本号以实施时 NuGet 最新稳定版为准。
