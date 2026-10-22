# SpiritAnimism

Valheim mod by Brruid. Changes how the animals summoned by the Spirit Caller staff look.

## Status

Pre-release. Client-side only. Per summon type (bear, moose, boar, wolf): a Spirit or Natural
look, a tint colour and a glow, reloaded live when the config file changes. A hotkey dismisses
your summons.

## Config

`BepInEx/config/Brruid.SpiritAnimism.cfg`. Every setting is per summon type, with keys `Bear`,
`Moose`, `Boar`, `Wolf`.

- `[Look]` - `Spirit` (vanilla translucent ghost in your tint) or `Natural` (the wild animal's own
  opaque body, borrowed from the wild creature at runtime, with a glow in your tint).
- `[Tint]` - hex colour. Spirit look: the body colour, vanilla brightness and transparency kept.
  Natural look: the glow colour. Default `40C8FF` (ice blue).
- `[Glow]` - glow strength for the Natural look, 0 to 5. 0 is a plain wild animal. Default 0.3.
- `[GlowArea]` - `Body` (whole body) or `Eyes` (the animal's own eye mask, in your tint). Bear and
  moose have an eye mask. Wolf eyes glow by themselves, from the wolf's texture. Boar has no eye
  mask, so `Eyes` lights nothing on it.
- `[Dismiss]` `Key` - hotkey that dismisses every spirit summon you called, with the vanilla
  despawn effect. Default `LeftAlt + X`. Ignored while the console, chat or menu is open.

Edits to the file are picked up while the game runs, including on summons already out.

## Known limitations

- Opacity is not adjustable. The vanilla spirit shader (`Custom/Fallen Warrior`) exposes no alpha
  or blend settings; its `_Cutoff` behaves as a hard visible/invisible threshold (measured: unchanged
  up to 0.62, gone from 0.63). Making summons more solid needs a different body material.

## Building

Requires the .NET SDK, a Valheim install, and a BepInEx 5 install (a Gale profile).
Paths default to the author's machine; override with `-p:ValheimManaged=...`,
`-p:GaleProfile=...` or `-p:PluginDeployDir=...`.

    dotnet build -c Release

The build copies the DLL into the Gale profile's plugins folder. Pass `-p:NoDeploy=true` to skip.
