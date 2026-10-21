# SpiritAnimism

Valheim mod by Brruid. Changes how the animals summoned by the Spirit Caller staff look.

## Status

Alpha. Client-side only. One configurable tint colour applied to all four summons
(bear, moose, boar, wolf), reloaded live when the config file changes.

## Config

`BepInEx/config/Brruid.SpiritAnimism.cfg`

- `Tint` - hex colour (RGB) applied to the summons. Default `40C8FF` (ice blue).
  Vanilla transparency is kept.

## Building

Requires the .NET SDK, a Valheim install, and a BepInEx 5 install (a Gale profile).
Paths default to the author's machine; override with `-p:ValheimManaged=...`,
`-p:GaleProfile=...` or `-p:PluginDeployDir=...`.

    dotnet build -c Release

The build copies the DLL into the Gale profile's plugins folder. Pass `-p:NoDeploy=true` to skip.
