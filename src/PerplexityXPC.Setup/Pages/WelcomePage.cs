using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PerplexityXPC.Setup.Helpers;

namespace PerplexityXPC.Setup.Pages;

/// <summary>
/// Page 1: Welcome screen with introduction text and prerequisite checks.
/// </summary>
public sealed class WelcomePage : UserControl, IWizardPage
{
    private readonly SetupWizard _wizard;
    private Panel _prereqPanel = null!;
    private Label _statusLabel = null!;

    /// <summary>Initializes a new instance of <see cref="WelcomePage"/>.</summary>
    public WelcomePage(SetupWizard wizard)
    {
        _wizard = wizard;
        BackColor = SetupWizard.ColorBackground;
        ForeColor = SetupWizard.ColorText;
        BuildLayout();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        // Title
        var title = new Label
        {
            Text      = "Welcome to PerplexityXPC",
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorAccent,
            AutoSize  = true,
            Location  = new Point(0, 0),
        };

        // Description
        var desc = new Label
        {
            Text = "This wizard will install the PerplexityXPC service on your machine.\n\n" +
                   "What gets installed:\n" +
                   "  - PerplexityXPC Windows Service (HTTP API on localhost)\n" +
                   "  - System tray application\n" +
                   "  - PowerShell module with 44 AI-powered functions\n" +
                   "  - Optional: VS Code integration, Windows Terminal profile\n" +
                   "  - Optional: Explorer context menu (right-click \"Ask Perplexity\")\n" +
                   "  - Optional: MCP server connections for local tool access\n\n" +
                   "Click Next to continue or review the prerequisites below.",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = false,
            Size      = new Size(620, 155),
            Location  = new Point(0, 36),
        };

        // Prerequisites header
        var prereqHeader = new Label
        {
            Text      = "Prerequisites",
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 200),
        };

        // Prerequisites panel (populated in OnPageShown)
        _prereqPanel = new Panel
        {
            Location  = new Point(0, 222),
            Size      = new Size(630, 80),
            BackColor = SetupWizard.ColorSurface,
        };

        // Status label
        _statusLabel = new Label
        {
            Text      = "Checking prerequisites...",
            Font      = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = true,
            Location  = new Point(0, 312),
        };

        Controls.Add(title);
        Controls.Add(desc);
        Controls.Add(prereqHeader);
        Controls.Add(_prereqPanel);
        Controls.Add(_statusLabel);

        ResumeLayout(false);
    }

    /// <inheritdoc/>
    public void OnPageShown()
    {
        _wizard.SetSubtitle("Step 1 of 7 - Welcome");
        RunPrerequisiteChecks();
    }

    private void RunPrerequisiteChecks()
    {
        _prereqPanel.Controls.Clear();
        _statusLabel.Text = "Checking prerequisites...";

        // Run checks on background thread, update UI on UI thread
        System.Threading.Tasks.Task.Run(() =>
        {
            var result = PrerequisiteChecker.CheckAll();
            return result;
        }).ContinueWith(t =>
        {
            if (t.IsFaulted) return;
            var result = t.Result;
            RenderPrereqResults(result);
        }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void RenderPrereqResults(PrerequisiteResult result)
    {
        _prereqPanel.Controls.Clear();

        var checks = new List<(string Label, bool Ok, string Detail, string? DownloadUrl)>
        {
            (".NET 8 Runtime", result.DotNetFound, result.DotNetVersion, "https://dotnet.microsoft.com/download/dotnet/8.0"),
            ("Node.js 20+",    result.NodeFound,   result.NodeVersion,   "https://nodejs.org/"),
            ("PowerShell 5.1+", result.PsFound,    result.PsVersion,     null),
            ("Windows 10 1809+", result.WindowsOk, result.WindowsBuild,  null),
        };

        int y = 8;
        bool allOk = true;

        foreach (var (label, ok, detail, url) in checks)
        {
            if (!ok) allOk = false;

            // Icon
            var icon = new Label
            {
                Text      = ok ? "\u2714" : "\u2718",
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ok ? SetupWizard.ColorSuccess : SetupWizard.ColorError,
                AutoSize  = true,
                Location  = new Point(10, y),
            };

            // Label
            var lbl = new Label
            {
                Text      = label,
                Font      = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = SetupWizard.ColorText,
                AutoSize  = true,
                Location  = new Point(34, y + 2),
            };

            // Detail
            var det = new Label
            {
                Text      = detail,
                Font      = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = ok ? SetupWizard.ColorTextMuted : SetupWizard.ColorError,
                AutoSize  = true,
                Location  = new Point(180, y + 2),
            };

            _prereqPanel.Controls.Add(icon);
            _prereqPanel.Controls.Add(lbl);
            _prereqPanel.Controls.Add(det);

            // Download link if needed
            if (!ok && url != null)
            {
                var link = new LinkLabel
                {
                    Text      = "Download",
                    Font      = new Font("Segoe UI", 9f),
                    LinkColor = SetupWizard.ColorAccent,
                    AutoSize  = true,
                    Location  = new Point(380, y + 2),
                    Tag       = url,
                };
                link.LinkClicked += (_, _) =>
                {
                    if (link.Tag is string u)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u) { UseShellExecute = true });
                };
                _prereqPanel.Controls.Add(link);
            }

            y += 18;
        }

        if (allOk)
        {
            _statusLabel.Text      = "All prerequisites met. You are ready to install.";
            _statusLabel.ForeColor = SetupWizard.ColorSuccess;
        }
        else
        {
            _statusLabel.Text      = "Some prerequisites are missing. You can continue, but some features may not work.";
            _statusLabel.ForeColor = SetupWizard.ColorWarning;
        }
    }

    /// <inheritdoc/>
    public new bool Validate() => true; // Welcome page is always valid
}
