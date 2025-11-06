using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace JrTools.Extensions
{
    public static class DispatcherQueueExtensions
    {
        public static Task EnqueueAsync(this DispatcherQueue dispatcher, Action action)
        {
            var tcs = new TaskCompletionSource();
            
            dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        public static Task EnqueueAsync(this DispatcherQueue dispatcher, Func<Task> asyncAction)
        {
            var tcs = new TaskCompletionSource();
            
            dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await asyncAction();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}