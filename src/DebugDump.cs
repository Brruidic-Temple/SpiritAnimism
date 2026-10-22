using System.Collections.Generic;
using UnityEngine;

namespace SpiritAnimism
{
    // Writes everything visual about the summons currently standing to the log: renderers, their
    // materials with emission settings, lights, and level effects. Triggered from the config file.
    internal static class DebugDump
    {
        public static void LiveSummons()
        {
            var count = 0;
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || !SummonLooks.IsSummon(character.gameObject.name)) continue;
                count++;
                var root = character.gameObject;
                Plugin.Log.LogInfo($"[dump] {root.name} level={character.GetLevel()} pos={root.transform.position}");

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

        private static string Path(Transform transform, Transform root)
        {
            var parts = new List<string>();
            for (var t = transform; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }
    }
}
