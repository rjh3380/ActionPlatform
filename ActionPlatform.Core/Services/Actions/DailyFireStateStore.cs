using System.Text.Json;

namespace ActionPlatform.Core.Services.Actions;

/// <summary>
/// 每日触发状态存储：记录各 Action 最近一次成功执行的北京日期。
/// GitHub Actions 托管 runner 单 job 上限 6 小时，「重启链」模式下进程会被周期性替换，
/// 进程内记忆（<see cref="ActionBase.LastTriggeredAt"/>）随进程消失 —— 本存储跨进程持久化「今天已成功」，
/// 保证重启后的新进程不会对当天已推送成功的定时 Action 重复执行（至多一次推送）。
/// 状态文件位于仓库内（默认 .ap-state/daily-fired.json，gitignore 排除），由 workflow 在进程自然退出后强制提交入库。
/// </summary>
public interface IDailyFireStateStore
{
    /// <summary>指定 Action 最近一次成功执行的北京日期；从未成功过返回 null。</summary>
    DateOnly? LastFiredBeijingDate(string actionName);

    /// <summary>记录指定 Action 在给定北京日期成功执行（覆盖式写入，内存 + 落盘）。</summary>
    void RecordFired(string actionName, DateOnly beijingDate);
}

/// <summary>
/// 基于 JSON 文件的 <see cref="IDailyFireStateStore"/> 实现。
/// 启动时一次性加载；本进程内的读取走内存缓存，写入时同步落盘（临时文件 + 原子替换）。
/// 说明：重启链保证同一仓库任一时刻至多一个运行实例，因此无需处理跨进程并发写。
/// 路径：环境变量 DAILY_STATE_FILE 优先，缺省 .ap-state/daily-fired.json（相对当前工作目录 = 项目目录）。
/// </summary>
public sealed class JsonFileDailyFireStateStore : IDailyFireStateStore
{
    private sealed class StateFile
    {
        // actionName -> 最近成功执行的北京日期（yyyy-MM-dd）
        public Dictionary<string, string> FiredByAction { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _sync = new(); // 内存字典读写锁：调度循环线程写、宿主线程 15s 轮询读，防 Dictionary 扩容期并发读崩溃
    private readonly string _path;
    private readonly StateFile _state;

    public JsonFileDailyFireStateStore(string? path = null)
    {
        _path = path ?? Environment.GetEnvironmentVariable("DAILY_STATE_FILE") ?? ".ap-state/daily-fired.json";
        _state = Load();
    }

    public DateOnly? LastFiredBeijingDate(string actionName)
    {
        lock (_sync)
        {
            return _state.FiredByAction.TryGetValue(actionName, out var value)
                && DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date)
                    ? date
                    : null;
        }
    }

    public void RecordFired(string actionName, DateOnly beijingDate)
    {
        lock (_sync)
        {
            _state.FiredByAction[actionName] = beijingDate.ToString("yyyy-MM-dd");
            Persist();
        }
    }

    private StateFile Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new StateFile();
            }
            return JsonSerializer.Deserialize<StateFile>(File.ReadAllText(_path), JsonOptions) ?? new StateFile();
        }
        catch (Exception ex)
        {
            // 状态文件损坏/不可读：按空状态继续（最坏后果是当天可能重复推送一次，不影响运行）
            Console.WriteLine($"警告：读取每日触发状态文件失败，按空状态继续：{_path} ({ex.Message})");
            return new StateFile();
        }
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_state, JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }
}
