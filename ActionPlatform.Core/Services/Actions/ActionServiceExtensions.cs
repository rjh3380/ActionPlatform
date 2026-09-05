using Microsoft.Extensions.DependencyInjection;

namespace ActionPlatform.Core.Services.Actions;

/// <summary>Action 机制 DI 注册扩展。</summary>
public static class ActionServiceExtensions
{
    /// <summary>
    /// 注册调度循环单例与每日触发状态存储（重启链下跨进程「今日已成功」去重，供定时 Action 与宿主退出判定使用）。
    /// Action 子类通过 <see cref="AddAction{T}"/>（或等价注册）注册后自动被循环收集。
    /// </summary>
    public static IServiceCollection AddActionLoop(this IServiceCollection services)
    {
        services.AddSingleton<IDailyFireStateStore>(_ => new JsonFileDailyFireStateStore());
        services.AddSingleton<ActionLoop>();
        return services;
    }

    /// <summary>注册 Action 子类（继承 <see cref="ActionBase"/>，单例）；多个注册会被 <see cref="ActionLoop"/> 通过 IEnumerable 收集。</summary>
    public static IServiceCollection AddAction<T>(this IServiceCollection services) where T : ActionBase
    {
        services.AddSingleton<ActionBase, T>();
        return services;
    }
}
