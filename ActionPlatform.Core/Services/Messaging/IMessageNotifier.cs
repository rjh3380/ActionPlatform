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
}
