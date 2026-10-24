---
status: accepted
---

# Saddle returns through the fitter chain, with a reply wait

When a saddled summon is unsummoned (by the Dismiss key or by the game's own distance, logout or cap rules), the saddle used to vanish with it. Only a player's own game can put an item into that player's inventory, and the game running the summon is often a different player's, so the game running the summon asks the Fitter's game to take the saddle, waits about two seconds for a reply, then asks the Summoner's game the same way, and if nobody answers it drops the saddle where the summon stood. The Fitter's name is stored on the summon when the saddle goes on, since the game itself records only that a saddle is present.

## Considered Options

- Always drop the saddle on the ground: simple, but the saddle is lost in practice when the summon is far away.
- Put the saddle straight into the recipient's inventory from the summon's game: not possible, the game only lets a client change its own player's inventory.
- Drop nearby, return to inventory when far, by a configurable distance: rejected as needless; the summoner wants the saddle back regardless.

## Consequences

- A player without the mod cannot receive a saddle; the reply wait is what moves it on to the next in the chain instead of losing it.
- Summons saddled before this version carry no Fitter name and fall straight through to the Summoner.
