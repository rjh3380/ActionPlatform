## Why

ActionPlatform 需要每天定时运行（如每日监控/定时任务），本地手动运行无法保证按时执行与持续可用。GitHub Actions 提供免费托管 runner 与 cron 调度能力，与仓库天然集成，无需额外服务器。

## What Changes

- 新增 `.github/workflows/scheduled-run.yml`：基于 cron 每天运行一次（北京时间 09:00 = UTC 01:00，持续 1 小时），并支持 `workflow_dispatch` 手动触发。
- 工作流执行：checkout 代码 → 安装 .NET 8 SDK → 构建并运行 ActionPlatform。
- 密钥注入：飞书 WebHook/Secret 通过 GitHub Secrets（`FEISHU_WEBHOOK` / `FEISHU_SECRET`）配置，在 runner 上动态生成 `appsettings.Local.json`（该文件已被 gitignore 排除，不会入库）；未配置 Secrets 时生成禁用渠道的配置，程序走"无渠道"警告路径正常退出。
- 日志产物：运行产生的 `logs/` 目录上传为 GitHub Actions artifact（保留 7 天），便于排查。
- 程序代码零改动（沿用现有 `appsettings.json` + `appsettings.Local.json` 加载机制）。

## Capabilities

### New Capabilities

- `scheduled-run`: GitHub Actions 定时运行能力——cron 调度、构建运行、Secrets 注入与日志产物

### Modified Capabilities

<!-- 无既有 spec 变更 -->

## Impact

- **新增文件**: `.github/workflows/scheduled-run.yml`
- **仓库配置**: GitHub Secrets（`FEISHU_WEBHOOK`、`FEISHU_SECRET`）需在仓库 Settings 中配置
- **不改动**: 程序代码、现有配置结构（`appsettings.json` 占位 + `appsettings.Local.json` 密钥）
- **依赖**: GitHub Actions 免费额度（每天一次运行消耗极小）；runner 需可访问飞书 API（托管 runner 可）
