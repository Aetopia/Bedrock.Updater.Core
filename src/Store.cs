using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store.Preview.InstallControl;

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

    struct Event()
    {
        readonly static Task task = Task.FromResult(true);

        internal readonly Queue<TaskCompletionSource<bool>> queue = new();

        bool signaled = default;

        internal Task WaitAsync()
        {
            lock (queue)
            {
                if (signaled) { signaled = false; return task; }
                TaskCompletionSource<bool> item = new(); queue.Enqueue(item); return item.Task;
            }
        }

        internal void Set()
        {
            TaskCompletionSource<bool> item = default;
            lock (queue) if (queue.Count > 0) item = queue.Dequeue(); else if (!signaled) signaled = true;
            item?.SetResult(true);
        }
    }

    [DllImport("Kernel32"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    static extern long GetPackagesByPackageFamily([MarshalAs(UnmanagedType.LPWStr)] string packageFamilyName, out uint count, nint packageFullNames, out uint bufferLength, nint buffer);

    const long ERROR_INSUFFICIENT_BUFFER = 0x7A;

    readonly static AppInstallManager manager = new();

    readonly static AppUpdateOptions options = new() { AutomaticallyDownloadAndInstallUpdateIfFound = true };

    static async Task<AppInstallItem> GetAsync((string, string) tuple)
    {
        var item = manager.AppInstallItems.FirstOrDefault(_ => _.ProductId.Equals(tuple.Item1, StringComparison.OrdinalIgnoreCase)) ?? await
        (GetPackagesByPackageFamily(tuple.Item2, out var _, default, out var _, default) == ERROR_INSUFFICIENT_BUFFER
        ? manager.SearchForUpdatesAsync(tuple.Item1, string.Empty, string.Empty, string.Empty, options)
        : manager.StartAppInstallAsync(tuple.Item1, string.Empty, false, false));
        if (item is not null) manager.MoveToFrontOfDownloadQueue(tuple.Item1, string.Empty);
        return item;
    }

    internal static async Task GetAsync((string, string) tuple, Action<AppInstallStatus> action, CancellationToken token)
    {
        await new _();

        var item = await GetAsync(tuple); if (item is null) return;
        Event @event = new(); AppInstallStatus status = default;

        item.Completed += (sender, _) => @event.Set();
        item.StatusChanged += (sender, _) =>
        {
            if (token.IsCancellationRequested) { sender.Cancel(); return; }

            action(status = sender.GetCurrentStatus());

            if (status.InstallState is AppInstallState.Paused or AppInstallState.PausedLowBattery or AppInstallState.PausedWiFiRecommended or AppInstallState.PausedWiFiRequired or AppInstallState.ReadyToDownload)
                manager.MoveToFrontOfDownloadQueue(sender.ProductId, string.Empty);
        };

        await @event.WaitAsync(); if (status.ErrorCode is not null) throw status.ErrorCode;
    }
}