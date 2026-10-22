using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpiritAnimism
{
    // Top-left list of the player's standing summons: a header with the count and the current pick,
    // then one row per summon with its name, what it is doing, and a health bar. Shown while the
    // Spirit Caller is in hand or a summon is out.
    internal static class SummonPanel
    {
        internal static ConfigEntry<bool> Show;
        internal static ConfigEntry<float> OffsetX;
        internal static ConfigEntry<float> OffsetY;
        internal static ConfigEntry<float> Scale;

        private const float RefreshSeconds = 0.25f;
        private const float RowHeight = 32f;
        private const float RowIndent = 32f;
        private const float BarWidth = 160f;
        private const float BarHeight = 6f;
        private static readonly Color HeaderColor = new Color(0.94f, 0.76f, 0.35f);
        private static readonly Color NameColor = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color ActivityColor = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color BarBackground = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color BarFill = new Color(0.4f, 0.8f, 0.25f);

        private static GameObject _root;
        private static TMP_Text _header;
        private static TMP_FontAsset _font;
        private static Material _fontMaterial;
        private static readonly List<Row> Rows = new List<Row>();
        private static float _nextRefresh;

        private sealed class Row
        {
            public GameObject Root;
            public TMP_Text Text;
            public RectTransform Fill;
        }

        public static void Init(ConfigFile config)
        {
            Show = config.Bind("Panel", "Show", true, "Show the list of your standing summons at the top left.");
            OffsetX = config.Bind("Panel", "OffsetX", 20f, "Distance from the left edge of the screen.");
            OffsetY = config.Bind("Panel", "OffsetY", 220f, "Distance from the top edge of the screen.");
            Scale = config.Bind("Panel", "Scale", 1f, new ConfigDescription("Size of the panel.", new AcceptableValueRange<float>(0.5f, 3f)));
            Show.SettingChanged += (_, _) => Place();
            OffsetX.SettingChanged += (_, _) => Place();
            OffsetY.SettingChanged += (_, _) => Place();
            Scale.SettingChanged += (_, _) => Place();
        }

        public static void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;

            var player = Player.m_localPlayer;
            if (Hud.instance == null || player == null)
            {
                if (_root != null) _root.SetActive(false);
                return;
            }
            if (_root == null && !Build()) return;

            var summons = MySummons(player);
            var visible = Show.Value && Hud.instance.IsVisible() && (summons.Count > 0 || Picker.IsSpiritCaller(player.GetCurrentWeapon()));
            _root.SetActive(visible);
            if (!visible) return;

            _header.text = $"SPIRITS [{summons.Count}]  <color=#{ColorUtility.ToHtmlStringRGB(HeaderColor)}>> {Picker.DisplayName(Picker.CurrentType())}</color>";
            while (Rows.Count < summons.Count) Rows.Add(BuildRow(Rows.Count));
            for (var i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                if (i >= summons.Count)
                {
                    row.Root.SetActive(false);
                    continue;
                }
                var character = summons[i];
                row.Root.SetActive(true);
                row.Text.text = $"{Name(character)}   <color=#{ColorUtility.ToHtmlStringRGB(ActivityColor)}>{Activity(character)}</color>";
                row.Fill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(character.GetHealthPercentage()), BarHeight);
            }
        }

        private static List<Character> MySummons(Player player)
        {
            var playerName = player.GetPlayerName();
            var summons = new List<Character>();
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || !SummonLooks.IsSummon(character.gameObject.name)) continue;
                var nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;
                if (nview.GetZDO().GetString(ZDOVars.s_follow, "") != playerName) continue;
                summons.Add(character);
            }
            return summons;
        }

        private static string Name(Character character)
        {
            foreach (var summon in SummonLooks.Summons)
            {
                if (character.gameObject.name.StartsWith(summon.PrefabName)) return Picker.DisplayName(summon);
            }
            return character.gameObject.name;
        }

        private static string Activity(Character character)
        {
            var ai = character.GetComponent<MonsterAI>();
            if (ai == null) return "";
            if (ai.GetTargetCreature() != null) return "Fighting";
            if (ai.IsAlerted()) return "Alert";
            if (ai.GetFollowTarget() != null) return "Following";
            return "Idle";
        }

        private static bool Build()
        {
            if (!PickFont()) return false;

            _root = new GameObject("SpiritAnimismPanel", typeof(RectTransform));
            _root.transform.SetParent(Hud.instance.m_rootObject.transform, false);
            var rect = (RectTransform)_root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(BarWidth, RowHeight);

            _header = MakeText(_root.transform, "Header", 14f, NameColor, FontStyles.Bold);
            ((RectTransform)_header.transform).anchoredPosition = Vector2.zero;

            Place();
            return true;
        }

        // The game's plain sans, not the pixel font its default settings point at.
        private const string PreferredFont = "Valheim-AveriaSansLibre";

        private static bool PickFont()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            var names = new List<string>();
            TMP_FontAsset chosen = null;
            foreach (var font in fonts)
            {
                names.Add(font.name);
                if (font.name == PreferredFont) chosen = font;
            }
            Plugin.Log.LogInfo($"TMP fonts available: {string.Join(", ", names)}");

            if (chosen == null) chosen = TMP_Settings.defaultFontAsset;
            if (chosen == null)
            {
                var hudText = Hud.instance.m_rootObject.GetComponentInChildren<TMP_Text>(true);
                if (hudText != null) chosen = hudText.font;
            }
            if (chosen == null)
            {
                Plugin.Log.LogWarning("No font found for the summon panel; it is off");
                Show.Value = false;
                return false;
            }
            _font = chosen;
            _fontMaterial = chosen.material;
            Plugin.Log.LogInfo($"Summon panel font: {chosen.name}");
            return true;
        }

        private static Row BuildRow(int index)
        {
            var row = new Row { Root = new GameObject($"Row{index}", typeof(RectTransform)) };
            row.Root.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)row.Root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(RowIndent, -RowHeight * (index + 0.7f));
            rect.sizeDelta = new Vector2(BarWidth, RowHeight);

            row.Text = MakeText(row.Root.transform, "Text", 13f, NameColor, FontStyles.Normal);
            ((RectTransform)row.Text.transform).anchoredPosition = Vector2.zero;

            var background = MakeImage(row.Root.transform, "Bar", BarBackground, new Vector2(BarWidth, BarHeight));
            background.anchoredPosition = new Vector2(0f, -20f);
            row.Fill = MakeImage(background, "Fill", BarFill, new Vector2(BarWidth, BarHeight));
            row.Fill.anchoredPosition = Vector2.zero;
            return row;
        }

        private static TMP_Text MakeText(Transform parent, string name, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(400f, 20f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSharedMaterial = _fontMaterial;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform MakeImage(Transform parent, string name, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void Place()
        {
            if (_root == null) return;
            var rect = (RectTransform)_root.transform;
            rect.anchoredPosition = new Vector2(OffsetX.Value, -OffsetY.Value);
            rect.localScale = Vector3.one * Scale.Value;
            _nextRefresh = 0f;
        }
    }
}
