using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SpiritAnimism
{
    // Which animal the Spirit Caller summons next. Secondary attack with the staff in hand cycles
    // through the configured order; the pick is remembered with the character.
    internal static class Picker
    {
        public const string Random = "Random";
        private const string StaffItemName = "$item_staff_spiritcaller";
        private const string StaffPrefabName = "StaffSpiritCaller";
        private const string OtherPickerGuid = "algo7.pickyourspirit";
        private const string PickKey = "SpiritAnimism.Pick";

        internal static ConfigEntry<string> Order;
        internal static ConfigEntry<bool> SkipFull;

        private static bool _disabled;

        public static void Init(ConfigFile config)
        {
            Order = config.Bind(
                "Pick",
                "Order",
                "Random, Bear, Moose, Boar, Wolf",
                "What the secondary attack cycles through with the Spirit Caller in hand, in this order. Random is the game's own roll. The current pick is remembered with your character.");
            SkipFull = config.Bind(
                "Pick",
                "SkipFull",
                true,
                "Skip animals you already have at your staff's limit, both when cycling and in the Random roll, so a cast never sends your oldest one home. When every kind is full the game's own roll applies.");

            if (Chainloader.PluginInfos.ContainsKey(OtherPickerGuid))
            {
                _disabled = true;
                Plugin.Log.LogWarning("PickYourSpirit is loaded; SpiritAnimism's own picker is switched off to avoid fighting over the cast");
            }
        }

        public static bool Enabled => !_disabled;

        public static bool IsSpiritCaller(ItemDrop.ItemData item)
        {
            if (item == null) return false;
            if (item.m_shared != null && item.m_shared.m_name == StaffItemName) return true;
            return item.m_dropPrefab != null && item.m_dropPrefab.name == StaffPrefabName;
        }

        // The picked summon type, or null for Random. The pick lives in the character's custom data,
        // which the game saves with the character.
        public static SummonType CurrentType()
        {
            var player = Player.m_localPlayer;
            return player != null && player.m_customData.TryGetValue(PickKey, out var name) ? Summons.Named(name) : null;
        }

        public static void Cycle()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var steps = Steps();
            if (steps.Count == 0) return;

            var current = CurrentType();
            var currentName = current != null ? current.Name : Random;
            var index = steps.FindIndex(step => string.Equals(step, currentName, StringComparison.OrdinalIgnoreCase));
            for (var attempt = 1; attempt <= steps.Count; attempt++)
            {
                var type = Summons.Named(steps[(index + attempt) % steps.Count]);
                if (type != null && SkipFull.Value && IsAtCap(type)) continue;
                player.m_customData[PickKey] = type != null ? type.Name : Random;
                break;
            }

            player.Message(MessageHud.MessageType.Center, $"Spirit: {DisplayName(CurrentType())}");
        }

        // The animal's name in the player's language, from the wild creature (the summons' own names are blank).
        public static string DisplayName(SummonType type)
        {
            if (type == null) return Random;
            var wild = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(type.WildPrefabName) : null;
            var character = wild != null ? wild.GetComponent<Character>() : null;
            if (character == null || string.IsNullOrEmpty(character.m_name)) return type.Name;
            return Localization.instance.Localize(character.m_name);
        }

        // Whether the player's summons of this kind fill the staff's limit. The limit is read from
        // the summons themselves: the game writes it into each one's network data when it spawns.
        public static bool IsAtCap(SummonType type)
        {
            var count = 0;
            var max = 0;
            foreach (var summon in Summons.Mine())
            {
                if (summon.Species != type) continue;
                count++;
                if (max == 0) max = summon.Zdo.GetInt(ZDOVars.s_maxInstances, 0);
            }
            return max > 0 && count >= max;
        }

        // For a Random cast: the staff's spawn list without the kinds already at the limit, so the
        // game's roll lands on something with room. If nothing has room, the vanilla list stands.
        public static GameObject[] WithRoom(GameObject[] spawnList)
        {
            var withRoom = new List<GameObject>();
            foreach (var prefab in spawnList)
            {
                if (prefab == null) continue;
                var type = Summons.SpeciesOf(prefab.name);
                if (type == null || !IsAtCap(type)) withRoom.Add(prefab);
            }
            return withRoom.Count > 0 && withRoom.Count < spawnList.Length ? withRoom.ToArray() : spawnList;
        }

        private static List<string> Steps()
        {
            var steps = new List<string>();
            foreach (var raw in Order.Value.Split(','))
            {
                var step = raw.Trim();
                if (step.Length == 0) continue;
                if (!string.Equals(step, Random, StringComparison.OrdinalIgnoreCase) && Summons.Named(step) == null)
                {
                    Plugin.Log.LogWarning($"Pick order entry '{step}' is not Random, Bear, Moose, Boar or Wolf; ignored");
                    continue;
                }
                steps.Add(step);
            }
            return steps;
        }
    }

    // The cast rolls one of the staff's spawn prefabs at random inside Setup; giving this cast's own
    // spawn component a one-entry list makes the roll land on the pick. Other casters and other
    // staffs are untouched.
    [HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.Setup))]
    internal static class SpawnAbilitySetupPatch
    {
        private static void Prefix(SpawnAbility __instance, Character owner, ItemDrop.ItemData item)
        {
            if (!Picker.Enabled || owner == null || owner != Player.m_localPlayer || !Picker.IsSpiritCaller(item)) return;
            if (__instance.m_spawnPrefab == null) return;

            var type = Picker.CurrentType();
            if (type == null)
            {
                if (Picker.SkipFull.Value) __instance.m_spawnPrefab = Picker.WithRoom(__instance.m_spawnPrefab);
                return;
            }

            foreach (var prefab in __instance.m_spawnPrefab)
            {
                if (prefab == null || prefab.name != type.PrefabName) continue;
                __instance.m_spawnPrefab = new[] { prefab };
                return;
            }
            Plugin.Log.LogWarning($"{type.PrefabName} is not in the staff's spawn list; this cast stays random");
        }
    }

    // The secondary-attack press, after the game has applied its own rules for when input counts
    // (menus, chat, map). The Spirit Caller has no secondary attack, so the press is otherwise unused.
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class PlayerSetControlsPatch
    {
        private static void Postfix(Player __instance, bool secondaryAttack)
        {
            if (!secondaryAttack || !Picker.Enabled || __instance != Player.m_localPlayer) return;
            if (!Picker.IsSpiritCaller(__instance.GetCurrentWeapon())) return;
            Picker.Cycle();
        }
    }
}
