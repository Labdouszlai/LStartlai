# LStartlai

**Startup Manager for Windows** — View, add, enable, disable, and remove startup programs across all Windows startup locations.

![License](https://img.shields.io/badge/license-MIT-blue)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)

---

## Features

- **View all startup entries** — Scans HKCU Run, HKLM Run (x64/x86), and Startup Folders (user + common)
- **Add programs** — Browse for `.exe` files and add them to HKCU Run with a custom name
- **Disable without deleting** — Moves entries out of the Run key so they never execute at boot
- **System-wide support** — Disable or remove HKLM entries with automatic UAC elevation
- **Drag & drop** — Drop `.exe`, `.lnk`, or `.url` files directly onto the window
- **Dark UI** — Elegant dark theme with card-based layout

## Screenshots

*(Add screenshot here)*

## Usage

| Action | Description |
|--------|-------------|
| **Add Program** | Click the `+ Add Program` button or press `Ctrl+N` |
| **Refresh** | Click `Refresh` or press `Ctrl+R` |
| **Toggle ON/OFF** | Click the diamond button or double-click an entry |
| **Remove** | Click the ✕ button to permanently remove an entry |
| **Drag & drop** | Drag `.exe` / `.lnk` / `.url` files onto the window |

## How it works

LStartlai scans five startup locations:

| Location | Type |
|----------|------|
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Per-user registry |
| `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` | Machine-wide registry |
| `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run` | 32-bit machine-wide |
| `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup` | User startup folder |
| `%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Startup` | Common startup folder |

When you **disable** an entry:
- Registry entries are moved to `HKCU\Software\LStartlai\Disabled` (they are **deleted** from the Run key, so Windows never runs them)
- Shortcut files are moved to `%LOCALAPPDATA%\LStartlai\DisabledShortcuts\`

When you **add** a program, it is saved to `HKCU\Run` (per-user, no admin required).

## Download

Pre-built binaries are available on the [Releases page](https://github.com/Labdouszlai/LStartlai/releases). The self-contained `.exe` requires no .NET runtime.

## Building from source

```bash
git clone https://github.com/Labdouszlai/LStartlai.git
cd LStartlai
dotnet publish -c Release --self-contained true -r win-x64 -p:PublishSingleFile=true
```

The compiled executable will be in `bin/Release/net8.0-windows/win-x64/publish/`.

## Requirements

- Windows 10 / Windows Server 2022 or later
- No .NET runtime required (self-contained build)

## License

MIT — Copyright © 2026 L'Abdouszlai

---

*Built by L'Abdouszlai*
