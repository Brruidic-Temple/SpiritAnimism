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
        public const string Version = "0.2.0";

        internal static ManualLogSource Log;

        // BepInEx does not watch config files, so poll the file's timestamp to reload edits made while playing.
        private const float ConfigPollSeconds = 1f;
        private float _nextConfigPoll;
        private System.DateTime _configLastWrite;

        private void Awake()
        {
            Log = Logger;
            var iceBlue = new Color(0.25f, 0.78f, 1f);
            foreach (var summon in SpiritTint.Summons)
            {
                summon.Tint = Config.Bind(
                    "Tint",
                    summon.ConfigKey,
                    iceBlue,
                    $"Colour of the summoned {summon.ConfigKey.ToLower()}'s spirit body (hex RRGGBBAA; alpha is ignored, vanilla brightness and transparency are kept).");
                summon.Tint.SettingChanged += (_, _) => SpiritTint.ApplyEverywhere();

                summon.Cutoff = Config.Bind(
                    "Cutoff (experimental)",
                    summon.ConfigKey,
                    0.589f,
                    new ConfigDescription(
                        $"The spirit shader's _Cutoff for the summoned {summon.ConfigKey.ToLower()}. Vanilla is 0.589. Candidate transparency control; effect not yet known.",
                        new AcceptableValueRange<float>(0f, 1f)));
                summon.Cutoff.SettingChanged += (_, _) => SpiritTint.ApplyEverywhere();

                summon.Alpha = Config.Bind(
                    "Alpha (experimental)",
                    summon.ConfigKey,
                    1f,
                    new ConfigDescription(
                        $"Alpha of the spirit tint for the summoned {summon.ConfigKey.ToLower()}. Vanilla is 1. Candidate transparency control; the shader may ignore it.",
                        new AcceptableValueRange<float>(0f, 1f)));
                summon.Alpha.SettingChanged += (_, _) => SpiritTint.ApplyEverywhere();
            }

            _configLastWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);

            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void Update()
        {
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
        private static void Postfix() => SpiritTint.ApplyToPrefabs();
    }
}
