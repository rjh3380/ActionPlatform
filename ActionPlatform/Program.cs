using ActionPlatform.Core.Services.Logging;
using ActionPlatform.Core.Services.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace ActionPlatform;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            // 1. 加载配置（appsettings.Local.json 覆盖基础配置，本地密钥不入库）
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .Build();

            // 2. 构建 IOC 容器并装配基础服务
            await using var provider = new ServiceCollection()
                .AddLoggingService()
                .AddMessagingService(configuration)
                .AddTransient<App>()
                .BuildServiceProvider();

            // 3. 从容器解析并运行
            var app = provider.GetRequiredService<App>();
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用启动失败: {ex}");
            throw;
        }
        finally
        {
            // 刷新并关闭 NLog 目标
            LogManager.Shutdown();
        }
    }
}
