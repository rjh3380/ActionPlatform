namespace ActionPlatform.Core.Services.Actions;

/// <summary>Action 触发模式。</summary>
public enum TriggerMode
{
    /// <summary>间隔触发：每 <see cref="ActionBase.Interval"/> 时长触发一次。</summary>
    Interval,

    /// <summary>每日定时触发：每天本地时区 <see cref="ActionBase.ScheduledTime"/> 时刻触发一次，次日重新生效。</summary>
    Scheduled,
}
