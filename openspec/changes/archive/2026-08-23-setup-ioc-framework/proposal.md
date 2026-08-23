## Why

项目目前是空白骨架（`ActionPlatform` 控制台入口 + `ActionPlatform.Core` 类库），`Program.cs` 只有 "Hello, World!"，没有任何依赖注入、日志与消息能力。在开始业务功能开发前，需要先搭建基础设施层：基于官方 `Microsoft.Extensions.DependencyInjection` 的 IOC 容器，并提供日志（NLog）与消息通知（EasyNotice）两个基础服务，供后续所有业务模块复用。

## What Changes

- 引入 IOC 容器：使用官方 `Microsoft.Extensions.DependencyInjection`，在 `ActionPlatform` 入口统一注册与解析服务，并提供服务注册的扩展方法约定。
- 新增日志服务：基于 NLog（`NLog.Extensions.Logging`）接入 `Microsoft.Extensions.Logging` 抽象，支持配置文件（`nlog.config`）驱动，业务代码通过 `ILogger<T>` 注入使用。
- 新增消息服务：基于 EasyNotice 开源库封装统一的消息通知能力（如钉钉、企业微信、飞书、邮件等渠道），在 `ActionPlatform.Core` 中定义抽象接口，提供可配置、可注入的 `IMessageNotifier` 服务。
- 改造 `Program.cs`：初始化容器、装配日志与消息服务，并附带最小可运行示例。
- 新增配置文件：`nlog.config`（日志规则与目标）、应用配置（EasyNotice 渠道配置）。

## Capabilities

### New Capabilities

- `ioc-container`: IOC 容器的初始化、服务注册与解析能力（基于 Microsoft.Extensions.DependencyInjection）
- `logging`: 基于 NLog 的日志记录能力，通过 Microsoft.Extensions.Logging 抽象提供
- `messaging`: 基于 EasyNotice 的消息通知能力，向钉钉/企业微信/飞书/邮件等渠道发送消息

### Modified Capabilities

<!-- 无既有 spec，首次引入 -->

## Impact

- **代码**: `ActionPlatform/Program.cs`（容器装配入口）、`ActionPlatform.Core`（新增服务抽象与实现）
- **项目引用**: `ActionPlatform`、`ActionPlatform.Core` 增加 NuGet 包引用
- **依赖**: `Microsoft.Extensions.DependencyInjection`、`NLog.Extensions.Logging`（NLog）、`EasyNotice` 相关包
- **配置文件**: 新增 `nlog.config`；EasyNotice 渠道配置（appsettings 或配置类）
