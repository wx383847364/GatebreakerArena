using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public readonly struct GatebreakerFrameInput
    {
        public GatebreakerFrameInput(int playerId, float moveAxis, bool servePressed, Vector2 aimDirection)
        {
            PlayerId = playerId;
            MoveAxis = moveAxis;
            ServePressed = servePressed;
            AimDirection = aimDirection;
        }

        public int PlayerId { get; }
        public float MoveAxis { get; }
        public bool ServePressed { get; }
        public Vector2 AimDirection { get; }
    }
}
