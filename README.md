# SpiritAnimism

Make the Spirit Caller's animals yours. Keep the ghostly look in a colour you choose, or call
real bears, boars, moose and wolves with a glow in your colour. Pick what the staff summons
next, watch your pack in a small panel, and send them all home with one key.

Client-side. Nothing to install on the server, and it plays fine next to people who do not
have it.

## Features

- **Spirit look** - the vanilla translucent animal, tinted any colour you like.
- **Natural look** - the wild animal's own solid body, with a glow in your colour. The moose
  can glow from its body, its eyes, or both.
- **Pick your summon** - secondary attack (middle mouse by default) with the staff in hand
  cycles which animal comes next. Animals already at your staff's limit are skipped, so a cast
  never sends your oldest one home.
- **Summon panel** - a top-left list of your animals with their health and what they are doing.
- **Dismiss key** - LeftAlt + X sends every summon you called home, with the usual despawn
  effect, including ones you left behind far away.
- **Live config** - edit the config file while playing and the change shows at once, even on
  animals already out.
- **Multiplayer aware** - other players with the mod see your look on your animals and their
  own on theirs. Players without the mod see vanilla.

## Install

Needs BepInEx. Unzip and put `SpiritAnimism.dll` in `BepInEx/plugins`.

## Config

The file is `BepInEx/config/Brruid.SpiritAnimism.cfg`. It is created on first launch and can
be edited while the game runs.

### One section per animal: `[Bear]`, `[Boar]`, `[Moose]`, `[Wolf]`

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

### `[Pick]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Order` | What the secondary attack cycles through. `Random` is the game's own roll. | `Random, Bear, Moose, Boar, Wolf` |
| `SkipFull` | Skip animals already at your staff's limit, when cycling and in the Random roll. | `true` |

The current pick is saved with your character. If PickYourSpirit is installed, this picker
turns itself off so the two do not fight.

### `[Panel]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Show` | Show the panel. It appears while the staff is in hand or a summon is out. | `true` |
| `OffsetX` | Distance from the left edge of the screen. | `20` |
| `OffsetY` | Distance from the top edge of the screen. | `220` |
| `Scale` | Size of the panel, 0.5 to 3. | `1` |

### `[Dismiss]`

| Setting | What it does | Default |
| --- | --- | --- |
| `Key` | Hotkey that dismisses every summon you called. Ignored while the console, chat or menu is open. | `X + LeftAlt` |

## Source

Source and build instructions: https://github.com/Brruidic-Temple/SpiritAnimism
