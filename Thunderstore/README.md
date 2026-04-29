# Map Vote With Preview

Add-on for [MapVote by Patrick](https://thunderstore.io/c/repo/p/Patrick/MapVote/) that adds a map preview feature.

## Features

- **Shift+Click** any map in the vote list to preview it
- Loading screen while the preview generates
- Procedurally generates a sample level using the map's actual room modules
- Fly around with **WASD + Mouse** freecam to explore the rooms
- Rooms connected with doors (using the game's ModulePropSwitch system)
- Level visual settings applied: fog, ambient color, bloom, vignette, chromatic aberration, grain, motion blur
- Pixelated rendering matching the game's retro look
- Other players see **(previewing)** next to the map name
- Preview auto-closes when the game starts

## Controls

### Voting
- **Left click** on a map = vote (handled by MapVote)
- **Shift + Left click** on a map = preview it

### Preview Mode
- **WASD** = move
- **Right mouse (hold)** = look around
- **Shift** = fast mode
- **Space / Ctrl** = up / down
- **"Back to Vote" button** = return to voting

## Installation

Install via r2modman or Thunderstore Mod Manager. MapVote will be installed automatically as a dependency.

**Requires:** MapVote, BepInEx, REPOLib, MenuLib

## Configuration

- `Preview Enabled` — enable/disable the preview feature
- `Freecam Speed` (1-50) — camera movement speed
