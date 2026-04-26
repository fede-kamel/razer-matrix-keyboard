# Razer Matrix Keyboard

A Windows application that brings the **Matrix digital rain** to your Razer keyboard via the Chroma SDK, with a matching animated UI.

![screenshot](screenshot.png)

---

## What it does

| Mode | Keyboard | UI |
|------|----------|----|
| **Matrix Rain** | All 22 columns of keys animate with falling green characters at independent speeds | Full animated rain fills the window |
| **Solid Green** | Every key lights up solid green | Same rain UI, status bar updates |

- Half-width katakana + digits fall as rain on both screen and keyboard
- ~17 % of drops have a white "head glint" for depth
- "T H E / M A T R I X" logo with glow and reflection rendered fresh each frame
- Switches persist across sessions via a Windows Scheduled Task

---

## Prerequisites

| Requirement | Notes |
|-------------|-------|
| **Windows 10 / 11** (x64) | The binary is self-contained — no .NET install needed |
| **Razer keyboard** with Chroma RGB | Any keyboard supported by Razer Chroma SDK |
| **Razer Synapse** installed and running | Provides the Chroma SDK REST server on port 54235 |

> If Synapse is not running the app still opens, but the keyboard stays dark until you start Synapse.

---

## Quick start (binary release)

1. Go to the [**Releases**](../../releases) page and download `RazerKeyboard.exe` and `icon.ico` into the **same folder**.
2. Double-click `RazerKeyboard.exe`.
3. Make sure Razer Synapse is open — the status bar will say **KEYBOARD: MATRIX RAIN** once connected.

### Optional — run at Windows startup

Open PowerShell as Administrator and run:

```powershell
$action   = New-ScheduledTaskAction -Execute "$env:USERPROFILE\Downloads\RazerKeyboard.exe"
$trigger  = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit 0 -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName "RazerMatrix" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest
```

---

## Controls

| Key / Action | Effect |
|---|---|
| **M** | Switch to Matrix rain mode |
| **G** | Switch to solid green mode |
| **ESC** | Close the application |
| Click **[ MATRIX RAIN ]** or **[ SOLID GREEN ]** buttons | Same as keyboard shortcuts |

---

## Build from source

### Requirements
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9) (Windows)
- Any Razer Chroma keyboard for live testing

```powershell
git clone https://github.com/YOUR_USERNAME/razer-matrix-keyboard
cd razer-matrix-keyboard

# Generate the icon (one-time)
cd GenIcon && dotnet run -- ..\icon.ico && cd ..

# Publish a self-contained single-file exe
dotnet publish -c Release -o .\release
copy icon.ico .\release\
```

The output `release\RazerKeyboard.exe` (~60 MB, self-contained) runs on any Windows 10/11 x64 machine with no runtime installed.

---

## Project structure

```
RazerKeyboard/
├── Program.cs        — Entry point ([STAThread] WinForms bootstrap)
├── MainForm.cs       — Animated window: Matrix rain, logo, mode buttons
├── ChromaClient.cs   — Razer Chroma REST API client (HTTP to localhost:54235)
├── MatrixRain.cs     — Keyboard color grid generator (6×22 Chroma layout)
├── GenIcon/
│   └── Program.cs    — One-time tool that generates icon.ico (5 resolutions)
└── icon.ico          — Generated Matrix-style icon (16, 32, 48, 64, 256 px)
```

---

## How it works

Razer Synapse exposes a local REST server at `http://localhost:54235/razer/chromasdk`.  
The app:

1. **Registers** a Chroma session on startup (`POST /razer/chromasdk`)
2. **Every 80 ms** sends a `CHROMA_CUSTOM` effect — a 6×22 array of BGR colour integers representing each key
3. **Every 4 s** sends a heartbeat (`PUT {session}/heartbeat`) to keep the session alive
4. **On close** sends `DELETE {session}` to release the keyboard back to Synapse

The Matrix rain algorithm assigns each column an independent drop with random speed (1–2 rows/tick) and trail length (3–6 rows). The visual window uses a "fade to black" technique: each frame a semi-transparent black rectangle is drawn over the canvas, then only the new head positions are painted — the trail emerges naturally as pixels decay.

---

## License

MIT
