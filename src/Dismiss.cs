using UnityEngine;

namespace SpiritAnimism
{
    internal static class Dismiss
    {
        // Mirrors the game's own unsummon path (Tameable.UnSummon is private): a "RPC_UnSummon" call to
        // everybody plays the despawn effect on each client and the owner destroys the creature. The game
        // identifies a summoner's summons by the ZDO follow string matching the player name.
        public static void MySummons()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var playerName = player.GetPlayerName();
            var dismissed = 0;

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || !SummonLooks.IsSummon(character.gameObject.name)) continue;

                var nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;
                if (nview.GetZDO().GetString(ZDOVars.s_follow, "") != playerName) continue;

                nview.InvokeRPC(ZNetView.Everybody, "RPC_UnSummon");
                dismissed++;
            }

            Plugin.Log.LogInfo($"Dismissed {dismissed} summon(s)");
            if (dismissed > 0) player.Message(MessageHud.MessageType.Center, $"Dismissed {dismissed} spirit{(dismissed == 1 ? "" : "s")}");
        }

        public static bool InputIsFree()
        {
            return !Console.IsVisible() && !Menu.IsVisible() && (Chat.instance == null || !Chat.instance.HasFocus());
        }
    }
}
