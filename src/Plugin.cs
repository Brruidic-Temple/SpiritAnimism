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
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<Color> Tint;

        // BepInEx does not watch config files, so poll the file's timestamp to reload edits made while playing.
        private const float ConfigPollSeconds = 1f;
        private float _nextConfigPoll;
        private System.DateTime _configLastWrite;

        private void Awake()
        {
            Log = Logger;
            Tint = Config.Bind(
                "Look",
                "Tint",
                new Color(0.25f, 0.78f, 1f),
                "Colour applied to the Spirit Caller summons (hex RRGGBBAA; alpha is ignored, vanilla transparency is kept).");
            Tint.SettingChanged += (_, _) => SpiritTint.ApplyEverywhere();

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
