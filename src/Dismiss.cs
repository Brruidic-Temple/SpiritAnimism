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
            var keptMount = false;
            foreach (var summon in Summons.Mine())
            {
                if (!Saddles.DismissWhileRiding.Value && Saddles.IsRiding(player, summon))
                {
                    keptMount = true;
                    continue;
                }
                if (!summon.View.HasOwner()) summon.View.ClaimOwnership();
                summon.View.InvokeRPC(ZNetView.Everybody, "RPC_UnSummon");
                dismissed++;
            }

            Plugin.Log.LogInfo($"Dismissed {dismissed} summon(s)");
            // One centre message, since a second would replace the first before it is read.
            var message = dismissed > 0 ? $"Dismissed {dismissed} spirit{(dismissed == 1 ? "" : "s")}" : "";
            if (keptMount) message = (message == "" ? "" : message + ". ") + "Dismount to dismiss your mount";
            if (message != "") player.Message(MessageHud.MessageType.Center, message);
        }

        public static bool InputIsFree()
        {
            return !Console.IsVisible() && !Menu.IsVisible() && (Chat.instance == null || !Chat.instance.HasFocus());
        }
    }
}
