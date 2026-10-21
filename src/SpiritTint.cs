using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpiritAnimism
{
    internal sealed class SummonType
    {
        public readonly string PrefabName;
        public readonly string ConfigKey;
        public readonly string MaterialPrefix;
        public ConfigEntry<Color> Tint;
        public ConfigEntry<float> Fade;

        public SummonType(string prefabName, string configKey, string materialPrefix)
        {
            PrefabName = prefabName;
            ConfigKey = configKey;
            MaterialPrefix = materialPrefix;
        }
    }

    internal static class SpiritTint
    {
        // The spirit body materials (<animal>_mat_spiritcaller, shader Custom/Fallen Warrior) carry the
        // pink as an HDR _TintColor. Opaque parts (horns, tusks, saddle) use _Color and are left alone.
        // Materials are shared assets and the bear prefab also holds the moose material on a hidden mesh,
        // so the colour is chosen by material name, never by the prefab the renderer was found on.
        internal static readonly SummonType[] Summons =
        {
            new SummonType("Bjorn_spiritcaller", "Bear", "Bjorn_"),
            new SummonType("Moose_spiritcaller", "Moose", "Moose_"),
            new SummonType("Boar_spiritcaller", "Boar", "Boar_"),
            new SummonType("Wolf_spiritcaller", "Wolf", "Wolf_"),
        };

        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        // The shader clips a built-in gradient against _Cutoff: raising it fades the body out evenly,
        // and vanilla (0.589) is already the most solid it gets. Fade 0..1 maps onto 0.589..1.
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private const float VanillaCutoff = 0.589f;

        private static readonly string[] PropertiesOfInterest =
        {
            "_Color", "_EmissionColor", "_TintColor", "_Hue", "_Saturation", "_Value", "_MainTex",
        };

        private static readonly Dictionary<Material, Color> OriginalColors = new Dictionary<Material, Color>();
        private static readonly HashSet<Material> Described = new HashSet<Material>();

        public static void ApplyToPrefabs()
        {
            if (ZNetScene.instance == null) return;

            foreach (var summon in Summons)
            {
                var prefab = ZNetScene.instance.GetPrefab(summon.PrefabName);
                if (prefab == null)
                {
                    Plugin.Log.LogWarning($"Prefab {summon.PrefabName} not found");
                    continue;
                }
                Apply(prefab, summon.PrefabName);
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
            foreach (var summon in Summons)
            {
                if (objectName.StartsWith(summon.PrefabName)) return true;
            }
            return false;
        }

        private static SummonType OwnerOf(Material material)
        {
            foreach (var summon in Summons)
            {
                if (material.name.StartsWith(summon.MaterialPrefix, StringComparison.OrdinalIgnoreCase)) return summon;
            }
            return null;
        }

        private static void Apply(GameObject root, string label)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    Describe(material, renderer, label);
                    if (!material.HasProperty(TintColorId)) continue;

                    var owner = OwnerOf(material);
                    if (owner == null)
                    {
                        Plugin.Log.LogWarning($"[{label}] tintable material {material.name} matches no summon type, left vanilla");
                        continue;
                    }

                    if (!OriginalColors.TryGetValue(material, out var original))
                    {
                        original = material.GetColor(TintColorId);
                        OriginalColors[material] = original;
                    }

                    material.SetColor(TintColorId, MatchIntensity(owner.Tint.Value, original));

                    if (material.HasProperty(CutoffId))
                    {
                        material.SetFloat(CutoffId, Mathf.Lerp(VanillaCutoff, 1f, owner.Fade.Value));
                    }
                }
            }
        }

        // Vanilla uses HDR values (peak channel ~3). Scale the chosen colour to the same peak so
        // brightness stays vanilla and only the hue changes; keep vanilla alpha (the shader ignores it anyway).
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

            if (material.HasProperty(TintColorId)) DescribeAllProperties(material);
        }

        // Full dump of the spirit shader's properties, to find what drives transparency.
        private static void DescribeAllProperties(Material material)
        {
            var shader = material.shader;
            var parts = new List<string>();
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                string value;
                switch (type)
                {
                    case ShaderPropertyType.Color: value = material.GetColor(name).ToString(); break;
                    case ShaderPropertyType.Vector: value = material.GetVector(name).ToString(); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: value = material.GetFloat(name).ToString("0.###"); break;
                    case ShaderPropertyType.Int: value = material.GetInt(name).ToString(); break;
                    case ShaderPropertyType.Texture:
                        var texture = material.GetTexture(name);
                        value = texture != null ? texture.name : "null";
                        break;
                    default: value = "?"; break;
                }
                parts.Add($"{name}:{type}={value}");
            }
            Plugin.Log.LogInfo($"  {material.name} keywords=[{string.Join(",", material.shaderKeywords)}] " +
                               $"blend src={material.GetInt("_SrcBlend")} dst={material.GetInt("_DstBlend")} " +
                               $"zwrite={material.GetInt("_ZWrite")} | {string.Join(" ", parts)}");
        }
    }
}
