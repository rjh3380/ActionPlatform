## 1. GitHub Actions 工作流

- [x] 1.1 创建 `.github/workflows/scheduled-run.yml`：`schedule` cron `0 1 * * *`（UTC，北京时间 09:00）+ `workflow_dispatch` 手动触发
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
- [x] 2.6 修复日志上传路径：NLog 相对路径基于 `AppDomain.BaseDirectory`（`bin/Debug/net8.0/logs/`）而非工作目录，upload-artifact 的 path 改为 glob `ActionPlatform/**/logs/`（含注释说明）
- [x] 2.7 修复 CI 崩溃（首次 workflow_dispatch 实测发现）：`App.RunAsync` 的 `Console.ReadKey()` 在无控制台/输入重定向时抛 `InvalidOperationException` → 改为 `Console.IsInputRedirected` 分支：交互终端按任意键退出，非交互环境按 `APP_RUN_SECONDS`（0/缺省=持续运行）运行后退出
- [x] 2.8 workflow 注入 `FinancialApi:ApiKey`（新 secret `FINANCIAL_API_KEY`，未配置时数据类 Action 报 2001 被隔离、程序不崩溃）；Run 步骤设置 `APP_RUN_SECONDS: 3600`（09:00 启动、持续 1 小时覆盖 09:25 三一票触发点）
- [x] 2.9 首次真实运行发现问题（2026-08-24 上午，用户报告「9 点未自动启动」+「手动启动后三一票未执行」）：
  - **cron 延迟**：schedule run 实际 02:14 UTC（北京 10:14）才启动，比 cron 01:00 UTC 延迟 74 分钟（GitHub 文档明确 schedule 可能延迟数小时）→ cron 提前到 `0 0 * * *`（北京 08:00），`APP_RUN_SECONDS` 3600 → 7200（零延迟时 08:00→10:00、延迟 1 小时时 09:00→11:00，均覆盖 09:25 触发点；旧配置零延迟时 09:00 退出、错过 9:25）
  - **时区错位（根因）**：`ActionBase` 定时判断用 `now.LocalDateTime`，GitHub runner 本地时区为 UTC → 09:25（北京）在 CI 中变为 17:25 才触发，三一票在 CI 永不触发 → 定时判断改为固定北京时间（UTC+8，`ChinaTime` helper），不依赖主机时区；已验证（模拟 UTC 时区 harness 11/11 通过：北京 09:25 触发、当日不重复、跨天重置、间隔模式不受影响）
  - ActionLoop 启动日志新增各 Action 触发配置明细（模式/时刻/启用状态），便于 CI 日志排查
