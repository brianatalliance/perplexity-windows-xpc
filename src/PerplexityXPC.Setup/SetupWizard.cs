using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PerplexityXPC.Setup.Models;
using PerplexityXPC.Setup.Pages;

namespace PerplexityXPC.Setup;

/// <summary>
/// Main setup wizard form. Hosts all wizard pages and manages navigation.
/// </summary>
public sealed class SetupWizard : Form
{
    // -------------------------------------------------------------------------
    // Theme constants
    // -------------------------------------------------------------------------
    internal static readonly Color ColorBackground = Color.FromArgb(0x1a, 0x1a, 0x2e);
    internal static readonly Color ColorSurface     = Color.FromArgb(0x16, 0x21, 0x3e);
    internal static readonly Color ColorAccent      = Color.FromArgb(0x6c, 0x63, 0xff);
    internal static readonly Color ColorAccentDark  = Color.FromArgb(0x48, 0x34, 0xd4);
    internal static readonly Color ColorText        = Color.FromArgb(0xe0, 0xe0, 0xe0);
    internal static readonly Color ColorTextMuted   = Color.FromArgb(0x9a, 0x9a, 0xb4);
    internal static readonly Color ColorSuccess     = Color.FromArgb(0x4c, 0xaf, 0x50);
    internal static readonly Color ColorError       = Color.FromArgb(0xef, 0x53, 0x50);
    internal static readonly Color ColorWarning     = Color.FromArgb(0xff, 0xb7, 0x40);

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------
    private int _currentPageIndex;
    private readonly List<UserControl> _pages = new();

    /// <summary>Shared configuration object passed across all wizard pages.</summary>
    public SetupConfig Config { get; } = new();

    // -------------------------------------------------------------------------
    // Controls
    // -------------------------------------------------------------------------
    private Panel _bannerPanel = null!;
    private Label _bannerTitle = null!;
    private Label _bannerSubtitle = null!;
    private Panel _pageContainer = null!;
    private Panel _navPanel = null!;
    private Button _btnBack = null!;
    private Button _btnNext = null!;
    private Button _btnCancel = null!;
    private Panel _dotsPanel = null!;
    private readonly List<Label> _dots = new();

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------
    public SetupWizard()
    {
        InitializeComponent();
        BuildPages();
        ShowPage(0);
    }

    // -------------------------------------------------------------------------
    // Form setup
    // -------------------------------------------------------------------------
    private void InitializeComponent()
    {
        SuspendLayout();

        // Form properties
        Text                = "PerplexityXPC Setup";
        Size                = new Size(700, 520);
        MinimumSize         = new Size(700, 520);
        MaximumSize         = new Size(700, 520);
        FormBorderStyle     = FormBorderStyle.FixedDialog;
        StartPosition       = FormStartPosition.CenterScreen;
        MaximizeBox         = false;
        BackColor           = ColorBackground;
        ForeColor           = ColorText;
        Font                = new Font("Segoe UI", 9f, FontStyle.Regular);

        // --- Banner ---
        _bannerPanel = new Panel
        {
            Dock        = DockStyle.Top,
            Height      = 80,
            BackColor   = Color.Transparent,
        };
        _bannerPanel.Paint += BannerPanel_Paint;

        _bannerTitle = new Label
        {
            Text      = "PerplexityXPC",
            Font      = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoSize  = true,
            Location  = new Point(20, 12),
        };

        _bannerSubtitle = new Label
        {
            Text      = "Setup Wizard v1.4.0",
            Font      = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(200, 220, 220, 255),
            BackColor = Color.Transparent,
            AutoSize  = true,
            Location  = new Point(22, 46),
        };

        _bannerPanel.Controls.Add(_bannerTitle);
        _bannerPanel.Controls.Add(_bannerSubtitle);

        // --- Progress dots ---
        _dotsPanel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 28,
            BackColor = ColorSurface,
        };
        BuildDots();

        // --- Page container ---
        _pageContainer = new Panel
        {
            Dock        = DockStyle.Fill,
            BackColor   = ColorBackground,
            Padding     = new Padding(20, 10, 20, 10),
        };

        // --- Navigation bar ---
        _navPanel = new Panel
        {
            Dock        = DockStyle.Bottom,
            Height      = 56,
            BackColor   = ColorSurface,
            Padding     = new Padding(16, 10, 16, 10),
        };

        _btnBack = CreateNavButton("< Back");
        _btnBack.Click  += (_, _) => Navigate(-1);
        _btnBack.Location = new Point(16, 12);

        _btnCancel = CreateNavButton("Cancel");
        _btnCancel.Click += BtnCancel_Click;
        _btnCancel.Location = new Point(590, 12);

        _btnNext = CreateNavButton("Next >");
        _btnNext.Click += (_, _) => Navigate(1);
        _btnNext.BackColor = ColorAccent;
        _btnNext.ForeColor = Color.White;
        _btnNext.Location  = new Point(490, 12);

        _navPanel.Controls.Add(_btnBack);
        _navPanel.Controls.Add(_btnNext);
        _navPanel.Controls.Add(_btnCancel);

