# MML — Mine Mogul Logic  v1.0.0

> **A BepInEx mod for [MineMogul](https://store.steampowered.com/app/MineMogul) that adds Satisfactory-style factory analytics to your mine.**  
> Made by **Astro16** — 📦 [GitHub Repository](https://github.com/p-astro16/MineMogulLogicMod/tree/main) · 🆓 Free & Open-source

---

## Features

| # | Feature | Hotkey |
|---|---------|--------|
| 1 | **Throughput Tracker** — real-time items/min measurement per conveyor belt | — |
| 2 | **Bottleneck Detector** — statistical outlier detection flags your slowest belts | — |
| 3 | **Factory HUD** — full overlay with Machines, Belts, Sales & Settings tabs | **F5** |
| 4 | **Belt Inspector** — kijk naar een belt → detail panel rechtsonder (status, doorvoer, snelheid). **F6** toont/verbergt zwevende labels boven alle naburige belts. | **F6** |
| 5 | **Splitter Inspector** — kijk naar een splitter → druk **F** om het configuratiescherm te openen. **Tab** opent het overzicht van alle splitters. Geen shop item nodig. | **F** / **Tab** |
| 6 | **Ore Analyser Upgrade** — extended scanner readout: type, sell value, history & sorter stats | — |
| 7 | **Sales Tracker** — lifetime earnings, session revenue and top-selling resources | — |

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
| **F5** | Open / close the Factory HUD overlay |
| **F6** | Toggle zwevende belt labels (items/min boven alle naburige belts) |
| **F** | Configureer de splitter waar je naar kijkt (crosshair) |
| **Tab** | Open globaal splitter overzicht (alle splitters op de map) |

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

> **Changelog v1.0.0** → Belt Inspector + Splitter Inspector via crosshair, geen shop items meer nodig. F6 = belt labels, F = splitter config, Tab = alle splitters.

*Not affiliated with or endorsed by NoodleForge / the MineMogul developers.*
