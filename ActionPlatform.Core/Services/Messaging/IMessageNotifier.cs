namespace ActionPlatform.Core.Services.Messaging;

/// <summary>
/// 统一消息通知门面，屏蔽底层 EasyNotice 各渠道 Provider，业务代码只依赖此接口。
/// </summary>
public interface IMessageNotifier
{
    /// <summary>发送标题与内容到所有已启用渠道。</summary>
    Task SendAsync(string title, string content, CancellationToken cancellationToken = default);

    /// <summary>将异常作为消息内容发送到所有已启用渠道。</summary>
    Task SendAsync(string title, Exception exception, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送富文本消息（标题 + 行模型）到所有已启用渠道，返回是否全部投递成功。
    /// 飞书以 post 富文本发送（手机端逐行渲染、不换行）；不支持富文本的渠道（钉钉/企业微信）降级为文本。
    /// false = 至少一个已启用渠道投递失败（发送层不抛异常）—— 调用方（如每日推送类 Action）应据此不声明完成并按退避重试。
    /// 未启用任何渠道视为无需投递，返回 true。
    /// </summary>
    Task<bool> SendRichTextAsync(string title, IReadOnlyList<RichTextLine> lines, CancellationToken cancellationToken = default);
}
