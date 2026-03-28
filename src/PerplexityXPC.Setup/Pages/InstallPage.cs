using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PerplexityXPC.Setup.Helpers;

namespace PerplexityXPC.Setup.Pages;

/// <summary>
/// Page 6: Installation progress page. Runs all install steps on a background thread.
/// </summary>
public sealed class InstallPage : UserControl, IWizardPage
{
    private readonly SetupWizard _wizard;

    private ProgressBar _progressBar = null!;
    private ListBox     _lstSteps    = null!;
    private Label       _lblCurrent  = null!;
    private bool        _installStarted;

    /// <summary>Initializes a new instance of <see cref="InstallPage"/>.</summary>
    public InstallPage(SetupWizard wizard)
    {
        _wizard   = wizard;
        BackColor = SetupWizard.ColorBackground;
        ForeColor = SetupWizard.ColorText;
        BuildLayout();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        var title = new Label
        {
            Text      = "Installing PerplexityXPC",
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorAccent,
            AutoSize  = true,
            Location  = new Point(0, 0),
        };

        var desc = new Label
        {
            Text      = "Please wait while the installation completes. Do not close this window.",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 38),
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(0, 64),
            Size     = new Size(630, 22),
            Minimum  = 0,
            Maximum  = 100,
            Value    = 0,
            Style    = ProgressBarStyle.Continuous,
        };

        _lblCurrent = new Label
        {
            Text      = "Ready to install.",
            Font      = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = true,
            Location  = new Point(0, 92),
        };

        _lstSteps = new ListBox
        {
            Location      = new Point(0, 114),
            Size          = new Size(630, 220),
            BackColor     = SetupWizard.ColorSurface,
            ForeColor     = SetupWizard.ColorText,
            Font          = new Font("Consolas", 8.5f),
            BorderStyle   = BorderStyle.None,
            SelectionMode = SelectionMode.None,
            IntegralHeight = false,
        };

        Controls.Add(title);
        Controls.Add(desc);
        Controls.Add(_progressBar);
        Controls.Add(_lblCurrent);
        Controls.Add(_lstSteps);

