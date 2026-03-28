using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PerplexityXPC.Setup.Models;

namespace PerplexityXPC.Setup.Pages;

/// <summary>
/// Page 4: Configure optional MCP (Model Context Protocol) servers.
/// </summary>
public sealed class McpServersPage : UserControl, IWizardPage
{
    private readonly SetupWizard _wizard;
    private DataGridView _grid = null!;

    private static readonly List<McpServerEntry> DefaultServers = new()
    {
        new McpServerEntry
        {
            Enabled     = false,
            Name        = "filesystem",
            Command     = @"npx -y @modelcontextprotocol/server-filesystem C:\Users\{username}\Documents",
            Description = "Read and write local files",
        },
        new McpServerEntry
        {
            Enabled     = false,
            Name        = "github",
            Command     = "npx -y @modelcontextprotocol/server-github",
            Description = "GitHub API access (needs GITHUB_PERSONAL_ACCESS_TOKEN env var)",
        },
        new McpServerEntry
        {
            Enabled     = false,
            Name        = "brave-search",
            Command     = "npx -y @modelcontextprotocol/server-brave-search",
            Description = "Brave Search integration (needs BRAVE_API_KEY env var)",
        },
        new McpServerEntry
        {
            Enabled     = false,
            Name        = "sqlite",
            Command     = "npx -y @modelcontextprotocol/server-sqlite",
            Description = "Local SQLite database access",
        },
        new McpServerEntry
        {
            Enabled     = false,
            Name        = "memory",
            Command     = "npx -y @modelcontextprotocol/server-memory",
            Description = "Persistent in-memory key-value store",
        },
    };

    /// <summary>Initializes a new instance of <see cref="McpServersPage"/>.</summary>
    public McpServersPage(SetupWizard wizard)
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
            Text      = "MCP Servers",
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = SetupWizard.ColorAccent,
            AutoSize  = true,
            Location  = new Point(0, 0),
        };

        // Description
        var desc = new Label
        {
            Text = "MCP servers extend Perplexity with local tools such as filesystem access,\n" +
                   "database queries, and external APIs. All servers are optional and disabled by default.",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = SetupWizard.ColorText,
            AutoSize  = true,
            Location  = new Point(0, 38),
        };

        // Grid
        _grid = new DataGridView
        {
            Location              = new Point(0, 84),
            Size                  = new Size(630, 210),
            BackgroundColor       = SetupWizard.ColorSurface,
            ForeColor             = SetupWizard.ColorText,
            GridColor             = Color.FromArgb(0x30, 0x30, 0x55),
            BorderStyle           = BorderStyle.None,
            RowHeadersVisible     = false,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
            EnableHeadersVisualStyles = false,
        };

        StyleGrid();
        PopulateGrid();

        // Add Custom button
        var btnAdd = new Button
        {
            Text      = "Add Custom...",
            Location  = new Point(0, 300),
            Size      = new Size(120, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = SetupWizard.ColorAccent,
            ForeColor = Color.White,
            Cursor    = Cursors.Hand,
        };
        btnAdd.FlatAppearance.BorderSize = 0;
        btnAdd.Click += BtnAdd_Click;

        // Note
        var note = new Label
        {
            Text      = "Requires Node.js. Enable only the servers you plan to use.",
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = SetupWizard.ColorTextMuted,
            AutoSize  = true,
            Location  = new Point(0, 338),
        };

        Controls.Add(title);
        Controls.Add(desc);
        Controls.Add(_grid);
        Controls.Add(btnAdd);
        Controls.Add(note);

        ResumeLayout(false);
    }

    private void StyleGrid()
    {
        var headerStyle = _grid.ColumnHeadersDefaultCellStyle;
        headerStyle.BackColor = SetupWizard.ColorSurface;
        headerStyle.ForeColor = SetupWizard.ColorText;
        headerStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        headerStyle.SelectionBackColor = SetupWizard.ColorSurface;

        var cellStyle = _grid.DefaultCellStyle;
        cellStyle.BackColor          = SetupWizard.ColorBackground;
        cellStyle.ForeColor          = SetupWizard.ColorText;
        cellStyle.SelectionBackColor = SetupWizard.ColorAccentDark;
        cellStyle.SelectionForeColor = Color.White;
        cellStyle.Font               = new Font("Segoe UI", 9f);
    }

    private void PopulateGrid()
    {
        _grid.Columns.Clear();

        var colEnabled = new DataGridViewCheckBoxColumn
        {
            HeaderText = "Enable",
            Name       = "colEnabled",
            Width      = 55,
        };
        var colName = new DataGridViewTextBoxColumn
        {
            HeaderText = "Name",
            Name       = "colName",
            Width      = 110,
            ReadOnly   = true,
        };
        var colCommand = new DataGridViewTextBoxColumn
        {
            HeaderText = "Command",
            Name       = "colCommand",
            Width      = 310,
        };
        var colDesc = new DataGridViewTextBoxColumn
        {
            HeaderText = "Description",
            Name       = "colDesc",
            Width      = 240,
            ReadOnly   = true,
        };

        _grid.Columns.AddRange(colEnabled, colName, colCommand, colDesc);

        string username = Environment.UserName;
        foreach (var server in DefaultServers)
        {
            string cmd = server.Command.Replace("{username}", username);
            _grid.Rows.Add(server.Enabled, server.Name, cmd, server.Description);
        }
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new AddMcpServerDialog();
        if (dlg.ShowDialog() == DialogResult.OK && dlg.Entry != null)
        {
            var entry = dlg.Entry;
            _grid.Rows.Add(entry.Enabled, entry.Name, entry.Command, entry.Description);
        }
    }

    // -------------------------------------------------------------------------
    // IWizardPage
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void OnPageShown()
    {
        _wizard.SetSubtitle("Step 4 of 7 - MCP Servers");
    }

    /// <inheritdoc/>
    public new bool Validate()
    {
        // Collect enabled servers into config
        _wizard.Config.McpServers.Clear();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            bool enabled = Convert.ToBoolean(row.Cells["colEnabled"].Value ?? false);
            if (!enabled) continue;

            _wizard.Config.McpServers.Add(new McpServerEntry
            {
                Enabled     = true,
                Name        = row.Cells["colName"].Value?.ToString() ?? string.Empty,
                Command     = row.Cells["colCommand"].Value?.ToString() ?? string.Empty,
                Description = row.Cells["colDesc"].Value?.ToString() ?? string.Empty,
            });
        }
        return true;
    }
}

