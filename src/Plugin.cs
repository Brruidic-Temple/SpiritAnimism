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

                summon.Fade = Config.Bind(
                    "Fade",
                    summon.ConfigKey,
                    0f,
                    new ConfigDescription(
                        $"How much more transparent than vanilla the summoned {summon.ConfigKey.ToLower()} is: 0 = vanilla, 1 = invisible. The shader cannot be made more solid than vanilla.",
                        new AcceptableValueRange<float>(0f, 1f)));
                summon.Fade.SettingChanged += (_, _) => SpiritTint.ApplyEverywhere();
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
