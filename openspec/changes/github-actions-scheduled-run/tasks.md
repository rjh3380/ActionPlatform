## 1. GitHub Actions 工作流

- [x] 1.1 创建 `.github/workflows/scheduled-run.yml`：`schedule` cron `23 15 * * *`（UTC，北京时间 07:15）+ `workflow_dispatch` 手动触发
- [x] 1.2 配置 job：`ubuntu-latest`、`actions/checkout@v4`、`actions/setup-dotnet@v4`（dotnet-version 8.0.x）
- [x] 1.3 添加 Secrets 注入步骤：bash 脚本根据 `${{ secrets.FEISHU_WEBHOOK }}` / `${{ secrets.FEISHU_SECRET }}` 生成 `ActionPlatform/appsettings.Local.json`；WebHook 为空时生成 `Enable: false` 配置（不泄露密钥内容）
- [x] 1.4 添加运行步骤：`dotnet run`（working-directory: ActionPlatform，CWD 与本地一致，日志写入 `logs/`）
- [x] 1.5 添加日志上传步骤：`actions/upload-artifact@v4` 上传 `logs/` 目录，`retention-days: 7`，`if: always()`
- [x] 1.6 验证 `.gitignore` 已排除 `appsettings.Local.json` 与 `logs/`（第 9、12 行，已覆盖）

## 2. 仓库配置与验证

- [x] 2.1 在 workflow 文件头部注释记录 GitHub Secrets 配置说明（`FEISHU_WEBHOOK` / `FEISHU_SECRET`）
- [ ] 2.2 推送 workflow 文件，通过 `workflow_dispatch` 手动触发一次运行
- [ ] 2.3 确认 job 成功：飞书收到运行通知（配置了真实 hook 时），Actions 页面显示成功
- [ ] 2.4 下载日志 artifact，确认日志内容符合 `nlog.config` 布局且不包含 WebHook 密钥
- [ ] 2.5 验证未配置 Secrets 的路径（临时删除 Secrets 或新分支空配置）:程序警告后正常退出、job 不失败
