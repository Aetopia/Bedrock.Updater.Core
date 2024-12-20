using System;
using System.Linq;
using System.Threading;
using System.Globalization;

static class Program
{
    [STAThread]
    static void Main(string[] _)
    {
        using Mutex mutex = new(true, "C7B58EAD-356C-40A1-A145-7262C3C04D00", out bool createdNew); if (!createdNew) return;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        new Window(_.Any(_ => _.Equals("/preview", StringComparison.OrdinalIgnoreCase))).ShowDialog();
    }
}