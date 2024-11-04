using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Store.Preview.InstallControl;

static class Store
{
    [DllImport("Kernel32"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    static extern long GetPackagesByPackageFamily([MarshalAs(UnmanagedType.LPWStr)] string packageFamilyName, out uint count, nint packageFullNames, out uint bufferLength, nint buffer);

    const long ERROR_INSUFFICIENT_BUFFER = 0x7A;

    readonly static AppInstallManager manager = new();

    readonly static AppUpdateOptions options = new() { AutomaticallyDownloadAndInstallUpdateIfFound = true };

    internal static async Task GetAsync((string Id, string Name) product, Action<AppInstallStatus> action, CancellationToken token)
    {
        var item = await (GetPackagesByPackageFamily(product.Name, out var _, default, out var _, default) == ERROR_INSUFFICIENT_BUFFER
        ? manager.SearchForUpdatesAsync(product.Id, string.Empty, string.Empty, string.Empty, options)
        : manager.StartAppInstallAsync(product.Id, string.Empty, false, false)).AsTask().ConfigureAwait(false);
        if (item is not null) manager.MoveToFrontOfDownloadQueue(product.Id, string.Empty); else return;

        using (token.Register(item.Cancel))
        {
            TaskCompletionSource<bool> source = new();
            item.Completed += (_, _) => source.TrySetResult(true);

            AppInstallStatus status = default;
            item.StatusChanged += (sender, _) =>
            {
                status = sender.GetCurrentStatus();
                switch (status.InstallState)
                {
                    case AppInstallState.Error:
                        sender.Cancel();
                        break;

                    case AppInstallState.Paused or AppInstallState.PausedLowBattery or AppInstallState.PausedWiFiRecommended or AppInstallState.PausedWiFiRequired or AppInstallState.ReadyToDownload:
                        manager.MoveToFrontOfDownloadQueue(sender.ProductId, string.Empty);
                        break;

                    default:
                        action(status);
                        break;
                }
            };

            await source.Task.ConfigureAwait(false);
            if (status.ErrorCode is not null && !token.IsCancellationRequested) throw status.ErrorCode;
        }
    }
}