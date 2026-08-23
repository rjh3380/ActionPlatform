## ADDED Requirements

### Requirement: 定时调度

系统 SHALL 提供 GitHub Actions 工作流，按 cron 每天定时运行一次（UTC 23:15，即北京时间 07:15），并支持 workflow_dispatch 手动触发。

#### Scenario: 定时触发运行

- **WHEN** 到达 cron 配置的调度时间
- **THEN** 工作流在 GitHub 托管 runner 上自动运行

#### Scenario: 手动触发运行

- **WHEN** 用户在 Actions 页面点击 "Run workflow" 或通过 API 触发 workflow_dispatch
- **THEN** 工作流立即运行，与定时运行行为一致

### Requirement: 构建并运行程序

工作流 SHALL 检出代码、安装 .NET 8 SDK、构建并运行 ActionPlatform 程序。

#### Scenario: 工作流执行程序

- **WHEN** 工作流 job 开始执行
- **THEN** 依次完成 checkout、setup-dotnet（.NET 8）、构建 ActionPlatform，并以程序退出码决定 job 成败

### Requirement: Secrets 密钥注入

工作流 SHALL 从 GitHub Secrets（FEISHU_WEBHOOK、FEISHU_SECRET）读取渠道配置并生成 appsettings.Local.json；Secrets 为空时生成禁用渠道的配置，程序正常运行。

#### Scenario: 已配置 Secrets

- **WHEN** 仓库 Secrets 包含 FEISHU_WEBHOOK 且非空
- **THEN** runner 生成 appsettings.Local.json，飞书渠道 Enable=true 并写入 WebHook/Secret，程序发送消息到飞书

#### Scenario: 未配置 Secrets

- **WHEN** FEISHU_WEBHOOK 为空或未设置
- **THEN** 生成的 appsettings.Local.json 中渠道 Enable=false，程序记录警告后正常退出，job 不失败

#### Scenario: 密钥不泄露

- **WHEN** 工作流运行并生成包含密钥的配置文件
- **THEN** 配置文件仅存在于 runner 工作目录（已被 .gitignore 排除），不进入日志输出与制品

### Requirement: 日志产物

工作流 SHALL 将运行产生的 logs/ 目录上传为 Actions artifact。

#### Scenario: 上传日志制品

- **WHEN** 工作流 job 结束
- **THEN** logs/ 目录作为 artifact 上传，保留 7 天，可从 Actions 页面下载排查
