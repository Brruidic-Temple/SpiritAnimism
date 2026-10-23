using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SpiritAnimism
{
    // One summon's appearance, as written into its network data by the summoner and read by every client.
    internal struct LookSpec
    {
        public const float MaxGlow = 5f;
        private const string Version = "2";

        public Look Look;
        public Color Tint;
        public float Glow;
        public float EyeGlow;

        public static LookSpec FromConfig(SummonType type) => new LookSpec
        {
            Look = type.Look.Value,
            Tint = type.Tint.Value,
            Glow = type.Glow.Value,
            EyeGlow = type.EyeGlow != null ? type.EyeGlow.Value : 0f,
        };

        public string Serialize() =>
            $"{Version};{Look};{ColorUtility.ToHtmlStringRGB(Tint)};{Strength(Glow)};{Strength(EyeGlow)}";

        private static string Strength(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        // The text comes from another client, so every field is range-checked rather than trusted.
        public static bool TryParse(string text, out LookSpec spec)
        {
            spec = default;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split(';');
            if (parts.Length != 5 || parts[0] != Version) return false;
            if (!Enum.TryParse(parts[1], out spec.Look) || !Enum.IsDefined(typeof(Look), spec.Look)) return false;
            if (!ColorUtility.TryParseHtmlString("#" + parts[2], out spec.Tint)) return false;
            return TryParseStrength(parts[3], out spec.Glow) && TryParseStrength(parts[4], out spec.EyeGlow);
        }

        private static bool TryParseStrength(string text, out float value)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            value = Mathf.Clamp(value, 0f, MaxGlow);
            return true;
        }
    }

    // Keeps every standing summon showing the right look. The summoner writes their config into the
    // summons they own and sees it at once; every other client reads it from the summon's network
    // data. Summons without a stored look (called by players without the mod) stay vanilla.
    internal static class SummonLooks
    {
        internal static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        internal static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        internal static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        // Custom key in each summon's network data. The game replicates it to every client and into
        // the save; a vanilla server relays it; clients without the mod ignore it.
        internal static readonly int LookKey = "SpiritAnimism.Look".GetStableHashCode();

        private const float RefreshSeconds = 0.25f;
        private static float _nextRefresh;

        public static void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            if (ZNetScene.instance == null) return;

            var player = Player.m_localPlayer;
            var playerName = player != null ? player.GetPlayerName() : null;

            foreach (var summon in Summons.Standing())
            {
                if (!LookUpAssets(summon.Species)) continue;
                var zdo = summon.Zdo;
                var stored = zdo.GetString(LookKey, "");
                LookSpec? spec;
                string key;

                if (summon.CalledBy(playerName))
                {
                    var mine = LookSpec.FromConfig(summon.Species);
                    spec = mine;
                    key = mine.Serialize();
                    // Only the owner may write; while someone else owns it the stored copy lags behind.
                    if (summon.View.IsOwner() && stored != key) zdo.Set(LookKey, key);
                }
                else if (LookSpec.TryParse(stored, out var theirs))
                {
                    spec = theirs;
                    key = stored;
                }
                else
                {
                    spec = null;
                    key = SummonLook.VanillaKey;
                }

                var look = summon.Character.GetComponent<SummonLook>() ?? summon.Character.gameObject.AddComponent<SummonLook>();
                look.Apply(summon.Species, spec, key);
            }
        }

        private static bool LookUpAssets(SummonType species)
        {
            if (species.AssetsLookedUp) return species.SpiritMaterial != null;
            species.AssetsLookedUp = true;

            // The spirit body material carries the vanilla pink as an HDR _TintColor. Opaque parts
            // (horns, tusks, saddle) use _Color and are left alone.
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
                    if (material == null || !material.HasProperty(TintColorId) || !species.OwnsMaterial(material)) continue;
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
                Plugin.Log.LogWarning($"No wild body material for {species.Name}; its Natural look falls back to Spirit");
            }

            Plugin.Log.LogInfo($"{species.Name}: spirit material {species.SpiritMaterial.name}, wild material {(species.WildMaterial != null ? species.WildMaterial.name : "none")}");
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

    // Lives on each summon: owns that summon's private copies of the body material, so two players'
    // summons can look different on the same screen. Destroys them with the summon.
    internal sealed class SummonLook : MonoBehaviour
    {
        public const string VanillaKey = "vanilla";

        // A renderer slot that held the body material when this summon was first seen. What sat there
        // is the vanilla look to restore: the shared spirit material, or the copy the game's level
        // effects make of it for starred creatures, which keeps the shared material's name.
        private struct BodySlot
        {
            public Renderer Renderer;
            public int Index;
            public Material Vanilla;
        }

        private SummonType _species;
        private readonly List<BodySlot> _slots = new List<BodySlot>();
        private string _applied;
        private Material _spirit;
        private Material _natural;
        // Emission map for a body-and-eyes glow, drawn for this summon's own body-to-eye ratio.
        private Texture2D _mixedGlow;
        private float _mixedBody;
        private float _mixedEyes;

        // Key identifies the look, so equal looks are not re-applied every tick.
        public void Apply(SummonType species, LookSpec? spec, string key)
        {
            if (key == _applied) return;
            if (_species == null)
            {
                _species = species;
                FindBodySlots();
            }
            _applied = key;

            foreach (var slot in _slots)
            {
                if (slot.Renderer == null) continue;
                var materials = slot.Renderer.sharedMaterials;
                if (slot.Index >= materials.Length) continue;
                var desired = spec.HasValue ? Styled(spec.Value, slot.Vanilla) : slot.Vanilla;
                if (materials[slot.Index] == desired) continue;
                materials[slot.Index] = desired;
                slot.Renderer.sharedMaterials = materials;
            }
        }

        private void FindBodySlots()
        {
            var bodyName = _species.SpiritMaterial.name;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.StartsWith(bodyName, StringComparison.Ordinal)) continue;
                    _slots.Add(new BodySlot { Renderer = renderer, Index = i, Vanilla = materials[i] });
                }
            }
            if (_slots.Count == 0) Plugin.Log.LogWarning($"{_species.Name} summon {name} has no body material slot; it stays vanilla");
        }

        private Material Styled(LookSpec spec, Material vanilla)
        {
            if (spec.Look == Look.Natural && _species.WildMaterial != null)
            {
                if (_natural == null)
                {
                    _natural = new Material(_species.WildMaterial) { name = _species.WildMaterial.name + "_spiritanimism" };
                    _natural.EnableKeyword("_EMISSION");
                }
                var body = spec.Glow;
                var eyes = _species.VanillaEmissionMap != null ? spec.EyeGlow : 0f;
                if (body <= 0f && eyes <= 0f)
                {
                    _natural.SetTexture(SummonLooks.EmissionMapId, _species.VanillaEmissionMap);
                    _natural.SetColor(SummonLooks.EmissionColorId, _species.VanillaEmissionColor);
                }
                else
                {
                    // The shader multiplies one colour by one map: the stronger glow sets the colour,
                    // the map carries each part's share of it.
                    var peak = Mathf.Max(body, eyes);
                    _natural.SetTexture(SummonLooks.EmissionMapId, GlowMap(body / peak, eyes / peak));
                    _natural.SetColor(SummonLooks.EmissionColorId, new Color(spec.Tint.r, spec.Tint.g, spec.Tint.b, 1f) * peak);
                }
                return _natural;
            }

            // Copying the vanilla slot material rather than the shared asset keeps any star tint on it.
            if (_spirit == null)
            {
                _spirit = new Material(vanilla) { name = _species.SpiritMaterial.name + "_spiritanimism" };
            }
            _spirit.SetColor(SummonLooks.TintColorId, SummonLooks.MatchIntensity(spec.Tint, _species.VanillaTint));
            return _spirit;
        }

        // White lights the whole body and the animal's own mask lights the eyes. A mix of the two is
        // drawn once per ratio: every pixel takes the brighter of the body share and the mask's eye share.
        private Texture GlowMap(float body, float eyes)
        {
            if (eyes <= 0f) return Texture2D.whiteTexture;
            if (body <= 0f) return _species.VanillaEmissionMap;
            if (_mixedGlow != null && body == _mixedBody && eyes == _mixedEyes) return _mixedGlow;

            var mask = _species.VanillaEmissionMap;
            if (_species.ReadableEyeMask == null) _species.ReadableEyeMask = Textures.ReadableCopy(mask);
            var pixels = _species.ReadableEyeMask.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var eyeShare = Mathf.Max(pixels[i].r, pixels[i].g, pixels[i].b) / 255f * eyes;
                var value = (byte)Mathf.RoundToInt(255f * Mathf.Max(eyeShare, body));
                pixels[i] = new Color32(value, value, value, 255);
            }

            if (_mixedGlow == null)
            {
                _mixedGlow = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, true)
                {
                    name = mask.name + "_spiritanimism",
                    wrapMode = mask.wrapMode,
                    filterMode = mask.filterMode,
                };
            }
            _mixedGlow.SetPixels32(pixels);
            _mixedGlow.Apply();
            _mixedBody = body;
            _mixedEyes = eyes;
            return _mixedGlow;
        }

        private void OnDestroy()
        {
            if (_spirit != null) Destroy(_spirit);
            if (_natural != null) Destroy(_natural);
            if (_mixedGlow != null) Destroy(_mixedGlow);
        }
    }

    internal static class Textures
    {
        // A CPU-readable copy of a texture asset, read back through a temporary render target.
        public static Texture2D ReadableCopy(Texture source)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