/// <summary>Simple dialog to add a custom MCP server entry.</summary>
internal sealed class AddMcpServerDialog : Form
{
    private TextBox _txtName    = null!;
    private TextBox _txtCommand = null!;
    private TextBox _txtDesc    = null!;

    public McpServerEntry? Entry { get; private set; }

    public AddMcpServerDialog()
    {
        Text            = "Add Custom MCP Server";
        Size            = new Size(480, 240);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = SetupWizard.ColorBackground;
        ForeColor       = SetupWizard.ColorText;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var lblName = new Label { Text = "Name:",    AutoSize = true, Location = new Point(12, 16), ForeColor = SetupWizard.ColorText };
        var lblCmd  = new Label { Text = "Command:", AutoSize = true, Location = new Point(12, 52), ForeColor = SetupWizard.ColorText };
        var lblDesc = new Label { Text = "Desc:",    AutoSize = true, Location = new Point(12, 88), ForeColor = SetupWizard.ColorText };

        _txtName    = MakeBox(80, 16, 360);
        _txtCommand = MakeBox(80, 52, 360);
        _txtDesc    = MakeBox(80, 88, 360);

        var btnOk = new Button
        {
            Text      = "Add",
            Size      = new Size(80, 28),
            Location  = new Point(284, 156),
            FlatStyle = FlatStyle.Flat,
            BackColor = SetupWizard.ColorAccent,
            ForeColor = Color.White,
            DialogResult = DialogResult.OK,
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            Entry = new McpServerEntry
            {
                Enabled     = true,
                Name        = _txtName.Text.Trim(),
                Command     = _txtCommand.Text.Trim(),
                Description = _txtDesc.Text.Trim(),
            };
        };

        var btnCancel = new Button
        {
            Text      = "Cancel",
            Size      = new Size(80, 28),
            Location  = new Point(372, 156),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0x2a, 0x2a, 0x4e),
            ForeColor = SetupWizard.ColorText,
            DialogResult = DialogResult.Cancel,
        };
        btnCancel.FlatAppearance.BorderColor = SetupWizard.ColorAccent;

        Controls.AddRange(new Control[] { lblName, lblCmd, lblDesc, _txtName, _txtCommand, _txtDesc, btnOk, btnCancel });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private static TextBox MakeBox(int x, int y, int w) => new()
    {
        Location    = new Point(x, y),
        Size        = new Size(w, 24),
        BackColor   = SetupWizard.ColorSurface,
        ForeColor   = SetupWizard.ColorText,
        BorderStyle = BorderStyle.FixedSingle,
        Font        = new Font("Segoe UI", 9.5f),
    };
}
