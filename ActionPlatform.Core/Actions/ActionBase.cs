using ActionPlatform.Core.Services.Messaging;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Core.Actions
{
    public abstract class ActionBase
    {
        internal readonly ILogger<ActionBase> _logger;
        internal readonly IMessageNotifier _notifier;

        public ActionBase(ILogger<ActionBase> logger, IMessageNotifier notifier)
        {
            _logger = logger;
            _notifier = notifier;
        }
    }
}
