# Building SpiritAnimism

Requires the .NET SDK, a Valheim install, and a BepInEx 5 install (a Gale profile).
Paths default to the author's machine; override with `-p:ValheimManaged=...`,
`-p:GaleProfile=...` or `-p:PluginDeployDir=...`.

    dotnet build -c Release

The build copies the DLL into the Gale profile's plugins folder. Pass `-p:NoDeploy=true` to skip.
A Debug build (`dotnet build`) adds a `[Debug]` config section: `DumpLiveSummons` writes every
standing summon's visual setup to the log, and `DumpKey` (default `LeftAlt + D`) writes every
summon's network state, loaded or not.

    dotnet build -c Release -t:Package

Builds the Thunderstore zip into `dist/`. Needs `package/icon.png` (256x256).

## Engine facts behind the limitations

- Eye glow is only controllable on the moose. Measured from the game's own emission masks: the
  moose mask lights a tiny patch at full strength (the eyes); the bear mask lights a small patch
  at half strength that is not visible on the model; the boar has no mask; the wolf's eye glow is
  baked into its texture and always on.
- Opacity is not adjustable. The vanilla spirit shader (`Custom/Fallen Warrior`) exposes no alpha
  or blend settings; its `_Cutoff` behaves as a hard visible/invisible threshold (measured:
  unchanged up to 0.62, gone from 0.63). Making summons more solid needs a different body material.
