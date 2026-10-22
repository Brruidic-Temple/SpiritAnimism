using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpiritAnimism
{
    internal enum Look
    {
        Spirit,
        Natural,
    }

    internal sealed class SummonType
    {
        public readonly string PrefabName;
        public readonly string WildPrefabName;
        public readonly string ConfigKey;
        public readonly string MaterialPrefix;

        public ConfigEntry<Look> Look;
        public ConfigEntry<Color> Tint;
        public ConfigEntry<float> Glow;

        // Captured from the summon prefab the first time its spirit body material is seen.
        public Material SpiritMaterial;
        public Color VanillaTint;
        // A private copy of the wild animal's body material, created on first use.
        public Material NaturalMaterial;
        public bool NaturalLookedUp;
        public Texture VanillaEmissionMap;
        public Color VanillaEmissionColor;

        public SummonType(string prefabName, string wildPrefabName, string configKey, string materialPrefix)
        {
            PrefabName = prefabName;
            WildPrefabName = wildPrefabName;
            ConfigKey = configKey;
            MaterialPrefix = materialPrefix;
        }
    }

    internal static class SummonLooks
    {
        // The spirit body materials (<animal>_mat_spiritcaller, shader Custom/Fallen Warrior) carry the
        // pink as an HDR _TintColor. Opaque parts (horns, tusks, saddle) use _Color and are left alone.
        // Materials are shared assets and the bear prefab also holds the moose material on a hidden mesh,
        // so the species is chosen by material name, never by the prefab the renderer was found on.
        internal static readonly SummonType[] Summons =
        {
            new SummonType("Bjorn_spiritcaller", "Bjorn", "Bear", "Bjorn_"),
            new SummonType("Moose_spiritcaller", "Moose", "Moose", "Moose_"),
            new SummonType("Boar_spiritcaller", "Boar", "Boar", "Boar_"),
            new SummonType("Wolf_spiritcaller", "Wolf", "Wolf", "Wolf_"),
        };

        // Opacity is not adjustable on the spirit shader. Its only transparency knob is _Cutoff (vanilla
        // 0.589), which measured in-game as a hard threshold: identical up to 0.62, gone from 0.63.
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        // Custom/Creature multiplies _EmissionColor by _EmissionMap. Bear and moose ship a mask that is
        // white only at the eyes, so a glow needs an all-white mask to cover the body.
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

        private static readonly string[] PropertiesOfInterest =
        {
            "_Color", "_EmissionColor", "_EmissionMap", "_TintColor", "_Hue", "_Saturation", "_Value", "_MainTex",
        };

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

        public static bool IsSummon(string objectName)
        {
            foreach (var summon in Summons)
            {
                if (objectName.StartsWith(summon.PrefabName)) return true;
            }
            return false;
        }

        private static SummonType SpeciesOf(Material material)
        {
            foreach (var summon in Summons)
            {
                if (material == summon.NaturalMaterial) return summon;
                if (material.HasProperty(TintColorId) &&
                    material.name.StartsWith(summon.MaterialPrefix, StringComparison.OrdinalIgnoreCase)) return summon;
            }
            return null;
        }

        private static void Apply(GameObject root, string label)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;

                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    Describe(material, renderer, label);

                    var species = SpeciesOf(material);
                    if (species == null) continue;

                    if (species.SpiritMaterial == null && material != species.NaturalMaterial)
                    {
                        species.SpiritMaterial = material;
                        species.VanillaTint = material.GetColor(TintColorId);
                    }

                    var desired = DesiredMaterial(species);
                    if (desired == null || desired == material) continue;
                    materials[i] = desired;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static Material DesiredMaterial(SummonType species)
        {
            if (species.SpiritMaterial == null) return null;
            var tint = species.Tint.Value;
            species.SpiritMaterial.SetColor(TintColorId, MatchIntensity(tint, species.VanillaTint));

            if (species.Look.Value == Look.Spirit) return species.SpiritMaterial;

            var natural = NaturalMaterialFor(species);
            if (natural == null) return species.SpiritMaterial;

            if (species.Glow.Value > 0f)
            {
                natural.SetTexture(EmissionMapId, Texture2D.whiteTexture);
                natural.SetColor(EmissionColorId, new Color(tint.r, tint.g, tint.b, 1f) * species.Glow.Value);
            }
            else
            {
                natural.SetTexture(EmissionMapId, species.VanillaEmissionMap);
                natural.SetColor(EmissionColorId, species.VanillaEmissionColor);
            }
            return natural;
        }

        // Finds the wild animal's body material by the body texture the spirit material already uses,
        // and keeps a private copy so wild animals are never affected by the glow.
        private static Material NaturalMaterialFor(SummonType species)
        {
            if (species.NaturalLookedUp) return species.NaturalMaterial;
            species.NaturalLookedUp = true;

            var wild = ZNetScene.instance.GetPrefab(species.WildPrefabName);
            if (wild == null)
            {
                Plugin.Log.LogWarning($"Wild prefab {species.WildPrefabName} not found; {species.ConfigKey} keeps the spirit look");
                return null;
            }

            var bodyTexture = species.SpiritMaterial.GetTexture(MainTexId);
            var bodyTextureName = bodyTexture != null ? bodyTexture.name : null;

            foreach (var renderer in wild.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    Describe(material, renderer, species.WildPrefabName);
                    if (material.HasProperty(TintColorId) || !material.HasProperty(MainTexId)) continue;
                    var texture = material.GetTexture(MainTexId);
                    if (texture == null || texture.name != bodyTextureName) continue;

                    species.NaturalMaterial = new Material(material) { name = material.name + "_spiritanimism" };
                    species.NaturalMaterial.EnableKeyword("_EMISSION");
                    species.VanillaEmissionMap = material.HasProperty(EmissionMapId) ? material.GetTexture(EmissionMapId) : null;
                    species.VanillaEmissionColor = material.GetColor(EmissionColorId);
                    Plugin.Log.LogInfo($"{species.ConfigKey}: natural material from {species.WildPrefabName}/{renderer.name}/{material.name}");
                    return species.NaturalMaterial;
                }
            }

            Plugin.Log.LogWarning($"No body material with texture {bodyTextureName} on {species.WildPrefabName}; {species.ConfigKey} keeps the spirit look");
            return null;
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
                var value = property == "_MainTex" || property == "_EmissionMap"
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

        // Full dump of the spirit shader's properties.
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
            Plugin.Log.LogInfo($"  {material.name} keywords=[{string.Join(",", material.shaderKeywords)}] | {string.Join(" ", parts)}");
        }
    }
}
