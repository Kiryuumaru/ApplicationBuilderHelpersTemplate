using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Application.Shared.Extensions;

public static class AdminPrivilegeChecker
{
    /// <summary>
    /// Throws an exception if the application is not running with administrator/sudo privileges
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when the application lacks required privileges</exception>
    public static void RequireAdminPrivileges()
    {
        if (!IsRunningAsAdmin())
        {
            string osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "Linux/macOS";
            string requiredPrivilege = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "administrator" : "sudo";

            throw new UnauthorizedAccessException(
                $"This application must be run as {requiredPrivilege} on {osType}. " +
                $"Please restart the application with elevated privileges.");
        }
    }

    /// <summary>
    /// Checks if the current process is running with administrator privileges on Windows
    /// or sudo privileges on Linux/macOS
    /// </summary>
    /// <returns>True if running with elevated privileges, false otherwise</returns>
    public static bool IsRunningAsAdmin()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsWindowsAdmin();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IsUnixSudo();
        }

        // Unknown platform - assume no admin privileges
        return false;
    }

    /// <summary>
    /// Checks if running as administrator on Windows
    /// </summary>
    [SupportedOSPlatform("Windows")]
    private static bool IsWindowsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if running with sudo privileges on Unix-like systems
    /// </summary>
    private static bool IsUnixSudo()
    {
        try
        {
            // Check if EUID (Effective User ID) is 0 (root)
            return Environment.GetEnvironmentVariable("USER") == "root" ||
                   Environment.GetEnvironmentVariable("SUDO_USER") != null ||
                   GetEffectiveUserId() == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the effective user ID on Unix systems
    /// </summary>
    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    private static uint GetEffectiveUserId()
    {
        try
        {
            return geteuid();
        }
        catch
        {
            return uint.MaxValue; // Return non-zero value if call fails
        }
    }
}
