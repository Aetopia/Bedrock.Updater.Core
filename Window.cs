using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Store.Preview.InstallControl;
using System.Threading;
using System.Reflection;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Net;
using Windows.Management.Deployment;

sealed class Window : System.Windows.Window
{
    [DllImport("Shell32", CharSet = CharSet.Auto, SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    static extern int ShellMessageBox(nint hAppInst = default, nint hWnd = default, string lpcText = default, string lpcTitle = "Bedrock Updater Core", int fuStyle = 0x00000010);

    AppInstallItem appInstallItem = default;

    readonly AppInstallManager appInstallManager = new();

    public Window(bool _)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(".ico");
        Icon = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        UseLayoutRounding = true;
        Title = "Bedrock Updater Core";
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        var text = _ ? "Updating Preview..." : "Updating Release...";

        Canvas canvas = new() { Width = 381, Height = 115 }; Content = canvas;

        TextBlock textBlock1 = new() { Text = text, Foreground = Brushes.White };
        canvas.Children.Add(textBlock1); Canvas.SetLeft(textBlock1, 11); Canvas.SetTop(textBlock1, 15);

        TextBlock textBlock2 = new() { Text = "Preparing...", Foreground = Brushes.White };
        canvas.Children.Add(textBlock2); Canvas.SetLeft(textBlock2, 11); Canvas.SetTop(textBlock2, 84);

        ProgressBar progressBar = new()
        {
            Width = 359,
            Height = 23,
            BorderThickness = default,
            IsIndeterminate = true,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 133, 66)),
            Background = new SolidColorBrush(Color.FromRgb(14, 14, 14))
        };
        canvas.Children.Add(progressBar); Canvas.SetLeft(progressBar, 11); Canvas.SetTop(progressBar, 46);

        Closed += (_, _) => { try { appInstallItem.Cancel(); } catch { } };

        Dispatcher.UnhandledException += (_, e) =>
        {
            e.Handled = true; var exception = e.Exception;
            while (exception.InnerException is not null) exception = exception.InnerException;
            ShellMessageBox(hWnd: new WindowInteropHelper(this).Handle, lpcText: exception.Message);
            Close();
        };

        ContentRendered += async (_, _) => await Task.Run(() =>
        {
            using WebClient client = new();
            using AutoResetEvent autoResetEvent = new(false);
            PackageManager packageManager = new();
            AppUpdateOptions updateOptions = new() { AutomaticallyDownloadAndInstallUpdateIfFound = true };
            string address = $"https://displaycatalog.mp.microsoft.com/v7.0/products/{{0}}?languages=iv&market={new GeographicRegion().CodeTwoLetter}";

            foreach (var appInstallItem in new string[] { "9WZDNCRD1HKW", _ ? "9P5X4QVLC2XR" : "9NBLGGH2JHXJ" }.Select(productId =>
            {
                var appInstallItem = appInstallManager.AppInstallItems.FirstOrDefault(_ => _.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
                if (appInstallItem is not null) return appInstallItem;

                using var stream = client.OpenRead(string.Format(address, productId));
                using var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max);
                var element = XElement.Load(reader); var skuId = element.Descendants("PreferredSkuId").First().Value;

                return (packageManager.FindPackagesForUser(string.Empty, element.Descendants("PackageFamilyName").First().Value).Any()
                ? appInstallManager.SearchForUpdatesAsync(productId, skuId, string.Empty, string.Empty, updateOptions)
                : appInstallManager.StartAppInstallAsync(productId, skuId, default, default)).AsTask().Result;
            }))
            {
                if (appInstallItem is null) continue;
                Console.WriteLine(appInstallItem.InstallType);
                AppInstallStatus appInstallStatus = default;
                (this.appInstallItem = appInstallItem).StatusChanged += (sender, args) => Dispatcher.Invoke(() =>
                {
                    if (progressBar.Value != (appInstallStatus = sender.GetCurrentStatus()).PercentComplete && appInstallStatus.PercentComplete != 0)
                    {
                        if (progressBar.IsIndeterminate) progressBar.IsIndeterminate = false;
                        textBlock2.Text = $"Preparing... {progressBar.Value = appInstallStatus.PercentComplete}%";
                    }

                    if (appInstallStatus.InstallState is AppInstallState.Canceled or AppInstallState.Error or AppInstallState.Completed)
                    {
                        if (!progressBar.IsIndeterminate) progressBar.IsIndeterminate = true;
                        progressBar.Value = 0;
                        textBlock2.Text = "Preparing...";
                        autoResetEvent.Set();
                    }
                    else if (appInstallStatus.InstallState
                    is AppInstallState.Paused
                    or AppInstallState.PausedLowBattery
                    or AppInstallState.PausedWiFiRecommended
                    or AppInstallState.PausedWiFiRequired or AppInstallState.ReadyToDownload)
                    {
                        if (!progressBar.IsIndeterminate) progressBar.IsIndeterminate = true;
                        textBlock2.Text = "Preparing..."; progressBar.Value = 0;
                        appInstallManager.MoveToFrontOfDownloadQueue(sender.ProductId, string.Empty);
                    }
                });
                autoResetEvent.WaitOne();
                if (appInstallStatus.InstallState is AppInstallState.Error or AppInstallState.Canceled) throw appInstallStatus.ErrorCode;
            }
            Dispatcher.Invoke(Close);
        });
    }
}