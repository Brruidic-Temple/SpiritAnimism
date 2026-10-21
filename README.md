# SpiritAnimism

Valheim mod by Brruid. Changes how the animals summoned by the Spirit Caller staff look.

## Status

Alpha. Client-side only. A configurable tint colour per summon type (bear, moose, boar,
wolf), reloaded live when the config file changes, and a hotkey to dismiss your summons.

## Config

`BepInEx/config/Brruid.SpiritAnimism.cfg`, section `[Tint]`:

- `Bear`, `Moose`, `Boar`, `Wolf` - hex colour of that summon's spirit body (the
  `_TintColor` of its `Custom/Fallen Warrior` material). Default `40C8FF` (ice blue) for all.
  Vanilla brightness and transparency are kept; horns, tusks and saddle stay vanilla.

Section `[Dismiss]`:

- `Key` - hotkey that dismisses every spirit summon you called, using the vanilla despawn
  effect. Default `LeftAlt + X`. Ignored while the console, chat or menu is open.

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
