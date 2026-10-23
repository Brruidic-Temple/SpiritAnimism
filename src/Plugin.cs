using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SpiritAnimism
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "Brruid.SpiritAnimism";
        public const string Name = "SpiritAnimism";
        public const string Version = "0.4.1";

        internal static ManualLogSource Log;
        internal static ConfigFile Settings;
        internal static ConfigEntry<KeyboardShortcut> DismissKey;

        // BepInEx does not watch config files, so poll the file's timestamp to reload edits made while playing.
        private const float ConfigPollSeconds = 1f;
        private float _nextConfigPoll;
        private DateTime _configLastWrite;

        private void Awake()
        {
            Log = Logger;
            Settings = Config;
            // Patched before binding, so the config file saved by the binds is already in section order.
            new Harmony(Guid).PatchAll();

            BindLooks();
            DismissKey = Config.Bind(
                "Dismiss",
                "Key",
                new KeyboardShortcut(KeyCode.X, KeyCode.LeftAlt),
                "Dismisses all spirit summons you called, with the vanilla despawn effect.");
            Picker.Init(Config);
            SummonPanel.Init(Config);
#if DEBUG
            DebugDump.Init(Config);
#endif
            _configLastWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);

            Log.LogInfo($"{Name} {Version} loaded");
        }

        // One config section per animal, holding everything about how it looks. The settings are
        // explained once, under the first animal, which is also the first section in the file.
        private void BindLooks()
        {
            var iceBlue = new Color32(0x40, 0xC8, 0xFF, 0xFF);
            foreach (var type in Summons.Types)
            {
                var explain = type == Summons.Types[0];
                type.Look = Config.Bind(type.Name, "Look", Look.Spirit, explain
                    ? "Spirit = the vanilla translucent ghost in your tint. Natural = the wild animal's own opaque body with a glow in your tint. Every animal's section takes the same settings."
                    : "");
                type.Tint = Config.Bind(type.Name, "Tint", (Color)iceBlue, explain
                    ? "Body colour of the Spirit look, glow colour of the Natural look. Hex RRGGBBAA; alpha is ignored."
                    : "");
                type.Glow = Config.Bind(type.Name, "Glow", 0.3f, new ConfigDescription(
                    explain ? "Body glow strength of the Natural look. 0 = plain wild animal." : "",
                    new AcceptableValueRange<float>(0f, LookSpec.MaxGlow)));
                if (type.HasEyeMask)
                {
                    type.EyeGlow = Config.Bind(type.Name, "EyeGlow", 0f, new ConfigDescription(
                        "Eye glow strength of the Natural look, drawn from the moose's own eye mask. Combine with Glow: body only, eyes only, or both at their own strengths.",
                        new AcceptableValueRange<float>(0f, LookSpec.MaxGlow)));
                }
            }
        }

        private void Update()
        {
            SummonLooks.Update();
            SummonPanel.Update();
            if (DismissKey.Value.IsDown() && Dismiss.InputIsFree()) Dismiss.MySummons();
#if DEBUG
            DebugDump.Update();
#endif
            ReloadConfigIfEdited();
        }

        private void ReloadConfigIfEdited()
        {
            if (Time.unscaledTime < _nextConfigPoll) return;
            _nextConfigPoll = Time.unscaledTime + ConfigPollSeconds;

            var path = Config.ConfigFilePath;
            if (!File.Exists(path))
            {
                Config.Save();
                _configLastWrite = File.GetLastWriteTimeUtc(path);
                Log.LogInfo("Config file recreated with the current settings");
                return;
            }
            if (File.GetLastWriteTimeUtc(path) == _configLastWrite) return;

            try
            {
                Config.Reload();
            }
            catch (IOException e)
            {
                // The editor may still hold the file; the next poll tries again.
                Log.LogWarning($"Config not reloaded yet: {e.Message}");
                return;
            }
            // Read after the reload, which may itself rewrite the file.
            _configLastWrite = File.GetLastWriteTimeUtc(path);
            Log.LogInfo("Config reloaded");
        }
    }

    // BepInEx saves the config file with its sections in alphabetical order. This rewrites the file
    // it has just saved with the sections in the order the plugin binds them: the animals, then
    // Dismiss, Pick and Panel. Sections the plugin does not know keep their place at the end.
    [HarmonyPatch(typeof(ConfigFile), nameof(ConfigFile.Save))]
    internal static class ConfigFileSavePatch
    {
        private static void Postfix(ConfigFile __instance)
        {
            if (__instance != Plugin.Settings) return;

            var order = new List<string>();
            foreach (var definition in __instance.Keys)
            {
                if (!order.Contains(definition.Section)) order.Add(definition.Section);
            }

            // Everything before the first section header is the file header; each header starts a block.
            var header = new List<string>();
            var blocks = new Dictionary<string, List<string>>();
            var block = header;
            foreach (var line in File.ReadAllLines(__instance.ConfigFilePath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var section = trimmed.Substring(1, trimmed.Length - 2);
                    if (!blocks.TryGetValue(section, out block))
                    {
                        block = new List<string>();
                        blocks[section] = block;
                    }
                    if (!order.Contains(section)) order.Add(section);
                }
                block.Add(line);
            }

            var ordered = new List<string>(header);
            foreach (var section in order)
            {
                if (blocks.TryGetValue(section, out var lines)) ordered.AddRange(lines);
            }
            File.WriteAllLines(__instance.ConfigFilePath, ordered, Utility.UTF8NoBom);
        }
    }
}
