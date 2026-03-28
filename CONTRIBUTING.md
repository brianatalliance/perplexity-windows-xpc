# Contributing to PerplexityXPC for Windows

Thank you for your interest in contributing. This document describes how to report bugs, propose features, submit pull requests, and follow the project's code style requirements.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)
- [Submitting Pull Requests](#submitting-pull-requests)
- [Code Style Guide](#code-style-guide)
  - [PowerShell](#powershell)
  - [C#](#c-sharp)
  - [Documentation and Markdown](#documentation-and-markdown)
- [Testing Requirements](#testing-requirements)
- [Development Setup](#development-setup)

---

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/1/code_of_conduct/). By participating you agree to uphold it. Respectful, constructive communication is expected in all issues, pull requests, and discussions.

---

## Reporting Bugs

Use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md) when filing a new issue.

Include:

- **Windows version and build number** (`winver` or `[System.Environment]::OSVersion`)
- **PowerShell version** (`$PSVersionTable.PSVersion`)
- **.NET SDK version** (`dotnet --version`)
- **Steps to reproduce** - the exact commands or actions that trigger the bug
- **Expected behavior** - what you expected to happen
- **Actual behavior** - what actually happened, including full error messages and stack traces
- **Service logs** - relevant lines from `%LOCALAPPDATA%\PerplexityXPC\logs\service-*.log`

Do not include your API key, personal access tokens, or other secrets in bug reports.

For security vulnerabilities, do not file a public issue. Use the private security advisory mechanism on GitHub instead.

---

## Suggesting Features

Use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md).

Describe:

- The problem the feature solves (use the format: "As a [role], I want [capability] so that [benefit]")
- Your proposed implementation approach (optional but helpful)
- Any alternatives you considered
- Whether you are willing to implement it yourself

---

## Submitting Pull Requests

1. **Fork** the repository and create a branch from `main`:
   ```
   git checkout -b feature/short-description
   ```

2. **Make your changes.** Follow the code style requirements in the next section.

3. **Test** on Windows 10 (build 1809+) and Windows 11 with both PowerShell 5.1 and PowerShell 7. See [Testing Requirements](#testing-requirements).

4. **Commit** with a clear, imperative message:
   ```
   Add Invoke-PerplexityDeviceAnalysis function
   Fix port validation in Summon-Aunties.ps1
   Update mcp-servers.json schema documentation
   ```

5. **Open a pull request** against `main`. Fill in the PR template completely, including:
   - What the change does
   - How it was tested
   - Screenshots or terminal output if the change affects UI or CLI output

6. A maintainer will review the PR. Be prepared to address feedback. PRs that do not pass the required checks will not be merged.

---

## Code Style Guide

### PowerShell

**Compatibility is the top priority.** All PowerShell code must run correctly on both PS 5.1 and PS 7+.

**Prohibited operators and syntax (PS 5.1 incompatible):**

- `?.` (null-conditional member access) - use explicit null checks instead
- `??=` (null coalescing assignment) - use `if ($null -eq $x) { $x = $default }`
- `??` - use explicit `if` checks

**Em dashes:**

- Never use Unicode em dash (U+2014 --). Use regular hyphens (`-`) only.
- This applies to all strings, comments, and documentation.

**Variable naming near colons:**

- Use `${variable}` syntax when the variable name is immediately adjacent to a colon to avoid parsing ambiguity:
  ```powershell
  # Correct
  "${env:LOCALAPPDATA}\PerplexityXPC"
  # Also correct
  $path = "$env:LOCALAPPDATA\PerplexityXPC"   # works in most contexts
  ```

**Function naming:**

- Use the standard PowerShell `Verb-Noun` convention. Run `Get-Verb` for the approved verb list.
- Do not invent verbs. Use `Invoke-`, `Get-`, `Set-`, `Add-`, `Remove-`, `Test-`, `Start-`, `Stop-` etc.

**Error handling:**

- Always use `try/catch` blocks for external calls (HTTP, filesystem, registry, service control).
- Use `Write-Error` with a descriptive message, not `throw` with a bare string.
- Where appropriate, include a `-ErrorAction Stop` on cmdlets inside `try` blocks so errors are catchable.

**Output:**

- Functions that return data should return structured objects or hashtables, not formatted strings.
- Use `Write-Verbose` for diagnostic output so callers can suppress it with `-Verbose:$false`.
- Never use `Write-Host` in module functions. Use `Write-Output`, `Write-Verbose`, or `Write-Warning`.
- `Write-Host` is acceptable in installer/wizard scripts where direct terminal interaction is intended.

**Strings:**

- Use single quotes for string literals that contain no variables or escape sequences.
- Use double quotes only when variable expansion or escape sequences (`\`n`, `\`t`) are needed.

**Parameter validation:**

- Use `[ValidateNotNullOrEmpty()]` and `[ValidateRange()]` attributes where appropriate.
- Validate API key format with a regex match before making network calls.

**Encoding:**

- All `.ps1` files must be saved as UTF-8 without BOM.
- Do not include characters above U+007E in script logic. Comments and strings may use common Latin extended characters sparingly.

**Example - correct style:**

```powershell
function Invoke-PerplexityQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [ValidateNotNullOrEmpty()]
        [string]$Query,

        [ValidateSet('sonar', 'sonar-pro', 'sonar-reasoning', 'sonar-deep-research')]
        [string]$Model = 'sonar'
    )

    process {
        $uri = "http://localhost:47777/perplexity"
        $body = @{
            model    = $Model
            messages = @(@{ role = 'user'; content = $Query })
        } | ConvertTo-Json -Depth 3

        try {
            $response = Invoke-RestMethod -Uri $uri -Method Post `
                -ContentType 'application/json' -Body $body `
                -ErrorAction Stop
            return $response
        } catch {
            Write-Error "Query failed: $_"
        }
    }
}
```

---

### C Sharp

- Follow [Microsoft's C# coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- Use `PascalCase` for types and `camelCase` for local variables.
- All `async` methods must have the `Async` suffix.
- Use `ILogger<T>` for all logging; do not use `Console.WriteLine`.
- Use `CancellationToken` for all async service methods.
- No hardcoded strings for configuration values - use `IOptions<AppConfig>`.
- XML documentation comments (`///`) on all public members.

---

### Documentation and Markdown

- No em dashes. Use regular hyphens or restructure the sentence.
- Code blocks must specify the language identifier (` ```powershell `, ` ```json `, ` ```csharp `).
- Tables must have header separators with at least three dashes per column.
- All links must be relative paths within the repo, not absolute GitHub URLs, unless linking to external resources.

---

## Testing Requirements

Before submitting a PR, verify the following:

### PowerShell

| Test | How to verify |
|------|--------------|
| PS 5.1 compatibility | Run `pwsh -Version 5` (or open Windows PowerShell) and execute all changed functions |
| PS 7 compatibility | Run in PowerShell 7 (`pwsh`) |
| No em dashes | `Select-String -Path .\*.ps1 -Pattern ([char]0x2014)` should return no matches |
| No `?.` or `??=` | `Select-String -Path .\*.ps1 -Pattern '\?\.'` and `'\?\?='` |
| Module imports cleanly | `Import-Module .\module\PerplexityXPC -Force` with no errors or warnings |
| All exported functions present | `Get-Command -Module PerplexityXPC \| Measure-Object` returns expected count |

### Service and Integration

| Test | How to verify |
|------|--------------|
| Service installs and starts | Run `.\Summon-Aunties.ps1` on a clean test machine or VM |
| Service stops cleanly | `Stop-Service PerplexityXPC` completes without hanging |
| HTTP endpoint responds | `Invoke-RestMethod http://localhost:47777/status` |
| Uninstall removes all components | `.\Summon-Aunties.ps1 -Uninstall` followed by manual verification |

### C# Build

```powershell
.\scripts\Build-PerplexityXPC.ps1
# Should complete with no errors and produce self-contained executables in .\bin\
```

---

## Development Setup

1. Clone the repository:
   ```
   git clone https://github.com/YOUR_USERNAME/perplexity-windows.git
   cd perplexity-windows
   ```

2. Install prerequisites:
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   winget install OpenJS.NodeJS.LTS
   ```

3. Set execution policy if needed:
   ```powershell
   Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
   ```

4. Build from source:
   ```powershell
   .\scripts\Build-PerplexityXPC.ps1
   ```

5. Install in development mode:
   ```powershell
   .\Summon-Aunties.ps1
   ```

6. Make changes, test, then submit your PR.
