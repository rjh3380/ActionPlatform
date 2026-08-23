namespace ActionPlatform.Core.Services.StockData.Models;

/// <summary>
/// 同花顺 Financial-API 统一响应信封。HTTP 状态恒为 200，业务结果通过 <see cref="Code"/> 表达（0 = 成功）。
/// </summary>
public class ApiResponse<T>
{
    /// <summary>业务状态码，0 表示成功。</summary>
    public int Code { get; set; }

    /// <summary>业务状态说明。</summary>
    public string? Message { get; set; }

    /// <summary>请求追踪 ID。</summary>
    public string? RequestId { get; set; }

    /// <summary>业务数据。</summary>
    public T? Data { get; set; }
}

/// <summary>分页信息（涨停/跌停/炸板池端点使用 page/size 分页）。</summary>
public class Pagination
{
    public int Total { get; set; }
    public int Pages { get; set; }
    public int Size { get; set; }
    public int Page { get; set; }
}
