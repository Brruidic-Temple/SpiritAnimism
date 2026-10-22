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
        internal static ConfigEntry<bool> DumpLiveSummons;

        // BepInEx does not watch config files, so poll the file's timestamp to reload edits made while playing.
        private const float ConfigPollSeconds = 1f;
        private float _nextConfigPoll;
        private System.DateTime _configLastWrite;

        private void Awake()
        {
            Log = Logger;
            var iceBlue = new Color(0.25f, 0.78f, 1f);
            foreach (var summon in SummonLooks.Summons)
            {
                var animal = summon.ConfigKey.ToLower();

                summon.Look = Config.Bind(
                    "Look",
                    summon.ConfigKey,
                    Look.Spirit,
                    $"How the summoned {animal} looks. Spirit = vanilla translucent ghost in your tint. Natural = the wild animal's own opaque body with a glow in your tint.");
                summon.Look.SettingChanged += (_, _) => SummonLooks.ApplyEverywhere();

                summon.Tint = Config.Bind(
                    "Tint",
                    summon.ConfigKey,
                    iceBlue,
                    $"Colour of the summoned {animal}: the spirit body for the Spirit look, the glow for the Natural look (hex RRGGBBAA; alpha is ignored).");
                summon.Tint.SettingChanged += (_, _) => SummonLooks.ApplyEverywhere();

                summon.Glow = Config.Bind(
                    "Glow",
                    summon.ConfigKey,
                    0.3f,
                    new ConfigDescription(
                        $"Glow strength of the Natural-look {animal}, in the tint colour. 0 = no glow, plain wild animal.",
                        new AcceptableValueRange<float>(0f, 5f)));
                summon.Glow.SettingChanged += (_, _) => SummonLooks.ApplyEverywhere();

                summon.Area = Config.Bind(
                    "GlowArea",
                    summon.ConfigKey,
                    GlowArea.Body,
                    $"Where the Natural-look {animal} glows. Body = whole body. Eyes = only the animal's own eye mask (only the moose has a visible one; wolf eyes glow on their own anyway; bear and boar show nothing).");
                summon.Area.SettingChanged += (_, _) => SummonLooks.ApplyEverywhere();
            }

            Picker.Init(Config);

            DismissKey = Config.Bind(
                "Dismiss",
                "Key",
                new KeyboardShortcut(KeyCode.X, KeyCode.LeftAlt),
                "Dismisses all spirit summons you called, with the vanilla despawn effect.");

            DumpLiveSummons = Config.Bind(
                "Debug",
                "DumpLiveSummons",
                false,
                "Set to true to write the visual setup of every summon currently standing to the BepInEx log. Resets itself to false.");
            DumpLiveSummons.SettingChanged += (_, _) =>
            {
                if (!DumpLiveSummons.Value) return;
                DebugDump.LiveSummons();
                DumpLiveSummons.Value = false;
            };

            _configLastWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);

            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void Update()
        {
            if (DismissKey.Value.IsDown() && Dismiss.InputIsFree()) Dismiss.MySummons();

            if (Time.unscaledTime < _nextConfigPoll) return;
            _nextConfigPoll = Time.unscaledTime + ConfigPollSeconds;

            var lastWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
            if (lastWrite == _configLastWrite) return;
            _configLastWrite = lastWrite;

            Config.Reload();
            Log.LogInfo("Config reloaded");
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetSceneAwakePatch
    {
        private static void Postfix() => SummonLooks.ApplyToPrefabs();
    }
}
