---
name: Bug Report
about: Report a reproducible problem with PerplexityXPC
title: '[Bug] '
labels: bug
assignees: ''
---

## Summary

A clear, one-sentence description of the bug.

---

## Environment

| Field | Value |
|-------|-------|
| PerplexityXPC version | e.g., 1.4.0 |
| Windows version | e.g., Windows 11 23H2 (build 22631) |
| PowerShell version | e.g., 5.1.19041.5247 or 7.4.2 |
| .NET SDK version | e.g., 8.0.300 |
| Node.js version | e.g., 20.12.2 LTS |
| Install path | e.g., C:\Program Files\PerplexityXPC |

---

## Steps to Reproduce

Provide the exact steps needed to trigger the bug.

1. 
2. 
3. 

---

## Expected Behavior

What you expected to happen.

---

## Actual Behavior

What actually happened. Include the full error message and stack trace.

```
Paste error output here
```

---

## Service Logs

Attach or paste relevant lines from the service log.

```powershell
# Command to retrieve logs:
Get-Content "$env:LOCALAPPDATA\PerplexityXPC\logs\service-*.log" -Tail 50
```

```
Paste log lines here
```

---

## HTTP API Response (if applicable)

If the bug involves an API call, include the request and response.

```powershell
# Example:
Invoke-RestMethod http://localhost:47777/status
```

```
Paste response here
```

---

## Additional Context

Any other information that might help diagnose the issue (screenshots, configuration changes, recent Windows updates, antivirus software, Group Policy settings, etc.).

---

**Checklist before submitting:**

- [ ] I searched existing issues and this is not a duplicate
- [ ] I am not including API keys, tokens, or other secrets in this report
- [ ] I can reproduce this consistently (not a one-time occurrence)
- [ ] I have provided service logs
