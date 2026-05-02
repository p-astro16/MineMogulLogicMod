# MML — Mine Mogul Logic  v1.0.0

> **A BepInEx mod for [MineMogul](https://store.steampowered.com/app/MineMogul) that adds Satisfactory-style factory analytics to your mine.**  
> 📦 [GitHub Repository](https://github.com/p-astro16/MineMogulLogicMod/tree/main) · 🆓 Free & Open-source

---

## Features

| # | Feature | Hotkey |
|---|---------|--------|
| 1 | **Throughput Tracker** — real-time items/min measurement per conveyor belt | — |
| 2 | **Bottleneck Detector** — statistical outlier detection flags your slowest belts | — |
| 3 | **Factory HUD** — full overlay with Machines, Belts, Sales & Settings tabs | **F5** |
| 4 | **Belt Scanner** — koopbaar item (shop → Tools). Uitgerust: zwevende items/min labels boven nearby belts. Gebruikt het model van de ResourceScanner tool. | Shop |
| 5 | **Ore Analyser Upgrade** — extended scanner readout: type, sell value, history & sorter stats | — |
| 6 | **Sales Tracker** — lifetime earnings, session revenue and top-selling resources | — |
| 7 | **Splitter Wrench** — koopbaar item (shop → Tools). LMB configureert de splitter waar je naar kijkt; RMB opent globaal beheer. Gebruikt het model van de ResourceScanner tool. | Shop |

---

## Installation

### ✅ Easy — Installer (recommended)
1. Download the latest release from the [GitHub Releases page](https://github.com/p-astro16/MineMogulLogicMod/releases)
2. Run `MML_Installer.exe`
3. Your game path is auto-detected via Steam — or click **Browse** to set it manually
4. Click **Install**

### 🔧 Manual
1. Download [BepInEx 5.4.23 x64](https://github.com/BepInEx/BepInEx/releases) and extract it into your MineMogul game folder
2. Copy `MML.dll` into `<MineMogul>/BepInEx/plugins/`
3. Launch the game — config is created at `BepInEx/config/com.minemogul.mml.cfg`

---

## In-game Usage

| Key | Action |
|-----|--------|
| **F5** | Open / close the MML HUD overlay |

All thresholds and display options are adjustable live in the **Settings** tab of the HUD.

---

## Uninstallation

Run `MML_Installer.exe` and click **Uninstall**, or manually delete `BepInEx/plugins/MML.dll`.

---

## Requirements

- MineMogul (Steam, Early Access)
- Windows 10 / 11 × 64-bit
- BepInEx 5.4.x x64 *(bundled with the installer)*

---

## Building from Source

```bash
git clone https://github.com/p-astro16/MineMogulLogicMod.git
cd MineMogulLogicMod/MineMogulMod
dotnet build -c Release
```

Requires the game's managed DLLs in `libs/` (copy from `MineMogul_Data/Managed/`).  
See [CONTRIBUTING.md](https://github.com/p-astro16/MineMogulLogicMod/tree/main) for full setup instructions.

---

## Links

- 🏠 [Repository](https://github.com/p-astro16/MineMogulLogicMod/tree/main)
- 🐛 [Report an issue](https://github.com/p-astro16/MineMogulLogicMod/issues)
- 📋 [Changelog](https://github.com/p-astro16/MineMogulLogicMod/releases)

---

*Not affiliated with or endorsed by NoodleForge / the MineMogul developers.*
