# messaging Specification

## Purpose
TBD - created by archiving change setup-ioc-framework. Update Purpose after archive.
## Requirements
### Requirement: 注册 EasyNotice 消息服务

系统 SHALL 使用 EasyNotice 提供的 AddEasyNotice 扩展方法将消息通知服务注册到 IOC 容器，并按配置启用渠道（钉钉、企业微信、飞书、邮件）。

#### Scenario: 注册并启用渠道

- **WHEN** 调用 AddEasyNotice 并配置启用至少一个渠道
- **THEN** 对应渠道的 EasyNotice 服务注册到容器，可从容器解析

### Requirement: 统一消息通知抽象

系统 SHALL 在 ActionPlatform.Core 中提供 IMessageNotifier 门面接口封装 EasyNotice 各渠道 Provider，业务代码只依赖该接口。

#### Scenario: 通过统一接口发送文本消息

- **WHEN** 业务代码调用 IMessageNotifier.SendAsync 发送标题与内容
- **THEN** 消息被转发到所有已启用的 EasyNotice 渠道

#### Scenario: 未启用渠道时安全失败

- **WHEN** 未启用任何渠道仍调用 IMessageNotifier.SendAsync
- **THEN** 记录警告日志且不抛出异常

### Requirement: 渠道配置可配置化

各渠道的 WebHook、Secret 等参数 SHALL 从应用配置（appsettings.json）读取，不硬编码在代码中。

#### Scenario: 从配置读取渠道参数

- **WHEN** appsettings.json 中配置了某渠道的 WebHook 与 Secret
- **THEN** 该渠道使用配置值完成初始化并可正常发送

### Requirement: 异常通知

系统 SHALL 支持将异常作为消息内容发送通知。

#### Scenario: 发送异常通知

- **WHEN** 调用 IMessageNotifier.SendAsync 并传入标题与异常对象
- **THEN** 异常信息（消息、堆栈等）作为通知内容发送到已启用渠道

