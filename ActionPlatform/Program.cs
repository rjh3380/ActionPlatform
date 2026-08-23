using ActionPlatform.Actions;
using ActionPlatform.Core.Services;
using ActionPlatform.Core.Services.ExceptionHandling;
using ActionPlatform.Core.Services.Logging;
using ActionPlatform.Core.Services.Messaging;
using ActionPlatform.Core.Services.StockData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;

namespace ActionPlatform;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            ServiceManager.InitService(typeof(App), typeof(IntervalLogAction), typeof(AuctionTopPushAction), typeof(AuctionThreePickAction));

            // 全局异常处理：崩溃级异常通过消息机制发送通知（需在容器就绪后注册）
            GlobalExceptionHandler.Register(
                ServiceManager.GetRequiredService<IMessageNotifier>(),
                ServiceManager.GetRequiredService<ILoggerFactory>());

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
