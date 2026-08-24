## ADDED Requirements

### Requirement: 富文本消息通知

系统 SHALL 在 `IMessageNotifier` 门面提供富文本消息方法（`SendRichTextAsync(title, lines)`）：飞书渠道以 post 富文本发送（见 feishu-rich-text 能力）；钉钉/企业微信渠道降级为文本（lines 逐行拼接后走既有文本发送路径）；未启用任何渠道时记录警告日志并安全返回。现有文本消息方法（`SendAsync`）行为 SHALL 保持不变。

#### Scenario: 发送富文本到飞书

- **WHEN** 业务代码调用 `SendRichTextAsync` 且飞书渠道已启用
- **THEN** 飞书收到 post 富文本消息（标题 + 行渲染），钉钉/企业微信（如启用）收到降级文本

#### Scenario: 降级为文本

- **WHEN** 业务代码调用 `SendRichTextAsync` 且仅启用钉钉或企业微信渠道
- **THEN** 消息以普通文本发送到已启用渠道，发送行为与文本消息一致

#### Scenario: 未启用渠道安全失败

- **WHEN** 未启用任何渠道仍调用 `SendRichTextAsync`
- **THEN** 记录警告日志且不抛出异常
