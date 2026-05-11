using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Zone
{
    public sealed class ZoneRuntimeState
    {
        public int PlayerId { get; set; }
        public int TeamId { get; set; }
        public Vector2 Normal { get; set; }
        public Vector2 Center { get; set; }
        public float HalfLength { get; set; }
        public bool IsDanger { get; set; }
        public int LastEnteredBallId { get; set; }
    }
}
