using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace SpiritAnimism
{
    internal enum Look
    {
        Spirit,
        Natural,
    }

    internal enum GlowArea
    {
        Body,
        Eyes,
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
        public ConfigEntry<GlowArea> Area;

        // Shared assets, looked up once: the summon's spirit body material and the wild animal's body material.
        public Material SpiritMaterial;
        public Color VanillaTint;
        public Material WildMaterial;
        public Texture VanillaEmissionMap;
        public Color VanillaEmissionColor;
        public bool AssetsLookedUp;
        public bool WarnedNoEyeMask;

        public SummonType(string prefabName, string wildPrefabName, string configKey, string materialPrefix)
        {
            PrefabName = prefabName;
            WildPrefabName = wildPrefabName;
            ConfigKey = configKey;
            MaterialPrefix = materialPrefix;
        }
    }

    // One summon's appearance, as written into its network data by the summoner and read by every client.
    internal struct LookSpec
    {
        private const string Version = "1";

        public Look Look;
        public Color Tint;
        public float Glow;
        public GlowArea Area;

        public static LookSpec FromConfig(SummonType type) => new LookSpec
        {
            Look = type.Look.Value, Tint = type.Tint.Value, Glow = type.Glow.Value, Area = type.Area.Value,
        };

        public string Serialize() =>
            $"{Version};{Look};{ColorUtility.ToHtmlStringRGB(Tint)};{Glow.ToString("0.###", CultureInfo.InvariantCulture)};{Area}";

        public static bool TryParse(string text, out LookSpec spec)
        {
            spec = default;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split(';');
            if (parts.Length != 5 || parts[0] != Version) return false;
            if (!Enum.TryParse(parts[1], out spec.Look)) return false;
            if (!ColorUtility.TryParseHtmlString("#" + parts[2], out spec.Tint)) return false;
            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out spec.Glow)) return false;
            return Enum.TryParse(parts[4], out spec.Area);
        }
    }

    internal static class SummonLooks
    {
        // The spirit body materials (<animal>_mat_spiritcaller, shader Custom/Fallen Warrior) carry the
        // pink as an HDR _TintColor. Opaque parts (horns, tusks, saddle) use _Color and are left alone.
        internal static readonly SummonType[] Summons =
        {
            new SummonType("Bjorn_spiritcaller", "Bjorn", "Bear", "Bjorn_"),
            new SummonType("Moose_spiritcaller", "Moose", "Moose", "Moose_"),
            new SummonType("Boar_spiritcaller", "Boar", "Boar", "Boar_"),
            new SummonType("Wolf_spiritcaller", "Wolf", "Wolf", "Wolf_"),
        };

        internal static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        internal static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        internal static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        // Custom key in each summon's network data. The game replicates it to every client and into
        // the save; a vanilla server relays it; clients without the mod ignore it.
        private static readonly int LookKey = "SpiritAnimism.Look".GetStableHashCode();

        private const float RefreshSeconds = 0.25f;
        private static float _nextRefresh;

        public static void Refresh() => _nextRefresh = 0f;

        public static bool IsSummon(string objectName) => SpeciesOf(objectName) != null;

        public static SummonType SpeciesOf(string objectName)
        {
            foreach (var summon in Summons)
            {
                if (objectName.StartsWith(summon.PrefabName)) return summon;
            }
            return null;
        }

        // Every summon in the world: write my look onto the ones I own, then show each one the look its
        // network data says, my config for my own while the write is in flight, vanilla for the rest.
        public static void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            if (ZNetScene.instance == null) return;

            var player = Player.m_localPlayer;
            var playerName = player != null ? player.GetPlayerName() : null;

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null) continue;
                var species = SpeciesOf(character.gameObject.name);
                if (species == null || !LookUpAssets(species)) continue;

                var nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;
                var zdo = nview.GetZDO();
                var mine = playerName != null && zdo.GetString(ZDOVars.s_follow, "") == playerName;

                if (mine && nview.IsOwner())
                {
                    var desired = LookSpec.FromConfig(species).Serialize();
                    if (zdo.GetString(LookKey, "") != desired) zdo.Set(LookKey, desired);
                }

                LookSpec? spec = null;
                if (LookSpec.TryParse(zdo.GetString(LookKey, ""), out var stored)) spec = stored;
                else if (mine) spec = LookSpec.FromConfig(species);

                var look = character.GetComponent<SummonLook>() ?? character.gameObject.AddComponent<SummonLook>();
                look.Apply(species, spec);
            }
        }

        private static bool LookUpAssets(SummonType species)
        {
            if (species.AssetsLookedUp) return species.SpiritMaterial != null;
            species.AssetsLookedUp = true;

            var prefab = ZNetScene.instance.GetPrefab(species.PrefabName);
            if (prefab == null)
            {
                Plugin.Log.LogWarning($"Prefab {species.PrefabName} not found");
                return false;
            }
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !material.HasProperty(TintColorId)) continue;
                    if (!material.name.StartsWith(species.MaterialPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    species.SpiritMaterial = material;
                    species.VanillaTint = material.GetColor(TintColorId);
                    break;
                }
                if (species.SpiritMaterial != null) break;
            }
            if (species.SpiritMaterial == null)
            {
                Plugin.Log.LogWarning($"No spirit body material on {species.PrefabName}");
                return false;
            }

            // The wild animal's body material is the one sharing the spirit material's body texture.
            var wild = ZNetScene.instance.GetPrefab(species.WildPrefabName);
            var bodyTexture = species.SpiritMaterial.GetTexture(MainTexId);
            var bodyTextureName = bodyTexture != null ? bodyTexture.name : null;
            if (wild != null)
            {
                foreach (var renderer in wild.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || material.HasProperty(TintColorId) || !material.HasProperty(MainTexId)) continue;
                        var texture = material.GetTexture(MainTexId);
                        if (texture == null || texture.name != bodyTextureName) continue;
                        species.WildMaterial = material;
                        species.VanillaEmissionMap = material.HasProperty(EmissionMapId) ? material.GetTexture(EmissionMapId) : null;
                        species.VanillaEmissionColor = material.GetColor(EmissionColorId);
                        break;
                    }
                    if (species.WildMaterial != null) break;
                }
            }
            if (species.WildMaterial == null)
            {
                Plugin.Log.LogWarning($"No wild body material for {species.ConfigKey}; its Natural look falls back to Spirit");
            }

            Plugin.Log.LogInfo($"{species.ConfigKey}: spirit material {species.SpiritMaterial.name}, wild material {(species.WildMaterial != null ? species.WildMaterial.name : "none")}");
            return true;
        }

        // Vanilla uses HDR values (peak channel ~3). Scale the chosen colour to the same peak so
        // brightness stays vanilla and only the hue changes; keep vanilla alpha.
        public static Color MatchIntensity(Color tint, Color original)
        {
            var originalPeak = Mathf.Max(original.r, original.g, original.b);
            var tintPeak = Mathf.Max(tint.r, tint.g, tint.b);
            var scale = tintPeak > 0f ? originalPeak / tintPeak : 1f;
            return new Color(tint.r * scale, tint.g * scale, tint.b * scale, original.a);
        }
    }

    // Lives on each summon: owns that summon's private copies of the body materials, so two
    // players' summons can look different on the same screen. Destroys them with the summon.
    internal sealed class SummonLook : MonoBehaviour
    {
        private SummonType _species;
        private string _applied;
        private Material _spirit;
        private Material _natural;

        public void Apply(SummonType species, LookSpec? spec)
        {
            _species = species;
            var key = spec.HasValue ? spec.Value.Serialize() : "vanilla";
            if (key == _applied) return;
            _applied = key;

            var desired = Desired(spec);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || !IsBodySlot(material) || material == desired) continue;
                    materials[i] = desired;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private bool IsBodySlot(Material material) =>
            material == _species.SpiritMaterial || material == _spirit || material == _natural;

        private Material Desired(LookSpec? maybe)
        {
            if (!maybe.HasValue) return _species.SpiritMaterial;
            var spec = maybe.Value;

            if (spec.Look == Look.Natural && _species.WildMaterial != null)
            {
                if (_natural == null)
                {
                    _natural = new Material(_species.WildMaterial) { name = _species.WildMaterial.name + "_spiritanimism" };
                    _natural.EnableKeyword("_EMISSION");
                }
                var eyesWithoutMask = spec.Area == GlowArea.Eyes && _species.VanillaEmissionMap == null;
                if (eyesWithoutMask && !_species.WarnedNoEyeMask)
                {
                    _species.WarnedNoEyeMask = true;
                    Plugin.Log.LogWarning($"{_species.ConfigKey} has no eye mask; GlowArea Eyes lights nothing on it");
                }
                if (spec.Glow > 0f && !eyesWithoutMask)
                {
                    _natural.SetTexture(SummonLooks.EmissionMapId, spec.Area == GlowArea.Body ? Texture2D.whiteTexture : _species.VanillaEmissionMap);
                    _natural.SetColor(SummonLooks.EmissionColorId, new Color(spec.Tint.r, spec.Tint.g, spec.Tint.b, 1f) * spec.Glow);
                }
                else
                {
                    _natural.SetTexture(SummonLooks.EmissionMapId, _species.VanillaEmissionMap);
                    _natural.SetColor(SummonLooks.EmissionColorId, _species.VanillaEmissionColor);
                }
                return _natural;
            }

            if (_spirit == null)
            {
                _spirit = new Material(_species.SpiritMaterial) { name = _species.SpiritMaterial.name + "_spiritanimism" };
            }
            _spirit.SetColor(SummonLooks.TintColorId, SummonLooks.MatchIntensity(spec.Tint, _species.VanillaTint));
            return _spirit;
        }

        private void OnDestroy()
        {
            if (_spirit != null) Destroy(_spirit);
            if (_natural != null) Destroy(_natural);
        }
    }
}
