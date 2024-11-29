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

    readonly static AppInstallManager AppInstallManager = new();

    readonly static AppUpdateOptions AppUpdateOptions = new()
    {
        AutomaticallyDownloadAndInstallUpdateIfFound = true,
        AllowForcedAppRestart = true
    };

    static async Task<AppInstallItem> GetAsync((string ProductId, string PackageFamilyName) _, CancellationToken token)
    {
        var operation = GetPackagesByPackageFamily(_.PackageFamilyName, out var _, default, out var _, default) == ERROR_INSUFFICIENT_BUFFER
        ? AppInstallManager.SearchForUpdatesAsync(_.ProductId, string.Empty, string.Empty, string.Empty, AppUpdateOptions)
        : AppInstallManager.StartAppInstallAsync(_.ProductId, string.Empty, false, false);

        var task = operation.AsTask(); using (token.Register(() =>
        {
            operation.Cancel();
            ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
        })) return await task.ConfigureAwait(false);
    }

    internal static async Task GetAsync((string ProductId, string PackageFamilyName) _, Action<AppInstallStatus> action, CancellationToken token)
    {
        var item = await GetAsync(_, token); if (item is not null) AppInstallManager.MoveToFrontOfDownloadQueue(_.ProductId, string.Empty); else return;

        AppInstallStatus status = default; TaskCompletionSource<bool> source = new(); using (token.Register(() =>
        {
            item.Cancel();
            ((IAsyncResult)source.Task).AsyncWaitHandle.WaitOne();
        }))
        {
            void StatusChanged(AppInstallItem sender, object args)
            {
                switch ((status = sender.GetCurrentStatus()).InstallState)
                {
                    case AppInstallState.Error:
                        sender.StatusChanged -= StatusChanged;
                        sender.Cancel(); source.TrySetException(status.ErrorCode);
                        break;

                    case AppInstallState.Paused or AppInstallState.PausedLowBattery or AppInstallState.PausedWiFiRecommended or AppInstallState.PausedWiFiRequired or AppInstallState.ReadyToDownload:
                        AppInstallManager.MoveToFrontOfDownloadQueue(sender.ProductId, string.Empty);
                        break;

                    default:
                        action(status);
                        break;
                }
            }

            item.StatusChanged += StatusChanged; item.Completed += (_, _) =>
            {
                if (status.InstallState is AppInstallState.Canceled) source.TrySetCanceled();
                else source.TrySetResult(true);
            };

            await source.Task.ConfigureAwait(false);
        }
    }

}