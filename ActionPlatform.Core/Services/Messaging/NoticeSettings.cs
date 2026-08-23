namespace ActionPlatform.Core.Services.Messaging;

/// <summary>
/// EasyNotice 渠道配置，对应 appsettings.json 中的 "EasyNotice" 配置段。
/// </summary>
public sealed class NoticeSettings
{
    public ChannelSettings Dingtalk { get; set; } = new();

    public ChannelSettings Feishu { get; set; } = new();

    public ChannelSettings Weixin { get; set; } = new();
}

/// <summary>单个通知渠道配置。</summary>
public sealed class ChannelSettings
{
    /// <summary>是否启用该渠道。</summary>
    public bool Enable { get; set; }

    /// <summary>群机器人 WebHook 地址。</summary>
    public string WebHook { get; set; } = string.Empty;

    /// <summary>签名密钥（如钉钉加签，企业微信无需 Secret）。</summary>
    public string Secret { get; set; } = string.Empty;
}