        ResumeLayout(false);
    }

    // -------------------------------------------------------------------------
    // IWizardPage
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void OnPageShown()
    {
        _wizard.SetSubtitle("Step 6 of 7 - Installing");
        _wizard.SetNextEnabled(false); // Disabled until install completes
    }

    /// <inheritdoc/>
    public new bool Validate()
    {
        if (!_installStarted)
        {
            // User clicked Install - start the process
            BeginInstall();
            return false; // Don't advance yet; install runs async
        }
        return true; // Already done
    }

    // -------------------------------------------------------------------------
    // Install runner
    // -------------------------------------------------------------------------

    private void BeginInstall()
    {
        _installStarted = true;
        _lstSteps.Items.Clear();
        _progressBar.Value = 0;

        var config   = _wizard.Config;
        var progress = new Progress<string>(msg => AppendStep(msg));

        Task.Run(async () =>
        {
            int total = 10;
            int step  = 0;

            void Report(string msg)
            {
                step++;
                int pct = (int)((double)step / total * 100);
                ((IProgress<string>)progress).Report(msg);
                Invoke(() =>
                {
                    _progressBar.Value = Math.Min(pct, 100);
                    _lblCurrent.Text   = msg;
                });
            }

            bool ok = true;

            // 1. Create directories
            Report($"[{step+1}/{total}] Creating installation directory...");
            ok &= InstallationEngine.CreateDirectories(config.InstallPath);
            UpdateLastStep(ok);

            // 2. Copy binaries
            Report($"[{step+1}/{total}] Copying service binaries...");
            string sourcePath = AppContext.BaseDirectory;
            ok &= InstallationEngine.CopyBinaries(sourcePath, config.InstallPath, progress);
            UpdateLastStep(ok);

            // 3. Encrypt API key
            Report($"[{step+1}/{total}] Encrypting and storing API key...");
            string dataPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PerplexityXPC");
            ok &= InstallationEngine.StoreApiKey(config.ApiKey, dataPath);
            UpdateLastStep(ok);

            // 4. Register service
            Report($"[{step+1}/{total}] Registering Windows Service...");
            string exePath = System.IO.Path.Combine(config.InstallPath, "PerplexityXPC.Service.exe");
            ok &= InstallationEngine.RegisterService(exePath);
            UpdateLastStep(ok);

            // 5. Configure firewall
            Report($"[{step+1}/{total}] Configuring Windows Firewall...");
            if (config.AddFirewall)
                ok &= InstallationEngine.ConfigureFirewall(config.Port);
            else
                UpdateLastStep(true, "SKIPPED");

            // 6. Set up MCP servers
            Report($"[{step+1}/{total}] Writing MCP server configuration...");
            ok &= InstallationEngine.CreateMcpConfig(dataPath, config.McpServers);
            UpdateLastStep(ok);

            // 7. Install PowerShell module
            Report($"[{step+1}/{total}] Installing PowerShell module...");
            if (config.InstallModule)
                ok &= InstallationEngine.InstallPowerShellModule(config.InstallPath);
            else
                UpdateLastStep(true, "SKIPPED");

            // 8. Configure integrations
            Report($"[{step+1}/{total}] Configuring integrations...");
            await RunIntegrationsAsync(config);
            UpdateLastStep(true);

            // 9. Start service
            Report($"[{step+1}/{total}] Starting service...");
            ok &= InstallationEngine.StartService();
            UpdateLastStep(ok);

            // 10. Verify
            Report($"[{step+1}/{total}] Verifying installation...");
            bool verified = InstallationEngine.VerifyInstallation(config.Port);
            UpdateLastStep(verified);

            // Done
            Invoke(() =>
            {
                _progressBar.Value = 100;
                _lblCurrent.Text   = verified
                    ? "Installation complete!"
                    : "Installation finished with some warnings.";
                _wizard.SetNextEnabled(true);
                // Auto-advance
                Task.Delay(600).ContinueWith(_ => Invoke(_wizard.GoNext),
                    TaskScheduler.FromCurrentSynchronizationContext());
            });
        });
    }

    private void AppendStep(string message)
    {
        _lstSteps.Items.Add(message);
        _lstSteps.TopIndex = _lstSteps.Items.Count - 1;
    }

    private void UpdateLastStep(bool ok, string? customSuffix = null)
    {
        if (_lstSteps.Items.Count == 0) return;
        Invoke(() =>
        {
            int idx = _lstSteps.Items.Count - 1;
            string current = _lstSteps.Items[idx]?.ToString() ?? string.Empty;
            string suffix  = customSuffix ?? (ok ? " [DONE]" : " [FAILED]");

            // Only append if not already suffixed
            if (!current.EndsWith(suffix, StringComparison.Ordinal))
                _lstSteps.Items[idx] = current + suffix;
        });
    }

    private static async Task RunIntegrationsAsync(Models.SetupConfig cfg)
    {
        string scriptDir = System.IO.Path.Combine(cfg.InstallPath, "integrations");

        if (cfg.InstallProfile)
            InstallationEngine.RunIntegration(
                System.IO.Path.Combine(scriptDir, "install-profile.ps1"), Array.Empty<string>());

        if (cfg.InstallTerminal)
            InstallationEngine.RunIntegration(
                System.IO.Path.Combine(scriptDir, "install-terminal.ps1"), Array.Empty<string>());

        if (cfg.InstallVSCode)
            InstallationEngine.RunIntegration(
                System.IO.Path.Combine(scriptDir, "install-vscode.ps1"), Array.Empty<string>());

        if (cfg.InstallRemote)
            InstallationEngine.RunIntegration(
                System.IO.Path.Combine(scriptDir, "install-cloudflare.ps1"), Array.Empty<string>());

        await Task.CompletedTask;
    }
}
