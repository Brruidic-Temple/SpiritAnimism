# Changelog

## 0.5.0 (unreleased)

- Formations: your summons follow in an arrangement around you instead of crowding your position. Built-in Leader (arc behind you), Defense (rings) and Bubble (one widening ring), plus your own formations in `Brruid.SpiritAnimism.Formations.cfg`, with slots that can be reserved for a kind. Stand still for a moment and the formation settles so you can walk up to an animal; a summon you face from close by holds still.
- Summons steer around other animals, players and mounts instead of walking into them, by the `Buffer` width.
- Config: new `[Formations]` section with `Formation`, `Distance` and `Buffer`. The formation's name travels with your summons to other players' machines; an unknown name there means Leader.
- A saddle no longer vanishes with a dismissed or unsummoned animal. It returns to the player who fitted it, failing that to the summoner, failing that it drops where the animal stood. Both players need the mod for a saddle to cross between them.
- `DismissWhileRiding` (default on) decides whether Dismiss also removes the animal you are riding.

## 0.4.1

- README rewritten for the Thunderstore page. Build notes moved to BUILDING.md. No gameplay changes.

## 0.4.0

- Natural look: the wild animal's own body with a glow in your tint; the moose's body and eyes glow at their own strengths.
- Picker: secondary attack with the Spirit Caller cycles what it summons next; cap-aware; remembered.
- Panel: top-left list of your summons with the current pick, activity and health.
- Your look travels with your summons to other players running the mod.
- Config: one section per animal; the EyeGlow strength exists only on the moose, the one animal it affects.
- Config file keeps the animals first, then Dismiss, Pick and Panel, instead of BepInEx's alphabetical order.
- Dismiss also reaches summons left standing far away that nobody owns, which the game's own unsummon cannot remove.

## 0.3.0

- Dismiss hotkey (default LeftAlt + X) for your own summons.

## 0.2.0

- A tint per summon type.

## 0.1.0

- Configurable spirit tint with live reload.
