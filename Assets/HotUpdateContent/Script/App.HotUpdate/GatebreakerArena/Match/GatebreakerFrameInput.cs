using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public readonly struct GatebreakerFrameInput
    {
        public GatebreakerFrameInput(int playerId, float moveAxis, bool servePressed, Vector2 aimDirection, bool abilityPressed = false)
        {
            PlayerId = playerId;
            MoveAxis = moveAxis;
            ServePressed = servePressed;
            AimDirection = aimDirection;
            AbilityPressed = abilityPressed;
        }

        public int PlayerId { get; }
        public float MoveAxis { get; }
        public bool ServePressed { get; }
        public Vector2 AimDirection { get; }
        public bool AbilityPressed { get; }
    }
}
