using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows.Forms;

namespace PerplexityXPC.Setup.Pages;

/// <summary>
/// Page 2: Enter and validate the Perplexity API key.
/// </summary>
public sealed class ApiKeyPage : UserControl, IWizardPage
{
    private readonly SetupWizard _wizard;

    private TextBox _txtApiKey    = null!;
    private Button  _btnShowHide  = null!;
    private Button  _btnTest      = null!;
    private Label   _lblStatus    = null!;
    private CheckBox _chkSkip     = null!;
    private bool _keyValidated;

    /// <summary>Initializes a new instance of <see cref="ApiKeyPage"/>.</summary>
    public ApiKeyPage(SetupWizard wizard)
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
            Text      = "Perplexity API Key",
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorAccent,
            AutoSize  = true,
            Location  = new Point(0, 0),
        };

        // Instruction
        var instr = new Label
        {
            Text      = "Enter your Perplexity API key below. The key is used to authenticate\nall queries sent to the Perplexity AI service.",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 38),
        };

        // Link to get key
        var link = new LinkLabel
        {
            Text      = "Get your key at perplexity.ai/settings/api",
            Font      = new Font("Segoe UI", 9.5f),
            LinkColor = SetupWizard.ColorAccent,
            AutoSize  = true,
            Location  = new Point(0, 74),
        };
        link.LinkClicked += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://perplexity.ai/settings/api") { UseShellExecute = true });

        // API Key label
        var lblKey = new Label
        {
            Text      = "API Key:",
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 108),
        };

        // API Key text box
        _txtApiKey = new TextBox
        {
            Location         = new Point(0, 126),
            Size             = new Size(520, 28),
            UseSystemPasswordChar = true,
            BackColor        = SetupWizard.ColorSurface,
            ForeColor        = SetupWizard.ColorText,
            Font             = new Font("Segoe UI", 10f),
            BorderStyle      = BorderStyle.FixedSingle,
        };
        _txtApiKey.TextChanged += (_, _) =>
        {
            _keyValidated = false;
            UpdateStatus(string.Empty, false);
            UpdateNextState();
        };

        // Show/hide toggle
        _btnShowHide = new Button
        {
            Text      = "Show",
            Size      = new Size(60, 28),
            Location  = new Point(526, 126),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0x2a, 0x2a, 0x4e),
            ForeColor = SetupWizard.ColorText,
            Cursor    = Cursors.Hand,
        };
        _btnShowHide.FlatAppearance.BorderColor = SetupWizard.ColorAccent;
        _btnShowHide.Click += BtnShowHide_Click;

        // Test button
        _btnTest = new Button
        {
            Text      = "Test Key",
            Size      = new Size(88, 28),
            Location  = new Point(596, 126),
            FlatStyle = FlatStyle.Flat,
            BackColor = SetupWizard.ColorAccent,
            ForeColor = Color.White,
            Cursor    = Cursors.Hand,
        };
        _btnTest.FlatAppearance.BorderSize = 0;
        _btnTest.Click += BtnTest_Click;

        // Status label
        _lblStatus = new Label
        {
            Text      = string.Empty,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize  = true,
            Location  = new Point(0, 162),
        };

        // Skip checkbox
        _chkSkip = new CheckBox
        {
            Text      = "Skip for now (configure later in Settings)",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = true,
            Location  = new Point(0, 188),
        };
        _chkSkip.CheckedChanged += (_, _) =>
        {
            _txtApiKey.Enabled    = !_chkSkip.Checked;
            _btnTest.Enabled      = !_chkSkip.Checked;
            _btnShowHide.Enabled  = !_chkSkip.Checked;
            UpdateNextState();
        };

        // Security note
        var note = new Label
        {
            Text = "Your key is encrypted locally using Windows DPAPI and never leaves this machine.\n" +
                   "It is stored at: %APPDATA%\\PerplexityXPC\\apikey.dat",
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = false,
            Size      = new Size(630, 36),
            Location  = new Point(0, 224),
        };

        Controls.Add(title);
        Controls.Add(instr);
        Controls.Add(link);
        Controls.Add(lblKey);
        Controls.Add(_txtApiKey);
        Controls.Add(_btnShowHide);
        Controls.Add(_btnTest);
        Controls.Add(_lblStatus);
        Controls.Add(_chkSkip);
        Controls.Add(note);

        ResumeLayout(false);
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private void BtnShowHide_Click(object? sender, EventArgs e)
    {
        _txtApiKey.UseSystemPasswordChar = !_txtApiKey.UseSystemPasswordChar;
        _btnShowHide.Text = _txtApiKey.UseSystemPasswordChar ? "Show" : "Hide";
    }

    private async void BtnTest_Click(object? sender, EventArgs e)
    {
        string key = _txtApiKey.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            UpdateStatus("Please enter an API key.", false);
            return;
        }

        _btnTest.Enabled = false;
        _btnTest.Text    = "Testing...";
        UpdateStatus("Contacting Perplexity API...", null);

        try
        {
            bool valid = await TestApiKeyAsync(key);
            if (valid)
            {
                _keyValidated              = true;
                _wizard.Config.ApiKey      = key;
                UpdateStatus("Key is valid.", true);
            }
            else
            {
                _keyValidated = false;
                UpdateStatus("Invalid key. Check the key and try again.", false);
            }
        }
        catch (Exception ex)
        {
            _keyValidated = false;
            UpdateStatus($"Connection error: {ex.Message}", false);
        }
        finally
        {
            _btnTest.Enabled = true;
            _btnTest.Text    = "Test Key";
            UpdateNextState();
        }
    }

    /// <summary>Makes a minimal API call to verify the key is accepted.</summary>
    private static async System.Threading.Tasks.Task<bool> TestApiKeyAsync(string apiKey)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new StringContent(
            "{\"model\":\"sonar\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],\"max_tokens\":1}",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("https://api.perplexity.ai/chat/completions", body);

        // 401 = bad key, 200/206 = good key, 400 = bad request but key accepted
        return response.StatusCode != System.Net.HttpStatusCode.Unauthorized;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void UpdateStatus(string text, bool? success)
    {
        _lblStatus.Text = text;
        _lblStatus.ForeColor = success switch
        {
            true  => SetupWizard.ColorSuccess,
            false => SetupWizard.ColorError,
            null  => SetupWizard.ColorTextMuted,
        };
    }

    private void UpdateNextState()
    {
        _wizard.SetNextEnabled(_keyValidated || _chkSkip.Checked);
    }

    // -------------------------------------------------------------------------
    // IWizardPage
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void OnPageShown()
    {
        _wizard.SetSubtitle("Step 2 of 7 - API Key");
        UpdateNextState();
    }

    /// <inheritdoc/>
    public new bool Validate()
    {
        if (_chkSkip.Checked) return true;

        if (!_keyValidated)
        {
            MessageBox.Show(
                "Please test your API key before continuing, or check \"Skip for now\".",
                "API Key Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }
}
