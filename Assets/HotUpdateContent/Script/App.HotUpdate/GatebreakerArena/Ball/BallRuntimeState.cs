using App.HotUpdate.GatebreakerArena.Core;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Ball
{
    public sealed class BallRuntimeState
    {
        public int BallId { get; set; }
        public int OwnerPlayerId { get; set; }
        public int OwnerTeamId { get; set; }
        public string SpawnSourceType { get; set; }
        public string BallTypeId { get; set; }
        public BallState BallState { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
    }
}
