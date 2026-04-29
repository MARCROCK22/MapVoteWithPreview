# MapVoteWithPreview

Add-on for [MapVote](https://thunderstore.io/c/repo/p/Patrick/MapVote/) that adds a map preview feature to R.E.P.O.

**Shift+Click** any map in the vote list to preview it before voting. Shows a loading screen, then generates a procedural level using the map's actual room modules and lets you fly around with a freecam.

## How it works

This mod patches MapVote to add preview functionality. It does NOT replace MapVote — it requires it as a dependency.

When previewing:
- A loading screen appears immediately (no freeze)
- A sample level is procedurally generated using the exact game algorithm (random walk grid, extraction/dead-end placement, module connections)
- Rooms are connected with doors via ModulePropSwitch
- Level visuals applied: fog, ambient color, bloom, vignette, chromatic aberration, grain, motion blur
- Pixelated rendering matching the game's retro look
- Other players see **(previewing)** next to the map name

## Controls

| Action | Key |
|--------|-----|
| Vote for a map | Left click |
| Preview a map | Shift + Left click |
| Move | WASD |
| Look around | Right mouse (hold) |
| Fast mode | Shift |
| Up / Down | Space / Ctrl |
| Exit preview | "Back to Vote" button |

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| Preview Enabled | true | Enable/disable the preview feature |
| Freecam Speed | 10 | Camera movement speed (1-50) |

## Building

```
dotnet build -c Release
```

Output zip: `../build/MapVoteWithPreview-{version}.zip`
