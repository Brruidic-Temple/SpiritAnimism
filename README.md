# SpiritAnimism

Valheim mod by Brruid. Changes how the animals summoned by the Spirit Caller staff look.

## Status

Alpha. Client-side only. A configurable tint colour per summon type (bear, moose, boar,
wolf), reloaded live when the config file changes.

## Config

`BepInEx/config/Brruid.SpiritAnimism.cfg`, section `[Tint]`:

- `Bear`, `Moose`, `Boar`, `Wolf` - hex colour of that summon's spirit body (the
  `_TintColor` of its `Custom/Fallen Warrior` material). Default `40C8FF` (ice blue) for all.
  Vanilla brightness and transparency are kept; horns, tusks and saddle stay vanilla.
- Edits to the file are picked up while the game runs, including on summons already out.

## Building

Requires the .NET SDK, a Valheim install, and a BepInEx 5 install (a Gale profile).
Paths default to the author's machine; override with `-p:ValheimManaged=...`,
`-p:GaleProfile=...` or `-p:PluginDeployDir=...`.

    dotnet build -c Release

The build copies the DLL into the Gale profile's plugins folder. Pass `-p:NoDeploy=true` to skip.
