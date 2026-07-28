using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public sealed class BrickDuelRuntime
    {
        private readonly BrickDuelRuleDefinition _rule;
        private readonly BrickDuelCollisionSolver _collisionSolver;
        private readonly BrickDuelAiController _aiController;
        private readonly List<BrickDuelBrickState> _bricks = new List<BrickDuelBrickState>();
        private readonly HashSet<int> _bottomHitBrickIds = new HashSet<int>();
        private readonly HashSet<int> _topHitBrickIds = new HashSet<int>();
        private GatebreakerDeterministicPrng _rowRandom;
        private int _nextBrickId;
        private float _rowTravelSinceSpawn;

        public BrickDuelRuntime(BrickDuelRuleDefinition rule, AiRuleDefinition aiRule)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            if (aiRule == null)
            {
                throw new ArgumentNullException(nameof(aiRule));
            }

            _collisionSolver = new BrickDuelCollisionSolver();
            _aiController = new BrickDuelAiController(
                aiRule,
                rule.SimulationFps,
                unchecked((uint)rule.RandomSeed) ^ 0xA17E91u);
            BottomPaddle = new BrickDuelPaddleState { Side = BrickDuelSide.Bottom };
            TopPaddle = new BrickDuelPaddleState { Side = BrickDuelSide.Top };
            BottomBall = new BrickDuelBallState { Side = BrickDuelSide.Bottom };
            TopBall = new BrickDuelBallState { Side = BrickDuelSide.Top };
            LastFrameEvents = new BrickDuelFrameEvents();
            ResetToWaiting();
        }

        public BrickDuelRuleDefinition Rule => _rule;
        public BrickDuelPhase Phase { get; private set; }
        public BrickDuelResult Result { get; private set; }
        public bool IsPaused { get; private set; }
        public int SimulationFrame { get; private set; }
        public int CountdownFramesRemaining { get; private set; }
        public int ElapsedFrames { get; private set; }
        public int BottomCoreHealth { get; private set; }
        public int TopCoreHealth { get; private set; }
        public BrickDuelPaddleState BottomPaddle { get; }
        public BrickDuelPaddleState TopPaddle { get; }
        public BrickDuelBallState BottomBall { get; }
        public BrickDuelBallState TopBall { get; }
        public IReadOnlyList<BrickDuelBrickState> Bricks => _bricks;
        public BrickDuelFrameEvents LastFrameEvents { get; }
        public float FrameDelta => 1f / Mathf.Max(1, _rule.SimulationFps);
        public int PressureLevel =>
            Mathf.FloorToInt(ElapsedFrames / (float)PressureIntervalFrames);
        public float PressureMultiplier =>
            1f + _rule.PressureIncrement * Mathf.Max(0, PressureLevel);
        public int PressureIntervalFrames =>
            Mathf.Max(1, Mathf.RoundToInt(_rule.PressureIntervalSeconds * _rule.SimulationFps));
        public int FramesUntilPressureIncrease
        {
            get
            {
                int remainder = ElapsedFrames % PressureIntervalFrames;
                return remainder == 0 && ElapsedFrames > 0
                    ? PressureIntervalFrames
                    : PressureIntervalFrames - remainder;
            }
        }

        public float BottomDangerDistance => GetDangerDistance(BrickDuelSide.Bottom);
        public float TopDangerDistance => GetDangerDistance(BrickDuelSide.Top);

        public void BeginCountdown()
        {
            SimulationFrame = 0;
            ElapsedFrames = 0;
            Result = BrickDuelResult.None;
            IsPaused = false;
            BottomCoreHealth = _rule.InitialCoreHealth;
            TopCoreHealth = _rule.InitialCoreHealth;
            CountdownFramesRemaining = _rule.CountdownSeconds * _rule.SimulationFps;
            _nextBrickId = 1;
            _rowTravelSinceSpawn = 0f;
            _rowRandom = new GatebreakerDeterministicPrng(unchecked((uint)_rule.RandomSeed));
            _aiController.Reset();
            _bricks.Clear();
            SpawnInitialRows();
            ResetPaddles();
            PositionBallForServe(BottomBall);
            PositionBallForServe(TopBall);
            BottomBall.IsActive = false;
            TopBall.IsActive = false;
            LastFrameEvents.Clear();
            Phase = BrickDuelPhase.Countdown;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = Phase == BrickDuelPhase.Playing && paused;
        }

        public void ResetToWaiting()
        {
            Phase = BrickDuelPhase.Waiting;
            Result = BrickDuelResult.None;
            IsPaused = false;
            SimulationFrame = 0;
            ElapsedFrames = 0;
            CountdownFramesRemaining = 0;
            BottomCoreHealth = _rule.InitialCoreHealth;
            TopCoreHealth = _rule.InitialCoreHealth;
            _bricks.Clear();
            ResetPaddles();
            PositionBallForServe(BottomBall);
            PositionBallForServe(TopBall);
            BottomBall.IsActive = false;
            TopBall.IsActive = false;
            LastFrameEvents.Clear();
        }

        public void StepFrame(BrickDuelFrameInput input)
        {
            LastFrameEvents.Clear();
            if (Phase == BrickDuelPhase.Waiting || Phase == BrickDuelPhase.Result || IsPaused)
            {
                return;
            }

            SimulationFrame++;
            if (Phase == BrickDuelPhase.Countdown)
            {
                CountdownFramesRemaining = Mathf.Max(0, CountdownFramesRemaining - 1);
                if (CountdownFramesRemaining > 0)
                {
                    return;
                }

                Phase = BrickDuelPhase.Playing;
                ActivateBall(BottomBall);
                ActivateBall(TopBall);
            }

            int previousPressureLevel = PressureLevel;
            ElapsedFrames++;
            LastFrameEvents.PressureLevelChanged = PressureLevel != previousPressureLevel;

            Vector2 bottomPaddleStart = BottomPaddle.Position;
            Vector2 topPaddleStart = TopPaddle.Position;
            MovePaddle(BottomPaddle, input.PlayerMoveAxis);
            float aiMoveAxis = _aiController.Step(
                TopBall,
                TopPaddle,
                _rule.PaddleSpawnY,
                _rule.ArenaHalfWidth - _rule.PaddleHalfWidth);
            MovePaddle(TopPaddle, aiMoveAxis);

            Vector2 bottomPaddleVelocity =
                (BottomPaddle.Position - bottomPaddleStart) / FrameDelta;
            Vector2 topPaddleVelocity =
                (TopPaddle.Position - topPaddleStart) / FrameDelta;
            float tideSpeed = _rule.BaseTideSpeed * PressureMultiplier;
            _bottomHitBrickIds.Clear();
            _topHitBrickIds.Clear();
            StepBall(
                BottomBall,
                BottomPaddle,
                bottomPaddleStart,
                bottomPaddleVelocity,
                tideSpeed,
                _bottomHitBrickIds);
            StepBall(
                TopBall,
                TopPaddle,
                topPaddleStart,
                topPaddleVelocity,
                tideSpeed,
                _topHitBrickIds);
            ApplyBrickHits(_bottomHitBrickIds);
            ApplyBrickHits(_topHitBrickIds);

            AdvanceBrickTide(tideSpeed);
            ResolveCoreDamage();
            ResolveResult();
        }

        public BrickDuelSnapshot CreateSnapshot()
        {
            return new BrickDuelSnapshot
            {
                Phase = Phase,
                Result = Result,
                IsPaused = IsPaused,
                SimulationFrame = SimulationFrame,
                CountdownFramesRemaining = CountdownFramesRemaining,
                ElapsedFrames = ElapsedFrames,
                BottomCoreHealth = BottomCoreHealth,
                TopCoreHealth = TopCoreHealth,
                PressureLevel = PressureLevel,
                PressureMultiplier = PressureMultiplier,
                FramesUntilPressureIncrease = FramesUntilPressureIncrease,
                BottomDangerDistance = BottomDangerDistance,
                TopDangerDistance = TopDangerDistance,
                BottomPaddle = ClonePaddle(BottomPaddle),
                TopPaddle = ClonePaddle(TopPaddle),
                BottomBall = CloneBall(BottomBall),
                TopBall = CloneBall(TopBall),
                Bricks = _bricks
                    .OrderBy(brick => brick.BrickId)
                    .Select(CloneBrick)
                    .ToArray(),
            };
        }

        public ulong GetChecksum()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            Hash(ref hash, (int)Phase, prime);
            Hash(ref hash, (int)Result, prime);
            Hash(ref hash, IsPaused ? 1 : 0, prime);
            Hash(ref hash, SimulationFrame, prime);
            Hash(ref hash, CountdownFramesRemaining, prime);
            Hash(ref hash, ElapsedFrames, prime);
            Hash(ref hash, BottomCoreHealth, prime);
            Hash(ref hash, TopCoreHealth, prime);
            Hash(ref hash, _nextBrickId, prime);
            Hash(ref hash, Quantize(_rowTravelSinceSpawn), prime);
            Hash(ref hash, unchecked((int)_rowRandom.State), prime);
            Hash(ref hash, unchecked((int)_aiController.RandomState), prime);
            Hash(ref hash, _aiController.FramesUntilReaction, prime);
            Hash(ref hash, Quantize(_aiController.TargetX), prime);
            HashBall(ref hash, BottomBall, prime);
            HashBall(ref hash, TopBall, prime);
            Hash(ref hash, Quantize(BottomPaddle.Position.x), prime);
            Hash(ref hash, Quantize(TopPaddle.Position.x), prime);
            foreach (BrickDuelBrickState brick in _bricks.OrderBy(item => item.BrickId))
            {
                Hash(ref hash, brick.BrickId, prime);
                Hash(ref hash, (int)brick.Side, prime);
                Hash(ref hash, (int)brick.InitialType, prime);
                Hash(ref hash, brick.Health, prime);
                Hash(ref hash, Quantize(brick.Position.x), prime);
                Hash(ref hash, Quantize(brick.Position.y), prime);
            }

            return hash;
        }

        private void ResetPaddles()
        {
            BottomPaddle.Position = new Vector2(0f, -_rule.PaddleSpawnY);
            BottomPaddle.MoveAxis = 0f;
            TopPaddle.Position = new Vector2(0f, _rule.PaddleSpawnY);
            TopPaddle.MoveAxis = 0f;
        }

        private void MovePaddle(BrickDuelPaddleState paddle, float moveAxis)
        {
            paddle.MoveAxis = Mathf.Clamp(moveAxis, -1f, 1f);
            float limit = Mathf.Max(0f, _rule.ArenaHalfWidth - _rule.PaddleHalfWidth);
            float nextX = paddle.Position.x + paddle.MoveAxis * _rule.PaddleMoveSpeed * FrameDelta;
            paddle.Position = new Vector2(Mathf.Clamp(nextX, -limit, limit), paddle.Position.y);
        }

        private void StepBall(
            BrickDuelBallState ball,
            BrickDuelPaddleState paddle,
            Vector2 paddleStartPosition,
            Vector2 paddleVelocity,
            float tideSpeed,
            ISet<int> hitBrickIds)
        {
            if (!ball.IsActive)
            {
                if (ball.ResetFramesRemaining > 0)
                {
                    ball.ResetFramesRemaining--;
                }

                if (ball.ResetFramesRemaining <= 0)
                {
                    ActivateBall(ball);
                }
                return;
            }

            Vector2 previous = ball.Position;
            _collisionSolver.StepBall(
                ball,
                paddle,
                paddleStartPosition,
                paddleVelocity,
                _bricks,
                _rule,
                FrameDelta,
                tideSpeed,
                hitBrickIds);
            Vector2 displacement = ball.Position - previous;
            if (displacement.sqrMagnitude <
                _rule.StuckMovementEpsilon * _rule.StuckMovementEpsilon * FrameDelta * FrameDelta)
            {
                ball.StuckFrames++;
            }
            else
            {
                ball.StuckFrames = 0;
            }

            int stuckFrameLimit = Mathf.Max(
                1,
                Mathf.RoundToInt(_rule.StuckTimeoutSeconds * _rule.SimulationFps));
            bool outside = Mathf.Abs(ball.Position.x) > _rule.ArenaHalfWidth + _rule.BallRadius ||
                           (ball.Side == BrickDuelSide.Bottom
                               ? ball.Position.y < -_rule.CoreLineY - _rule.BallRadius
                               : ball.Position.y > _rule.CoreLineY + _rule.BallRadius);
            if (outside || ball.StuckFrames >= stuckFrameLimit)
            {
                BeginBallReset(ball);
            }

        }

        private void BeginBallReset(BrickDuelBallState ball)
        {
            PositionBallForServe(ball);
            ball.IsActive = false;
            ball.ResetFramesRemaining = Mathf.Max(
                1,
                Mathf.RoundToInt(_rule.BallResetSeconds * _rule.SimulationFps));
            ball.StuckFrames = 0;
            if (ball.Side == BrickDuelSide.Bottom)
            {
                LastFrameEvents.BottomBallReset = true;
            }
            else
            {
                LastFrameEvents.TopBallReset = true;
            }
        }

        private void PositionBallForServe(BrickDuelBallState ball)
        {
            float direction = ball.Side == BrickDuelSide.Bottom ? 1f : -1f;
            float paddleY = ball.Side == BrickDuelSide.Bottom
                ? -_rule.PaddleSpawnY
                : _rule.PaddleSpawnY;
            float offset = _rule.PaddleHalfHeight + _rule.BallRadius + 0.02f;
            ball.Position = new Vector2(0f, paddleY + direction * offset);
            ball.Velocity = new Vector2(0f, direction * _rule.BallSpeed);
            ball.ResetFramesRemaining = 0;
            ball.StuckFrames = 0;
        }

        private void ActivateBall(BrickDuelBallState ball)
        {
            PositionBallForServe(ball);
            ball.IsActive = true;
        }

        private void AdvanceBrickTide(float tideSpeed)
        {
            float distance = tideSpeed * FrameDelta;
            for (int i = 0; i < _bricks.Count; i++)
            {
                BrickDuelBrickState brick = _bricks[i];
                float direction = brick.Side == BrickDuelSide.Bottom ? -1f : 1f;
                brick.Position += new Vector2(0f, direction * distance);
            }

            _rowTravelSinceSpawn += distance;
            while (_rowTravelSinceSpawn + 0.000001f >= _rule.BrickHeight)
            {
                _rowTravelSinceSpawn -= _rule.BrickHeight;
                SpawnMirroredRow(GenerateWeightedRow(), 0);
            }
        }

        private void ResolveCoreDamage()
        {
            int bottomDamage = 0;
            int topDamage = 0;
            float halfHeight = _rule.BrickHeight * 0.5f;
            for (int i = _bricks.Count - 1; i >= 0; i--)
            {
                BrickDuelBrickState brick = _bricks[i];
                bool reachedCore = brick.Side == BrickDuelSide.Bottom
                    ? brick.Position.y - halfHeight <= -_rule.CoreLineY
                    : brick.Position.y + halfHeight >= _rule.CoreLineY;
                if (!reachedCore)
                {
                    continue;
                }

                if (brick.Side == BrickDuelSide.Bottom)
                {
                    bottomDamage += _rule.BrickCoreDamage;
                }
                else
                {
                    topDamage += _rule.BrickCoreDamage;
                }
                _bricks.RemoveAt(i);
            }

            if (bottomDamage > 0)
            {
                BottomCoreHealth = Mathf.Max(0, BottomCoreHealth - bottomDamage);
                LastFrameEvents.BottomCoreDamage = bottomDamage;
            }
            if (topDamage > 0)
            {
                TopCoreHealth = Mathf.Max(0, TopCoreHealth - topDamage);
                LastFrameEvents.TopCoreDamage = topDamage;
            }
        }

        private void ResolveResult()
        {
            if (BottomCoreHealth > 0 && TopCoreHealth > 0)
            {
                return;
            }

            if (BottomCoreHealth <= 0 && TopCoreHealth <= 0)
            {
                Result = BrickDuelResult.Draw;
            }
            else if (TopCoreHealth <= 0)
            {
                Result = BrickDuelResult.PlayerWin;
            }
            else
            {
                Result = BrickDuelResult.PlayerLose;
            }

            Phase = BrickDuelPhase.Result;
            IsPaused = false;
            BottomBall.IsActive = false;
            TopBall.IsActive = false;
        }

        private void ApplyBrickHits(ISet<int> brickIds)
        {
            if (brickIds.Count == 0)
            {
                return;
            }

            for (int i = _bricks.Count - 1; i >= 0; i--)
            {
                BrickDuelBrickState brick = _bricks[i];
                if (!brickIds.Contains(brick.BrickId))
                {
                    continue;
                }

                brick.Health = Mathf.Max(0, brick.Health - 1);
                if (brick.Health == 0)
                {
                    LastFrameEvents.AddDestroyed(brick);
                    _bricks.RemoveAt(i);
                }
            }
        }

        private void SpawnInitialRows()
        {
            for (int rowIndex = 0; rowIndex < _rule.InitialRows; rowIndex++)
            {
                string pattern = _rule.InitialRowPatterns[rowIndex];
                BrickDuelBrickType[] row = pattern
                    .Split(',')
                    .Select(ParseBrickType)
                    .ToArray();
                SpawnMirroredRow(row, rowIndex);
            }
        }

        private void SpawnMirroredRow(IReadOnlyList<BrickDuelBrickType> row, int rowIndex)
        {
            float startX = -(_rule.Columns - 1) * _rule.BrickWidth * 0.5f;
            float y = (rowIndex + 0.5f) * _rule.BrickHeight;
            for (int column = 0; column < _rule.Columns; column++)
            {
                BrickDuelBrickType type = row[column];
                float x = startX + column * _rule.BrickWidth;
                AddBrick(BrickDuelSide.Bottom, type, new Vector2(x, -y));
                AddBrick(BrickDuelSide.Top, type, new Vector2(x, y));
            }
        }

        private BrickDuelBrickType[] GenerateWeightedRow()
        {
            var row = new BrickDuelBrickType[_rule.Columns];
            for (int i = 0; i < row.Length; i++)
            {
                float value = (_rowRandom.NextUInt() & 0x00FFFFFFu) / 16777216f;
                if (value < _rule.GreenWeight)
                {
                    row[i] = BrickDuelBrickType.Green;
                }
                else if (value < _rule.GreenWeight + _rule.RedWeight)
                {
                    row[i] = BrickDuelBrickType.Red;
                }
                else if (value < _rule.GreenWeight + _rule.RedWeight + _rule.YellowWeight)
                {
                    row[i] = BrickDuelBrickType.Yellow;
                }
                else
                {
                    row[i] = BrickDuelBrickType.Mystery;
                }
            }
            return row;
        }

        private void AddBrick(BrickDuelSide side, BrickDuelBrickType type, Vector2 position)
        {
            _bricks.Add(new BrickDuelBrickState
            {
                BrickId = _nextBrickId++,
                Side = side,
                InitialType = type,
                Health = GetInitialHealth(type),
                Position = position,
            });
        }

        private int GetInitialHealth(BrickDuelBrickType type)
        {
            switch (type)
            {
                case BrickDuelBrickType.Green:
                    return _rule.GreenHealth;
                case BrickDuelBrickType.Red:
                    return _rule.RedHealth;
                case BrickDuelBrickType.Yellow:
                    return _rule.YellowHealth;
                case BrickDuelBrickType.Mystery:
                    return _rule.MysteryHealth;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private float GetDangerDistance(BrickDuelSide side)
        {
            float nearest = float.PositiveInfinity;
            float halfHeight = _rule.BrickHeight * 0.5f;
            for (int i = 0; i < _bricks.Count; i++)
            {
                BrickDuelBrickState brick = _bricks[i];
                if (brick.Side != side)
                {
                    continue;
                }

                float distance = side == BrickDuelSide.Bottom
                    ? brick.Position.y - halfHeight + _rule.CoreLineY
                    : _rule.CoreLineY - brick.Position.y - halfHeight;
                nearest = Mathf.Min(nearest, Mathf.Max(0f, distance));
            }

            return float.IsPositiveInfinity(nearest) ? _rule.CoreLineY : nearest;
        }

        private static BrickDuelBrickType ParseBrickType(string value)
        {
            return (BrickDuelBrickType)Enum.Parse(
                typeof(BrickDuelBrickType),
                (value ?? string.Empty).Trim(),
                true);
        }

        private static BrickDuelPaddleState ClonePaddle(BrickDuelPaddleState source)
        {
            return new BrickDuelPaddleState
            {
                Side = source.Side,
                Position = source.Position,
                MoveAxis = source.MoveAxis,
            };
        }

        private static BrickDuelBallState CloneBall(BrickDuelBallState source)
        {
            return new BrickDuelBallState
            {
                Side = source.Side,
                Position = source.Position,
                Velocity = source.Velocity,
                IsActive = source.IsActive,
                ResetFramesRemaining = source.ResetFramesRemaining,
                StuckFrames = source.StuckFrames,
            };
        }

        private static BrickDuelBrickState CloneBrick(BrickDuelBrickState source)
        {
            return new BrickDuelBrickState
            {
                BrickId = source.BrickId,
                Side = source.Side,
                InitialType = source.InitialType,
                Health = source.Health,
                Position = source.Position,
            };
        }

        private static void HashBall(ref ulong hash, BrickDuelBallState ball, ulong prime)
        {
            Hash(ref hash, (int)ball.Side, prime);
            Hash(ref hash, Quantize(ball.Position.x), prime);
            Hash(ref hash, Quantize(ball.Position.y), prime);
            Hash(ref hash, Quantize(ball.Velocity.x), prime);
            Hash(ref hash, Quantize(ball.Velocity.y), prime);
            Hash(ref hash, ball.IsActive ? 1 : 0, prime);
            Hash(ref hash, ball.ResetFramesRemaining, prime);
            Hash(ref hash, ball.StuckFrames, prime);
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 10000f);
        }

        private static void Hash(ref ulong hash, int value, ulong prime)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= prime;
            }
        }
    }
}
