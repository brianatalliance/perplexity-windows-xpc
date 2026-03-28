using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PerplexityXPC.Setup.Pages;

/// <summary>
/// Page 5: Choose which optional integrations to install.
/// </summary>
public sealed class IntegrationsPage : UserControl, IWizardPage
{
    private readonly SetupWizard _wizard;
    private readonly List<IntegrationRow> _rows = new();

    // -------------------------------------------------------------------------
    // Integration definitions
    // -------------------------------------------------------------------------
    private static readonly IntegrationDef[] IntegrationDefs =
    {
        new("module",   true,
            "PowerShell Module",
            "Install the PerplexityXPC PowerShell module with 44 AI-powered functions " +
            "and aliases. Adds commands like Invoke-PerplexityQuery, Get-CodeReview, " +
            "Get-SecurityAudit, and more to every PowerShell session."),
        new("profile",  true,
            "PowerShell Profile",
            "Append short aliases (pplx, pplxcode, pplxsec, etc.) and helper functions " +
            "to your PowerShell profile so they are always available without importing " +
            "the module manually."),
        new("terminal", false,
            "Windows Terminal Profile",
            "Add a dedicated PerplexityXPC tab profile to Windows Terminal with custom " +
            "colors, icon, and keyboard shortcuts for quick access to the CLI."),
        new("vscode",   false,
            "VS Code Integration",
            "Install tasks.json and keybindings for code review (Ctrl+Shift+R), " +
            "security audit (Ctrl+Shift+S), debug assist, and quick-query from the " +
            "editor. Works with the VS Code CLI (code)."),
        new("context",  true,
            "Explorer Context Menu",
            "Adds right-click \"Ask Perplexity\" and \"Explain with Perplexity\" entries " +
            "to Windows Explorer for files and folders. Requires admin privileges."),
        new("cloudflare", false,
            "Cloudflare Tunnel (Remote Access)",
            "Advanced: Set up a Cloudflare Tunnel so you can query PerplexityXPC from " +
            "your phone or other devices outside your home network. Requires a free " +
            "Cloudflare account and cloudflared installed."),
    };

    /// <summary>Initializes a new instance of <see cref="IntegrationsPage"/>.</summary>
    public IntegrationsPage(SetupWizard wizard)
    {
        _wizard   = wizard;
        BackColor = SetupWizard.ColorBackground;
        ForeColor = SetupWizard.ColorText;
        BuildLayout();
    }

    private void BuildLayout()
    {
        SuspendLayout();
        AutoScroll = true;

        // Title
        var title = new Label
        {
            Text      = "Integrations",
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorAccent,
            AutoSize  = true,
            Location  = new Point(0, 0),
        };

        // Instruction
        var desc = new Label
        {
            Text      = "Choose which integrations to install. You can add or remove them later by re-running setup.",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 38),
        };

        Controls.Add(title);
        Controls.Add(desc);

        // Integration rows
        int y = 68;
        foreach (var def in IntegrationDefs)
        {
            var row = new IntegrationRow(def);
            row.Location = new Point(0, y);
            row.Width    = 630;
            Controls.Add(row);
            _rows.Add(row);
            y += row.CollapsedHeight + 4;
            row.HeightChanged += (_, newH) => ReflowRows();
        }

        ResumeLayout(false);
    }

    private void ReflowRows()
    {
        int y = 68;
        foreach (var row in _rows)
        {
            row.Location = new Point(0, y);
            y += row.Height + 4;
        }
    }

    // -------------------------------------------------------------------------
    // IWizardPage
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void OnPageShown()
    {
        _wizard.SetSubtitle("Step 5 of 7 - Integrations");
    }

    /// <inheritdoc/>
    public new bool Validate()
    {
        foreach (var row in _rows)
        {
            switch (row.IntegrationId)
            {
                case "module":   _wizard.Config.InstallModule   = row.IsChecked; break;
                case "profile":  _wizard.Config.InstallProfile  = row.IsChecked; break;
                case "terminal": _wizard.Config.InstallTerminal = row.IsChecked; break;
                case "vscode":   _wizard.Config.InstallVSCode   = row.IsChecked; break;
                case "context":  _wizard.Config.AddContextMenu  = row.IsChecked; break;
                case "cloudflare": _wizard.Config.InstallRemote = row.IsChecked; break;
            }
        }
        return true;
    }
}

// =============================================================================
// Supporting types
// =============================================================================

/// <summary>Definition record for a single integration option.</summary>
internal sealed record IntegrationDef(
    string Id,
    bool DefaultChecked,
    string Title,
    string Description);

/// <summary>A collapsible row showing one integration with a checkbox and expand arrow.</summary>
internal sealed class IntegrationRow : Panel
{
    public string IntegrationId  { get; }
    public bool   IsChecked      => _chk.Checked;
    public int    CollapsedHeight => 32;

    public event EventHandler<int>? HeightChanged;

    private readonly CheckBox _chk;
    private readonly Button   _btnExpand;
    private readonly Label    _lblDesc;
    private bool _expanded;

    public IntegrationRow(IntegrationDef def)
    {
        IntegrationId = def.Id;
        BackColor     = SetupWizard.ColorSurface;
        Height        = CollapsedHeight;

        _chk = new CheckBox
        {
            Text      = def.Title,
            Checked   = def.DefaultChecked,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(8, 7),
        };

        _btnExpand = new Button
        {
            Text      = "+",
            Size      = new Size(24, 18),
            Location  = new Point(598, 7),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = SetupWizard.ColorTextMuted,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
        };
        _btnExpand.FlatAppearance.BorderSize = 0;
        _btnExpand.Click += (_, _) => ToggleExpand();

        _lblDesc = new Label
        {
            Text      = def.Description,
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = false,
            Location  = new Point(24, 36),
            Size      = new Size(580, 0),
            Visible   = false,
        };

        Controls.Add(_chk);
        Controls.Add(_btnExpand);
        Controls.Add(_lblDesc);
    }

    private void ToggleExpand()
    {
        _expanded = !_expanded;
        _btnExpand.Text = _expanded ? "-" : "+";

        if (_expanded)
        {
            // Measure required height for label
            SizeF sz = TextRenderer.MeasureText(
                _lblDesc.Text,
                _lblDesc.Font,
                new Size(_lblDesc.Width, int.MaxValue),
                TextFormatFlags.WordBreak);

            _lblDesc.Height  = (int)sz.Height + 8;
            _lblDesc.Visible = true;
            Height           = CollapsedHeight + _lblDesc.Height + 8;
        }
        else
        {
            _lblDesc.Visible = false;
            Height           = CollapsedHeight;
        }

        HeightChanged?.Invoke(this, Height);
    }
}
