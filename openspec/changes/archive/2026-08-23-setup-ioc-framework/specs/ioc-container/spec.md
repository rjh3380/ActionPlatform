## ADDED Requirements

### Requirement: 应用启动时构建依赖注入容器

系统 SHALL 在应用启动时使用官方 Microsoft.Extensions.DependencyInjection 构建 ServiceCollection 与 ServiceProvider，作为整个应用的服务容器。

#### Scenario: 构建容器

- **WHEN** 应用启动并执行容器初始化
- **THEN** 创建 ServiceCollection 并构建 ServiceProvider，且可从容器解析已注册的服务

### Requirement: 服务注册通过扩展方法

系统 SHALL 通过静态扩展方法按模块注册服务（如 AddInfrastructure、AddLoggingService、AddMessagingService），保持启动入口整洁。

#### Scenario: 注册基础服务

- **WHEN** 调用日志与消息服务的注册扩展方法
- **THEN** 对应服务注册到容器，可从容器解析

### Requirement: 构造函数注入解析服务

业务代码 SHALL 通过构造函数注入解析容器中的服务，而非直接从容器手动获取。

#### Scenario: 构造函数注入

- **WHEN** 一个服务类型声明构造函数参数依赖 ILogger<T> 或 IMessageNotifier
- **THEN** 容器在创建该服务实例时自动注入已注册的依赖实例

### Requirement: 应用退出时释放容器

系统 SHALL 在应用退出时 Dispose ServiceProvider，确保注册为 IDisposable/IAsyncDisposable 的服务被释放。

#### Scenario: 优雅退出释放容器

- **WHEN** 应用主流程结束并执行容器释放
- **THEN** ServiceProvider.Dispose() 被调用，已注册的单例与作用域服务按容器规则释放
