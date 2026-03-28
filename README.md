# PerplexityXPC for Windows

**Integrate Perplexity AI into your entire Windows workflow - from PowerShell to Office to your phone.**

![Version](https://img.shields.io/badge/version-1.4.0-blue)
![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)
![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6)

PerplexityXPC runs a Windows Service bound exclusively to `127.0.0.1:47777`, proxying requests to the Perplexity Sonar API while managing MCP server processes, DPAPI-encrypted key storage, and a full REST/WebSocket interface. The system tray app, Explorer context menu, PowerShell module, and remote gateway all connect through this single local broker.

---

## Table of Contents

- [Features](#features)
- [Windows Defender / ASR Compatibility](#windows-defender--asr-compatibility)
- [Quick Start](#quick-start)
- [Screenshots](#screenshots)
- [Architecture](#architecture)
- [PowerShell Quick Reference](#powershell-quick-reference)
- [Alias Cheat Sheet](#alias-cheat-sheet)
- [Configuration](#configuration)
  - [appsettings.json](#appsettingsjson)
  - [mcp-servers.json](#mcp-serversjson)
  - [Environment Variables](#environment-variables)
- [Documentation](#documentation)
- [Security](#security)
- [Contributing](#contributing)
- [License](#license)
- [Acknowledgments](#acknowledgments)

---

## Features

### Core
- **Windows Service broker** - Kestrel HTTP/WebSocket server bound to `127.0.0.1:47777` only; never accessible from other machines
- **System tray application** - `Ctrl+Alt+P` global hotkey opens a floating query popup with dark/light theme support
- **Explorer context menu** - Right-click any text file or folder to send it directly to Perplexity
- **MCP server manager** - Start, stop, and restart MCP servers via JSON-RPC 2.0 over stdio; auto-restart on crash

### PowerShell
- **44 functions** - Queries, file and folder analysis, batch research, report generation, and IT-specific integrations
- **11 aliases** - Short-form commands (`pplx`, `pplxcode`, `pplxfile`, and more) for interactive use
- **Pipeline integration** - Functions accept pipeline input; compose with standard PowerShell verbs

### Office and Microsoft 365
- **Outlook** - Analyze emails, summarize threads, draft responses
- **Word** - Review and improve documents, generate structured reports
- **Excel** - Explain formulas, summarize data, produce commentary
- **Teams** - Analyze meeting transcripts and channel conversations

### Windows Administration
- **Event log analysis** - Correlate and explain Windows Event Log entries
- **Network diagnostics** - Interpret `netstat`, `tracert`, and firewall logs
- **Active Directory** - Analyze AD query output and group policy results
- **RDP and Task Scheduler** - Query historical session data and scheduled task output

### Premium Models
- **Deep research** - `sonar-deep-research` for multi-step, citation-backed research reports
- **Reasoning** - `sonar-reasoning` for complex analytical tasks
- **Structured output** - JSON-schema-constrained responses for automation pipelines
- **Academic and SEC search** - Specialized search modes for papers and filings

### Remote Access
- **Phone-to-PC control** - Cloudflare Tunnel integration lets you query your PC from your phone browser
- **Remote Gateway** - ASP.NET-based gateway with token authentication and sandboxed command execution

### Security
- **DPAPI encryption** - API key encrypted with `ProtectedData.Protect` at user+machine scope; plaintext only in memory during API calls
- **Localhost-only binding** - Kestrel explicitly binds to `127.0.0.1`; no network-facing exposure
- **Windows Firewall rule** - Defense-in-depth inbound block on port 47777
- **Per-user Named Pipe ACL** - IPC pipe restricted to the current user SID via `PipeAccessRule`
- **Command sandboxing** - Remote gateway executes only pre-approved command patterns
- **No API key in HTTP responses** - The `/config` endpoint deliberately omits the encrypted key

---

## Windows Defender / ASR Compatibility

PerplexityXPC runs as a Windows Service and spawns child processes (MCP servers, tray app, context menu helper). Without exclusions, Windows Defender real-time protection and Attack Surface Reduction (ASR) rules may quarantine executables, block script execution, or prevent child process creation during installation and at runtime.

### Easiest path - just double-click Setup.cmd

The included `Setup.cmd` launcher handles everything automatically:

1. Strips the Mark of the Web (MOTW) Zone.Identifier flag from all downloaded files.
2. Launches the PowerShell setup wizard, which detects and adds Defender exclusions as Step 0.5.

```
Double-click Setup.cmd   (no PowerShell needed to start)
```

`.cmd` files do not carry the same MOTW execution restrictions as `.ps1` files, so this sidesteps most ASR blocks immediately.

### Manual steps (if you prefer PowerShell directly)

**Step 1 - Unblock the downloaded files**

After extracting the ZIP, right-click the ZIP file > Properties > check **Unblock** before extracting. Or, after extracting, open PowerShell in the folder and run:

```powershell
Get-ChildItem -Recurse | Unblock-File
```

Alternatively, run the dedicated script:

```powershell
.\scripts\Unblock-PerplexityXPC.ps1
```

**Step 2 - Add Defender exclusions (requires Admin)**

```powershell
# Run from an elevated PowerShell prompt
.\scripts\Add-DefenderExclusions.ps1
```

This adds path and process exclusions for `C:\Program Files\PerplexityXPC\`, `%LOCALAPPDATA%\PerplexityXPC\`, and the PowerShell module directory. It also adds `AttackSurfaceReductionOnlyExclusions` if ASR rules are active.

To remove exclusions during uninstall:

```powershell
.\scripts\Add-DefenderExclusions.ps1 -Uninstall
```

**Step 3 - Run setup**

```powershell
.\Summon-Aunties.ps1
```

The setup wizard automatically re-runs the MOTW and Defender checks as Step 0.5.

### Code signing (optional)

Self-signed code signing eliminates "untrusted script" warnings without relying solely on exclusions:

```powershell
# Create a cert, sign all scripts, and add to TrustedPublisher (requires Admin)
.\scripts\New-CodeSigningCert.ps1 -SignScripts -TrustCert
```

### Enterprise / Intune / GPO environments

For managed environments where Defender policy is controlled centrally:

- Use `Install-PerplexityXPC.ps1 -SkipDefenderExclusions` to suppress automatic exclusion setup.
- Add exclusions via Microsoft Intune (Endpoint Security > Attack Surface Reduction) or Group Policy (`Computer Configuration > Administrative Templates > Windows Components > Microsoft Defender Antivirus > Exclusions`).
- ASR exclusion paths to whitelist:
  - `C:\Program Files\PerplexityXPC\`
  - `%LOCALAPPDATA%\PerplexityXPC\`
  - `%USERPROFILE%\Documents\WindowsPowerShell\Modules\PerplexityXPC\`

---

## Quick Start

```powershell
git clone https://github.com/YOUR_USERNAME/perplexity-windows.git
cd perplexity-windows
.\Summon-Aunties.ps1
```

That's it - the wizard handles everything: prerequisites check, API key encryption, service registration, firewall rules, MCP configuration, and integration setup.

### Automated / Silent Install

```powershell
# Pre-supply key, use all defaults
.\Summon-Aunties.ps1 -Silent -ApiKey "pplx-your-key-here"

# Load from config file (for batch/RMM deployment)
.\Summon-Aunties.ps1 -Silent -ConfigFile ".\setup-config.json"
```

Copy `setup-config.template.json` to `setup-config.json`, fill in your API key and preferences, then run the silent install. Suitable for Intune, Atera, or any RMM platform.

### Uninstall

```powershell
.\Summon-Aunties.ps1 -Uninstall
```

---

## Screenshots

```
[Screenshot: Setup Wizard - Welcome Page]
Terminal showing the ASCII art banner, version number, and first step of the wizard.

[Screenshot: Setup Wizard - API Key Configuration]
Step 2 of the wizard prompting for the Perplexity API key with format validation and test option.

[Screenshot: System Tray App with Query Popup]
The floating query popup triggered by Ctrl+Alt+P, showing a dark-mode response from Perplexity.

[Screenshot: PowerShell Module xpc -h Output]
Terminal output of the module help listing all 44 functions grouped by category.
```

---

## Architecture

```
+-----------------------------------------------------------------------+
|                           PerplexityXPC                               |
|                                                                       |
|  +-----------------+    Named Pipe    +---------------------------+   |
|  | System Tray App |<--------------->|     Windows Service        |   |
|  | (Ctrl+Alt+P)  |                 |     (PerplexityXPC)        |   |
|  +-----------------+                 |                           |   |
|                                      |   HTTP: 127.0.0.1:47777   |   |
|  +-----------------+    HTTP/SSE     |   WS:   ws://127.0.0.1:   |   |
|  | Context Menu    |<--------------->|         47777/ws           |   |
|  | (Right-click)   |                 |                           |   |
|  +-----------------+                 |  +---------------------+  |   |
|                                      |  | Perplexity Sonar    |  |   |
|  +-----------------+    HTTP/SSE     |  | API Proxy           |  |   |
|  | PowerShell /    |<--------------->|  |                     |  |   |
|  | curl / scripts  |                 |  | sonar               |  |   |
|  +-----------------+                 |  | sonar-pro           |  |   |
|                                      |  | sonar-reasoning     |  |   |
|  +-----------------+    HTTP         |  | sonar-deep-research |  |   |
|  | Remote Gateway  |<--------------->|  +---------------------+  |   |
|  | (Cloudflare     |                 |                           |   |
|  |  Tunnel)        |                 |  +---------------------+  |   |
|  +-----------------+                 |  | MCP Server Manager  |  |   |
|                                      |  |                     |  |   |
|                                      |  | filesystem          |  |   |
|                                      |  | github              |  |   |
|                                      |  | brave-search        |  |   |
|                                      |  | memory              |  |   |
|                                      |  | sqlite              |  |   |
|                                      |  | (custom...)         |  |   |
|                                      |  +---------------------+  |   |
|                                      +---------------------------+   |
+-----------------------------------------------------------------------+
                                  |
                     DPAPI-encrypted API key
                     (never transmitted via HTTP)
```

---

## PowerShell Quick Reference

Import the module after installation:

```powershell
Import-Module PerplexityXPC
```

| Command | Description | Example |
|---------|-------------|---------|
| `Invoke-Perplexity` | Send a query and get a response | `Invoke-Perplexity 'What is BGP?'` |
| `Invoke-Perplexity -Model sonar-pro` | Use a specific Sonar model | `Invoke-Perplexity 'Explain zero trust' -Model sonar-pro` |
| `Invoke-Perplexity -Stream` | Stream the response token by token | `Invoke-Perplexity 'Write a bash script' -Stream` |
| `Get-XPCStatus` | Show service health and uptime | `Get-XPCStatus` |
| `Get-XPCConfig` | Read current broker configuration | `Get-XPCConfig` |
| `Set-XPCConfig` | Update broker configuration at runtime | `Set-XPCConfig -DefaultModel sonar-pro` |
| `Get-McpServer` | List all MCP servers and their state | `Get-McpServer` |
| `Restart-McpServer` | Restart a named MCP server | `Restart-McpServer -Name filesystem` |
| `Invoke-McpRequest` | Send a JSON-RPC request to an MCP server | `Invoke-McpRequest -Server filesystem -Method 'tools/list'` |
| `Invoke-PerplexityFileAnalysis` | Analyze a file's content | `Invoke-PerplexityFileAnalysis -Path .\error.log` |
| `Invoke-PerplexityFolderAnalysis` | Summarize a folder's structure and contents | `Invoke-PerplexityFolderAnalysis -Path C:\Projects\MyApp` |
| `Invoke-PerplexityBatch` | Run multiple queries in parallel | `@('Topic A','Topic B') \| Invoke-PerplexityBatch` |
| `Invoke-PerplexityReport` | Compile multi-topic research into a report | `Invoke-PerplexityReport -Topics $list -OutputPath .\report.md` |
| `Invoke-PerplexityTicketAnalysis` | Analyze an IT support ticket | `Invoke-PerplexityTicketAnalysis -TicketId 12345` |
| `Invoke-PerplexitySecurityAnalysis` | Analyze security event log entries | `Invoke-PerplexitySecurityAnalysis -EventIds @(4625,4648)` |

Full reference with all 44 functions, parameter tables, and return types: [docs/MODULE.md](docs/MODULE.md)

---

## Alias Cheat Sheet

| Alias | Maps To | Description |
|-------|---------|-------------|
| `pplx` | `Invoke-Perplexity` | General query |
| `pplxpro` | `Invoke-Perplexity -Model sonar-pro` | Pro model query |
| `pplxcode` | `Invoke-Perplexity -SystemPrompt $CodePrompt` | Code-focused query |
| `pplxfile` | `Invoke-PerplexityFileAnalysis` | Analyze a file |
| `pplxfolder` | `Invoke-PerplexityFolderAnalysis` | Analyze a folder |
| `pplxbatch` | `Invoke-PerplexityBatch` | Batch queries |
| `pplxreport` | `Invoke-PerplexityReport` | Generate a report |
| `pplxticket` | `Invoke-PerplexityTicketAnalysis` | IT ticket analysis |
| `pplxsec` | `Invoke-PerplexitySecurityAnalysis` | Security analysis |
| `xpc` | `Get-XPCStatus` | Show service status |
| `xpcmcp` | `Get-McpServer` | List MCP servers |

---

## Configuration

### appsettings.json

Located at `%LOCALAPPDATA%\PerplexityXPC\appsettings.json` after installation.
Template: `config\appsettings.template.json`

| Key | Default | Description |
|-----|---------|-------------|
| `PerplexityXPC.ApiEndpoint` | `https://api.perplexity.ai` | Perplexity API base URL |
| `PerplexityXPC.HttpPort` | `47777` | Local HTTP port |
| `PerplexityXPC.PipeServerName` | `PerplexityXPCPipe` | Named Pipe server name |
| `PerplexityXPC.LogLevel` | `Information` | Logging level (Trace/Debug/Information/Warning/Error) |
| `PerplexityXPC.DefaultModel` | `sonar` | Default Sonar model |
| `PerplexityXPC.ApiTimeoutSec` | `60` | API call timeout in seconds |
| `PerplexityXPC.MaxTokens` | `2048` | Default max tokens per response |
| `PerplexityXPC.MaxFileSizeKB` | `10240` | Max file size for context menu reads |
| `Mcp.AutoRestart` | `true` | Auto-restart crashed MCP processes |
| `Mcp.TimeoutSec` | `30` | MCP request timeout |
| `Mcp.MaxConcurrentServers` | `5` | Max simultaneous MCP processes |

### mcp-servers.json

Located at `%LOCALAPPDATA%\PerplexityXPC\mcp-servers.json`.
Template: `config\mcp-servers.template.json`

Each entry under `mcpServers` defines one server process:

| Field | Type | Description |
|-------|------|-------------|
| `disabled` | boolean | Set to `true` to exclude from auto-start |
| `description` | string | Human-readable label (optional) |
| `command` | string | Executable to launch (e.g., `npx`, `node`, `python`) |
| `args` | array | Arguments passed to the command |
| `env` | object | Environment variables injected into the process |

Example:

```json
{
  "mcpServers": {
    "filesystem": {
      "disabled": false,
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\You\\Documents"],
      "env": {}
    },
    "github": {
      "disabled": false,
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_..."
      }
    }
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `PERPLEXITYXPC_DEBUG` | Set to `1` to enable verbose debug logging |
| `PERPLEXITYXPC_PORT` | Override the HTTP port (default: 47777) |

---

## Documentation

| Document | Description |
|----------|-------------|
| [Installation Guide](docs/INSTALL.md) | Full build, install, upgrade, and uninstall steps |
| [PowerShell Module Reference](docs/MODULE.md) | All 44 functions with parameters, return types, and examples |
| [HTTP API Reference](docs/API.md) | Complete REST and WebSocket endpoint documentation |
| [Integration Guide](integrations/README.md) | Windows Terminal, VS Code, PowerShell profile setup |
| [Remote Access Guide](remote/README.md) | Cloudflare Tunnel setup and mobile access |

---

## Security

PerplexityXPC is designed to be a local-only broker. Key design decisions:

- **No network exposure by default** - Kestrel binds to `127.0.0.1` and a firewall inbound-block rule is created during installation. Your API key cannot be reached from another machine.
- **DPAPI at rest** - `ProtectedData.Protect` with `CurrentUser` scope encrypts the key to the current Windows user account. Even with filesystem access, the encrypted blob cannot be decrypted by another user or on another machine.
- **No secrets in HTTP** - The `/config` endpoint returns all configuration fields except the API key. There is no endpoint that returns the plaintext or encrypted key.
- **Named Pipe isolation** - The tray-to-service IPC pipe uses a `PipeAccessRule` that allows only the current user's SID, preventing other local accounts from sending commands to the service.
- **Service account** - The Windows Service runs as `LocalService`, minimizing its privilege footprint.
- **Command sandboxing** - When the Remote Gateway is enabled, only commands matching an explicit allow-list pattern are executed. Arbitrary shell commands are rejected.

If you discover a security issue, please report it privately via the issue tracker rather than as a public bug report.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full contributor guide.

Quick summary:

1. Fork the repository and create a feature branch: `git checkout -b feature/my-feature`
2. Make your changes following the code style guidelines below
3. Test on Windows 10 and Windows 11 with both PowerShell 5.1 and PowerShell 7
4. Submit a pull request with a clear description of the change and why it is needed

**Code style requirements:**

- PowerShell must be compatible with PS 5.1 and PS 7+ (no `?.` or `??=` operators, no Unicode 2014 em dashes)
- Use regular hyphens (`-`) only - never em dashes
- Use `${variable}` syntax when the variable name is immediately followed by a colon
- C# code follows standard .NET naming conventions
- PowerShell follows Verb-Noun naming (`Invoke-Perplexity`, not `QueryPerplexity`)
- All error handling uses `try/catch` with descriptive `Write-Error` messages

---

## License

MIT License - see [LICENSE](LICENSE) for details.

---

## Acknowledgments

- [Perplexity AI](https://www.perplexity.ai) for the Sonar API and models that power this integration
- [Model Context Protocol](https://modelcontextprotocol.io) for the open MCP standard that enables local tool integration
- [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/) for the zero-config remote access infrastructure
