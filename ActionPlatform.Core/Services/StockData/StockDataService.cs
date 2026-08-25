using System.Net;
using System.Text.Json;
using ActionPlatform.Core.Services.StockData.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionPlatform.Core.Services.StockData;

/// <summary>
/// 同花顺 Financial-API 数据服务实现。
/// 所有端点 HTTP 状态恒为 200，业务错误经响应信封 code 表达（0 = 成功），统一在 <see cref="GetDataAsync{T}"/> 中校验。
/// </summary>
public class StockDataService : IStockDataService
{
    // 上游 JSON 字段为 snake_case（如 auction_price），需用 SnakeCaseLower 命名策略映射到 PascalCase 属性；
    // 仅 PropertyNameCaseInsensitive 不处理下划线，会导致数值字段解析为 0。
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private const int MaxPageSize = 200; // 涨跌停/炸板池 size 上限

    private const int BatchSize = 100; // 竞价快照等批量端点每批 thscodes 数量（上游未声明上限，按估值端点 100 保守取值）

    /// <summary>全市场扫描串行路径每批之间的节流间隔，避免瞬时请求过多触发上游限流（429）。</summary>
    private static readonly TimeSpan SerialBatchDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>429 限流重试：最多重试次数与初始退避间隔（指数倍增）。</summary>
    private const int MaxRateLimitRetries = 3;
    private const int RateLimitRetryInitialDelayMs = 2000;

    /// <summary>上游瞬时超时业务错误码（5002 "Auction DataAPI timeout"，实测 09:25 竞价刚结束时出现过）：按限流同样指数退避重试。</summary>
    private const int RetryableBusinessErrorCode = 5002;

    private readonly HttpClient _http;
    private readonly FinancialApiOptions _options;
    private readonly ILogger<StockDataService> _logger;

    public StockDataService(HttpClient http, IOptions<FinancialApiOptions> options, ILogger<StockDataService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    // ---- 行情 / K 线 ----

    public Task<SnapshotData> GetSnapshotAsync(string? thscodes = null, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["thscodes"] = thscodes,
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString(),
        };
        return GetDataAsync<SnapshotData>("/api/a-share/prices/snapshot", query, cancellationToken);
    }

