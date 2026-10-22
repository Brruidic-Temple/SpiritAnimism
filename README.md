# SpiritAnimism

Valheim mod by Brruid. Changes how the animals summoned by the Spirit Caller staff look.

## Status

Pre-release. Client-side; nothing to install on the server. Per summon type (bear, moose,
boar, wolf): a Spirit or Natural look, a tint colour and a glow, reloaded live when the config
file changes. A picker chooses what the staff summons next, a panel lists your summons, and a
hotkey dismisses them.

Your chosen look travels with each summon you call: other players running the mod see your
look on your animals and theirs on their own. Summons called by players without the mod stay
vanilla. Players without the mod see everything as vanilla.

## Config

`BepInEx/config/Brruid.SpiritAnimism.cfg`. Every setting is per summon type, with keys `Bear`,
`Moose`, `Boar`, `Wolf`.

- `[Look]` - `Spirit` (vanilla translucent ghost in your tint) or `Natural` (the wild animal's own
  opaque body, borrowed from the wild creature at runtime, with a glow in your tint).
- `[Tint]` - hex colour. Spirit look: the body colour, vanilla brightness and transparency kept.
  Natural look: the glow colour. Default `40C8FF` (ice blue).
- `[Glow]` - glow strength for the Natural look, 0 to 5. 0 is a plain wild animal. Default 0.3.
- `[GlowArea]` - `Body` (whole body) or `Eyes` (the animal's own eye mask, in your tint). Only the
  moose has a usable eye mask. Wolf eyes glow by themselves, from the wolf's texture. The bear's
  mask marks a hidden spot and the boar has none, so `Eyes` lights nothing visible on them.
- `[Pick]` - with the Spirit Caller in hand, the secondary attack (middle mouse by default) cycles
  which animal the next cast summons; a centre message names it. `Order` is the cycle (default
  `Random, Bear, Moose, Boar, Wolf`), `Current` is the remembered pick, and `SkipFull` skips kinds
  already at your staff's limit, both when cycling and in the Random roll. If PickYourSpirit is also installed, this picker switches itself off.
- `[Panel]` - a top-left list of your standing summons: header with the count and the current pick,
  then each summon's name, what it is doing, and a health bar. Shown while the Spirit Caller is in
  hand or a summon is out. `Show`, `OffsetX` (20), `OffsetY` (220) and `Scale` (1).
- `[Dismiss]` `Key` - hotkey that dismisses every spirit summon you called, with the vanilla
  despawn effect. Default `LeftAlt + X`. Ignored while the console, chat or menu is open.

Edits to the file are picked up while the game runs, including on summons already out.

## Known limitations

- Eye glow is only controllable on the moose. Measured from the game's own masks: the moose mask
  lights a tiny patch at full strength (the eyes); the bear mask lights a small patch at half
  strength that is not visible on the model; the boar has no mask; the wolf's eye glow is baked
  into its texture and always on.

- Opacity is not adjustable. The vanilla spirit shader (`Custom/Fallen Warrior`) exposes no alpha
  or blend settings; its `_Cutoff` behaves as a hard visible/invisible threshold (measured: unchanged
  up to 0.62, gone from 0.63). Making summons more solid needs a different body material.

## Building

Requires the .NET SDK, a Valheim install, and a BepInEx 5 install (a Gale profile).
Paths default to the author's machine; override with `-p:ValheimManaged=...`,
`-p:GaleProfile=...` or `-p:PluginDeployDir=...`.

    dotnet build -c Release

The build copies the DLL into the Gale profile's plugins folder. Pass `-p:NoDeploy=true` to skip.

    dotnet build -c Release -t:Package

Builds the Thunderstore zip into `dist/`. Needs `package/icon.png` (256x256).

## License

MIT. See LICENSE.
