namespace SpiritAnimism
{
    internal static class Dismiss
    {
        // Mirrors the game's own unsummon path (Tameable.UnSummon is private): a "RPC_UnSummon" call to
        // everybody plays the despawn effect on each client and the owner destroys the creature.
        // A summon left standing far away may be loaded yet owned by nobody; the game never claims
        // those, so nothing would destroy it. Claiming it first makes this client the owner.
        public static void MySummons()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var dismissed = 0;
            foreach (var summon in Summons.Mine())
            {
                if (!summon.View.HasOwner()) summon.View.ClaimOwnership();
                summon.View.InvokeRPC(ZNetView.Everybody, "RPC_UnSummon");
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
