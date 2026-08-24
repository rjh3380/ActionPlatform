## ADDED Requirements

### Requirement: 飞书富文本（post）消息直发

系统 SHALL 提供飞书群机器人 post 类型富文本消息的直发能力（EasyNotice.Feishu 仅支持 text，无法满足富文本）。直发实现 SHALL 复用现有 `EasyNotice:Feishu` 配置段（Enable/WebHook/Secret），不新增配置。启用时 SHALL 通过 webhook 发送 `msg_type=post` 请求，携带飞书加签签名（`timestamp\nSecret` 的 HMAC-SHA256 → Base64，header `x-lark-signature`）；未启用时记录警告日志并安全返回，不抛出异常。请求响应 code 非 0 时 SHALL 记录警告日志（含 errmsg，不含密钥）。

#### Scenario: 发送富文本消息

- **WHEN** 系统启用飞书渠道并发送富文本消息（标题 + 行/片段模型）
- **THEN** 飞书收到 post 类型消息，内容为标题加逐行渲染的文本（支持片段加粗），手机端逐行显示不换行错位

#### Scenario: 加签请求

- **WHEN** 飞书渠道配置了 Secret
- **THEN** 请求携带按 `timestamp + "\n" + Secret` 计算的 `x-lark-signature` 头与 body 中的 timestamp，飞书校验通过

#### Scenario: 未启用飞书渠道

- **WHEN** `EasyNotice:Feishu.Enable` 为 false 或 WebHook 为空且调用富文本发送
- **THEN** 记录警告日志且不发送请求，不抛出异常

#### Scenario: 飞书返回错误

- **WHEN** 飞书 webhook 响应 code 非 0
- **THEN** 记录警告日志（含 errmsg），不抛出异常

### Requirement: 富文本行模型

系统 SHALL 以结构化行模型表达富文本消息内容：消息为行（`RichTextLine`）列表，每行为片段（`RichTextSegment`，文本 + 加粗标记）列表。模型 SHALL 不含渠道特定字段，各渠道按自身格式序列化或降级。

#### Scenario: 行内多片段

- **WHEN** 构建一行包含普通文本与加粗文本的多个片段
- **THEN** 模型保持片段顺序与加粗标记，序列化到飞书 post 时正确渲染
