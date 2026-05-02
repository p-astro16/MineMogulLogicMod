# MML — Mine Mogul Logic  v1.0.0

A BepInEx mod for **MineMogul** that adds Satisfactory-style factory analytics to your mine.

## Features

| # | Feature | Key |
|---|---------|-----|
| 1 | **Throughput Tracker** — items/min per conveyor belt | — |
| 2 | **Bottleneck Detector** — statistical outlier detection across all belts | — |
| 3 | **Factory HUD** — IMGUI overlay with Machines / Belts / Sales / Sorter Presets / Settings tabs | F5 |
| 4 | **Belt Item Counter** — floating world-space IPM labels on nearby belts | F6 |
| 5 | **Ore Analyser Upgrade** — extended scanner readout (type, sell value, history, sorter stats) | — |
| 6 | **Sales Tracker** — total earnings, session revenue, top resources | — |
| 7 | **Sorter Presets** — save & load named filter criteria sets for sorter baskets | HUD |

## Installation

### Easy (Installer)
1. Run `MineMogulModInstaller.exe`
2. The game path is auto-detected via Steam — otherwise click **Browse**
3. Click **Installeren**

### Manual
1. Copy the `BepInEx/` folder from [BepInEx 5.4.23.2 x64](https://github.com/BepInEx/BepInEx/releases) into your MineMogul game folder
2. Copy `MML.dll` into `MineMogul/BepInEx/plugins/`
3. Launch the game — settings are written to `BepInEx/config/com.minemogul.mml.cfg`

## In-game Usage

- **F5** — open/close the MML HUD overlay
- **F6** — toggle floating belt IPM counters
- All settings are adjustable live in the **Settings** tab of the HUD

## Uninstallation

Run the installer and click **Verwijderen**, or delete `BepInEx/plugins/MML.dll`.

## Requirements

- MineMogul (Steam, Early Access)
- Windows 10/11 x64
- BepInEx 5.4.x x64 (bundled in installer)

## Source

Built with BepInEx 5 + HarmonyLib.  
Feedback and issues: open a GitHub issue.
