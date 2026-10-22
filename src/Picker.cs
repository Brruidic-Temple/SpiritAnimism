using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SpiritAnimism
{
    // Which animal the Spirit Caller summons next. Secondary attack with the staff in hand cycles
    // through the configured order; the pick is remembered in the config file.
    internal static class Picker
    {
        public const string Random = "Random";
        private const string StaffItemName = "$item_staff_spiritcaller";
        private const string StaffPrefabName = "StaffSpiritCaller";
        private const string OtherPickerGuid = "algo7.pickyourspirit";

        internal static ConfigEntry<string> Order;
        internal static ConfigEntry<string> Current;
        internal static ConfigEntry<bool> SkipFull;

        private static bool _disabled;

        public static void Init(ConfigFile config)
        {
            Order = config.Bind(
                "Pick",
                "Order",
                "Random, Bear, Moose, Boar, Wolf",
                "What the secondary attack cycles through with the Spirit Caller in hand, in this order. Random is the game's own roll.");
            Current = config.Bind(
                "Pick",
                "Current",
                Random,
                "What the Spirit Caller summons next: Random, Bear, Moose, Boar or Wolf. Changed in game with the secondary attack; remembered here.");
            SkipFull = config.Bind(
                "Pick",
                "SkipFull",
                true,
                "Skip animals you already have at your staff's limit when cycling, so a pick never sends your oldest one home.");

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

        // The picked summon type, or null for Random (also for anything unrecognised in the config).
        public static SummonType CurrentType() => TypeNamed(Current.Value);

        public static void Cycle()
        {
            var steps = Steps();
            if (steps.Count == 0) return;

            var index = steps.FindIndex(step => string.Equals(step, Current.Value, StringComparison.OrdinalIgnoreCase));
            for (var attempt = 1; attempt <= steps.Count; attempt++)
            {
                var candidate = steps[(index + attempt) % steps.Count];
                var type = TypeNamed(candidate);
                if (type != null && SkipFull.Value && IsAtCap(type)) continue;
                Current.Value = type != null ? type.ConfigKey : Random;
                break;
            }

            ShowCurrent();
        }

        public static void ShowCurrent()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            player.Message(MessageHud.MessageType.Center, $"Spirit: {DisplayName(CurrentType())}");
        }

        // The animal's name in the player's language, from the wild creature (the summons' own names are blank).
        public static string DisplayName(SummonType type)
        {
            if (type == null) return Random;
            var wild = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(type.WildPrefabName) : null;
            var character = wild != null ? wild.GetComponent<Character>() : null;
            if (character == null || string.IsNullOrEmpty(character.m_name)) return type.ConfigKey;
            return Localization.instance.Localize(character.m_name);
        }

        // Owned summons of this kind, and whether they fill the staff's limit. The limit is read from
        // the summons themselves: the game writes it into each one's network data when it spawns.
        public static bool IsAtCap(SummonType type)
        {
            var player = Player.m_localPlayer;
            if (player == null) return false;
            var playerName = player.GetPlayerName();
            var count = 0;
            var max = 0;

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || !character.gameObject.name.StartsWith(type.PrefabName)) continue;
                var nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;
                var zdo = nview.GetZDO();
                if (zdo.GetString(ZDOVars.s_follow, "") != playerName) continue;
                count++;
                if (max == 0) max = zdo.GetInt(ZDOVars.s_maxInstances, 0);
            }

            return max > 0 && count >= max;
        }

        private static List<string> Steps()
        {
            var steps = new List<string>();
            foreach (var raw in Order.Value.Split(','))
            {
                var step = raw.Trim();
                if (step.Length == 0) continue;
                if (!string.Equals(step, Random, StringComparison.OrdinalIgnoreCase) && TypeNamed(step) == null)
                {
                    Plugin.Log.LogWarning($"Pick order entry '{step}' is not Random, Bear, Moose, Boar or Wolf; ignored");
                    continue;
                }
                steps.Add(step);
            }
            return steps;
        }

        private static SummonType TypeNamed(string name)
        {
            foreach (var summon in SummonLooks.Summons)
            {
                if (string.Equals(summon.ConfigKey, name, StringComparison.OrdinalIgnoreCase)) return summon;
            }
            return null;
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
            var type = Picker.CurrentType();
            if (type == null || __instance.m_spawnPrefab == null) return;

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
