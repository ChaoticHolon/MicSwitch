using System.Diagnostics;
using System.Security.Principal;
using MicSwitch.Platform;
using Microsoft.Win32;

namespace MicSwitch.Windows;

/// <summary>
/// Registers MicSwitch to start at sign-in. Elevated processes are blocked from the Run key at logon,
/// so when running elevated a scheduled task with highest privileges is used instead.
/// </summary>
public sealed class StartupService : IStartupRegistration
{
    public const string AutostartArgument = "--autostart";
    private const string Name = "MicSwitch";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsElevated { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    private static string Command => $"\"{Environment.ProcessPath}\" {AutostartArgument}";

    public bool IsEnabled => HasRunKey() || HasScheduledTask();

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (IsElevated)
            {
                var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
                Schtasks($"/Create /F /TN \"{Name}\" /TR \"{Command.Replace("\"", "\\\"", StringComparison.Ordinal)}\" /SC ONLOGON /RL HIGHEST /RU \"{user}\" /IT");
            }
            else
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                key.SetValue(Name, Command);
            }

            return;
        }

        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
        {
            key?.DeleteValue(Name, throwOnMissingValue: false);
        }

        if (HasScheduledTask() && Schtasks($"/Delete /F /TN \"{Name}\"") != 0)
        {
            throw new InvalidOperationException("Run MicSwitch as administrator to remove the elevated startup task.");
        }
    }

    private static bool HasRunKey()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(Name) is not null;
    }

    private static bool HasScheduledTask() => Schtasks($"/Query /TN \"{Name}\"") == 0;

    private static int Schtasks(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
