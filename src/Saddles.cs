using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SpiritAnimism
{
    // Gives a saddle back when the summon wearing it is unsummoned. The game drops a saddle on death
    // but lets it vanish on unsummon, whether by the Dismiss key or by its own distance, logout and
    // cap rules. Only a player's own game can put an item into their inventory, and the game running
    // the summon is often another player's, so that game asks the fitter's game to take the saddle,
    // waits for a reply, then asks the summoner's game, and drops the saddle on the ground if nobody
    // answers. See docs/adr/0001.
    internal static class Saddles
    {
        private const string FitterKey = "SpiritAnimism.Fitter";
        private const string TakeRpc = "SpiritAnimism_TakeSaddle";
        private const string TakenRpc = "SpiritAnimism_SaddleTaken";
        private const float ReplySeconds = 2f;

        internal static ConfigEntry<bool> ReturnSaddle;
        internal static ConfigEntry<bool> DismissWhileRiding;

        // One saddle on its way back: who is still to be asked, and where to drop it if nobody takes it.
        private sealed class Pending
        {
            public long Id;
            public string PrefabName;
            public Vector3 Position;
            public Queue<string> Recipients;
            public float Deadline;
        }

        private static readonly List<Pending> _pending = new List<Pending>();
        private static ZRoutedRpc _registeredOn;
        private static long _nextId = 1;

        public static void Init(ConfigFile config)
        {
            ReturnSaddle = config.Bind(
                "Dismiss",
                "ReturnSaddle",
                true,
                "A saddle on a dismissed or unsummoned animal goes back to the player who fitted it, failing that to the summoner, failing that it drops where the animal stood. Off = the saddle is lost, as in the unmodded game.");
            DismissWhileRiding = config.Bind(
                "Dismiss",
                "DismissWhileRiding",
                true,
                "Whether Dismiss also removes the animal you are riding. Off = your mount stays and the others go.");
        }

        public static void Update()
        {
            RegisterRpcs();
            if (_pending.Count == 0) return;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime >= _pending[i].Deadline) Advance(_pending[i]);
            }
        }

        // The routed RPC table belongs to the world session, so register again whenever a new one appears.
        private static void RegisterRpcs()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredOn) return;
            rpc.Register<string, string, long>(TakeRpc, OnTakeSaddle);
            rpc.Register<long>(TakenRpc, OnSaddleTaken);
            _registeredOn = rpc;
            _pending.Clear();
        }

        // Called on the game that owns the summon, just before the summon is destroyed.
        public static void Return(string prefabName, Vector3 position, string fitter, string summoner)
        {
            var recipients = new Queue<string>();
            if (!string.IsNullOrEmpty(fitter)) recipients.Enqueue(fitter);
            if (!string.IsNullOrEmpty(summoner) && summoner != fitter) recipients.Enqueue(summoner);

            var pending = new Pending { Id = _nextId++, PrefabName = prefabName, Position = position, Recipients = recipients };
            _pending.Add(pending);
            Advance(pending);
        }

        // Asks the next connected recipient, or drops the saddle when nobody is left to ask.
        private static void Advance(Pending pending)
        {
            while (pending.Recipients.Count > 0)
            {
                var name = pending.Recipients.Dequeue();
                var player = Player.m_localPlayer;
                if (player != null && player.GetPlayerName() == name)
                {
                    Take(player, pending.PrefabName);
                    _pending.Remove(pending);
                    return;
                }
                if (!IsConnected(name)) continue;

                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, TakeRpc, name, pending.PrefabName, pending.Id);
                pending.Deadline = Time.unscaledTime + ReplySeconds;
                Plugin.Log.LogInfo($"Asked {name} to take a {pending.PrefabName}");
                return;
            }

            Drop(pending.PrefabName, pending.Position);
            _pending.Remove(pending);
        }

        private static void OnTakeSaddle(long sender, string recipient, string prefabName, long id)
        {
            var player = Player.m_localPlayer;
            if (player == null || player.GetPlayerName() != recipient) return;
            Take(player, prefabName);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, TakenRpc, id);
        }

        private static void OnSaddleTaken(long sender, long id)
        {
            var taken = _pending.RemoveAll(p => p.Id == id);
            if (taken > 0) Plugin.Log.LogInfo("Saddle taken by its recipient");
        }

        // Spawns the saddle at the player's feet and picks it up the way walking over it would, with
        // the game's own pickup message. A full inventory leaves it lying there with the game's message.
        private static void Take(Player player, string prefabName)
        {
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Plugin.Log.LogWarning($"Unknown saddle prefab {prefabName}");
                return;
            }
            var item = Object.Instantiate(prefab, player.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            player.Pickup(item, autoequip: false, autoPickupDelay: false);
        }

        // Mirrors the game's own saddle drop: spawn above the animal's position and toss it upwards.
        private static void Drop(string prefabName, Vector3 position)
        {
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Plugin.Log.LogWarning($"Unknown saddle prefab {prefabName}");
                return;
            }
            var item = Object.Instantiate(prefab, position + Vector3.up, Quaternion.identity);
            var body = item.GetComponent<Rigidbody>();
            if (body != null) body.AddForce(Vector3.up * 5f, ForceMode.VelocityChange);
            Plugin.Log.LogInfo($"Dropped a {prefabName} where the summon stood");
        }

        private static bool IsConnected(string playerName)
        {
            if (ZNet.instance == null) return false;
            foreach (var info in ZNet.instance.GetPlayerList())
            {
                if (info.m_name == playerName) return true;
            }
            return false;
        }

        // The name of the player whose game sent a routed RPC. A client only holds a connection to
        // the server, so other players are found through the player list, by session id.
        private static string SenderName(long sender)
        {
            if (sender == ZNet.GetUID()) return Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "";
            if (ZNet.instance == null) return "";
            foreach (var info in ZNet.instance.GetPlayerList())
            {
                if (info.m_characterID.UserID == sender) return info.m_name;
            }
            return "";
        }

        public static bool HasSaddle(Tameable tameable, ZNetView view) =>
            tameable.m_saddle != null && tameable.m_saddleItem != null && view.GetZDO().GetBool(ZDOVars.s_haveSaddleHash);

        // The local player is in the saddle of this summon.
        public static bool IsRiding(Player player, Summon summon)
        {
            var controller = player.GetDoodadController() as Sadle;
            if (controller == null) return false;
            var tameable = summon.Character.GetComponent<Tameable>();
            return tameable != null && tameable.m_saddle == controller;
        }

        // The owner records who fitted the saddle, since the game itself only records that one is on.
        [HarmonyPatch(typeof(Tameable), "RPC_AddSaddle")]
        private static class AddSaddlePatch
        {
            private static void Prefix(Tameable __instance, ZNetView ___m_nview, out bool __state)
            {
                __state = ___m_nview.IsValid() && HasSaddle(__instance, ___m_nview);
            }

            private static void Postfix(Tameable __instance, ZNetView ___m_nview, long sender, bool __state)
            {
                if (__state || !___m_nview.IsValid() || !___m_nview.IsOwner()) return;
                if (Summons.SpeciesOf(__instance.gameObject.name) == null) return;
                if (!HasSaddle(__instance, ___m_nview)) return;
                var fitter = SenderName(sender);
                ___m_nview.GetZDO().Set(FitterKey, fitter);
                Plugin.Log.LogInfo($"Saddle fitted by {(fitter == "" ? "an unknown player" : fitter)}");
            }
        }

        // Every game runs the unsummon RPC; the owner is the one about to destroy the summon, so it
        // starts the return before the saddle goes with it.
        [HarmonyPatch(typeof(Tameable), "RPC_UnSummon")]
        private static class UnSummonPatch
        {
            private static void Prefix(Tameable __instance, ZNetView ___m_nview)
            {
                if (!ReturnSaddle.Value) return;
                if (___m_nview == null || !___m_nview.IsValid() || !___m_nview.IsOwner()) return;
                if (Summons.SpeciesOf(__instance.gameObject.name) == null) return;
                if (!HasSaddle(__instance, ___m_nview)) return;

                var zdo = ___m_nview.GetZDO();
                Return(
                    __instance.m_saddleItem.gameObject.name,
                    __instance.transform.position,
                    zdo.GetString(FitterKey, ""),
                    zdo.GetString(ZDOVars.s_follow, ""));
            }
        }
    }
}
