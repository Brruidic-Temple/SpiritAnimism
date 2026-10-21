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

        private void Awake()
        {
            Log = Logger;
            Tint = Config.Bind(
                "Look",
                "Tint",
                new Color(0.25f, 0.78f, 1f),
                "Colour applied to the Spirit Caller summons (hex RRGGBBAA; alpha is ignored, vanilla transparency is kept).");
            Tint.SettingChanged += (_, _) => SpiritTint.ApplyEverywhere();

            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetSceneAwakePatch
    {
        private static void Postfix() => SpiritTint.ApplyToPrefabs();
    }
}
