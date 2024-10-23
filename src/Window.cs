using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Threading;
using System.Reflection;
using System.Windows.Media.Imaging;

sealed class Window : System.Windows.Window
{
    [DllImport("Shell32", CharSet = CharSet.Auto, SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    static extern int ShellMessageBox(nint hAppInst = default, nint hWnd = default, string lpcText = default, string lpcTitle = "Bedrock Updater Core", int fuStyle = 0x00000010);

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

        CancellationTokenSource source = new();

        Task task = default;

        Closed += (_, _) =>
        {
            try { source.Cancel(); } catch { }
        };

        Dispatcher.UnhandledException += (_, e) =>
        {
            e.Handled = true; var exception = e.Exception;
            while (exception.InnerException is not null) exception = exception.InnerException;
            ShellMessageBox(hWnd: new WindowInteropHelper(this).Handle, lpcText: exception.Message);
            Close();
        };

        ContentRendered += async (_, _) =>
        {
            foreach (var item in new (string, string)[] {
                new("9WZDNCRD1HKW", "Microsoft.XboxIdentityProvider_8wekyb3d8bbwe"), _ ?
                new("9P5X4QVLC2XR", "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe") :
                new("9NBLGGH2JHXJ", "Microsoft.MinecraftUWP_8wekyb3d8bbwe") })
            {
                using (source = new()) await (task = Store.GetAsync(item, (_) => Dispatcher.Invoke(() =>
                {
                    if (progressBar.Value != _.PercentComplete)
                    {
                        if (progressBar.IsIndeterminate) progressBar.IsIndeterminate = false;
                        textBlock2.Text = $"Preparing...  {progressBar.Value = _.PercentComplete}%";
                    }
                    else if (_.PercentComplete is 0 or 100)
                    {
                        if (!progressBar.IsIndeterminate) progressBar.IsIndeterminate = true;
                        textBlock2.Text = "Preparing..."; progressBar.Value = 0;
                    }
                }), source.Token));
            }
            Close();
        };
    }
}