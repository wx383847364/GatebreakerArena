using App.HotUpdate.GatebreakerArena.Application;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.UI
{
    // UI input adapter. Views pass their explicit button state here; the match remains
    // responsible for consuming AbilityPressed and applying hero rules.
    public sealed class GatebreakerArenaInputPresenter
    {
        public PlayerInputFrame BuildFrame(
            int playerId,
            float moveAxis,
            bool servePressed,
            Vector2 aimDirection,
            bool abilityPressed)
        {
            return new PlayerInputFrame(playerId, moveAxis, servePressed, aimDirection, abilityPressed);
        }
    }
}
