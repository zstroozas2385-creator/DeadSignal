# Dead Signal

A 2D survival game for CS583. You are an astronaut carrying a radio signal orb through a dark, damaged space station. Find your 5 missing crewmates and lead them back to the escape pod - in small groups if you're smart - before your energy - or the 3:00 clock - runs out.

**The twist:** your pulse is the only way to see, and every creature on the station can hear it.

## Controls
| Key | Action |
|---|---|
| WASD / Arrows | Move |
| Space | Signal pulse (reveals the area, costs energy, alerts enemies) |
| Escape | Pause |

## Scripts (`Assets/Scripts`)
| Script | Job |
|---|---|
| `GameManager` | Title screen, game state, survivors, HUD, 3:00 countdown + best time, pause/win/lose |
| `LevelBuilder` | Builds the level from `Level1.txt`; BFS pathfinding for enemies |
| `PlayerSignal` | Movement, pulse, energy, health, proximity alarm |
| `Revealable` | Hides objects in the dark and fades them in when revealed |
| `EnemyBase` | Shared enemy code: path following, sight, hearing, contact damage |
| `Hunter` | Dormant until it hears a pulse, then rushes to it |
| `Drifter` | Wanders, chases on sight |
| `Survivor` | Waits in the dark, follows the player in a line, runs home if snatched |
| `EscapePod` | Safe drop-off point; alarm when the last survivor is found; launches when all 5 are inside |
| `HealthPack` / `PickupSpawner` | Medkits scattered in the dark (+15 health), respawning over time |
| `PulseRing`, `CameraFollow`, `AudioManager` | Visual/audio feedback, including the low-energy distortion hum |

See `CHANGES.txt` for differences from the original one-pager.
