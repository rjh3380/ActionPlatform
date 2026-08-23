using ActionPlatform.Actions;
using ActionPlatform.Core.Services;
using ActionPlatform.Core.Services.Logging;
using ActionPlatform.Core.Services.Messaging;
using ActionPlatform.Core.Services.StockData;
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
            ServiceManager.InitService(typeof(App), typeof(IntervalLogAction), typeof(AuctionTopPushAction), typeof(AuctionThreePickAction));
            var app = ServiceManager.GetRequiredService<App>();
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
