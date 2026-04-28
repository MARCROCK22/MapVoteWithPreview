# Map Vote With Preview

Vote for the next map with your team — plus preview maps before voting!

Based on [MapVote by Patrick](https://thunderstore.io/c/repo/p/Patrick/MapVote/).

## Features

### Map Voting (from original MapVote)
- Vote for any map in the lobby or truck
- Radial vote counter shows live voting
- Random option available
- Countdown timer after first vote
- Animated spin wheel for tied votes
- Configurable voting time
- No-repeat option

### Map Preview (new)
- **Shift+Click** any map in the vote list to preview it
- Procedurally generates a sample level using the map's actual room modules
- Fly around with **WASD + Mouse** freecam to explore the rooms
- Rooms are properly connected with doors (using the game's ModulePropSwitch system)
- Level visual settings applied: fog, ambient color, post-processing (bloom, vignette, chromatic aberration, grain, motion blur)
- Pixelated rendering matching the game's retro look
- Other players see "(previewing)" indicator next to the map name
- Preview auto-closes when the game starts
- Click **"Back to Vote"** to return

### Preview Details
- Start room + normal/passage/dead-end/extraction modules placed in a connected layout
- Uses the exact grid generation algorithm from the game (random walk, farthest-first extraction placement)
- Module connections set correctly (walls open where rooms connect, closed where they don't)
- Level lighting: fog color/distance, ambient color from the Level ScriptableObject
- Post-processing: bloom, color grading, vignette, chromatic aberration, grain, motion blur, auto exposure

## Controls

### Voting
- **Left click** on a map = vote for it
- **Shift + Left click** on a map = preview it

### Preview Mode
- **WASD** = move
- **Right mouse (hold)** = look around
- **Shift** = fast mode
- **Space / Ctrl** = up / down
- **"Back to Vote" button** = return to voting

## Installation

Install via Gale, r2modman, or Thunderstore Mod Manager.

**Requires:** BepInEx 5.x, REPOLib 3.x, MenuLib 2.x

**Note:** If you have the original MapVote mod installed, disable it before using this one — they cannot run at the same time.

## Configuration

- `Voting Time` (3-30 seconds) — countdown after first vote
- `Hide in Menu` — hide vote UI in lobby menu
- `No Repeated Maps` — prevent same map twice in a row
- `Preview Enabled` — enable/disable the preview feature
- `Freecam Speed` (1-50) — camera movement speed
