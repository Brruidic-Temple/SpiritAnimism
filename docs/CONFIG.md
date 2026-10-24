# SpiritAnimism settings

The file is `BepInEx/config/Brruid.SpiritAnimism.cfg`. It is created on first launch and can
be edited while the game runs.

## One section per animal: `[Bear]`, `[Boar]`, `[Moose]`, `[Wolf]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Look` | `Spirit` for the vanilla ghost in your tint, `Natural` for the wild animal's own body with a glow in your tint. | `Spirit` |
| `Tint` | The colour, as hex `RRGGBBAA`. Alpha is ignored. Body colour of the Spirit look, glow colour of the Natural look. | `40C8FFFF` (ice blue) |
| `Glow` | Body glow strength of the Natural look, 0 to 5. 0 is a plain wild animal. | `0.3` |
| `EyeGlow` | Moose only. Eye glow strength of the Natural look, 0 to 5. Combine with `Glow` for body only, eyes only, or both. | `0` |

Example - a real wolf with faintly glowing red fur:

    [Wolf]
    Look = Natural
    Tint = FF3030FF
    Glow = 0.2

## `[Pick]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Order` | What the secondary attack cycles through. `Random` is the game's own roll. | `Random, Bear, Moose, Boar, Wolf` |
| `SkipFull` | Skip animals already at your staff's limit, when cycling and in the Random roll. | `true` |

The current pick is saved with your character. If PickYourSpirit is installed, this picker
turns itself off so the two do not fight.

## `[Panel]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Show` | Show the panel. It appears while the staff is in hand or a summon is out. | `true` |
| `OffsetX` | Distance from the left edge of the screen. | `20` |
| `OffsetY` | Distance from the top edge of the screen. | `220` |
| `Scale` | Size of the panel, 0.5 to 3. | `1` |

## `[Dismiss]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Key` | Hotkey that dismisses every summon you called. Ignored while the console, chat or menu is open. | `X + LeftAlt` |
| `ReturnSaddle` | Give a saddle back when its animal is dismissed or unsummoned: to the player who fitted it, failing that the summoner, failing that dropped where the animal stood. Off = lost, as in the unmodded game. | `true` |
| `DismissWhileRiding` | Whether Dismiss also removes the animal you are riding. Off keeps your mount and dismisses the rest. | `true` |

## `[Formations]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Formation` | `Leader` = an arc behind you, further rows back when full. `Defense` = a ring around you, further rings out when full. `Bubble` = one ring that widens to fit everyone. Or the name of a formation from your formations file. | `Leader` |
| `Distance` | How far the nearest row or ring stands from you, in metres, 3 to 20. | `5` |
| `Buffer` | Air between animals, in metres, 0 to 5: the gap between spots in formation, and how wide a walking animal steers around anyone in its way, you and your mount included. `0` = bodies touch and nobody steers, as in the unmodded game. | `1` |

Your own formations go in `Brruid.SpiritAnimism.Formations.cfg`, created beside the config
with a worked example. Each `[Name]` is a formation; each `SlotN = right, forward` line is a
spot in metres from you, negative for left or behind, and `SlotN = Wolf, 3, -2` reserves it
for one kind. Summons beyond the listed slots form a Leader arc. Both files reload while you
play. Other players' machines lay out your summons by the formation's name, so a formation of
your own shows as Leader on a machine that doesn't have it.
