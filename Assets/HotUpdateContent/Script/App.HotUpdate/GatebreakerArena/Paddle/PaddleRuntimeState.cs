using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Paddle
{
    public sealed class PaddleRuntimeState
    {
        public int PlayerId { get; set; }
        public int TeamId { get; set; }
        public Vector2 Normal { get; set; }
        public Vector2 Tangent { get; set; }
        public Vector2 Position { get; set; }
        public float AxisPosition { get; set; }
        public float MoveAxis { get; set; }
        public float TangentVelocity { get; set; }
        public float Length { get; set; }
        public float Thickness { get; set; }
        public float Speed { get; set; }
    }
}
