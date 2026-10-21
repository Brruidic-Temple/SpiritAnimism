using System.Collections.Generic;
using UnityEngine;

namespace SpiritAnimism
{
    internal static class SpiritTint
    {
        private static readonly string[] SummonPrefabs =
        {
            "Bjorn_spiritcaller",
            "Moose_spiritcaller",
            "Boar_spiritcaller",
            "Wolf_spiritcaller",
        };

        // The spirit body materials (*_mat_spiritcaller, shader Custom/Fallen Warrior) carry the
        // pink as an HDR _TintColor. Opaque parts (horns, tusks, saddle) use _Color and are left alone.
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        private static readonly string[] PropertiesOfInterest =
        {
            "_Color", "_EmissionColor", "_TintColor", "_Hue", "_Saturation", "_Value", "_MainTex",
        };

        private static readonly Dictionary<Material, Color> OriginalColors = new Dictionary<Material, Color>();
        private static readonly HashSet<Material> Described = new HashSet<Material>();

        public static void ApplyToPrefabs()
        {
            if (ZNetScene.instance == null) return;

            foreach (var name in SummonPrefabs)
            {
                var prefab = ZNetScene.instance.GetPrefab(name);
                if (prefab == null)
                {
                    Plugin.Log.LogWarning($"Prefab {name} not found");
                    continue;
                }
                Apply(prefab, name);
            }
        }

        public static void ApplyEverywhere()
        {
            ApplyToPrefabs();

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null) continue;
                var name = character.gameObject.name;
                if (IsSummon(name)) Apply(character.gameObject, name);
            }
        }

        private static bool IsSummon(string objectName)
        {
            foreach (var prefabName in SummonPrefabs)
            {
                if (objectName.StartsWith(prefabName)) return true;
            }
            return false;
        }

        private static void Apply(GameObject root, string label)
        {
            var tint = Plugin.Tint.Value;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    Describe(material, renderer, label);
                    if (!material.HasProperty(TintColorId)) continue;

                    if (!OriginalColors.TryGetValue(material, out var original))
                    {
                        original = material.GetColor(TintColorId);
                        OriginalColors[material] = original;
                    }

                    material.SetColor(TintColorId, MatchIntensity(tint, original));
                }
            }
        }

        // Vanilla uses HDR values (peak channel ~3). Scale the chosen colour to the same peak so
        // brightness stays vanilla and only the hue changes; keep vanilla alpha.
        private static Color MatchIntensity(Color tint, Color original)
        {
            var originalPeak = Mathf.Max(original.r, original.g, original.b);
            var tintPeak = Mathf.Max(tint.r, tint.g, tint.b);
            var scale = tintPeak > 0f ? originalPeak / tintPeak : 1f;
            return new Color(tint.r * scale, tint.g * scale, tint.b * scale, original.a);
        }

        private static void Describe(Material material, Renderer renderer, string label)
        {
            if (!Described.Add(material)) return;
            var parts = new List<string>();
            foreach (var property in PropertiesOfInterest)
            {
                if (!material.HasProperty(property)) continue;
                var value = property == "_MainTex"
                    ? (material.GetTexture(property) != null ? material.GetTexture(property).name : "null")
                    : property == "_Hue" || property == "_Saturation" || property == "_Value"
                        ? material.GetFloat(property).ToString("0.###")
                        : material.GetColor(property).ToString();
                parts.Add($"{property}={value}");
            }

            Plugin.Log.LogInfo(
                $"[{label}] renderer={renderer.name} material={material.name} shader={material.shader.name} " +
                $"queue={material.renderQueue} {string.Join(" ", parts)}");
        }
    }
}
