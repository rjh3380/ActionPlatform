## 1. 项目与包引用

- [x] 1.1 为 `ActionPlatform.Core` 添加 NuGet 包引用：`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions`、`Microsoft.Extensions.Configuration.Abstractions`、`NLog.Extensions.Logging`、`EasyNotice.Core`、`EasyNotice.Dingtalk`、`EasyNotice.Feishu`、`EasyNotice.Weixin`（版本取 NuGet 最新稳定版）
- [x] 1.2 为 `ActionPlatform` 添加 NuGet 包引用：`Microsoft.Extensions.DependencyInjection`、`Microsoft.Extensions.Configuration.Json`、`Microsoft.Extensions.Configuration.Binder`、`Microsoft.Extensions.Logging`
- [x] 1.3 执行 `dotnet build` 确认两项目均编译通过

## 2. 配置与资源文件

- [x] 2.1 在 `ActionPlatform` 新建 `nlog.config`：配置文件目标（`logs/` 目录、layout 含时间/级别/分类/消息）、根规则（默认 Info 级），并设置 `CopyToOutputDirectory=PreserveNewest`
- [x] 2.2 在 `ActionPlatform` 新建 `appsettings.json`：定义 `EasyNotice` 配置段（钉钉/飞书/企业微信各自的 `Enable`、`WebHook`、`Secret`），并设置 `CopyToOutputDirectory=PreserveNewest`
- [x] 2.3 更新 `.gitignore`：将可能包含 WebHook 密钥的本地配置文件（如 `appsettings.Local.json` / 实际含密钥文件）排除提交，避免密钥入库
- [x] 2.4 支持 `appsettings.Local.json`：Program.cs 以 optional 方式叠加加载覆盖基础配置，本地文件设置 `CopyToOutputDirectory=PreserveNewest`，真实渠道密钥只写入本地文件

## 3. 日志服务

- [x] 3.1 在 `ActionPlatform.Core` 实现 `AddLoggingService(this IServiceCollection)` 扩展方法：`AddLogging()` + `AddNLog()`（NLog.Extensions.Logging），返回 `IServiceCollection` 支持链式调用
- [x] 3.2 验证日志服务可从容器解析 `ILogger<T>`，且日志按 `nlog.config` 级别规则过滤

## 4. 消息服务

- [x] 4.1 在 `ActionPlatform.Core` 定义 `IMessageNotifier` 接口：`SendAsync(string title, string content, CancellationToken)` 与 `SendAsync(string title, Exception exception, CancellationToken)` 两个重载
- [x] 4.2 实现 `EasyNoticeMessageNotifier`：注入各渠道 Provider（钉钉/飞书/企业微信），转发到已启用渠道；未启用任何渠道时记录警告日志并安全返回（不抛异常）
- [x] 4.3 在 `ActionPlatform.Core` 实现 `AddMessagingService(this IServiceCollection, IConfiguration)` 扩展方法：调用 `AddEasyNotice`，按 `appsettings.json`/`appsettings.Local.json` 的 `Enable` 标志调用 `UseDingTalk`/`UseFeishu`/`UseWeixin`，注册 `IMessageNotifier` 为单例

## 5. 入口装配与演示

- [x] 5.1 改造 `Program.cs`：加载配置（ConfigurationBuilder + Json + Local 覆盖）、构建 ServiceCollection/ServiceProvider、调用 `AddLoggingService` 与 `AddMessagingService`
- [x] 5.2 在演示代码中通过构造函数注入 `ILogger<Program>` 记录日志，并调用 `IMessageNotifier.SendAsync` 发送演示消息
- [x] 5.3 主流程结束执行 `LogManager.Shutdown()` 与 `ServiceProvider.Dispose()`

## 6. 验证

- [x] 6.1 `dotnet build` 全部通过
- [x] 6.2 运行程序：`logs/` 下生成日志文件且内容格式符合 `nlog.config` 布局
- [x] 6.3 未配置渠道（`Enable=false`）时运行：发送走警告路径，程序不崩溃
- [x] 6.4 飞书渠道端到端验证：Local 配置覆盖生效、`IFeishuProvider` 被解析并真实调用飞书 API（占位 WebHook 返回 19001 且被记录为警告，不崩溃）
- [x] 6.5 在 `appsettings.Local.json` 填入真实飞书机器人 WebHook 后运行，确认飞书群内收到「ActionPlatform 启动通知」
- [x] 6.6 修复 EasyNotice.Feishu 2.1.4 的 `SendAsync(title, message)` bug（反编译确认 message 参数被忽略，仅发送 title）：门面层将标题与内容拼接传入，实测飞书 API 返回成功、消息包含完整内容
