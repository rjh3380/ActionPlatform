namespace ActionPlatform.Core.Services.StockData;

/// <summary>
/// 同花顺 Financial-API 调用异常。
/// 上游 HTTP 状态恒为 200，业务错误通过响应信封 code 表达（如 1001 参数缺失、2001 认证失败、1002 参数非法、1003 超限）。
/// </summary>
public class FinancialApiException : Exception
{
    /// <summary>业务状态码（0 为成功；此处恒为非 0）。</summary>
    public int Code { get; }

    /// <summary>请求追踪 ID，便于上游排查。</summary>
    public string? RequestId { get; }

    public FinancialApiException(int code, string message, string? requestId, Exception? innerException = null)
        : base(BuildMessage(code, message, requestId), innerException)
    {
        Code = code;
        RequestId = requestId;
    }

    private static string BuildMessage(int code, string message, string? requestId)
        => $"同花顺数据服务调用失败 (code={code}){(string.IsNullOrEmpty(requestId) ? "" : $", requestId={requestId}")}: {message}";
}
