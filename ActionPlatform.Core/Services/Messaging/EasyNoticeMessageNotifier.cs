using EasyNotice;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Core.Services.Messaging;

/// <summary>
/// IMessageNotifier 的 EasyNotice 实现：将消息转发到所有已启用渠道；
/// 未启用任何渠道时记录警告并安全返回，不抛出异常。
/// </summary>
public sealed class EasyNoticeMessageNotifier : IMessageNotifier
{
    private readonly IReadOnlyList<IDingtalkProvider> _dingtalkProviders;
    private readonly IReadOnlyList<IWeixinProvider> _weixinProviders;
    private readonly IReadOnlyList<IFeishuProvider> _feishuProviders;
    private readonly ILogger<EasyNoticeMessageNotifier> _logger;

    public EasyNoticeMessageNotifier(
        IEnumerable<IDingtalkProvider> dingtalkProviders,
        IEnumerable<IWeixinProvider> weixinProviders,
        IEnumerable<IFeishuProvider> feishuProviders,
        ILogger<EasyNoticeMessageNotifier> logger)
    {
        _dingtalkProviders = dingtalkProviders.ToArray();
        _weixinProviders = weixinProviders.ToArray();
        _feishuProviders = feishuProviders.ToArray();
        _logger = logger;
    }

    public async Task SendAsync(string title, string content, CancellationToken cancellationToken = default)
    {
        if (_dingtalkProviders.Count == 0 && _weixinProviders.Count == 0 && _feishuProviders.Count == 0)
        {
            _logger.LogWarning("未启用任何消息渠道，消息未发送。Title: {Title}", title);
            return;
        }

        foreach (var provider in _dingtalkProviders)
        {
            var response = await provider.SendMarkdownAsync(title, content);
            LogIfFailed("钉钉", response.IsSuccess, response.ErrCode, response.ErrMsg);
        }

        foreach (var provider in _weixinProviders)
        {
            var response = await provider.SendMarkdownMessageAsync(title, content);
            LogIfFailed("企业微信", response.IsSuccess, response.ErrCode, response.ErrMsg);
        }

        foreach (var provider in _feishuProviders)
        {
            // Workaround: EasyNotice.Feishu 2.1.4 的 SendAsync(title, message) 存在 bug，
            // 消息内容只取 title 参数（new TextMessage(title, ...)），message 被忽略，
            // 因此将标题与内容拼接后作为第一个参数传入。
            var combined = string.Join(Environment.NewLine, title, content);
            var response = await provider.SendAsync(combined, content);
            LogIfFailed("飞书", response.IsSuccess, response.ErrCode, response.ErrMsg);
        }
    }

    public async Task SendAsync(string title, Exception exception, CancellationToken cancellationToken = default)
    {
        // 注意：EasyNotice 发送接口不支持取消，cancellationToken 保留用于接口契约一致。
        await SendAsync(title, exception.ToString(), cancellationToken);
    }

    private void LogIfFailed(string channel, bool isSuccess, int errCode, string errMsg)
    {
        if (!isSuccess)
        {
            _logger.LogWarning("{Channel} 消息发送失败。ErrCode: {ErrCode}, ErrMsg: {ErrMsg}", channel, errCode, errMsg);
        }
    }
}
