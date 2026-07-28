using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public sealed class BrickDuelAiController
    {
        private readonly AiRuleDefinition _rule;
        private readonly int _reactionFrames;
        private readonly uint _seed;
        private GatebreakerDeterministicPrng _random;
        private int _framesUntilReaction;
        private float _targetX;

        public BrickDuelAiController(AiRuleDefinition rule, int simulationFps, uint seed)
        {
            _rule = rule;
            _reactionFrames = Mathf.Max(1, Mathf.RoundToInt(rule.ReactionDelay * simulationFps));
            _seed = seed;
            Reset();
        }

        public uint RandomState => _random.State;
        public int FramesUntilReaction => _framesUntilReaction;
        public float TargetX => _targetX;

        public void Reset()
        {
            _random = new GatebreakerDeterministicPrng(_seed);
            _framesUntilReaction = 0;
            _targetX = 0f;
        }

        public float Step(
            BrickDuelBallState ball,
            BrickDuelPaddleState paddle,
            float paddleY,
            float arenaHalfWidth)
        {
            if (_framesUntilReaction <= 0)
            {
                _targetX = PredictTargetX(ball, paddleY, arenaHalfWidth) + NextError();
                _targetX = Mathf.Clamp(_targetX, -arenaHalfWidth, arenaHalfWidth);
                _framesUntilReaction = _reactionFrames - 1;
            }
            else
            {
                _framesUntilReaction--;
            }

            float delta = _targetX - paddle.Position.x;
            if (Mathf.Abs(delta) <= 0.04f)
            {
                return 0f;
            }

            return Mathf.Sign(delta);
        }

        private float PredictTargetX(BrickDuelBallState ball, float paddleY, float arenaHalfWidth)
        {
            if (ball == null || !ball.IsActive || Mathf.Abs(ball.Velocity.y) < 0.0001f)
            {
                return 0f;
            }

            float travelTime = (paddleY - ball.Position.y) / ball.Velocity.y;
            if (travelTime <= 0f)
            {
                return ball.Position.x;
            }

            float rawX = ball.Position.x + ball.Velocity.x * travelTime;
            float span = arenaHalfWidth * 2f;
            if (span <= 0.0001f)
            {
                return 0f;
            }

            float shifted = rawX + arenaHalfWidth;
            float period = span * 2f;
            shifted %= period;
            if (shifted < 0f)
            {
                shifted += period;
            }
            if (shifted > span)
            {
                shifted = period - shifted;
            }

            return shifted - arenaHalfWidth;
        }

        private float NextError()
        {
            float normalized = (_random.NextUInt() & 0x00FFFFFFu) / 16777215f;
            return (normalized * 2f - 1f) * Mathf.Max(0f, _rule.PredictError);
        }
    }
}
