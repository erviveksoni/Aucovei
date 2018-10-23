using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Aucovei.Device.Helper
{
    public static class TaskHelper
    {
        public static async Task WithTimeoutAfterStart(Func<CancellationToken, Task> operation, TimeSpan timeout)
        {
            var source = new CancellationTokenSource();
            var task = operation(source.Token);
            //After task starts timeout begin to tick
            source.CancelAfter(timeout);
            await task;
        }

        public static async Task CancelTaskAfterTimeout(Func<CancellationToken, Task> operation, TimeSpan timeout)
        {
            var source = new CancellationTokenSource();
            var task = operation(source.Token);
            //After task starts timeout begin to tick
            source.CancelAfter(timeout);
            await task;
        }

        public static Task DispatchAsync(CoreDispatcherPriority priority, DispatchedHandler handler)
        {
            return Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(priority, handler).AsTask();
        }
    }
}
