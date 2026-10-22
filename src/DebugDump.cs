using System.Collections.Generic;
using UnityEngine;

namespace SpiritAnimism
{
    // Writes everything visual about the summons currently standing to the log: renderers, their
    // materials with emission settings, lights, and level effects. Triggered from the config file.
    internal static class DebugDump
    {
        private static readonly int LookKey = "SpiritAnimism.Look".GetStableHashCode();

        public static void LiveSummons()
        {
            var count = 0;
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || !SummonLooks.IsSummon(character.gameObject.name)) continue;
                count++;
                var root = character.gameObject;
                var nview = character.GetComponent<ZNetView>();
                var zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
                var follow = zdo != null ? zdo.GetString(ZDOVars.s_follow, "") : "?";
                var stored = zdo != null ? zdo.GetString(LookKey, "") : "?";
                var owner = nview != null && nview.IsValid() ? nview.IsOwner().ToString() : "?";
                Plugin.Log.LogInfo($"[dump] {root.name} level={character.GetLevel()} pos={root.transform.position} follow={follow} owner={owner} look=\"{stored}\"");

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
