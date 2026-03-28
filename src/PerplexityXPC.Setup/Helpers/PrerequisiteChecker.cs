using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PerplexityXPC.Setup.Helpers;

/// <summary>
/// Static helper that detects whether required software is installed
/// before the setup wizard runs.
/// </summary>
public static class PrerequisiteChecker
{
    // -------------------------------------------------------------------------
    // .NET 8
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether .NET 8 runtime is installed.
    /// </summary>
    /// <returns>Tuple of (found, version string).</returns>
    public static (bool Found, string Version) CheckDotNet()
    {
        try
        {
            var output = RunCommand("dotnet", "--list-runtimes");
            // Look for Microsoft.NETCore.App 8.x
            var match = Regex.Match(output,
                @"Microsoft\.NETCore\.App\s+(8\.\d+\.\d+)",
                RegexOptions.IgnoreCase);

            if (match.Success)
                return (true, $"Found v{match.Groups[1].Value}");

            // Fallback: dotnet --version
            string ver = RunCommand("dotnet", "--version").Trim();
            if (ver.StartsWith("8.", StringComparison.Ordinal))
                return (true, $"Found v{ver}");

            return (false, "Not found (dotnet --list-runtimes returned no .NET 8 entry)");
        }
        catch
        {
            return (false, "Not found (dotnet.exe not in PATH)");
        }
    }

    // -------------------------------------------------------------------------
    // Node.js
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether Node.js v20 or later is installed.
    /// </summary>
    /// <returns>Tuple of (found, version string).</returns>
    public static (bool Found, string Version) CheckNodeJs()
    {
        try
        {
            string ver = RunCommand("node", "--version").Trim(); // e.g. "v20.11.0"
            var match = Regex.Match(ver, @"v?(\d+)\.(\d+)\.(\d+)");
            if (match.Success && int.Parse(match.Groups[1].Value) >= 20)
                return (true, $"Found {ver}");

            if (match.Success)
                return (false, $"Found {ver} - v20+ required");

            return (false, "Not found");
        }
        catch
        {
            return (false, "Not found (node not in PATH)");
        }
    }

    // -------------------------------------------------------------------------
    // PowerShell
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether PowerShell 5.1 or later is available.
    /// </summary>
    /// <returns>Tuple of (found, version string).</returns>
    public static (bool Found, string Version) CheckPowerShell()
    {
        // Try PowerShell 7+ (pwsh) first
        try
        {
            string ver = RunCommand("pwsh", "-NoProfile -Command $PSVersionTable.PSVersion.ToString()").Trim();
            if (!string.IsNullOrEmpty(ver))
                return (true, $"PowerShell 7: {ver}");
        }
        catch { /* fall through */ }

        // Try classic PowerShell 5
        try
        {
            string ver = RunCommand("powershell", "-NoProfile -Command $PSVersionTable.PSVersion.ToString()").Trim();
            if (!string.IsNullOrEmpty(ver))
                return (true, $"PowerShell 5: {ver}");
        }
        catch { /* fall through */ }

        return (false, "Not found");
    }

    // -------------------------------------------------------------------------
    // Windows version
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether the OS is Windows 10 1809 (build 17763) or later.
    /// </summary>
    /// <returns>Tuple of (supported, build description).</returns>
    public static (bool Supported, string Build) CheckWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (false, "Not Windows");

        var ver = Environment.OSVersion.Version;
        // Windows 10 1809 = build 17763
        bool ok = ver.Major > 10 ||
                  (ver.Major == 10 && ver.Build >= 17763);

        string buildStr = $"Windows {ver.Major} Build {ver.Build}";
        return (ok, ok ? $"Supported ({buildStr})" : $"Unsupported ({buildStr}) - Windows 10 1809+ required");
    }

    // -------------------------------------------------------------------------
    // All checks combined
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs all prerequisite checks and returns a combined result object.
    /// </summary>
    public static PrerequisiteResult CheckAll()
    {
        var dotnet = CheckDotNet();
        var node   = CheckNodeJs();
        var ps     = CheckPowerShell();
        var win    = CheckWindows();

        return new PrerequisiteResult
        {
            DotNetFound  = dotnet.Found,
            DotNetVersion = dotnet.Version,
            NodeFound    = node.Found,
            NodeVersion  = node.Version,
            PsFound      = ps.Found,
            PsVersion    = ps.Version,
            WindowsOk    = win.Supported,
            WindowsBuild = win.Build,
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string RunCommand(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start {exe}");

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }
}

/// <summary>Aggregated result from all prerequisite checks.</summary>
public sealed class PrerequisiteResult
{
    /// <summary>True if .NET 8 runtime was found.</summary>
    public bool   DotNetFound    { get; init; }
    /// <summary>Human-readable .NET version or error message.</summary>
    public string DotNetVersion  { get; init; } = string.Empty;

    /// <summary>True if Node.js v20+ was found.</summary>
    public bool   NodeFound      { get; init; }
    /// <summary>Human-readable Node.js version or error message.</summary>
    public string NodeVersion    { get; init; } = string.Empty;

    /// <summary>True if PowerShell 5.1 or later was found.</summary>
    public bool   PsFound        { get; init; }
    /// <summary>Human-readable PowerShell version or error message.</summary>
    public string PsVersion      { get; init; } = string.Empty;

    /// <summary>True if the OS is Windows 10 1809 or later.</summary>
    public bool   WindowsOk      { get; init; }
    /// <summary>Human-readable Windows build description.</summary>
    public string WindowsBuild   { get; init; } = string.Empty;

    /// <summary>True if all prerequisite checks pass.</summary>
    public bool AllMet => DotNetFound && NodeFound && PsFound && WindowsOk;
}
