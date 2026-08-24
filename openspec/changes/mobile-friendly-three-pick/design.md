## Context

三一票推送当前用等宽文本表格（9 列，总宽约 96 显示列，CJK 字符计 2 列），在飞书 PC 端可读，但手机端消息渲染宽度约 40 半角字符，表格自动换行、列错位，体验差。

消息体系现状：`IMessageNotifier`（统一门面，`SendAsync(title, content)` 文本 + `SendAsync(title, Exception)`）由 `EasyNoticeMessageNotifier` 实现，转发到 EasyNotice 的钉钉/企业微信/飞书 Provider。**EasyNotice.Feishu 2.1.4 只支持 text 消息**（反编译确认仅 `msg_type=text`，无 post 类型），飞书富文本必须直发 webhook API。配置段 `EasyNotice:Feishu`（Enable/WebHook/Secret）已在 appsettings.Local.json（本地）与 CI 生成逻辑（workflow）中就位，无需新配置。

## Goals / Non-Goals

**Goals:**

- 三一票推送在飞书手机端逐行渲染、不换行错位
- 富文本能力以结构化模型暴露（行 + 可加粗片段），不绑定飞书细节
- 复用现有 `EasyNotice:Feishu` 配置，不新增配置段；钉钉/企业微信渠道行为不劣化
- 飞书不可用时行为与现状一致（警告日志，不崩溃）

**Non-Goals:**

- 不做 interactive 卡片（按钮/交互），post 富文本即可满足
- 不为钉钉/企业微信实现原生富文本卡片（它们各自有不同格式，本期降级文本）
- 不改 EasyNotice 库本身、不升级 EasyNotice 包
- 不做消息持久化/重试队列

## Decisions

### D1: 富文本行模型（`RichTextLine` / `RichTextSegment`）

```csharp
public sealed record RichTextSegment(string Text, bool Bold = false);
public sealed record RichTextLine(IReadOnlyList<RichTextSegment> Segments);
// 便捷工厂：RichTextLine.Plain("...")、RichTextLine.Bold("...")
```

行 = 片段列表（可加粗）。飞书 post 的 `content` 数组与「行」一一对应；降级为文本时按行拼接。模型不含飞书字段名，业务层与渠道实现解耦。

### D2: `IMessageNotifier` 新增 `SendRichTextAsync`

```csharp
Task SendRichTextAsync(string title, IReadOnlyList<RichTextLine> lines, CancellationToken cancellationToken = default);
```

- 飞书 → post 富文本直发（D3）
- 钉钉/企业微信 → 降级：lines 逐行拼接为普通文本（片段按 `Text` 串联），走现有 `SendAsync` 文本路径（保持既有 markdown 渲染）
- 未启用任何渠道 → 沿用现有「警告日志、安全返回」行为
- 现有两个 `SendAsync` 方法签名与行为不变

**BREAKING**：接口新增方法，其他实现类需补实现；当前仅 `EasyNoticeMessageNotifier` 一个实现，无外部使用者。

### D3: 飞书 post 直发（新增 `FeishuPostSender`）

EasyNotice.Feishu 只支持 text，post 需直发：

- 从 `IConfiguration` 读 `EasyNotice:Feishu`（Enable/WebHook/Secret），与 EasyNotice 初始化同源
- Payload（飞书群机器人 post 消息规范）：
  ```json
  {
    "msg_type": "post",
    "content": {
      "post": {
        "title": "<标题>",
        "content": [ [ {"tag":"text","text":"..."} ], [ {"tag":"text","text":"...","style":{"bold":true}} ] ]
      }
    }
  }
  ```
- 签名（飞书群机器人加签规范）：`timestamp`（秒）+ `\n` + `Secret` 的 HMAC-SHA256（key=Secret）→ Base64 → header `x-lark-signature`；body 带 `timestamp` 字段
- 响应校验：`code == 0` 为成功，否则警告日志（含 errmsg，不含密钥）
- HttpClient：直接 `new HttpClient()`（控制台应用未注册 IHttpClientFactory；无复用收益，不引入依赖）
- 注册方式：`AddSingleton<FeishuPostSender>`（构造注入 IConfiguration + ILogger）；未启用飞书时 `SendPostAsync` 记录警告并安全返回（与现有门面行为一致）
- 备选：反射/子类化 EasyNotice.Feishu —— 内部类不可控、签名实现未公开，不如自建 60 行直发代码可控。

### D4: 三一票卡片布局

每只票一个块，字段按行排布（手机端逐行渲染，无对齐需求，无需 PadCjk）：

```
🥇 1. 300142 沃森生物    得分 0.994        ← 代码/名称/得分加粗
   现价 15.60 ｜ 竞价金额 3.80亿
   实际换手 2.15% ｜ 竞价涨幅 +6.30%
   流通市值 254.11亿
```

- 前三名行首 `🥇 🥈 🥉`
- 每行两对字段、`｜` 分隔，总字段行数从表格的 9 列压到每票 4 行
- 无行业数据（上游「敬请期待」）→ 不渲染行业行
- `BuildTable` 保留（`public` 不破坏），三一票发送改为 `SendRichTextAsync`；`BuildTable` 仍可用于日志/降级场景

### D5: 实现顺序

接口+模型 → FeishuPostSender → 门面实现 → 三一票卡片 → 验证（本地发真实飞书消息确认手机渲染 + 签名正确 + 未启用渠道降级路径）

## Risks / Trade-offs

- **飞书 post API 结构/签名细节错误** → 本地真实发送验证（复用 `.ap_tmp` 模式 + 真实 appsettings.Local.json），失败时日志可见 errmsg
- **接口 BREAKING（新增方法）** → 仅一个实现类 + 无外部消费者，影响面为零；若未来多实现需同步
- **钉钉/企业微信降级文本** → 与其现行为（markdown 文本）一致，无劣化
- **密钥泄露** → 日志不打印 WebHook/Secret（沿用现有约定）；直发类同样遵守

## Migration Plan

- 代码改动 + 本地验证后推送；CI workflow 无需改动（配置段相同）
- 回滚：三一票改回 `SendAsync(title, BuildTable(picks))` 一行，其余新代码无害保留

## Open Questions

- 手机端 `｜` 分隔与字段对组合的排版是否满意（可微调行内字段组合，不涉架构）
- 是否需要把 Top10 推送（当前禁用的 AuctionTopPushAction）也切到富文本（本期不做，机制就绪后一行切换）