    public Task<HistoricalData> GetHistoricalKLineAsync(string thscode, long startMs, long endMs, string adjust = "forward", CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["thscode"] = thscode,
            ["interval"] = "1d",
            ["start"] = startMs.ToString(),
            ["end"] = endMs.ToString(),
            ["adjust"] = adjust,
        };
        return GetDataAsync<HistoricalData>("/api/a-share/prices/historical", query, cancellationToken);
    }

    public Task<AdjustmentFactorsData> GetAdjustmentFactorsAsync(string thscode, string? from = null, string? to = null, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["thscode"] = thscode,
            ["from"] = from,
            ["to"] = to,
        };
        return GetDataAsync<AdjustmentFactorsData>("/api/a-share/corporate-actions/adjustment-factors", query, cancellationToken);
    }

    // ---- 元信息 ----

    public Task<TickerListData> SearchTickersAsync(string q, string? exchange = null, string? assetType = null, int limit = 10, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["q"] = q,
            ["exchange"] = exchange,
            ["asset_type"] = assetType,
            ["limit"] = limit.ToString(),
        };
        return GetDataAsync<TickerListData>("/api/meta/tickers/search", query, cancellationToken);
    }

    public Task<TickerListData> ListTickersAsync(string exchange = "SH,SZ", string? assetType = null, int limit = 1000, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["exchange"] = exchange,
            ["asset_type"] = assetType,
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString(),
        };
        return GetDataAsync<TickerListData>("/api/meta/tickers/list", query, cancellationToken);
    }

    public Task<TradingDaysData> GetTradingDaysAsync(CancellationToken cancellationToken = default)
        => GetDataAsync<TradingDaysData>("/api/a-share/calendar/trading-days", null, cancellationToken);

    // ---- 集合竞价 ----

    public Task<AuctionSnapshotData> GetAuctionSnapshotAsync(string thscodes, string stage = "final", CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["thscodes"] = thscodes,
            ["stage"] = stage,
        };
        return GetDataAsync<AuctionSnapshotData>("/api/a-share/auction/snapshot", query, cancellationToken);
    }

    public Task<ShortTermBenchmarkData> GetShortTermBenchmarkAsync(string? date = null, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?> { ["date"] = date };
        return GetDataAsync<ShortTermBenchmarkData>("/api/a-share/auction/short-term-benchmark", query, cancellationToken);
    }

    public async Task<IReadOnlyList<AuctionSnapshotItem>> GetAllAuctionSnapshotsAsync(string stage = "final", int? maxConcurrency = null, CancellationToken cancellationToken = default)
    {
        var codes = await GetAllAStockCodesAsync(cancellationToken).ConfigureAwait(false);
        if (codes.Count == 0)
        {
            return Array.Empty<AuctionSnapshotItem>();
        }

        var batches = codes.Chunk(BatchSize).Select(b => string.Join(",", b)).ToList();

        if (maxConcurrency is null or <= 1)
        {
            // 串行路径：每批之间轻微节流，控制请求速率避免触发上游限流（429）
            var all = new List<AuctionSnapshotItem>(codes.Count);
            for (var i = 0; i < batches.Count; i++)
            {
                var data = await GetAuctionSnapshotAsync(batches[i], stage, cancellationToken).ConfigureAwait(false);
                all.AddRange(data.Item);
                if (i < batches.Count - 1)
                {
                    await Task.Delay(SerialBatchDelay, cancellationToken).ConfigureAwait(false);
                }
            }
            return all;
        }

        // 并发拉取：每个批次写自己的槽位（Parallel.ForEachAsync 各迭代互斥处理不同索引，写数组安全）
        var results = new AuctionSnapshotData[batches.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, batches.Count), new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency.Value,
            CancellationToken = cancellationToken,
        }, async (i, ct) =>
        {
            results[i] = await GetAuctionSnapshotAsync(batches[i], stage, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return results.SelectMany(r => r.Item).ToList();
    }

    public async Task<IReadOnlyList<AuctionSnapshotItem>> GetTopAuctionAsync(int top = 10, AuctionSortField sortField = AuctionSortField.Amount, string stage = "final", int? maxConcurrency = null, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAuctionSnapshotsAsync(stage, maxConcurrency, cancellationToken).ConfigureAwait(false);
        return all
            .OrderByDescending(x => sortField switch
            {
                AuctionSortField.Pct => x.AuctionPct,
                AuctionSortField.VolumeRatio => x.AuctionVolumeRatio,
                AuctionSortField.TurnoverPct => x.AuctionTurnoverPct,
                _ => x.AuctionAmount,
            })
            .Take(Math.Max(0, top))
            .ToList();
    }

    // ---- 涨跌停 / 异动 / 热榜 / 龙虎榜 ----

    public Task<LimitPoolData<LimitUpPoolItem>> GetLimitUpPoolAsync(long? dateMs = null, int page = 1, int size = 50, string? sortField = null, string? sortDir = null, CancellationToken cancellationToken = default)
        => GetLimitPoolAsync<LimitUpPoolItem>("/api/a-share/special-data/limit-up-pool", dateMs, page, size, sortField, sortDir, cancellationToken);

    public async Task<IReadOnlyList<LimitUpPoolItem>> GetAllLimitUpPoolAsync(long? dateMs = null, CancellationToken cancellationToken = default)
        => await GetAllLimitPoolAsync(GetLimitUpPoolAsync, dateMs, cancellationToken).ConfigureAwait(false);

    public Task<LimitPoolData<LimitDownPoolItem>> GetLimitDownPoolAsync(long? dateMs = null, int page = 1, int size = 50, string? sortField = null, string? sortDir = null, CancellationToken cancellationToken = default)
        => GetLimitPoolAsync<LimitDownPoolItem>("/api/a-share/special-data/limit-down-pool", dateMs, page, size, sortField, sortDir, cancellationToken);

    public async Task<IReadOnlyList<LimitDownPoolItem>> GetAllLimitDownPoolAsync(long? dateMs = null, CancellationToken cancellationToken = default)
        => await GetAllLimitPoolAsync(GetLimitDownPoolAsync, dateMs, cancellationToken).ConfigureAwait(false);

    public Task<LimitPoolData<LimitBreakPoolItem>> GetLimitBreakPoolAsync(long? dateMs = null, int page = 1, int size = 50, string? sortField = null, string? sortDir = null, CancellationToken cancellationToken = default)
        => GetLimitPoolAsync<LimitBreakPoolItem>("/api/a-share/special-data/limit-break-pool", dateMs, page, size, sortField, sortDir, cancellationToken);

    public async Task<IReadOnlyList<LimitBreakPoolItem>> GetAllLimitBreakPoolAsync(long? dateMs = null, CancellationToken cancellationToken = default)
        => await GetAllLimitPoolAsync(GetLimitBreakPoolAsync, dateMs, cancellationToken).ConfigureAwait(false);

    public Task<LimitUpLadderData> GetLimitUpLadderAsync(CancellationToken cancellationToken = default)
        => GetDataAsync<LimitUpLadderData>("/api/a-share/special-data/limit-up-ladder", null, cancellationToken);

    public Task<AnomalyAnalysisData> GetAnomalyAnalysisListAsync(string? tagCodes = null, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?> { ["tag_codes"] = tagCodes };
        return GetDataAsync<AnomalyAnalysisData>("/api/a-share/special-data/anomaly-analysis-list", query, cancellationToken);
    }

    public Task<AnomalyAnalysisData> GetAnomalyAnalysisStockAsync(string thscodes, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?> { ["thscodes"] = thscodes };
        return GetDataAsync<AnomalyAnalysisData>("/api/a-share/special-data/anomaly-analysis-stock", query, cancellationToken);
    }

    public Task<RankListData> GetSkyrocketListAsync(string period = "day", CancellationToken cancellationToken = default)
        => GetRankListAsync("/api/a-share/special-data/skyrocket-list", period, cancellationToken);

    public Task<RankListData> GetHotStockListAsync(string period = "day", CancellationToken cancellationToken = default)
        => GetRankListAsync("/api/a-share/special-data/hot-stock-list", period, cancellationToken);

    public Task<HotStockHistoryData> GetHotStockListHistoryAsync(string date, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?> { ["date"] = date };
        return GetDataAsync<HotStockHistoryData>("/api/a-share/special-data/hot-stock-list-history", query, cancellationToken);
    }

    public Task<RankTrendData> GetHotStockRankTrendAsync(string thscode, string startDate, string endDate, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["thscode"] = thscode,
            ["start_date"] = startDate,
            ["end_date"] = endDate,
        };
        return GetDataAsync<RankTrendData>("/api/a-share/special-data/hot-stock-rank-trend", query, cancellationToken);
    }

    public Task<DragonTigerData> GetDragonTigerListAsync(string boardType = "all", string? date = null, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["board_type"] = boardType,
            ["date"] = date,
        };
        return GetDataAsync<DragonTigerData>("/api/a-share/special-data/dragon-tiger-list", query, cancellationToken);
    }

    // ---- 私有辅助 ----

    private Task<LimitPoolData<TItem>> GetLimitPoolAsync<TItem>(string path, long? dateMs, int page, int size, string? sortField, string? sortDir, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["date_ms"] = dateMs?.ToString(),
            ["page"] = page.ToString(),
            ["size"] = size.ToString(),
            ["sort_field"] = sortField,
            ["sort_dir"] = sortDir,
        };
        return GetDataAsync<LimitPoolData<TItem>>(path, query, cancellationToken);
    }

    private async Task<IReadOnlyList<TItem>> GetAllLimitPoolAsync<TItem>(
        Func<long?, int, int, string?, string?, CancellationToken, Task<LimitPoolData<TItem>>> pageFn,
        long? dateMs, CancellationToken cancellationToken)
    {
        var result = new List<TItem>();
        var first = await pageFn(dateMs, 1, MaxPageSize, null, null, cancellationToken).ConfigureAwait(false);
        if (first.Item.Count == 0)
        {
            return result;
        }
        result.AddRange(first.Item);
        var pages = first.Pagination?.Pages ?? 1;
        for (var p = 2; p <= pages; p++)
        {
            var page = await pageFn(dateMs, p, MaxPageSize, null, null, cancellationToken).ConfigureAwait(false);
            result.AddRange(page.Item);
        }
        return result;
    }

    private Task<RankListData> GetRankListAsync(string path, string period, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?> { ["period"] = period };
        return GetDataAsync<RankListData>(path, query, cancellationToken);
    }

    /// <summary>全市场 A 股 thscode 列表（SH/SZ，asset_type=a-share，分页取全）。</summary>
    private async Task<List<string>> GetAllAStockCodesAsync(CancellationToken cancellationToken)
    {
        const int pageSize = 1000;
        var codes = new List<string>();
        var offset = 0;
        while (true)
        {
            var page = await ListTickersAsync("SH,SZ", "a-share", pageSize, offset, cancellationToken).ConfigureAwait(false);
            codes.AddRange(page.Item.Where(i => !string.IsNullOrEmpty(i.Thscode)).Select(i => i.Thscode!));
            if (page.Item.Count < pageSize)
            {
                break;
            }
            offset += pageSize;
        }
        return codes;
    }

    /// <summary>统一请求与信封解析：ApiKey 校验 → GET 请求 → 反序列化信封 → code!=0 抛异常 / data 缺失抛异常。HTTP 429 限流时指数退避重试。</summary>
    private async Task<T> GetDataAsync<T>(string path, IDictionary<string, string?>? query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new FinancialApiException(2001, "FinancialApi:ApiKey 未配置，无法调用同花顺数据服务（请在 appsettings.Local.json 中配置）", null);
        }

        var url = BuildUrl(path, query);
        var retryDelayMs = RateLimitRetryInitialDelayMs;

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-api-key", _options.ApiKey);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // 限流（429 request limit exceeded）：指数退避后重试
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRateLimitRetries)
            {
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs *= 2;
                continue;
            }

            // 文档约定 HTTP 恒为 200，防御性处理非 200（如网关错误）。
            if (!response.IsSuccessStatusCode)
            {
                throw new FinancialApiException((int)response.StatusCode, $"HTTP {(int)response.StatusCode}: {json}", null);
            }

            T data;
            try
            {
                data = DeserializeData<T>(json);
            }
            catch (FinancialApiException ex) when (ex.Code == RetryableBusinessErrorCode && attempt < MaxRateLimitRetries)
            {
                // 上游瞬时超时（5002 Auction DataAPI timeout）：与 429 同样指数退避重试
                _logger.LogWarning("上游数据接口瞬时超时（code={Code}），{Delay}ms 后重试。RequestId: {RequestId}", ex.Code, retryDelayMs, ex.RequestId);
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs *= 2;
                continue;
            }
            return data;
        }
    }

    private T DeserializeData<T>(string json)
    {
        ApiResponse<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FinancialApiException(0, $"响应反序列化失败（上游字段可能变更）: {ex.Message}", null, ex);
        }

        if (envelope is null)
        {
            throw new FinancialApiException(0, "响应为空或格式不正确", null);
        }
        if (envelope.Code != 0)
        {
            throw new FinancialApiException(envelope.Code, envelope.Message ?? "未知错误", envelope.RequestId);
        }
        if (envelope.Data is null)
        {
            throw new FinancialApiException(0, "响应缺少 data 字段", envelope.RequestId);
        }
        return envelope.Data;
    }

    private static string BuildUrl(string path, IDictionary<string, string?>? query)
    {
        if (query is null || query.Count == 0)
        {
            return path;
        }
        var pairs = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        return path + "?" + string.Join("&", pairs);
    }
}
