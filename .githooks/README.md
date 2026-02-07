# Git Hooks - No Warnings Policy

This directory contains Git hooks enforced on the Telemetry Kitchen project.

## Pre-Commit Hook

**Location:** `.githooks/pre-commit` and `.githooks/pre-commit.ps1`

**Purpose:** Enforce zero-warnings compilation policy - prevents commits if the code has compilation errors or warnings.

### What It Does

Before each commit, the hook:
1. ✅ Runs `dotnet build` to compile the solution
2. ❌ Blocks commit if any compilation errors are detected
3. ⚠️ Blocks commit if any compilation warnings are detected
4. ✅ Allows commit only if build is clean

### Usage

**Automatic:** The hook runs automatically on every `git commit`

**Manual Trigger:**
```bash
# Bash (Git Bash / Unix)
bash .githooks/pre-commit

# PowerShell (Windows)
powershell -ExecutionPolicy Bypass -File .githooks/pre-commit.ps1
```

### Bypass (Emergency Only)

To skip the pre-commit hook (not recommended):
```bash
git commit --no-verify
```

### Cross-Platform Support

- **Windows (PowerShell):** Uses `pre-commit.ps1` with PowerShell colors and formatting
- **Windows (Git Bash):** Uses `pre-commit` wrapper that calls PowerShell on Windows
- **Unix/Linux/Mac:** Uses `pre-commit` native Bash script

### Configuration

The hook path is configured in `.git/config`:
```
[core]
    hooksPath = .githooks
```

To set this up on a fresh clone:
```bash
git config core.hooksPath .githooks
```

### Making Hooks Executable

On Unix-like systems:
```bash
chmod +x .githooks/pre-commit
chmod +x .githooks/pre-commit.ps1
```

On Windows, the hook scripts have the correct permissions set automatically.

## Zero-Warnings Policy Standards

All code in this repository must compile with:
- ❌ **No compilation errors**
- ❌ **No compilation warnings**

Developers should regularly check:
```bash
dotnet build
```

And resolve any issues immediately before committing.
