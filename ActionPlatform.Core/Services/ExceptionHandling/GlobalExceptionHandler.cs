using ActionPlatform.Core.Services.Messaging;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Core.Services.ExceptionHandling;

/// <summary>
/// 全局异常处理：挂接 AppDomain 未处理异常与未观察任务异常，
/// 出现崩溃级异常时通过 <see cref="IMessageNotifier"/> 发送通知消息，然后按 .NET 默认行为处理（进程终止/继续运行）。
/// 需在 IOC 容器构建完成后调用 <see cref="Register"/> 注册。
/// </summary>
public static class GlobalExceptionHandler
{
    private static readonly object SyncRoot = new();

    private static IMessageNotifier? _notifier;
    private static ILoggerFactory? _loggerFactory;
    private static bool _registered;

    /// <summary>注册全局异常钩子（幂等，重复调用无效）。通知渠道未注册时仅输出控制台/日志，不抛异常。</summary>
    public static void Register(IMessageNotifier? notifier, ILoggerFactory? loggerFactory)
    {
        lock (SyncRoot)
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            _notifier = notifier;
            _loggerFactory = loggerFactory;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }
    }

    /// <summary>
    /// 进程级未处理异常：进程即将终止，必须同步阻塞等待消息发出（限时 10 秒），然后交回运行时按默认行为退出。
    /// </summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        _loggerFactory?.CreateLogger(nameof(GlobalExceptionHandler)).LogError(ex, "捕获未处理异常，进程即将终止");
        Console.Error.WriteLine($"[GlobalExceptionHandler] 未处理异常：{ex}");

        try
        {
            // 阻塞等待发送完成，否则进程退出后消息来不及发出；限时 10 秒避免死等
            _notifier?.SendAsync("ActionPlatform 崩溃通知（未处理异常）", ex).Wait(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // 通知失败不递归抛异常、不阻断进程退出流程
        }
    }

    /// <summary>未观察的任务异常（fire-and-forget Task 抛异常）：标记已观察避免触发进程级行为，异步发送通知，进程继续运行。</summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        var ex = e.Exception;

        _loggerFactory?.CreateLogger(nameof(GlobalExceptionHandler)).LogError(ex, "捕获未观察任务异常");
        Console.Error.WriteLine($"[GlobalExceptionHandler] 未观察任务异常：{ex}");

        try
        {
            _ = _notifier?.SendAsync("ActionPlatform 任务异常通知", ex);
        }
        catch
        {
            // 忽略通知失败
        }
    }
}
