using System.Collections.Generic;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Application
{
    public sealed class GatebreakerInputService
    {
        private readonly Dictionary<int, PlayerInputFrame> _frames = new Dictionary<int, PlayerInputFrame>();

        public void SetFrame(PlayerInputFrame frame)
        {
            _frames[frame.PlayerId] = frame;
        }

        public PlayerInputFrame GetFrame(int playerId)
        {
            return _frames.TryGetValue(playerId, out PlayerInputFrame frame)
                ? frame
                : new PlayerInputFrame(playerId, 0f, false, Vector2.zero);
        }

        public void ClearServeRequests()
        {
            var keys = new List<int>(_frames.Keys);
            foreach (int playerId in keys)
            {
                PlayerInputFrame frame = _frames[playerId];
                _frames[playerId] = new PlayerInputFrame(frame.PlayerId, frame.MoveAxis, false, frame.AimDirection, frame.AbilityPressed);
            }
        }
    }

    public readonly struct PlayerInputFrame
    {
        public PlayerInputFrame(int playerId, float moveAxis, bool servePressed, Vector2 aimDirection, bool abilityPressed = false)
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
