using System;
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
        public const string Version = "0.4.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> DismissKey;

        // BepInEx does not watch config files, so poll the file's timestamp to reload edits made while playing.
        private const float ConfigPollSeconds = 1f;
        private float _nextConfigPoll;
        private DateTime _configLastWrite;

        private void Awake()
        {
            Log = Logger;
            BindLooks();
            Picker.Init(Config);
            SummonPanel.Init(Config);
            DismissKey = Config.Bind(
                "Dismiss",
                "Key",
                new KeyboardShortcut(KeyCode.X, KeyCode.LeftAlt),
                "Dismisses all spirit summons you called, with the vanilla despawn effect.");
#if DEBUG
            DebugDump.Init(Config);
#endif
            _configLastWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);

            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded");
        }

        // One config section per animal, holding everything about how it looks. The settings are
        // explained once, under the first animal; BepInEx writes sections alphabetically, so that
        // section is also the first one in the file.
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
                    explain ? "Glow strength of the Natural look. 0 = plain wild animal." : "",
                    new AcceptableValueRange<float>(0f, LookSpec.MaxGlow)));
                if (type.HasEyeMask)
                {
                    type.Area = Config.Bind(type.Name, "GlowArea", GlowArea.Body,
                        "Where the Natural look glows: the whole body, or only the eyes.");
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
}
