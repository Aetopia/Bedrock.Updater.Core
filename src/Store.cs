using System;
using System.Net;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.Security;
using System.Threading;
using Windows.Globalization;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using Windows.ApplicationModel.Store.Preview.InstallControl;

static class Store
{
    readonly struct _ : INotifyCompletion
    {
        internal bool IsCompleted => SynchronizationContext.Current is null;

        internal _ GetAwaiter() => this;

        internal void GetResult() { }

        public void OnCompleted(Action continuation)
        {
            var _ = SynchronizationContext.Current;
            try { SynchronizationContext.SetSynchronizationContext(default); continuation(); }
            finally { SynchronizationContext.SetSynchronizationContext(_); }
        }
    }

    static readonly string Address = $"https://displaycatalog.mp.microsoft.com/v7.0/products/{{0}}?languages=iv&market={new GeographicRegion().CodeTwoLetter}";

    static readonly WebClient WebClient = new();

    static readonly AppInstallManager AppInstallManager = new();

    static readonly AppUpdateOptions AppUpdateOptions = new() { AllowForcedAppRestart = true, AutomaticallyDownloadAndInstallUpdateIfFound = true };

    [DllImport("Kernel32", CharSet = CharSet.Auto), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    static extern long GetPackagesByPackageFamily([MarshalAs(UnmanagedType.LPWStr)] string packageFamilyName, out uint count, nint packageFullNames, out uint bufferLength, nint buffer);

    const long ERROR_INSUFFICIENT_BUFFER = 0x7A;

    static async Task<bool> PackageAsync(string productId)
    {
        using var stream = await WebClient.OpenReadTaskAsync(string.Format(Address, productId));
        using var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max);
        return GetPackagesByPackageFamily(XElement.Load(reader).Descendants("PackageFamilyName").First().Value, out var _, default, out var _, default) is ERROR_INSUFFICIENT_BUFFER;
    }

    static async Task<AppInstallItem> AppInstallItemAsync(string productId) =>
    AppInstallManager.AppInstallItems.FirstOrDefault(_ => _.ProductId == productId)
    ?? AppInstallManager.AppInstallItemsWithGroupSupport.FirstOrDefault(_ => _.ProductId == productId)
    ?? (await PackageAsync(productId)
    ? await AppInstallManager.SearchForUpdatesAsync(productId, string.Empty, string.Empty, string.Empty, AppUpdateOptions)
    : await AppInstallManager.StartAppInstallAsync(productId, string.Empty, false, false));

    static async Task GetAsync(string productId, Action<AppInstallStatus> action, CancellationToken token = default)
    {
        var item = await AppInstallItemAsync(productId); if (item is null) return;
        AppInstallManager.MoveToFrontOfDownloadQueue(item.ProductId, string.Empty);

        TaskCompletionSource<bool> source = new();
        using var _ = token.Register(() => { item.Cancel(); ((IAsyncResult)source.Task).AsyncWaitHandle.WaitOne(); });

        item.Completed += (sender, _) =>
        {
            var status = sender.GetCurrentStatus();
            switch (status.InstallState)
            {
                case AppInstallState.Completed:
                    source.TrySetResult(true);
                    break;

                case AppInstallState.Canceled:
                    if (!source.Task.IsFaulted) source.TrySetException(status.ErrorCode);
                    break;
            }
        };

        item.StatusChanged += (sender, _) =>
        {
            var status = sender.GetCurrentStatus();
            switch (status.InstallState)
            {
                case AppInstallState.Error:
                    source.TrySetException(status.ErrorCode);
                    sender.Cancel();
                    break;

                case AppInstallState.Paused
                or AppInstallState.ReadyToDownload
                or AppInstallState.PausedLowBattery
                or AppInstallState.PausedWiFiRequired
                or AppInstallState.PausedWiFiRecommended:
                    AppInstallManager.MoveToFrontOfDownloadQueue(sender.ProductId, string.Empty);
                    break;

                default:
                    action(status);
                    break;
            }
        };

        await source.Task;
    }

    internal static async Task GetAsync(string[] productIds, Action<AppInstallStatus> action, CancellationToken token = default)
    {
        await new _();
        foreach (var productId in productIds)
            await GetAsync(productId, action, token);
    }
}