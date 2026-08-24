## Why

三一票推送目前是等宽文本表格（9 列，总宽约 96 显示列）。手机上飞书消息渲染宽度约 40 半角字符，表格必然自动换行，错位难读。用户诉求：手机上好看、不换行。

## What Changes

- `IMessageNotifier` 新增富文本消息方法（`SendRichTextAsync`）：结构化行模型（每行若干可加粗片段），飞书端以 **post 富文本**消息发送（手机端原生逐行渲染、不换行），钉钉/企业微信降级为普通文本（保持多渠道一致）。
- 新增飞书富文本直发实现：EasyNotice.Feishu 只支持 text 消息（无 post 类型），需自建飞书 webhook 直发（HttpClient + `msg_type=post` + 飞书签名算法），**复用现有 `EasyNotice:Feishu` 配置**（WebHook/Secret/Enable，无新配置）。
- 三一票推送改为富文本卡片：每只票一个卡片块、字段按行排布（不再依赖等宽对齐），手机端友好。原 `BuildTable` 保留作为降级文本来源。

## Capabilities

### New Capabilities
- `feishu-rich-text`: 飞书富文本（post 类型）消息发送能力——直发实现、签名、行模型序列化，复用 EasyNotice 配置段

### Modified Capabilities
- `messaging`: `IMessageNotifier` 增加富文本消息方法（新增 Requirement，现有文本方法行为不变）

## Impact

- `ActionPlatform.Core/Services/Messaging/IMessageNotifier.cs` — 新增富文本方法（**BREAKING**：接口增加方法，实现类需同步；当前仅 `EasyNoticeMessageNotifier` 一个实现）
- `ActionPlatform.Core/Services/Messaging/EasyNoticeMessageNotifier.cs` — 实现富文本方法：飞书直发 post，钉钉/企业微信降级文本
- 新增 `ActionPlatform.Core/Services/Messaging/` 下富文本模型与飞书直发类（复用 `EasyNotice:Feishu` 配置，复用现有 HttpClient 注入或自建）
- `ActionPlatform/Actions/AuctionThreePickAction.cs` — 推送内容由表格改为富文本卡片（新增卡片构建逻辑，`BuildTable` 保留）
- 依赖：无新增 NuGet（System.Net.Http.Json 等 BCL 自带）；需验证飞书 post API 结构与签名算法
