## Context

ActionPlatform 是 .NET 8 控制台应用（`ActionPlatform` 入口 + `ActionPlatform.Core` 类库），已完成 IOC/日志/消息基础设施：程序从工作目录读取 `appsettings.json`（占位配置，已入库）与 `appsettings.Local.json`（真实 WebHook，被 gitignore 排除、不入库）。当前只能本地手动运行，需要配置 GitHub Actions 每天定时执行。

## Goals / Non-Goals

**Goals:**

- GitHub Actions 工作流每天定时运行 ActionPlatform（北京时间 07:15）
- 支持手动触发（workflow_dispatch）便于测试
- 飞书 WebHook 密钥通过 GitHub Secrets 注入，不在仓库与日志中泄露
- 运行日志可下载排查（artifact）
- 程序代码零改动

**Non-Goals:**

- 不引入第三方通知/调度服务（GitHub Actions 自身能力足够）
- 不做多平台矩阵（仅 ubuntu-latest）
- 不做镜像构建（Docker 等）——托管 runner 直接运行即可
- 不加密 Secrets（GitHub Secrets 已加密存储）

## Decisions

### D1: cron 时间取 UTC 23:15（北京时间 07:15），避开整点

GitHub Actions cron 固定使用 UTC。北京时间 07:15 = UTC 23:15（UTC+8），避开 :00/:30 整点分钟（官方建议错峰，避免 runner 排队）。schedule 不保证精确秒级，分钟级足够。

- 备选：UTC 00:00 整点 —— 易与全球大量 job 撞车排队。

### D2: 密钥注入用「runner 生成 appsettings.Local.json」，程序零改动

工作流内用 bash 步骤根据 `${{ secrets.FEISHU_WEBHOOK }}` 生成 `appsettings.Local.json`（写到工作目录 `ActionPlatform/appsettings.Local.json`，程序按既有机制读取）。GitHub Secrets 在 runner 上自动注入为环境变量，且 Actions 日志中会被打码。

- 备选 A：给 Program.cs 加 `AddEnvironmentVariables()` —— 需改程序代码，且环境变量键映射与配置段结构有偏差；文件方案零改动。
- 备选 B：直接提交真实 hook 到 appsettings.json —— 密钥入库，不可接受。

### D3: 运行方式 `dotnet run --project ActionPlatform/ActionPlatform.csproj`

runner 每次全新 checkout，需 restore NuGet 依赖；`dotnet run` 一站式处理 restore/build/run，退出码即 job 结果。前置 setup-dotnet@v4 指定 8.0.x 版本。

- 备选：预编译提交二进制 —— 反模式，不可取。

### D4: 日志上传 actions/upload-artifact@v4

`logs/` 目录（`${shortdate}` 命名文件）上传为 artifact，retention-days: 7，`if: always()` 保证失败时也能下载日志。

### D5: Secrets 命名为 FEISHU_WEBHOOK / FEISHU_SECRET

与现有 `appsettings.Local.json` 的飞书配置一一对应；未设置时生成 `Enable: false` 配置（spec 已固化该场景），程序走既有警告路径。

## Risks / Trade-offs

- **cron 延迟/跳过**：Actions schedule 不保证精确执行时间（排队、停机维护）→ 用 `if: always()` 与 workflow_dispatch 兜底，重要任务可加第二个 schedule 或外部调度。
- **Secrets 泄露**：WebHook 出现在日志/console 输出 → 程序不打印配置值；workflow 中不回显 Secret；`appsettings.Local.json` 已 gitignore（双保险：runner 工作目录在 job 结束后自动销毁）。
- **私有仓库额度**：免费 2000 分钟/月，每天一次运行远低于额度；公开仓库无限。
- **runner 访问飞书 API**：GitHub 托管 runner 外网可达（默认出站允许），可行。
- **本地 vs CI 行为差异**：CI 工作目录结构与本地不同 → 生成 Local 文件路径与 `dotnet run` 工作目录保持一致（`dotnet run` 的 CWD 为项目目录）。

## Migration Plan

- 推送 workflow 文件后手动触发一次验证（workflow_dispatch）。
- 回滚：删除 workflow 文件即可，无其他变更。

## Open Questions

- 定时时间是否满足业务（默认北京 07:15，改 cron 一行即可）。
- 是否需要在失败时额外通知（如 GitHub 邮件通知已默认按仓库配置发送；程序内部业务通知由既有 IMessageNotifier 负责）。
