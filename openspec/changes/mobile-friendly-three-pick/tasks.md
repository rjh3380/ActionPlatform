## 1. 富文本模型与接口

- [x] 1.1 新建 `RichTextSegment`（Text/Bold）与 `RichTextLine`（片段列表）记录，提供 `Plain`/`Bold` 便捷工厂
- [x] 1.2 `IMessageNotifier` 新增 `SendRichTextAsync(string title, IReadOnlyList<RichTextLine> lines, CancellationToken)`（现有 `SendAsync` 保持不变）

## 2. 飞书 post 直发

- [x] 2.1 新建 `FeishuPostSender`：从 `IConfiguration` 读 `EasyNotice:Feishu`（Enable/WebHook/Secret），`SendPostAsync(title, lines)` 发送 `msg_type=post` 请求（行→content 数组、片段加粗→style.bold），未启用时警告并安全返回
- [x] 2.2 实现飞书加签：`timestamp\nSecret` → HMAC-SHA256 → Base64 → `x-lark-signature` 头 + body timestamp；响应 code 非 0 记录警告（含 errmsg，不含密钥）
- [x] 2.3 `AddSingleton<FeishuPostSender>` 注册（构造注入 IConfiguration + ILogger）

## 3. 门面实现与三一票改造

- [x] 3.1 `EasyNoticeMessageNotifier` 实现 `SendRichTextAsync`：飞书 → `FeishuPostSender`；钉钉/企业微信 → lines 拼接为文本走既有 `SendAsync`；无渠道 → 警告安全返回
- [x] 3.2 `AuctionThreePickAction` 新增卡片构建（🥇🥈🥉 排名、每票 4 行字段、名称/代码/得分加粗、无行业行），推送改调 `SendRichTextAsync`；`BuildTable` 保留

## 4. 验证

- [x] 4.1 本地真实飞书验证：发送三一票富文本卡片，确认手机端渲染不换行、签名通过（复用 `.ap_tmp` 临时项目 + 真实 appsettings.Local.json）
- [x] 4.2 验证降级路径：仅钉钉/企微渠道时发送文本成功；未启用渠道时警告且不抛异常
- [x] 4.3 主解决方案构建通过，验证完成后清理 `.ap_tmp`
