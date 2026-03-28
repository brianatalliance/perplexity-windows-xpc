using System;
using System.Windows.Forms;

namespace PerplexityXPC.Setup;

/// <summary>
/// Entry point for the PerplexityXPC Setup Wizard.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Enable PerMonitorV2 DPI scaling
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Launch the setup wizard
        Application.Run(new SetupWizard());
    }
}
