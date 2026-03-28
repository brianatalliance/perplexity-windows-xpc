using System.Collections.Generic;

namespace PerplexityXPC.Setup.Models;

/// <summary>
/// Holds all configuration choices collected during the setup wizard.
/// Passed by reference across all wizard pages via <see cref="SetupWizard.Config"/>.
/// </summary>
public sealed class SetupConfig
{
    // -------------------------------------------------------------------------
    // API
    // -------------------------------------------------------------------------

    /// <summary>The raw Perplexity API key entered by the user. May be empty if skipped.</summary>
    public string ApiKey { get; set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Service
    // -------------------------------------------------------------------------

    /// <summary>TCP port the PerplexityXPC service binds to. Default: 47777.</summary>
    public int Port { get; set; } = 47777;

    /// <summary>Absolute path to the installation directory.</summary>
    public string InstallPath { get; set; } = @"C:\Program Files\PerplexityXPC";

    // -------------------------------------------------------------------------
    // Startup options
    // -------------------------------------------------------------------------

    /// <summary>If true, registers the service to start automatically at boot.</summary>
    public bool AutoStartService { get; set; } = true;

    /// <summary>If true, adds the tray application to Windows startup.</summary>
    public bool AddToStartup { get; set; } = true;

    /// <summary>If true, creates a Windows Firewall rule to block external access.</summary>
    public bool AddFirewall { get; set; } = true;

    /// <summary>If true, registers the Explorer right-click context menu entries.</summary>
    public bool AddContextMenu { get; set; } = true;

    // -------------------------------------------------------------------------
    // MCP servers
    // -------------------------------------------------------------------------

    /// <summary>List of MCP server entries that the user has enabled.</summary>
    public List<McpServerEntry> McpServers { get; set; } = new();

    // -------------------------------------------------------------------------
    // Integrations
    // -------------------------------------------------------------------------

    /// <summary>If true, installs the PowerShell module to the Modules directory.</summary>
    public bool InstallModule { get; set; } = true;

    /// <summary>If true, appends aliases and helpers to the user's PowerShell profile.</summary>
    public bool InstallProfile { get; set; } = true;

    /// <summary>If true, adds a profile to Windows Terminal settings.json.</summary>
    public bool InstallTerminal { get; set; }

    /// <summary>If true, installs tasks and keybindings for VS Code.</summary>
    public bool InstallVSCode { get; set; }

    /// <summary>If true, sets up a Cloudflare Tunnel for remote access.</summary>
    public bool InstallRemote { get; set; }
}

/// <summary>Represents a single MCP server entry.</summary>
public sealed class McpServerEntry
{
    /// <summary>Whether this server is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Short identifier name (e.g., "filesystem").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full command line to launch the server.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Human-readable description shown in the grid.</summary>
    public string Description { get; set; } = string.Empty;
}
