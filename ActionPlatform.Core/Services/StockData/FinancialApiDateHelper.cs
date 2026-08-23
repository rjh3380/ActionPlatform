using System.Globalization;

namespace ActionPlatform.Core.Services.StockData;

/// <summary>
/// Financial-API 时间参数辅助：上游时间统一为毫秒级 Unix 时间戳，按 Asia/Shanghai 时区解释（中国无夏令时，固定 +08:00）。
/// </summary>
public static class FinancialApiDateHelper
{
    /// <summary>Asia/Shanghai 固定偏移（中国不使用夏令时）。</summary>
    public static readonly TimeSpan ShanghaiOffset = TimeSpan.FromHours(8);

    /// <summary>DateTimeOffset → 毫秒时间戳。</summary>
    public static long ToMilliseconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

    /// <summary>毫秒时间戳 → DateTimeOffset。</summary>
    public static DateTimeOffset FromMilliseconds(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);

    /// <summary>
    /// 日期字符串（yyyy-MM-dd 或 yyyyMMdd，按 Asia/Shanghai 零点解释）→ 毫秒时间戳。
    /// </summary>
    public static long DateStringToMilliseconds(string date)
    {
        var normalized = date.Replace("-", "");
        var d = DateTime.ParseExact(normalized, "yyyyMMdd", CultureInfo.InvariantCulture);
        return new DateTimeOffset(d, ShanghaiOffset).ToUnixTimeMilliseconds();
    }

    /// <summary>毫秒时间戳 → yyyy-MM-dd（Asia/Shanghai）。</summary>
    public static string MillisecondsToDateString(long milliseconds)
        => FromMilliseconds(milliseconds).ToOffset(ShanghaiOffset).ToString("yyyy-MM-dd");
}
