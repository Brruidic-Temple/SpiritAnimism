#if DEBUG
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace SpiritAnimism
{
    // Debug builds only. Writes everything visual about the summons currently standing to the log:
    // renderers, their materials with emission settings, lights, and level effects.
    internal static class DebugDump
    {
        private static ConfigEntry<bool> _dumpLiveSummons;
        private static ConfigEntry<KeyboardShortcut> _dumpKey;

        public static void Init(ConfigFile config)
        {
            _dumpLiveSummons = config.Bind(
                "Debug",
                "DumpLiveSummons",
                false,
                "Set to true to write the visual setup of every summon currently standing to the BepInEx log. Resets itself to false.");
            _dumpLiveSummons.SettingChanged += (_, _) =>
            {
                if (!_dumpLiveSummons.Value) return;
                LiveSummons();
                _dumpLiveSummons.Value = false;
            };
            _dumpKey = config.Bind(
                "Debug",
                "DumpKey",
                new KeyboardShortcut(KeyCode.D, KeyCode.LeftAlt),
                "Writes the state of every summon the mod can see, and every summon the world knows about, to the BepInEx log.");
        }

        public static void Update()
        {
            if (_dumpKey.Value.IsDown() && Dismiss.InputIsFree()) Summary();
        }

        // One line per summon-named character, whether or not its network view is valid, followed by
        // every summon the world database holds, loaded or not.
        public static void Summary()
        {
            var player = Player.m_localPlayer;
            var playerName = player != null ? player.GetPlayerName() : "?";
            var playerPos = player != null ? player.transform.position : Vector3.zero;
            Plugin.Log.LogInfo($"[state] player={playerName} pos={playerPos} session={ZDOMan.GetSessionID()} characters={Character.GetAllCharacters().Count}");

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || Summons.SpeciesOf(character.gameObject.name) == null) continue;
                var go = character.gameObject;
                var view = character.GetComponent<ZNetView>();
                var zdo = view != null ? view.GetZDO() : null;
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                var enabled = 0;
                var visible = 0;
                foreach (var renderer in renderers)
                {
                    if (renderer.enabled) enabled++;
                    if (renderer.isVisible) visible++;
                }
                var ai = character.GetComponent<MonsterAI>();
                var line = $"[state] {go.name}#{go.GetInstanceID()} active={go.activeInHierarchy} pos={go.transform.position} dist={Vector3.Distance(go.transform.position, playerPos):0.#} level={character.GetLevel()} hp={character.GetHealth():0.#}/{character.GetMaxHealth():0.#} dead={character.IsDead()} renderers={enabled}/{renderers.Length} visible={visible} tameable={character.GetComponent<Tameable>() != null} followTarget={(ai != null && ai.GetFollowTarget() != null ? ai.GetFollowTarget().name : "none")}";
                if (view == null) line += " view=NONE";
                else if (zdo == null) line += " view=reset";
                else
                {
                    line += $" zdo={zdo.m_uid} valid={view.IsValid()} created={zdo.Created} persistent={zdo.Persistent} owner={zdo.GetOwner()} isOwner={view.IsOwner()}"
                          + $" registered={ZDOMan.instance.GetZDO(zdo.m_uid) == zdo} instance={(ZNetScene.instance.FindInstance(zdo) == view ? "this" : ZNetScene.instance.FindInstance(zdo) == null ? "NONE" : "OTHER")}"
                          + $" zdoPos={zdo.GetPosition()} follow=\"{zdo.GetString(ZDOVars.s_follow, "")}\" max={zdo.GetInt(ZDOVars.s_maxInstances, 0)} look=\"{zdo.GetString(SummonLooks.LookKey, "")}\"";
                }
                Plugin.Log.LogInfo(line);
            }

            foreach (var type in Summons.Types)
            {
                var zdos = new List<ZDO>();
                var index = 0;
                while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(type.PrefabName, zdos, ref index)) { }
                foreach (var zdo in zdos)
                {
                    var instance = ZNetScene.instance.FindInstance(zdo);
                    Plugin.Log.LogInfo($"[state] world {type.PrefabName} zdo={zdo.m_uid} pos={zdo.GetPosition()} dist={Vector3.Distance(zdo.GetPosition(), playerPos):0.#} owner={zdo.GetOwner()} persistent={zdo.Persistent} created={zdo.Created} instance={(instance != null ? instance.gameObject.name + "#" + instance.gameObject.GetInstanceID() : "none")} follow=\"{zdo.GetString(ZDOVars.s_follow, "")}\"");
                }
            }
        }

        public static void LiveSummons()
        {
            var count = 0;
            foreach (var summon in Summons.Standing())
            {
                count++;
                var root = summon.Character.gameObject;
                var zdo = summon.Zdo;
                Plugin.Log.LogInfo($"[dump] {root.name} level={summon.Character.GetLevel()} pos={root.transform.position} follow={zdo.GetString(ZDOVars.s_follow, "")} owner={summon.View.IsOwner()} look=\"{zdo.GetString(SummonLooks.LookKey, "")}\"");

                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new List<string>();
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) { materials.Add("null"); continue; }
                        var emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor").ToString() : "-";
                        var emissionMap = material.HasProperty("_EmissionMap")
                            ? (material.GetTexture("_EmissionMap") != null ? material.GetTexture("_EmissionMap").name : "null")
                            : "-";
                        materials.Add($"{material.name}[{material.shader.name} emission={emissionColor} map={emissionMap} kw={string.Join(",", material.shaderKeywords)}]");
                        if (material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") != null)
                        {
                            DescribeMask(material.GetTexture("_EmissionMap"));
                        }
                    }
                    Plugin.Log.LogInfo($"[dump]   renderer {Path(renderer.transform, root.transform)} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} {string.Join(" ", materials)}");
                }

                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    Plugin.Log.LogInfo($"[dump]   light {Path(light.transform, root.transform)} enabled={light.enabled} active={light.gameObject.activeInHierarchy} color={light.color} intensity={light.intensity} range={light.range}");
                }

                foreach (var effects in root.GetComponentsInChildren<LevelEffects>(true))
                {
                    Plugin.Log.LogInfo($"[dump]   LevelEffects on {Path(effects.transform, root.transform)} setups={effects.m_levelSetups.Count}");
                }
            }
            Plugin.Log.LogInfo($"[dump] {count} summon(s) dumped");
        }

        private static readonly HashSet<Texture> DescribedMasks = new HashSet<Texture>();

        // Reads a mask through a temporary render target (the asset itself is not CPU-readable) and
        // reports how much of it is lit and where, so we know what the shader would multiply by.
        private static void DescribeMask(Texture mask)
        {
            if (!DescribedMasks.Add(mask)) return;
            var target = RenderTexture.GetTemporary(mask.width, mask.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(mask, target);
                RenderTexture.active = target;
                var readable = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0);
                readable.Apply();
                var pixels = readable.GetPixels32();
                Object.Destroy(readable);

                int lit = 0, minX = mask.width, minY = mask.height, maxX = -1, maxY = -1;
                float maxBrightness = 0f;
                for (var i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    var brightness = Mathf.Max(p.r, p.g, p.b) / 255f;
                    if (brightness > maxBrightness) maxBrightness = brightness;
                    if (brightness < 0.1f) continue;
                    lit++;
                    var x = i % mask.width;
                    var y = i / mask.width;
                    if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
                Plugin.Log.LogInfo($"[dump]     mask {mask.name} {mask.width}x{mask.height}: {100f * lit / pixels.Length:0.##}% lit (>10%), peak={maxBrightness:0.##}, lit box x={minX}..{maxX} y={minY}..{maxY}");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static string Path(Transform transform, Transform root)
        {
            var parts = new List<string>();
            for (var t = transform; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }
    }
}
#endif
