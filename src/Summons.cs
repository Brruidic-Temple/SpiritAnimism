using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace SpiritAnimism
{
    internal enum Look
    {
        Spirit,
        Natural,
    }

    internal enum GlowArea
    {
        Body,
        Eyes,
    }

    // One kind of Spirit Caller summon: its prefabs, its config entries and the shared assets its
    // looks are built from.
    internal sealed class SummonType
    {
        public readonly string Name;
        public readonly string PrefabName;
        public readonly string WildPrefabName;
        // Only the moose carries an emission mask that marks its eyes, so only it gets a GlowArea entry.
        public readonly bool HasEyeMask;

        public ConfigEntry<Look> Look;
        public ConfigEntry<Color> Tint;
        public ConfigEntry<float> Glow;
        public ConfigEntry<GlowArea> Area;

        // Shared assets, looked up once: the summon's spirit body material and the wild animal's body material.
        public Material SpiritMaterial;
        public Color VanillaTint;
        public Material WildMaterial;
        public Texture VanillaEmissionMap;
        public Color VanillaEmissionColor;
        public bool AssetsLookedUp;
        public bool WarnedNoEyeMask;

        public SummonType(string name, string wildPrefabName, bool hasEyeMask = false)
        {
            Name = name;
            WildPrefabName = wildPrefabName;
            PrefabName = wildPrefabName + "_spiritcaller";
            HasEyeMask = hasEyeMask;
        }

        // The spirit body material is named after the wild prefab: <wild>_mat_spiritcaller.
        public bool OwnsMaterial(Material material) =>
            material.name.StartsWith(WildPrefabName + "_", StringComparison.OrdinalIgnoreCase);
    }

    // A summon standing in the loaded world, with the network view every feature reads it through.
    internal readonly struct Summon
    {
        public readonly Character Character;
        public readonly SummonType Species;
        public readonly ZNetView View;

        public Summon(Character character, SummonType species, ZNetView view)
        {
            Character = character;
            Species = species;
            View = view;
        }

        public ZDO Zdo => View.GetZDO();

        // The game marks each summon with its summoner's player name.
        public bool CalledBy(string playerName) => Zdo.GetString(ZDOVars.s_follow, "") == playerName;
    }

    internal static class Summons
    {
        internal static readonly SummonType[] Types =
        {
            new SummonType("Bear", "Bjorn"),
            new SummonType("Moose", "Moose", hasEyeMask: true),
            new SummonType("Boar", "Boar"),
            new SummonType("Wolf", "Wolf"),
        };

        // The type of a summon prefab, or of a spawned instance (named after the prefab plus "(Clone)").
        public static SummonType SpeciesOf(string objectName)
        {
            foreach (var type in Types)
            {
                if (objectName.StartsWith(type.PrefabName, StringComparison.Ordinal)) return type;
            }
            return null;
        }

        // The type the config names, in any letter case; null for anything else.
        public static SummonType Named(string name)
        {
            foreach (var type in Types)
            {
                if (string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase)) return type;
            }
            return null;
        }

        // Every summon that is loaded and networked right now.
        public static IEnumerable<Summon> Standing()
        {
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null) continue;
                var species = SpeciesOf(character.gameObject.name);
                if (species == null) continue;
                var view = character.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) continue;
                yield return new Summon(character, species, view);
            }
        }

        // The standing summons the local player called.
        public static IEnumerable<Summon> Mine()
        {
            var player = Player.m_localPlayer;
            if (player == null) yield break;
            var playerName = player.GetPlayerName();
            foreach (var summon in Standing())
            {
                if (summon.CalledBy(playerName)) yield return summon;
            }
        }
    }
}
