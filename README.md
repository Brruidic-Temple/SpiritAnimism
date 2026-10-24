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
- **Saddles come back** - a saddle on a dismissed or unsummoned animal returns to whoever
  fitted it, or to the summoner, instead of vanishing with the animal.
- **Formations** - your summons follow in an arc behind you, a ring around you, or a shape
  you draw yourself, instead of piling onto your position. Stand still a moment and they
  settle, so you can walk up to one and interact. Animals keep a buffer of air between them
  and steer around each other, you and your mount instead of shoving through.
- **Live config** - edit the config file while playing and the change shows at once, even on
  animals already out.
- **Multiplayer aware** - other players with the mod see your look on your animals and their
  own on theirs. Players without the mod see vanilla.

## Install

Needs BepInEx. Unzip and put `SpiritAnimism.dll` in `BepInEx/plugins`.

## Config

The file is `BepInEx/config/Brruid.SpiritAnimism.cfg`, created on first launch, with every
setting explained inside it. Edit it while the game runs and the change applies at once. Your
own formations go in `Brruid.SpiritAnimism.Formations.cfg` beside it, created with a worked
example.

Every setting, with defaults and examples:
https://github.com/Brruidic-Temple/SpiritAnimism/blob/main/docs/CONFIG.md

## Source

Source and build instructions: https://github.com/Brruidic-Temple/SpiritAnimism
