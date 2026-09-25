using System.Windows;
using MicSwitch.Services;
using Velopack;

namespace MicSwitch;

internal static class Program
{
    private const string InstanceId = "MicSwitch-{567EBFFF-E391-4B38-AC85-469978EB37C4}";

    [STAThread]
    private static void Main(string[] args)
    {
        // Must run first: handles install/uninstall/update hooks and may exit the process.
        VelopackApp.Build().Run();

        using var mutex = new Mutex(initiallyOwned: true, $@"Local\{InstanceId}", out var isFirstInstance);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{InstanceId}-show");
        if (!isFirstInstance)
        {
            showSignal.Set();
            return;
        }

        var app = new App(args.Contains(StartupService.AutostartArgument, StringComparer.OrdinalIgnoreCase), showSignal);
        app.InitializeComponent();
        app.Run();
        GC.KeepAlive(mutex);
    }
}
