# logging Specification

## Purpose
TBD - created by archiving change setup-ioc-framework. Update Purpose after archive.
## Requirements
### Requirement: 基于 Microsoft.Extensions.Logging 的日志抽象

日志能力 SHALL 接入 Microsoft.Extensions.Logging 抽象层，业务代码通过注入 ILogger<T> 记录日志，不直接依赖 NLog 类型。

#### Scenario: 注入 ILogger 记录日志

- **WHEN** 业务类构造函数注入 ILogger<T> 并调用 LogInformation/LogError 等方法
- **THEN** 日志事件被写入日志提供程序

### Requirement: NLog 作为日志提供程序

系统 SHALL 通过 NLog.Extensions.Logging 将 NLog 注册为 Microsoft.Extensions.Logging 的日志提供程序。

#### Scenario: 注册 NLog 提供程序

- **WHEN** 日志服务初始化完成
- **THEN** NLog 提供程序已附加到日志工厂，日志事件由 NLog 处理

#### Scenario: 按日志级别过滤

- **WHEN** 记录一条低于 nlog.config 中配置的最低级别的日志
- **THEN** 该日志不会被输出

### Requirement: 基于 nlog.config 的日志配置

系统 SHALL 从项目根目录的 nlog.config 文件加载日志规则与目标（含文件输出目标），该文件随构建复制到输出目录。

#### Scenario: 加载配置文件

- **WHEN** 应用启动并加载日志服务
- **THEN** NLog 从 nlog.config 加载目标与规则

#### Scenario: 日志写入文件

- **WHEN** 通过 ILogger<T> 记录一条 Info 级别日志且 nlog.config 配置了文件目标
- **THEN** 日志以配置的布局（含时间、级别、分类、消息）写入指定日志文件

