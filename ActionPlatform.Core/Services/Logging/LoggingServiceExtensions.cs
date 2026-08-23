using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace ActionPlatform.Core.Services.Logging;

/// <summary>
/// 日志服务注册扩展：接入 Microsoft.Extensions.Logging 抽象，NLog 作为日志提供程序（配置来自 nlog.config）。
/// </summary>
public static class LoggingServiceExtensions
{
    public static IServiceCollection AddLoggingService(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddNLog());
        return services;
    }
}
