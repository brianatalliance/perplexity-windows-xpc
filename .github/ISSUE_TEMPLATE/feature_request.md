---
name: Feature Request
about: Propose a new feature or enhancement for PerplexityXPC
title: '[Feature] '
labels: enhancement
assignees: ''
---

## Summary

A clear, one-sentence description of the feature you are proposing.

---

## Problem Statement

Describe the problem this feature solves. Use the format:

> As a [role], I want [capability] so that [benefit].

Example: "As a sysadmin, I want to send Windows Event Log entries directly to Perplexity so that I can diagnose service failures without manually copying log text."

---

## Proposed Solution

Describe your proposed implementation. Be as specific as possible:

- Which component is affected (service, tray app, PowerShell module, context menu, remote gateway)?
- What new commands, endpoints, or UI elements would be added?
- What configuration changes (if any) would be required?

---

## Alternatives Considered

Describe any alternative approaches you considered and why you chose the proposed solution over them.

---

## Example Usage

Show what using this feature would look like - a code snippet, command, or UI interaction.

```powershell
# Example:
Invoke-PerplexityEventLog -LogName System -Newest 20 -Issue 'Service keeps crashing'
```

---

## Impact and Scope

- **Breaking change?** Yes / No
- **New dependencies?** (NuGet packages, Node modules, external services)
- **Platform requirements?** (Windows 11 only? Requires specific build?)
- **Estimated complexity:** Small / Medium / Large

---

## Would you implement this?

- [ ] Yes, I would like to submit a PR for this feature
- [ ] No, I am requesting this for someone else to implement
- [ ] Unsure

---

## Additional Context

Any other information - screenshots, links to similar tools, related issues, or prior discussion.
