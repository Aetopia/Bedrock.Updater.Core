using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Store.Preview.InstallControl;
using System.Threading;

sealed class Window : System.Windows.Window
{
    [DllImport("Shell32", CharSet = CharSet.Auto, SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    static extern int ShellMessageBox(nint hAppInst = default, nint hWnd = default, string lpcText = default, string lpcTitle = "Bedrock Updater", int fuStyle = 0x00000010);

    //   enum Unit { B, KB, MB, GB }
    //
    //   static string Size(float value) { var unit = (int)Math.Log(value, 1024); return $"{value / Math.Pow(1024, unit):0.00} {(Unit)unit}"; }

    AppInstallItem appInstallItem = default;

    readonly AutoResetEvent autoResetEvent = new(false);

    Exception exception = default;

    public Window(bool _)
    {
        //  Icon = global::Resources.Get<ImageSource>(".ico");
        UseLayoutRounding = true;
        Title = "Bedrock Updater Deluxe";
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

        //  
        Closed += (_, _) => Environment.Exit(0);

        Dispatcher.UnhandledException += (_, e) =>
        {
            e.Handled = true; var exception = e.Exception;
            while (exception.InnerException is not null) exception = exception.InnerException;
            ShellMessageBox(hWnd: new WindowInteropHelper(this).Handle, lpcText: exception.Message);
            Close();
        };

        ContentRendered += async (_, _) => await Task.Run(() =>
        {
            foreach (var appInstallItem in Store.Get("9WZDNCRD1HKW", _ ? "9P5X4QVLC2XR" : "9NBLGGH2JHXJ"))
            {
                if ((this.appInstallItem = appInstallItem) is null) continue;

                var appInstallStatus = appInstallItem.GetCurrentStatus();
                if (appInstallStatus.InstallState is AppInstallState.Canceled or AppInstallState.Error) throw appInstallStatus.ErrorCode;
                else if (appInstallStatus.InstallState is AppInstallState.Completed) continue;

                appInstallItem.StatusChanged += (sender, args) =>
                {
                    appInstallStatus = sender.GetCurrentStatus();
                    Dispatcher.Invoke(() =>
                    {
                        if (progressBar.Value != appInstallStatus.PercentComplete)
                        {
                            if (progressBar.IsIndeterminate) progressBar.IsIndeterminate = false;
                            textBlock2.Text = $"Preparing... {progressBar.Value = appInstallStatus.PercentComplete}%";
                        }
                        if (appInstallStatus.InstallState is AppInstallState.Canceled or AppInstallState.Error or AppInstallState.Completed)
                        {
                            if (progressBar.IsIndeterminate) progressBar.IsIndeterminate = false;
                            progressBar.Value = 0;
                            textBlock2.Text = "Preparing...";
                            autoResetEvent.Set();
                        }
                    });
                };

                autoResetEvent.WaitOne();
            }
            Dispatcher.Invoke(Close);
        });
    }
}