        // Add in Z-order (Bottom first so Fill works correctly)
        Controls.Add(_pageContainer);
        Controls.Add(_dotsPanel);
        Controls.Add(_bannerPanel);
        Controls.Add(_navPanel);

        ResumeLayout(false);
    }

    private void BuildDots()
    {
        int totalDots = 7;
        int dotSize   = 12;
        int gap       = 10;
        int totalW    = totalDots * dotSize + (totalDots - 1) * gap;
        int startX    = (700 - totalW) / 2;

        for (int i = 0; i < totalDots; i++)
        {
            var dot = new Label
            {
                Size      = new Size(dotSize, dotSize),
                Location  = new Point(startX + i * (dotSize + gap), (_dotsPanel.Height - dotSize) / 2),
                BackColor = ColorTextMuted,
                Text      = string.Empty,
                Tag       = i,
            };
            dot.Paint += Dot_Paint;
            _dots.Add(dot);
            _dotsPanel.Controls.Add(dot);
        }
    }

    private static void Dot_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Label lbl) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(lbl.BackColor);
        e.Graphics.FillEllipse(brush, 0, 0, lbl.Width - 1, lbl.Height - 1);
    }

    private static void BannerPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel) return;
        using var brush = new LinearGradientBrush(
            panel.ClientRectangle,
            Color.FromArgb(0x6c, 0x63, 0xff),
            Color.FromArgb(0x48, 0x34, 0xd4),
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, panel.ClientRectangle);
    }

    private static Button CreateNavButton(string text)
    {
        return new Button
        {
            Text        = text,
            Size        = new Size(88, 34),
            FlatStyle   = FlatStyle.Flat,
            BackColor   = Color.FromArgb(0x2a, 0x2a, 0x4e),
            ForeColor   = ColorText,
            Font        = new Font("Segoe UI", 9f, FontStyle.Regular),
            Cursor      = Cursors.Hand,
            FlatAppearance = { BorderColor = ColorAccent, BorderSize = 1 },
        };
    }

    // -------------------------------------------------------------------------
    // Page management
    // -------------------------------------------------------------------------
    private void BuildPages()
    {
        _pages.Add(new WelcomePage(this));
        _pages.Add(new ApiKeyPage(this));
        _pages.Add(new ConfigPage(this));
        _pages.Add(new McpServersPage(this));
        _pages.Add(new IntegrationsPage(this));
        _pages.Add(new InstallPage(this));
        _pages.Add(new CompletePage(this));
    }

    private void ShowPage(int index)
    {
        _currentPageIndex = index;

        // Swap page
        _pageContainer.Controls.Clear();
        var page = _pages[index];
        page.Dock = DockStyle.Fill;
        _pageContainer.Controls.Add(page);

        // Notify page it is being shown
        if (page is IWizardPage wp) wp.OnPageShown();

        // Update dots
        for (int i = 0; i < _dots.Count; i++)
        {
            _dots[i].BackColor = (i == index) ? ColorAccent : ColorTextMuted;
            _dots[i].Invalidate();
        }

        // Button states
        _btnBack.Enabled = index > 0 && index < _pages.Count - 1;

        bool isInstallPage = index == _pages.Count - 2;
        bool isCompletePage = index == _pages.Count - 1;

        if (isCompletePage)
        {
            _btnNext.Text     = "Finish";
            _btnNext.Enabled  = true;
            _btnCancel.Visible = false;
            _btnBack.Visible   = false;
        }
        else if (isInstallPage)
        {
            _btnNext.Text    = "Install";
            _btnNext.Enabled = true;
        }
        else
        {
            _btnNext.Text    = "Next >";
            _btnNext.Enabled = true;
            _btnCancel.Visible = true;
            _btnBack.Visible   = true;
        }
    }

    private void Navigate(int delta)
    {
        int target = _currentPageIndex + delta;

        if (delta > 0)
        {
            // Complete page: close
            if (_currentPageIndex == _pages.Count - 1)
            {
                Close();
                return;
            }

            // Validate before advancing
            if (_pages[_currentPageIndex] is IWizardPage wp && !wp.Validate())
                return;
        }

        if (target < 0 || target >= _pages.Count) return;
        ShowPage(target);
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to cancel the installation?",
            "Cancel Setup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
            Close();
    }

    // -------------------------------------------------------------------------
    // Public API for pages
    // -------------------------------------------------------------------------
    /// <summary>Advances the wizard to the next page programmatically.</summary>
    public void GoNext() => Navigate(1);

    /// <summary>Sets the Next button enabled state.</summary>
    public void SetNextEnabled(bool enabled) => _btnNext.Enabled = enabled;

    /// <summary>Updates the banner subtitle text.</summary>
    public void SetSubtitle(string text) => _bannerSubtitle.Text = text;
}

/// <summary>Interface implemented by wizard pages that support validation and activation.</summary>
public interface IWizardPage
{
    /// <summary>Called when this page becomes visible. Use for initialization.</summary>
    void OnPageShown();

    /// <summary>Returns true if the page is valid and the wizard may advance.</summary>
    new bool Validate();
}
