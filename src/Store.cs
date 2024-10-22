using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Windows.ApplicationModel.Store.Preview.InstallControl;
using Windows.Management.Deployment;
using Windows.System.UserProfile;


static class Store
{
    readonly struct _ : INotifyCompletion
    {
        internal readonly bool IsCompleted => SynchronizationContext.Current is null;

        internal readonly _ GetAwaiter() => this;

        internal readonly void GetResult() { }

        public readonly void OnCompleted(Action continuation)
        {
            var _ = SynchronizationContext.Current;
            try { SynchronizationContext.SetSynchronizationContext(null); continuation(); }
            finally { SynchronizationContext.SetSynchronizationContext(_); }
        }
    }

    struct Event
    {
        readonly static Task task = Task.FromResult(true);

        readonly static Queue<TaskCompletionSource<bool>> queue = new();

        static bool @bool = default;

        internal readonly Task WaitAsync()
        {
            lock (queue)
            {
                if (@bool) { @bool = false; return task; }
                TaskCompletionSource<bool> item = new(); queue.Enqueue(item); return item.Task;
            }
        }

        internal readonly void Set()
        {
            TaskCompletionSource<bool> item = default;
            lock (queue)
            {
                if (queue.Count > 0) item = queue.Dequeue();
                else if (!@bool) @bool = true;
            }
            item?.SetResult(true);
        }
    }

    readonly static PackageManager packageManager = new();

    readonly static AppInstallManager appInstallManager = new();

    readonly static WebClient client = new();

    readonly static string address = $"https://displaycatalog.mp.microsoft.com/v7.0/products/{{0}}?languages=iv&market={GlobalizationPreferences.HomeGeographicRegion}";

    readonly static AppUpdateOptions updateOptions = new() { AutomaticallyDownloadAndInstallUpdateIfFound = true };

    static async Task<AppInstallItem> GetAsync(string productId)
    {
        var appInstallItem = appInstallManager.AppInstallItems.FirstOrDefault(_ => _.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
        if (appInstallItem is not null) { appInstallManager.MoveToFrontOfDownloadQueue(productId, string.Empty); return appInstallItem; }

      //  using var reader = JsonReaderWriterFactory.CreateJsonReader(await client.DownloadDataTaskAsync(string.Format(address, productId)), XmlDictionaryReaderQuotas.Max);
        return await appInstallManager.StartAppInstallAsync(productId, string.Empty, false, false);
    }

    internal static async Task GetAsync(string productId, Action<AppInstallStatus> action, CancellationToken token)
    {
        await new _();

        var appInstallItem = await GetAsync(productId); if (appInstallItem is null) return;
        Event @event = new(); AppInstallStatus appInstallStatus = default;

        appInstallItem.StatusChanged += (sender, _) =>
        {
            if (token.IsCancellationRequested) try { sender.Cancel(); } catch { }
            appInstallStatus = sender.GetCurrentStatus(); action(appInstallStatus);
            Console.WriteLine(appInstallStatus.InstallState);
            if (appInstallStatus.InstallState is AppInstallState.Completed or AppInstallState.Canceled or AppInstallState.Error) @event.Set();
        };
        await @event.WaitAsync();

        if (appInstallStatus.InstallState is AppInstallState.Canceled or AppInstallState.Error) throw appInstallStatus.ErrorCode;
    }
}