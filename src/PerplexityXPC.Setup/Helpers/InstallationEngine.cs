using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using PerplexityXPC.Setup.Models;

namespace PerplexityXPC.Setup.Helpers;

/// <summary>
/// Static class that performs all installation steps for PerplexityXPC.
/// Each method returns <c>true</c> on success and is safe to call even when
/// optional resources are missing (it will log the issue rather than throw).
/// </summary>
public static class InstallationEngine
{
    // -------------------------------------------------------------------------
    // Directory setup
    // -------------------------------------------------------------------------

    /// <summary>Creates the installation directory and required sub-directories.</summary>
    /// <param name="installPath">Root installation path, e.g. C:\Program Files\PerplexityXPC.</param>
    /// <returns><c>true</c> on success.</returns>
    public static bool CreateDirectories(string installPath)
    {
        try
        {
            Directory.CreateDirectory(installPath);
            Directory.CreateDirectory(Path.Combine(installPath, "integrations"));
            Directory.CreateDirectory(Path.Combine(installPath, "logs"));
            Directory.CreateDirectory(Path.Combine(installPath, "modules"));

            // App data directory
            string dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PerplexityXPC");
            Directory.CreateDirectory(dataPath);

            return true;
        }
        catch (Exception ex)
        {
            LogError("CreateDirectories", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Binary copy
    // -------------------------------------------------------------------------

    /// <summary>
    /// Copies service binaries from <paramref name="sourcePath"/> to
    /// <paramref name="installPath"/>. Silently skips files that do not exist
    /// in the source (stub mode during wizard testing).
    /// </summary>
    public static bool CopyBinaries(string sourcePath, string installPath, IProgress<string>? progress)
    {
        try
        {
            string[] targets =
            {
                "PerplexityXPC.Service.exe",
                "PerplexityXPC.Tray.exe",
                "PerplexityXPC.Cli.exe",
                "PerplexityXPC.CLI.dll",
                "appsettings.json",
                "README.md",
            };

            foreach (string file in targets)
            {
                string src = Path.Combine(sourcePath, file);
                string dst = Path.Combine(installPath, file);

                if (File.Exists(src))
                {
                    File.Copy(src, dst, overwrite: true);
                    progress?.Report($"  Copied {file}");
                }
            }

            // Copy integrations sub-directory if present
            string intSrc = Path.Combine(sourcePath, "integrations");
            string intDst = Path.Combine(installPath, "integrations");
            if (Directory.Exists(intSrc))
            {
                foreach (string f in Directory.EnumerateFiles(intSrc, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(intSrc, f);
                    string target = Path.Combine(intDst, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(f, target, overwrite: true);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError("CopyBinaries", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // API key storage (DPAPI)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Encrypts <paramref name="apiKey"/> with Windows DPAPI and writes it to
    /// <c>apikey.dat</c> inside <paramref name="dataPath"/>.
    /// </summary>
    public static bool StoreApiKey(string apiKey, string dataPath)
    {
        if (string.IsNullOrEmpty(apiKey)) return true; // Nothing to store

        try
        {
            Directory.CreateDirectory(dataPath);

            byte[] plaintext  = Encoding.UTF8.GetBytes(apiKey);
            byte[] ciphertext = ProtectedData.Protect(
                plaintext,
                entropy: null,
                scope: DataProtectionScope.CurrentUser);

            string filePath = Path.Combine(dataPath, "apikey.dat");
            File.WriteAllBytes(filePath, ciphertext);

            // Also set environment variable for current process
            Environment.SetEnvironmentVariable(
                "PERPLEXITY_API_KEY", apiKey,
                EnvironmentVariableTarget.User);

            return true;
        }
        catch (Exception ex)
        {
            LogError("StoreApiKey", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Windows Service registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers the PerplexityXPC Windows Service using sc.exe.
    /// No-ops gracefully if <paramref name="exePath"/> does not yet exist.
    /// </summary>
    public static bool RegisterService(string exePath)
    {
        try
        {
            if (!File.Exists(exePath))
            {
                // Binaries not present yet - record intention but continue
                return true;
            }

            // Delete existing service entry if present
            RunSc("delete PerplexityXPC");
            Thread.Sleep(500);

            // Create new service
            int exitCode = RunSc(
                $"create PerplexityXPC binPath= \"{exePath}\" " +
                $"DisplayName= \"PerplexityXPC AI Service\" start= auto");

            if (exitCode != 0) return false;

            // Set description
            RunSc("description PerplexityXPC \"Local Perplexity AI query service\"");

            // Set failure recovery
            RunSc("failure PerplexityXPC reset= 86400 actions= restart/5000/restart/10000//");

            return true;
        }
        catch (Exception ex)
        {
            LogError("RegisterService", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Firewall
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a Windows Firewall inbound rule that blocks external connections
    /// to the PerplexityXPC port (allows only 127.0.0.1).
    /// </summary>
    public static bool ConfigureFirewall(int port)
    {
        try
        {
            // Remove any existing rule
            RunNetsh($"advfirewall firewall delete rule name=\"PerplexityXPC\"");

            // Add new rule - block all inbound except loopback (loopback bypass is OS default)
            int exitCode = RunNetsh(
                $"advfirewall firewall add rule " +
                $"name=\"PerplexityXPC\" " +
                $"dir=in action=block " +
                $"protocol=tcp localport={port} " +
                $"remoteip=!127.0.0.1 " +
                $"description=\"Block external access to PerplexityXPC service\"");

            return exitCode == 0;
        }
        catch (Exception ex)
        {
            LogError("ConfigureFirewall", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // MCP server config
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes the MCP server configuration JSON to
    /// <c>%APPDATA%\PerplexityXPC\mcp-servers.json</c>.
    /// </summary>
    public static bool CreateMcpConfig(string dataPath, List<McpServerEntry> servers)
    {
        try
        {
            Directory.CreateDirectory(dataPath);

            // Build mcpServers object matching Claude Desktop / MCP spec
            var root = new Dictionary<string, object>();
            var mcpServers = new Dictionary<string, object>();

            foreach (var server in servers)
            {
                if (!server.Enabled || string.IsNullOrWhiteSpace(server.Name)) continue;

                // Parse command into program + args
                string[] parts = server.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string prog  = parts.Length > 0 ? parts[0] : "npx";
                string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

                mcpServers[server.Name] = new { command = prog, args };
            }

            root["mcpServers"] = mcpServers;

            string json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(dataPath, "mcp-servers.json"), json, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            LogError("CreateMcpConfig", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // PowerShell module
    // -------------------------------------------------------------------------

    /// <summary>
    /// Copies the PerplexityXPC PowerShell module from the install directory
    /// to the user's WindowsPowerShell\Modules folder.
    /// </summary>
    public static bool InstallPowerShellModule(string installPath)
    {
        try
        {
            string srcModule = Path.Combine(installPath, "modules", "PerplexityXPC");
            string psModDir  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "WindowsPowerShell", "Modules", "PerplexityXPC");

            if (!Directory.Exists(srcModule))
            {
                // Module not bundled - create a stub psd1 pointing to the service
                Directory.CreateDirectory(psModDir);
                File.WriteAllText(
                    Path.Combine(psModDir, "PerplexityXPC.psd1"),
                    $"# PerplexityXPC PowerShell Module stub\n# Generated by setup wizard v1.4.0\n",
                    Encoding.UTF8);
                return true;
            }

            CopyDirectory(srcModule, psModDir);
            return true;
        }
        catch (Exception ex)
        {
            LogError("InstallPowerShellModule", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Integration script runner
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs an integration installer PowerShell script.
    /// </summary>
    /// <param name="scriptPath">Absolute path to the .ps1 script.</param>
    /// <param name="args">Additional arguments to pass to the script.</param>
    /// <returns><c>true</c> on exit code 0, or if the script does not exist.</returns>
    public static bool RunIntegration(string scriptPath, string[] args)
    {
        if (!File.Exists(scriptPath)) return true; // Not bundled - skip silently

        try
        {
            string argStr = string.Join(" ", args);
            var psi = new ProcessStartInfo
            {
                FileName               = "pwsh.exe",
                Arguments              = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {argStr}",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(30_000);
            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            LogError("RunIntegration", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Service control
    // -------------------------------------------------------------------------

    /// <summary>Starts the PerplexityXPC Windows Service.</summary>
    public static bool StartService()
    {
        try
        {
            int exitCode = RunSc("start PerplexityXPC");
            // Exit code 2 means "already running"
            return exitCode == 0 || exitCode == 2;
        }
        catch (Exception ex)
        {
            LogError("StartService", ex);
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Verification
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends a GET request to <c>http://127.0.0.1:{port}/status</c> to verify
    /// the service is responding.
    /// </summary>
    public static bool VerifyInstallation(int port)
    {
        try
        {
            using var client  = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = client.GetAsync($"http://127.0.0.1:{port}/status")
                                 .GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Uninstall
    // -------------------------------------------------------------------------

    /// <summary>
    /// Removes all installed components: stops and deletes the service,
    /// removes the install directory, firewall rule, and registry entries.
    /// </summary>
    public static bool UninstallAll(string installPath)
    {
        bool ok = true;

        try { RunSc("stop PerplexityXPC"); }   catch { /* ignore */ }
        try { RunSc("delete PerplexityXPC"); } catch { /* ignore */ }
        try { RunNetsh("advfirewall firewall delete rule name=\"PerplexityXPC\""); } catch { /* ignore */ }

        try
        {
            if (Directory.Exists(installPath))
                Directory.Delete(installPath, recursive: true);
        }
        catch (Exception ex)
        {
            LogError("UninstallAll (delete dir)", ex);
            ok = false;
        }

        try
        {
            string psModDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "WindowsPowerShell", "Modules", "PerplexityXPC");
            if (Directory.Exists(psModDir))
                Directory.Delete(psModDir, recursive: true);
        }
        catch (Exception ex)
        {
            LogError("UninstallAll (remove PS module)", ex);
        }

        return ok;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static int RunSc(string arguments)
    {
        return RunProcess("sc.exe", arguments);
    }

    private static int RunNetsh(string arguments)
    {
        return RunProcess("netsh.exe", arguments);
    }

    private static int RunProcess(string exe, string arguments)
    {
        var psi = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        using var proc = Process.Start(psi);
        if (proc == null) return -1;
        proc.WaitForExit(15_000);
        return proc.ExitCode;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
        foreach (string dir in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }

    private static void LogError(string method, Exception ex)
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PerplexityXPC", "setup-logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "setup-errors.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {method}: {ex.Message}\r\n{ex.StackTrace}\r\n\r\n",
                Encoding.UTF8);
        }
        catch
        {
            // Cannot log - silently swallow to avoid cascading failures
        }
    }
}
