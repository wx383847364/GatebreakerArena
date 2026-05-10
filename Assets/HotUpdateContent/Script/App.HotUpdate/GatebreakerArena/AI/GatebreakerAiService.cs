using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Match;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.AI
{
    public sealed class GatebreakerAiService
    {
        public PlayerInputFrame BuildFrame(PlayerRuntimeState player, GatebreakerMatchRuntime runtime)
        {
            if (player == null)
            {
                return new PlayerInputFrame(0, 0f, false, Vector2.zero);
            }

            bool shouldServe = runtime != null &&
                               player.ServeResource != null &&
                               player.ServeResource.CurrentServeAmmo > 0 &&
                               player.ServeResource.ServeCooldownRemaining <= 0f;
            return new PlayerInputFrame(player.PlayerId, 0f, shouldServe, Vector2.zero);
        }
    }
}
