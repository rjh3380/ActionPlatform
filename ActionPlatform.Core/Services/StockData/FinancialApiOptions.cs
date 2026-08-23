namespace ActionPlatform.Core.Services.StockData;

/// <summary>
/// 同花顺 Financial-API 服务配置（配置节 FinancialApi）。
/// ApiKey 为敏感信息，应通过 appsettings.Local.json 覆盖（已被 .gitignore 排除，不入库）。
/// </summary>
public class FinancialApiOptions
{
    /// <summary>API 基地址。</summary>
    public string BaseUrl { get; set; } = "https://fuyao.aicubes.cn";

    /// <summary>API Key（X-api-key 请求头），从 fuyao.aicubes.cn 申请。为空时数据方法调用抛 <see cref="FinancialApiException"/>。</summary>
    public string? ApiKey { get; set; }

    /// <summary>HTTP 请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
