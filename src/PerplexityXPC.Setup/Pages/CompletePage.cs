using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PerplexityXPC.Setup.Helpers;

namespace PerplexityXPC.Setup.Pages;

/// <summary>
/// Page 7: Installation complete summary with quick-start information.
/// </summary>
public sealed class CompletePage : UserControl, IWizardPage
{
    private readonly SetupWizard _wizard;
    private Label  _lblServiceStatus = null!;
    private RichTextBox _rtbSummary  = null!;
    private RichTextBox _rtbCommands = null!;

    /// <summary>Initializes a new instance of <see cref="CompletePage"/>.</summary>
    public CompletePage(SetupWizard wizard)
    {
        _wizard   = wizard;
        BackColor = SetupWizard.ColorBackground;
        ForeColor = SetupWizard.ColorText;
        BuildLayout();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        // Big checkmark
        var iconLabel = new Label
        {
            Text      = "\u2714",
            Font      = new Font("Segoe UI", 36f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorSuccess,
            AutoSize  = true,
            Location  = new Point(0, 0),
        };

        // Headline
        var headline = new Label
        {
            Text      = "PerplexityXPC is installed and running!",
            Font      = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(58, 8),
        };

        // Service status line
        _lblServiceStatus = new Label
        {
            Text      = "Service: Checking...",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = true,
            Location  = new Point(58, 36),
        };

        // Summary box
        var lblSummary = new Label
        {
            Text      = "Installation Summary",
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 66),
        };

        _rtbSummary = new RichTextBox
        {
            Location   = new Point(0, 88),
            Size       = new Size(630, 90),
            ReadOnly   = true,
            BackColor  = SetupWizard.ColorSurface,
            ForeColor  = SetupWizard.ColorText,
            BorderStyle = BorderStyle.None,
            Font       = new Font("Consolas", 9f),
            ScrollBars = RichTextBoxScrollBars.None,
            WordWrap   = false,
        };

        // Quick-start commands
        var lblCmd = new Label
        {
            Text      = "Quick Start",
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 188),
        };

        _rtbCommands = new RichTextBox
        {
            Location    = new Point(0, 210),
            Size        = new Size(630, 72),
            ReadOnly    = true,
            BackColor   = SetupWizard.ColorSurface,
            ForeColor   = SetupWizard.ColorText,
            BorderStyle = BorderStyle.None,
            Font        = new Font("Consolas", 9f),
            ScrollBars  = RichTextBoxScrollBars.None,
        };
        _rtbCommands.Text =
            "  Press Ctrl+Alt+P    - Open the PerplexityXPC query popup\r\n" +
            "  pplx \"question\"      - Query Perplexity from PowerShell\r\n" +
            "  xpc -h               - Full command reference";

        // Action buttons
        var btnPs = CreateActionButton("Open PowerShell", 292);
        btnPs.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("pwsh.exe")
            {
                UseShellExecute = true,
            });

        var btnDocs = CreateActionButton("Open Documentation", 414);
        btnDocs.Click += (_, _) =>
        {
            string readmePath = Path.Combine(_wizard.Config.InstallPath, "README.md");
            if (File.Exists(readmePath))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", readmePath)
                    { UseShellExecute = true });
            else
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://github.com/perplexity/perplexityxpc") { UseShellExecute = true });
        };

        Controls.Add(iconLabel);
        Controls.Add(headline);
        Controls.Add(_lblServiceStatus);
        Controls.Add(lblSummary);
        Controls.Add(_rtbSummary);
        Controls.Add(lblCmd);
        Controls.Add(_rtbCommands);
        Controls.Add(btnPs);
        Controls.Add(btnDocs);

        ResumeLayout(false);
    }

    private static Button CreateActionButton(string text, int x) => new()
    {
        Text      = text,
        Location  = new Point(x, 290),
        Size      = new Size(138, 30),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(0x2a, 0x2a, 0x4e),
        ForeColor = SetupWizard.ColorText,
        Cursor    = Cursors.Hand,
        Font      = new Font("Segoe UI", 9f),
        FlatAppearance = { BorderColor = SetupWizard.ColorAccent, BorderSize = 1 },
    };

    // -------------------------------------------------------------------------
    // IWizardPage
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void OnPageShown()
    {
        _wizard.SetSubtitle("Setup Complete");
        PopulateSummary();
        CheckServiceStatus();
    }

    private void PopulateSummary()
    {
        var cfg = _wizard.Config;

        var integrations = new List<string>();
        if (cfg.InstallModule)   integrations.Add("PowerShell Module");
        if (cfg.InstallProfile)  integrations.Add("PS Profile");
        if (cfg.InstallTerminal) integrations.Add("Windows Terminal");
        if (cfg.InstallVSCode)   integrations.Add("VS Code");
        if (cfg.AddContextMenu)  integrations.Add("Context Menu");
        if (cfg.InstallRemote)   integrations.Add("Cloudflare Tunnel");

        string integrStr = integrations.Count > 0
            ? string.Join(", ", integrations)
            : "None";

        _rtbSummary.Text =
            $"  Service      : http://127.0.0.1:{cfg.Port}\r\n" +
            $"  Install Path : {cfg.InstallPath}\r\n" +
            $"  API Key      : {(string.IsNullOrEmpty(cfg.ApiKey) ? "Not configured" : "Configured")}\r\n" +
            $"  MCP Servers  : {cfg.McpServers.Count} configured\r\n" +
            $"  Integrations : {integrStr}";
    }

    private void CheckServiceStatus()
    {
        int port = _wizard.Config.Port;
        _lblServiceStatus.Text      = "Service: Checking...";
        _lblServiceStatus.ForeColor = SetupWizard.ColorTextMuted;

        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(800);
            bool running = InstallationEngine.VerifyInstallation(port);
            Invoke(() =>
            {
                _lblServiceStatus.Text = running
                    ? $"Service: Running  (http://127.0.0.1:{port})"
                    : "Service: Not responding yet (it may take a moment to start)";
                _lblServiceStatus.ForeColor = running
                    ? SetupWizard.ColorSuccess
                    : SetupWizard.ColorWarning;
            });
        });
    }

    /// <inheritdoc/>
    public new bool Validate() => true;
}
