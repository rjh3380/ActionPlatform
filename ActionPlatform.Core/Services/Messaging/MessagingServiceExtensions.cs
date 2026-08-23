using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ActionPlatform.Core.Services.Messaging;

/// <summary>
/// 消息服务注册扩展：通过 AddEasyNotice 按配置启用渠道（钉钉/飞书/企业微信），
/// 并注册统一门面 IMessageNotifier。
/// </summary>
public static class MessagingServiceExtensions
{
    public static IServiceCollection AddMessagingService(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("EasyNotice").Get<NoticeSettings>() ?? new NoticeSettings();

        services.AddEasyNotice(options =>
        {
            if (settings.Dingtalk.Enable && !string.IsNullOrWhiteSpace(settings.Dingtalk.WebHook))
            {
                options.UseDingTalk(dingtalk =>
                {
                    dingtalk.WebHook = settings.Dingtalk.WebHook;
                    dingtalk.Secret = settings.Dingtalk.Secret;
                });
            }

            if (settings.Feishu.Enable && !string.IsNullOrWhiteSpace(settings.Feishu.WebHook))
            {
                options.UseFeishu(feishu =>
                {
                    feishu.WebHook = settings.Feishu.WebHook;
                    feishu.Secret = settings.Feishu.Secret;
                });
            }

            if (settings.Weixin.Enable && !string.IsNullOrWhiteSpace(settings.Weixin.WebHook))
            {
                options.UseWeixin(weixin => weixin.WebHook = settings.Weixin.WebHook);
            }
        });

        services.AddSingleton<IMessageNotifier, EasyNoticeMessageNotifier>();
        return services;
    }
}
