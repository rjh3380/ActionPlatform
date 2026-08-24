using ActionPlatform.Core.Services.Actions;
using ActionPlatform.Core.Services.Logging;
using ActionPlatform.Core.Services.Messaging;
using ActionPlatform.Core.Services.StockData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ActionPlatform.Core.Services
{
    public static class ServiceManager
    {
        static ServiceProvider _serviceProvider;

        public static void InitService(params IEnumerable<Type> types)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .Build();

            // 2. 构建 IOC 容器并装配基础服务（configuration 注册进容器，供构造注入的服务按需读取）
            var sc = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddLoggingService()
                .AddMessagingService(configuration)
                .AddStockDataService(configuration)
                .AddActionLoop();
            foreach (var type in types)
            {
                sc.AddTransient(type);
                // Action 子类同时注册为 ActionBase 单例，供 ActionLoop 通过 IEnumerable<ActionBase> 收集
                if (type != typeof(ActionBase) && typeof(ActionBase).IsAssignableFrom(type))
                {
                    sc.AddSingleton(typeof(ActionBase), type);
                }
            }
            _serviceProvider = sc.BuildServiceProvider();
        }

        public static T GetRequiredService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>() as T;
        }
    }
}
