using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ActionPlatform.Core.Services.StockData;

/// <summary>
/// 股票数据服务 DI 注册扩展。
/// 注册 <see cref="IStockDataService"/>（typed HttpClient），配置读取 FinancialApi 节（appsettings.Local.json 可覆盖 ApiKey）。
/// </summary>
public static class StockDataServiceExtensions
{
    public static IServiceCollection AddStockDataService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FinancialApiOptions>(configuration.GetSection("FinancialApi"));

        services.AddHttpClient<IStockDataService, StockDataService>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<FinancialApiOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });

        return services;
    }
}
