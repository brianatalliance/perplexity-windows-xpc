using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PerplexityXPC.Setup.Pages;

/// <summary>
/// Page 3: Service configuration - port, install path, and startup options.
/// </summary>
public sealed class ConfigPage : UserControl, IWizardPage
{
    private readonly SetupWizard _wizard;

    private NumericUpDown _nudPort     = null!;
    private TextBox       _txtPath     = null!;
    private Button        _btnBrowse   = null!;
    private CheckBox      _chkAutoStart    = null!;
    private CheckBox      _chkTrayStartup  = null!;
    private CheckBox      _chkFirewall     = null!;
    private CheckBox      _chkContextMenu  = null!;

    /// <summary>Initializes a new instance of <see cref="ConfigPage"/>.</summary>
    public ConfigPage(SetupWizard wizard)
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
            Text      = "Configuration",
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorAccent,
            AutoSize  = true,
            Location  = new Point(0, 0),
        };

        // --- Port ---
        var lblPort = MakeLabel("Service Port:", 42);
        _nudPort = new NumericUpDown
        {
            Location  = new Point(140, 40),
            Size      = new Size(100, 26),
            Minimum   = 1024,
            Maximum   = 65535,
            Value     = 47777,
            BackColor = SetupWizard.ColorSurface,
            ForeColor = SetupWizard.ColorText,
            Font      = new Font("Segoe UI", 10f),
        };
        var lblPortHint = new Label
        {
            Text      = "The local port the PerplexityXPC service listens on (default: 47777).",
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = true,
            Location  = new Point(248, 44),
        };

        // --- Install path ---
        var lblPath = MakeLabel("Install Path:", 82);
        _txtPath = new TextBox
        {
            Location    = new Point(140, 80),
            Size        = new Size(380, 26),
            Text        = @"C:\Program Files\PerplexityXPC",
            BackColor   = SetupWizard.ColorSurface,
            ForeColor   = SetupWizard.ColorText,
            Font        = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle,
        };

        _btnBrowse = new Button
        {
            Text      = "Browse...",
            Location  = new Point(528, 80),
            Size      = new Size(78, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0x2a, 0x2a, 0x4e),
            ForeColor = SetupWizard.ColorText,
            Cursor    = Cursors.Hand,
        };
        _btnBrowse.FlatAppearance.BorderColor = SetupWizard.ColorAccent;
        _btnBrowse.Click += BtnBrowse_Click;

        // --- Separator ---
        var sep = new Label
        {
            Text      = "Options",
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 118),
        };
        var line = new Panel
        {
            Location  = new Point(0, 138),
            Size      = new Size(630, 1),
            BackColor = SetupWizard.ColorTextMuted,
        };

        // --- Checkboxes ---
        _chkAutoStart = MakeCheckBox(
            "Start service automatically at boot (installs as Windows Service)",
            148, true);
        _chkTrayStartup = MakeCheckBox(
            "Add system tray app to Windows startup",
            174, true);
        _chkFirewall = MakeCheckBox(
            "Add Windows Firewall rule (blocks external access - recommended)",
            200, true);
        _chkContextMenu = MakeCheckBox(
            "Register Explorer context menus (right-click \"Ask Perplexity\")",
            226, true);

        Controls.Add(title);
        Controls.Add(lblPort);
        Controls.Add(_nudPort);
        Controls.Add(lblPortHint);
        Controls.Add(lblPath);
        Controls.Add(_txtPath);
        Controls.Add(_btnBrowse);
        Controls.Add(sep);
        Controls.Add(line);
        Controls.Add(_chkAutoStart);
        Controls.Add(_chkTrayStartup);
        Controls.Add(_chkFirewall);
        Controls.Add(_chkContextMenu);

        ResumeLayout(false);
    }

    private static Label MakeLabel(string text, int y) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 9.5f),
        ForeColor = SetupWizard.ColorText,
        AutoSize  = true,
        Location  = new Point(0, y + 3),
    };

    private static CheckBox MakeCheckBox(string text, int y, bool isChecked) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 9.5f),
        ForeColor = SetupWizard.ColorText,
        AutoSize  = true,
        Checked   = isChecked,
        Location  = new Point(0, y),
    };

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description         = "Select installation folder",
            SelectedPath        = _txtPath.Text,
            ShowNewFolderButton = true,
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtPath.Text = dlg.SelectedPath;
    }

    // -------------------------------------------------------------------------
    // IWizardPage
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void OnPageShown()
    {
        _wizard.SetSubtitle("Step 3 of 7 - Configuration");
    }

    /// <inheritdoc/>
    public new bool Validate()
    {
        string path = _txtPath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show("Please enter an installation path.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Check drive exists
        try
        {
            string? root = Path.GetPathRoot(path);
            if (root == null || !Directory.Exists(root))
            {
                MessageBox.Show("The drive for the install path does not exist.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }
        catch
        {
            MessageBox.Show("The install path is invalid.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Persist to config
        _wizard.Config.Port            = (int)_nudPort.Value;
        _wizard.Config.InstallPath     = path;
        _wizard.Config.AutoStartService = _chkAutoStart.Checked;
        _wizard.Config.AddToStartup    = _chkTrayStartup.Checked;
        _wizard.Config.AddFirewall     = _chkFirewall.Checked;
        _wizard.Config.AddContextMenu  = _chkContextMenu.Checked;

        return true;
    }
}
