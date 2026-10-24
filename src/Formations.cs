using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SpiritAnimism
{
    // One spot in a formation: metres to the player's right and ahead of them, negative for left and
    // behind. A slot may be reserved for one kind of summon.
    internal readonly struct Slot
    {
        public readonly float Right;
        public readonly float Forward;
        public readonly SummonType Kind;

        public Slot(float right, float forward, SummonType kind = null)
        {
            Right = right;
            Forward = forward;
            Kind = kind;
        }

        public Vector3 WorldPosition(Vector3 origin, Vector3 forward)
        {
            var right = Vector3.Cross(Vector3.up, forward);
            return origin + right * Right + forward * Forward;
        }
    }

    // A way of laying out slots for any number of summons. Built-in formations stretch to the count;
    // a custom one from the formations file has fixed slots and overflows into a Leader arc.
    internal abstract class Formation
    {
        public const float RowSpacing = 3f;

        public abstract string Name { get; }

        // Slots for `count` summons, each at least `gap` metres from its neighbours, the nearest row
        // at `distance`. Slots come in the order they should be handed out: oldest summon first.
        public abstract List<Slot> Layout(int count, float distance, float gap);

        public static readonly Formation Leader = new LeaderFormation();
        public static readonly Formation Defense = new DefenseFormation();
        public static readonly Formation Bubble = new BubbleFormation();
        public static readonly Formation[] BuiltIn = { Leader, Defense, Bubble };

        public static Formation BuiltInNamed(string name)
        {
            foreach (var formation in BuiltIn)
            {
                if (string.Equals(formation.Name, name, StringComparison.OrdinalIgnoreCase)) return formation;
            }
            return null;
        }

        // Evenly spaced points on a full ring of radius r, the first straight ahead.
        protected static void Ring(List<Slot> slots, int n, float r)
        {
            for (var i = 0; i < n; i++)
            {
                var angle = i * 2f * Mathf.PI / n;
                slots.Add(new Slot(r * Mathf.Sin(angle), r * Mathf.Cos(angle)));
            }
        }
    }

    // An arc behind the player, further rows back when one is full; the oldest stands in the middle.
    internal sealed class LeaderFormation : Formation
    {
        private const float ArcRadians = 160f * Mathf.Deg2Rad;

        public override string Name => "Leader";

        public override List<Slot> Layout(int count, float distance, float gap)
        {
            var slots = new List<Slot>(count);
            for (var row = 0; slots.Count < count; row++)
            {
                var r = distance + row * RowSpacing;
                var capacity = Mathf.Max(1, Mathf.FloorToInt(r * ArcRadians / gap) + 1);
                var n = Mathf.Min(count - slots.Count, capacity);
                var step = gap / r;

                // Positions along the arc, centred, handed out from the middle outwards.
                var angles = new List<float>(n);
                for (var i = 0; i < n; i++) angles.Add((i - (n - 1) / 2f) * step);
                angles.Sort((a, b) => Mathf.Abs(a).CompareTo(Mathf.Abs(b)));
                foreach (var angle in angles)
                {
                    slots.Add(new Slot(r * Mathf.Sin(angle), -r * Mathf.Cos(angle)));
                }
            }
            return slots;
        }
    }

    // A ring around the player, further rings out when one is full.
    internal sealed class DefenseFormation : Formation
    {
        public override string Name => "Defense";

        public override List<Slot> Layout(int count, float distance, float gap)
        {
            var slots = new List<Slot>(count);
            for (var ring = 0; slots.Count < count; ring++)
            {
                var r = distance + ring * RowSpacing;
                var capacity = Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * r / gap));
                Ring(slots, Mathf.Min(count - slots.Count, capacity), r);
            }
            return slots;
        }
    }

    // One ring around the player that widens until everyone fits.
    internal sealed class BubbleFormation : Formation
    {
        public override string Name => "Bubble";

        public override List<Slot> Layout(int count, float distance, float gap)
        {
            var slots = new List<Slot>(count);
            Ring(slots, count, Mathf.Max(distance, count * gap / (2f * Mathf.PI)));
            return slots;
        }
    }

    // A formation written in the formations file: fixed slots, extra summons in a Leader arc.
    internal sealed class CustomFormation : Formation
    {
        private readonly string _name;
        public readonly List<Slot> Slots;

        public CustomFormation(string name, List<Slot> slots)
        {
            _name = name;
            Slots = slots;
        }

        public override string Name => _name;

        // Overflow rows start a row beyond the furthest listed slot, so they never land on one.
        public override List<Slot> Layout(int count, float distance, float gap)
        {
            var slots = new List<Slot>(Slots);
            if (count > slots.Count)
            {
                var reach = 0f;
                foreach (var slot in Slots)
                {
                    if (slot.Forward > gap) continue; // well ahead of the player, out of any arc's way
                    reach = Mathf.Max(reach, Mathf.Sqrt(slot.Right * slot.Right + slot.Forward * slot.Forward));
                }
                var overflowDistance = Mathf.Max(distance, reach + Mathf.Max(RowSpacing, gap));
                slots.AddRange(Leader.Layout(count - slots.Count, overflowDistance, gap));
            }
            return slots;
        }
    }

    // Keeps a player's following summons in formation instead of letting them all crowd the player's
    // exact position, which is what the game's own follow rule does. Runs on whichever machine owns
    // the summon, like the rest of its AI. The summoner's choice of formation and distance travels
    // with the summon by name; a machine that does not know the name uses Leader.
    internal static class Formations
    {
        private const string FileName = "Brruid.SpiritAnimism.Formations.cfg";
        internal static readonly int FormationKey = "SpiritAnimism.Formation".GetStableHashCode();
        private const string SpecVersion = "1";

        private const float RefreshSeconds = 0.25f;
        private const float FilePollSeconds = 1f;
        private const float DeadZone = 2f;
        private const float ArriveWithin = 1f;
        private const float RunBeyond = 10f;
        private const float SettleAfterSeconds = 1f;
        private const float SettleBreakMargin = 3f;
        private const float FreezeDistance = 5f;
        private const float FreezeFacingDot = 0.8f;

        internal static ConfigEntry<string> Chosen;
        internal static ConfigEntry<float> Distance;
        internal static ConfigEntry<float> Buffer;

        private static string _filePath;
        private static DateTime _fileLastWrite;
        private static float _nextFilePoll;
        private static readonly Dictionary<string, CustomFormation> _custom = new Dictionary<string, CustomFormation>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _warnedUnknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // How one player's formation stands right now: where it is anchored and which summon goes where.
        private sealed class FollowState
        {
            public Vector3 LastPosition;
            public float StillSince;
            public bool Settled;
            public Vector3 AnchorPosition;
            public Vector3 AnchorForward;
            public float NextRefresh;
            public readonly Dictionary<ZDOID, Vector3> Targets = new Dictionary<ZDOID, Vector3>();
        }

        private static readonly Dictionary<string, FollowState> _states = new Dictionary<string, FollowState>();

        // BaseAI.MoveTo is protected; this calls it on any BaseAI.
        private static readonly Func<BaseAI, float, Vector3, float, bool, bool> MoveTo =
            (Func<BaseAI, float, Vector3, float, bool, bool>)Delegate.CreateDelegate(
                typeof(Func<BaseAI, float, Vector3, float, bool, bool>),
                AccessTools.Method(typeof(BaseAI), "MoveTo"));

        public static void Init(ConfigFile config)
        {
            Chosen = config.Bind(
                "Formations",
                "Formation",
                "Leader",
                "How your summons arrange themselves around you while following. Leader = an arc behind you. Defense = a ring around you, more rings when full. Bubble = one ring that widens to fit. Or the name of a formation from " + FileName + " beside this file.");
            Distance = config.Bind(
                "Formations",
                "Distance",
                5f,
                new ConfigDescription("How far the nearest row or ring stands from you, in metres.", new AcceptableValueRange<float>(3f, 20f)));
            Buffer = config.Bind(
                "Formations",
                "Buffer",
                1f,
                new ConfigDescription("Air between animals, in metres: the gap between their spots in formation, and how wide a walking animal steers around anyone standing in its way, you and your mount included. 0 = bodies touch and nobody steers.", new AcceptableValueRange<float>(0f, 5f)));

            _filePath = Path.Combine(Path.GetDirectoryName(config.ConfigFilePath), FileName);
            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, DefaultFile);
                Plugin.Log.LogInfo($"Formations file created at {_filePath}");
            }
            LoadFile();
        }

        public static void Update()
        {
            if (Time.unscaledTime < _nextFilePoll) return;
            _nextFilePoll = Time.unscaledTime + FilePollSeconds;
            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, DefaultFile);
                LoadFile();
                Plugin.Log.LogInfo("Formations file recreated");
                return;
            }
            if (File.GetLastWriteTimeUtc(_filePath) != _fileLastWrite) LoadFile();
        }

        // The file is [Name] sections of "SlotN = right, forward" or "SlotN = Kind, right, forward".
        private static void LoadFile()
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(_filePath);
            }
            catch (IOException e)
            {
                // The editor may still hold the file; the next poll tries again.
                Plugin.Log.LogWarning($"Formations file not read yet: {e.Message}");
                return;
            }
            _fileLastWrite = File.GetLastWriteTimeUtc(_filePath);
            _custom.Clear();
            _warnedUnknown.Clear();

            string section = null;
            List<Slot> slots = null;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    if (Formation.BuiltInNamed(section) != null)
                    {
                        Plugin.Log.LogWarning($"Formations file line {i + 1}: {section} is built in and cannot be redefined");
                        section = null;
                        slots = null;
                        continue;
                    }
                    slots = new List<Slot>();
                    _custom[section] = new CustomFormation(section, slots);
                    continue;
                }
                if (slots == null) continue;

                var equals = line.IndexOf('=');
                if (equals < 0) continue;
                var parts = line.Substring(equals + 1).Split(',');
                SummonType kind = null;
                var first = 0;
                if (parts.Length == 3)
                {
                    kind = Summons.Named(parts[0].Trim());
                    if (kind == null)
                    {
                        Plugin.Log.LogWarning($"Formations file line {i + 1}: unknown kind {parts[0].Trim()}; use Bear, Boar, Moose or Wolf");
                        continue;
                    }
                    first = 1;
                }
                else if (parts.Length != 2)
                {
                    Plugin.Log.LogWarning($"Formations file line {i + 1}: expected 'right, forward' or 'Kind, right, forward'");
                    continue;
                }
                if (!TryParseMetres(parts[first], out var right) || !TryParseMetres(parts[first + 1], out var forward))
                {
                    Plugin.Log.LogWarning($"Formations file line {i + 1}: coordinates must be numbers in metres");
                    continue;
                }
                slots.Add(new Slot(right, forward, kind));
            }
            Plugin.Log.LogInfo($"Formations file loaded: {_custom.Count} custom formation(s)");
        }

        private static bool TryParseMetres(string text, out float value) =>
            float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

        // The formation a name means on this machine: built in, from the file, or Leader when unknown.
        private static Formation Named(string name)
        {
            var formation = Formation.BuiltInNamed(name);
            if (formation != null) return formation;
            if (_custom.TryGetValue(name, out var custom)) return custom;
            if (_warnedUnknown.Add(name)) Plugin.Log.LogWarning($"Unknown formation {name}; using Leader");
            return Formation.Leader;
        }

        // The formation and distance to use for a summon: the local config for the local player's
        // summons, written into the summon for other machines; otherwise what the summon carries.
        private static bool SpecFor(Summon summon, Player summoner, out Formation formation, out float distance)
        {
            var zdo = summon.Zdo;
            if (summoner == Player.m_localPlayer)
            {
                formation = Named(Chosen.Value);
                distance = Distance.Value;
                var spec = $"{SpecVersion};{formation.Name};{distance.ToString("0.#", CultureInfo.InvariantCulture)}";
                if (summon.View.IsOwner() && zdo.GetString(FormationKey, "") != spec) zdo.Set(FormationKey, spec);
                return true;
            }

            var parts = zdo.GetString(FormationKey, "").Split(';');
            if (parts.Length != 3 || parts[0] != SpecVersion || !TryParseMetres(parts[2], out distance))
            {
                formation = null;
                distance = 0f;
                return false;
            }
            formation = Named(parts[1]);
            distance = Mathf.Clamp(distance, 3f, 20f);
            return true;
        }

        // Where this summon should stand, or null to leave the game's own follow rule in charge.
        private static Vector3? TargetFor(Summon summon, Player summoner)
        {
            if (!SpecFor(summon, summoner, out var formation, out var distance)) return null;

            var name = summoner.GetPlayerName();
            if (!_states.TryGetValue(name, out var state))
            {
                state = new FollowState { LastPosition = summoner.transform.position, StillSince = Time.time };
                _states[name] = state;
            }
            if (Time.time >= state.NextRefresh)
            {
                state.NextRefresh = Time.time + RefreshSeconds;
                Refresh(state, summoner, formation, distance);
            }
            return state.Targets.TryGetValue(summon.Zdo.m_uid, out var target) ? target : (Vector3?)null;
        }

        private static void Refresh(FollowState state, Player summoner, Formation formation, float distance)
        {
            var position = summoner.transform.position;
            var forward = Flat(summoner.transform.forward);

            // Settle once the player has stood still a moment; break once they walk well away.
            if (Vector3.Distance(position, state.LastPosition) > 0.2f)
            {
                state.LastPosition = position;
                state.StillSince = Time.time;
            }
            if (!state.Settled && Time.time - state.StillSince >= SettleAfterSeconds)
            {
                state.Settled = true;
                state.AnchorPosition = position;
                state.AnchorForward = forward;
            }
            else if (state.Settled && Vector3.Distance(position, state.AnchorPosition) > distance + SettleBreakMargin)
            {
                state.Settled = false;
            }
            var origin = state.Settled ? state.AnchorPosition : position;
            var facing = state.Settled ? state.AnchorForward : forward;

            // Everyone this player called that is standing here and not being ridden, oldest first.
            var followers = new List<Summon>();
            var playerName = summoner.GetPlayerName();
            var largestRadius = 0f;
            foreach (var summon in Summons.Standing())
            {
                if (!summon.CalledBy(playerName)) continue;
                if (summon.Zdo.GetLong(ZDOVars.s_user, 0L) != 0L) continue;
                followers.Add(summon);
                largestRadius = Mathf.Max(largestRadius, summon.Character.GetRadius());
            }
            followers.Sort((a, b) => Age(b).CompareTo(Age(a)));
            var gap = 2f * largestRadius + Buffer.Value;

            state.Targets.Clear();
            var slots = formation.Layout(followers.Count, distance, gap);
            foreach (var pair in Assign(followers, slots))
            {
                state.Targets[pair.Key.Zdo.m_uid] = pair.Value.WorldPosition(origin, facing);
            }
        }

        private static TimeSpan Age(Summon summon)
        {
            var ai = summon.Character.GetComponent<BaseAI>();
            return ai != null ? ai.GetTimeSinceSpawned() : TimeSpan.Zero;
        }

        // Reserved slots go to their kind first, oldest first; the rest go out by age; the overflow
        // slots that Layout appended take whoever is left.
        private static List<KeyValuePair<Summon, Slot>> Assign(List<Summon> followers, List<Slot> slots)
        {
            var result = new List<KeyValuePair<Summon, Slot>>(followers.Count);
            var waiting = new List<Summon>(followers);
            var open = new List<Slot>();
            foreach (var slot in slots)
            {
                if (slot.Kind == null)
                {
                    open.Add(slot);
                    continue;
                }
                var index = waiting.FindIndex(s => s.Species == slot.Kind);
                if (index < 0) continue;
                result.Add(new KeyValuePair<Summon, Slot>(waiting[index], slot));
                waiting.RemoveAt(index);
            }
            for (var i = 0; i < waiting.Count && i < open.Count; i++)
            {
                result.Add(new KeyValuePair<Summon, Slot>(waiting[i], open[i]));
            }
            return result;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.001f ? Vector3.forward : direction.normalized;
        }

        // Replaces the game's follow rule for summons with a formation: head for the slot, stand still
        // once near it, and hold still when the player comes up close looking at you.
        [HarmonyPatch(typeof(BaseAI), "Follow")]
        private static class FollowPatch
        {
            private static bool Prefix(BaseAI __instance, GameObject go, float dt, Character ___m_character)
            {
                if (___m_character == null || go == null) return true;
                var species = Summons.SpeciesOf(___m_character.gameObject.name);
                if (species == null) return true;
                var summoner = go.GetComponent<Player>();
                if (summoner == null) return true;
                var view = ___m_character.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) return true;

                var summon = new Summon(___m_character, species, view);
                var target = TargetFor(summon, summoner);
                if (target == null) return true;

                // Hold still when the player comes up close looking at you, so they can pet or saddle
                // you. Not while they ride, when they can do neither; and not when your own slot lies
                // ahead of them, since then you are crossing their view on purpose and must keep walking.
                var position = ___m_character.transform.position;
                if (!summoner.IsAttached() && InFrontOf(summoner, position) && !AheadOf(summoner, target.Value))
                {
                    __instance.StopMoving();
                    return false;
                }

                var remaining = Utils.DistanceXZ(target.Value, position);
                if (remaining < DeadZone)
                {
                    __instance.StopMoving();
                    return false;
                }
                var waypoint = Buffer.Value > 0f ? AroundOthers(summon, position, target.Value) : target.Value;
                MoveTo(__instance, dt, waypoint, ArriveWithin, remaining > RunBeyond);
                return false;
            }

            private static bool InFrontOf(Player player, Vector3 point) =>
                Vector3.Distance(point, player.transform.position) < FreezeDistance && AheadOf(player, point);

            private static bool AheadOf(Player player, Vector3 point) =>
                Vector3.Dot(Flat(player.transform.forward), Flat(point - player.transform.position)) > FreezeFacingDot;

            // Which side each summon last passed a blocker on, so a blocker dead ahead is not dodged
            // left one moment and right the next.
            private static readonly Dictionary<ZDOID, float> _passSide = new Dictionary<ZDOID, float>();
            private const float UndecidedAcross = 0.5f;

            // The game walks a follower straight at its path, blind to other characters, and lets them
            // shove each other apart. This finds the first character whose body the straight line to
            // the target would cut through, and aims beside it instead, a Buffer's width clear.
            private static Vector3 AroundOthers(Summon summon, Vector3 from, Vector3 target)
            {
                var span = Utils.DistanceXZ(target, from);
                if (span < 0.01f) return target;
                var dir = Flat(target - from);
                var right = Vector3.Cross(Vector3.up, dir);
                var self = summon.Character;

                Character blocker = null;
                var blockerStart = span;
                var blockerAcross = 0f;
                var blockerClearance = 0f;
                foreach (var other in Character.GetAllCharacters())
                {
                    if (other == self || other.IsDead()) continue;
                    var offset = other.transform.position - from;
                    offset.y = 0f;
                    var along = Vector3.Dot(offset, dir);
                    if (along <= 0f || along >= span) continue;
                    var across = Vector3.Dot(offset, right);
                    var clearance = self.GetRadius() + other.GetRadius() + Buffer.Value;
                    if (Mathf.Abs(across) >= clearance) continue;
                    // The body whose clearance circle the line enters first wins, so a rider's wide
                    // mount counts over the rider sitting on it.
                    if (along - clearance >= blockerStart) continue;
                    blocker = other;
                    blockerStart = along - clearance;
                    blockerAcross = across;
                    blockerClearance = clearance;
                }
                if (blocker == null) return target;

                var id = summon.Zdo.m_uid;
                float side;
                if (Mathf.Abs(blockerAcross) >= UndecidedAcross || !_passSide.TryGetValue(id, out side))
                {
                    side = blockerAcross >= 0f ? -1f : 1f;
                }
                _passSide[id] = side;
                return blocker.transform.position + right * (side * blockerClearance);
            }
        }

        private const string DefaultFile =
@"## SpiritAnimism formations. Each [Name] below is a formation you can pick with
## Formation = Name in Brruid.SpiritAnimism.cfg. One line per slot:
##   SlotN = right, forward          metres from you; negative = left / behind
##   SlotN = Kind, right, forward    reserved for one kind: Bear, Boar, Moose or Wolf
## You stand at 0, 0 and the shape turns with you. A reserved slot stays empty when
## that kind is not out. Summons beyond the listed slots form a Leader arc behind you.
## Leader, Defense and Bubble are built in and cannot be redefined here.
## Edits apply while you play.

[Escort]
Slot1 = Bear, 3, 10
Slot2 = Bear, -3, 10
Slot3 = Wolf, 3, -2
Slot4 = Wolf, -3, -2
Slot5 = 0, -5
Slot6 = 2.5, -6
Slot7 = -2.5, -6
";
    }
}
