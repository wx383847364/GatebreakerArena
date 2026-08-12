using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public enum BrickDuelTacticalAiBehavior
    {
        Idle = 0,
        AimEmergency = 1,
        CollectCapsule = 2,
        AimMystery = 3,
        AimClear = 4,
    }

    /// <summary>
    /// Deterministic tactical controller used only by the two-way brick-tide mode.
    /// It selects one front brick per column and steers real paddle rebounds toward
    /// the highest-priority target without mutating ball or collision state.
    /// </summary>
    public sealed class BrickDuelTacticalAiController
    {
        private const float HitOffsetInfluence = 0.72f;
        private const float PaddleMoveInfluence = 0.18f;
        private const float TimeEpsilon = 0.0001f;

        private readonly BrickDuelRuleDefinition _rule;
        private readonly BrickDuelAiRuleDefinition _aiRule;
        private readonly BrickDuelSide _side;
        private int _framesUntilDecision;
        private int _targetTier;

        public BrickDuelTacticalAiController(
            BrickDuelRuleDefinition rule,
            BrickDuelAiRuleDefinition aiRule,
            BrickDuelSide side)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _aiRule = aiRule ?? throw new ArgumentNullException(nameof(aiRule));
            _side = side;
            Reset();
        }

        public int CurrentTargetBrickId { get; private set; }
        public int CurrentTargetTier => _targetTier;
        public int PlannedBallId { get; private set; }
        public BrickDuelTacticalAiBehavior CurrentBehavior { get; private set; }
        public float TargetX { get; private set; }
        public int PlannedWallBounces { get; private set; }
        public int FramesUntilDecision => _framesUntilDecision;

        public void Reset()
        {
            _framesUntilDecision = 0;
            _targetTier = int.MaxValue;
            CurrentTargetBrickId = -1;
            PlannedBallId = -1;
            CurrentBehavior = BrickDuelTacticalAiBehavior.Idle;
            TargetX = 0f;
            PlannedWallBounces = 0;
        }

        public float Step(
            BrickDuelBallState primaryBall,
            IReadOnlyList<BrickDuelBallState> splitBalls,
            BrickDuelPaddleState paddle,
            IReadOnlyList<BrickDuelBrickState> bricks,
            IReadOnlyList<BrickDuelItemCapsuleState> capsules,
            float tideSpeed,
            float paddleHalfWidth,
            float ballRadius)
        {
            if (paddle == null)
            {
                CurrentBehavior = BrickDuelTacticalAiBehavior.Idle;
                PlannedBallId = -1;
                TargetX = 0f;
                PlannedWallBounces = 0;
                return 0f;
            }

            var frontBricks = BuildFrontBricks(bricks);
            BallIntercept intercept = FindEarliestBallIntercept(
                primaryBall,
                splitBalls,
                paddle,
                paddleHalfWidth,
                ballRadius);
            if (_framesUntilDecision <= 0 || !ContainsBrick(frontBricks, CurrentTargetBrickId))
            {
                SelectBrickTarget(
                    frontBricks,
                    intercept,
                    paddle,
                    tideSpeed,
                    paddleHalfWidth,
                    ballRadius);
                _framesUntilDecision = Mathf.Max(1, _aiRule.DecisionIntervalFrames) - 1;
            }
            else
            {
                _framesUntilDecision--;
            }

            BrickDuelBrickState targetBrick = FindBrick(frontBricks, CurrentTargetBrickId);
            bool hasEmergency = HasEmergency(frontBricks);
            BrickDuelItemCapsuleState capsule = FindReachableCapsule(capsules, paddle, paddleHalfWidth);
            bool hasUrgentControlWindow = hasEmergency &&
                                          intercept.IsValid &&
                                          IsInsideControlWindow(intercept, paddleHalfWidth);
            float moveAxis = 0f;

            if (hasUrgentControlWindow && targetBrick != null)
            {
                CurrentBehavior = BrickDuelTacticalAiBehavior.AimEmergency;
                PlannedBallId = intercept.Ball.BallId;
                PaddleControlPlan control = BuildPaddleControlPlan(
                    intercept,
                    paddle,
                    targetBrick,
                    tideSpeed,
                    paddleHalfWidth,
                    ballRadius);
                TargetX = control.TargetX;
                moveAxis = control.MoveAxis;
                PlannedWallBounces = control.WallBounces;
            }
            else if (capsule != null)
            {
                CurrentBehavior = BrickDuelTacticalAiBehavior.CollectCapsule;
                PlannedBallId = -1;
                TargetX = ClampPaddleX(capsule.Position.x, paddleHalfWidth);
                moveAxis = MoveTowardTarget(paddle.Position.x, TargetX, paddleHalfWidth);
                PlannedWallBounces = 0;
            }
            else if (targetBrick != null && intercept.IsValid)
            {
                CurrentBehavior = targetBrick.InitialType == BrickDuelBrickType.Mystery
                    ? BrickDuelTacticalAiBehavior.AimMystery
                    : BrickDuelTacticalAiBehavior.AimClear;
                PlannedBallId = intercept.Ball.BallId;
                PaddleControlPlan control = BuildPaddleControlPlan(
                    intercept,
                    paddle,
                    targetBrick,
                    tideSpeed,
                    paddleHalfWidth,
                    ballRadius);
                TargetX = control.TargetX;
                moveAxis = control.MoveAxis;
                PlannedWallBounces = control.WallBounces;
            }
            else if (targetBrick != null)
            {
                CurrentBehavior = targetBrick.InitialType == BrickDuelBrickType.Mystery
                    ? BrickDuelTacticalAiBehavior.AimMystery
                    : hasEmergency
                        ? BrickDuelTacticalAiBehavior.AimEmergency
                        : BrickDuelTacticalAiBehavior.AimClear;
                PlannedBallId = -1;
                TargetX = ClampPaddleX(targetBrick.Position.x, paddleHalfWidth);
                moveAxis = MoveTowardTarget(paddle.Position.x, TargetX, paddleHalfWidth);
                PlannedWallBounces = 0;
            }
            else
            {
                CurrentBehavior = BrickDuelTacticalAiBehavior.Idle;
                PlannedBallId = -1;
                TargetX = 0f;
                PlannedWallBounces = 0;
            }

            return moveAxis;
        }

        private List<BrickDuelBrickState> BuildFrontBricks(
            IReadOnlyList<BrickDuelBrickState> bricks)
        {
            var byColumn = new Dictionary<int, BrickDuelBrickState>();
            if (bricks == null)
            {
                return new List<BrickDuelBrickState>();
            }

            for (int i = 0; i < bricks.Count; i++)
            {
                BrickDuelBrickState brick = bricks[i];
                if (brick == null || brick.Side != _side || brick.Health <= 0)
                {
                    continue;
                }

                if (!byColumn.TryGetValue(brick.ColumnId, out BrickDuelBrickState current) ||
                    CompareFrontPosition(brick, current) < 0)
                {
                    byColumn[brick.ColumnId] = brick;
                }
            }

            var result = new List<BrickDuelBrickState>(byColumn.Values);
            result.Sort(CompareStable);
            return result;
        }

        private void SelectBrickTarget(
            List<BrickDuelBrickState> frontBricks,
            BallIntercept intercept,
            BrickDuelPaddleState paddle,
            float tideSpeed,
            float paddleHalfWidth,
            float ballRadius)
        {
            var candidates = new List<BrickDuelBrickState>();
            int desiredTier;
            for (int i = 0; i < frontBricks.Count; i++)
            {
                if (GetEdgeDistance(frontBricks[i]) <= _aiRule.EmergencyDistance)
                {
                    candidates.Add(frontBricks[i]);
                }
            }

            if (candidates.Count > 0)
            {
                desiredTier = 1;
            }
            else
            {
                for (int i = 0; i < frontBricks.Count; i++)
                {
                    if (frontBricks[i].InitialType == BrickDuelBrickType.Mystery)
                    {
                        candidates.Add(frontBricks[i]);
                    }
                }
                desiredTier = candidates.Count > 0 ? 3 : 4;
                if (candidates.Count == 0)
                {
                    candidates.AddRange(frontBricks);
                }
            }

            BrickDuelBrickState locked = FindBrick(candidates, CurrentTargetBrickId);
            BrickDuelBrickState selected = locked != null && _targetTier == desiredTier
                ? locked
                : SelectMostUrgent(candidates);
            if (locked == null && selected != null && intercept.IsValid)
            {
                PaddleControlPlan preferredPlan = BuildPaddleControlPlan(
                    intercept,
                    paddle,
                    selected,
                    tideSpeed,
                    paddleHalfWidth,
                    ballRadius);
                if (preferredPlan.TotalError > TimeEpsilon)
                {
                    selected = SelectBestReachableTarget(
                        candidates,
                        intercept,
                        paddle,
                        tideSpeed,
                        paddleHalfWidth,
                        ballRadius);
                }
            }
            CurrentTargetBrickId = selected?.BrickId ?? -1;
            _targetTier = selected == null ? int.MaxValue : desiredTier;
        }

        private BrickDuelBrickState SelectBestReachableTarget(
            List<BrickDuelBrickState> candidates,
            BallIntercept intercept,
            BrickDuelPaddleState paddle,
            float tideSpeed,
            float paddleHalfWidth,
            float ballRadius)
        {
            BrickDuelBrickState best = null;
            PaddleControlPlan bestPlan = PaddleControlPlan.None;
            for (int i = 0; i < candidates.Count; i++)
            {
                PaddleControlPlan plan = BuildPaddleControlPlan(
                    intercept,
                    paddle,
                    candidates[i],
                    tideSpeed,
                    paddleHalfWidth,
                    ballRadius);
                int pathComparison = bestPlan.IsValid ? plan.CompareTo(bestPlan) : -1;
                if (best == null || pathComparison < 0 ||
                    pathComparison == 0 && CompareUrgency(candidates[i], best) < 0)
                {
                    best = candidates[i];
                    bestPlan = plan;
                }
            }
            return best;
        }

        private BrickDuelBrickState SelectMostUrgent(List<BrickDuelBrickState> candidates)
        {
            BrickDuelBrickState best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (best == null || CompareUrgency(candidates[i], best) < 0)
                {
                    best = candidates[i];
                }
            }
            return best;
        }

        private BallIntercept FindEarliestBallIntercept(
            BrickDuelBallState primaryBall,
            IReadOnlyList<BrickDuelBallState> splitBalls,
            BrickDuelPaddleState paddle,
            float paddleHalfWidth,
            float ballRadius)
        {
            BallIntercept best = BallIntercept.None;
            TryBallIntercept(primaryBall, paddle, paddleHalfWidth, ballRadius, ref best);
            if (splitBalls != null)
            {
                for (int i = 0; i < splitBalls.Count; i++)
                {
                    TryBallIntercept(
                        splitBalls[i],
                        paddle,
                        paddleHalfWidth,
                        _rule.BallRadius,
                        ref best);
                }
            }
            return best;
        }

        private bool IsInsideControlWindow(BallIntercept intercept, float paddleHalfWidth)
        {
            float usableHalfWidth = Mathf.Max(0f, _rule.ArenaHalfWidth - paddleHalfWidth);
            float fullPaddleTraverseTime = usableHalfWidth * 2f /
                                           Mathf.Max(TimeEpsilon, _rule.PaddleMoveSpeed);
            float contactHalfWidth = paddleHalfWidth + intercept.BallRadius;
            float requiredTravel = Mathf.Max(
                0f,
                Mathf.Abs(intercept.Position.x - intercept.PaddleStartX) - contactHalfWidth);
            float requiredTime = requiredTravel / Mathf.Max(TimeEpsilon, _rule.PaddleMoveSpeed);
            return requiredTime <= intercept.Time + TimeEpsilon &&
                   intercept.Time <= fullPaddleTraverseTime + TimeEpsilon;
        }

        private void TryBallIntercept(
            BrickDuelBallState ball,
            BrickDuelPaddleState paddle,
            float paddleHalfWidth,
            float ballRadius,
            ref BallIntercept best)
        {
            if (ball == null || !ball.IsActive || ball.Side != _side)
            {
                return;
            }

            float outwardSign = _side == BrickDuelSide.Top ? 1f : -1f;
            if (ball.Velocity.y * outwardSign <= TimeEpsilon)
            {
                return;
            }

            float contactDistance = _rule.PaddleHalfHeight + ballRadius;
            float contactY = paddle.Position.y - outwardSign * contactDistance;
            float time = (contactY - ball.Position.y) / ball.Velocity.y;
            if (time < 0f)
            {
                return;
            }

            float minX = -_rule.ArenaHalfWidth + ballRadius;
            float maxX = _rule.ArenaHalfWidth - ballRadius;
            float rawX = ball.Position.x + ball.Velocity.x * time;
            float interceptX = FoldToArena(rawX, minX, maxX);
            float paddleLimit = Mathf.Max(0f, _rule.ArenaHalfWidth - paddleHalfWidth);
            float reachableDistance = _rule.PaddleMoveSpeed * time;
            float reachableMin = Mathf.Max(-paddleLimit, paddle.Position.x - reachableDistance);
            float reachableMax = Mathf.Min(paddleLimit, paddle.Position.x + reachableDistance);
            float contactHalfWidth = paddleHalfWidth + ballRadius;
            if (interceptX + contactHalfWidth < reachableMin - TimeEpsilon ||
                interceptX - contactHalfWidth > reachableMax + TimeEpsilon)
            {
                return;
            }

            if (!best.IsValid || time < best.Time - TimeEpsilon ||
                Mathf.Abs(time - best.Time) <= TimeEpsilon && ball.BallId < best.Ball.BallId)
            {
                best = new BallIntercept(
                    ball,
                    time,
                    new Vector2(interceptX, contactY),
                    ballRadius,
                    paddle.Position.x);
            }
        }

        private BrickDuelItemCapsuleState FindReachableCapsule(
            IReadOnlyList<BrickDuelItemCapsuleState> capsules,
            BrickDuelPaddleState paddle,
            float paddleHalfWidth)
        {
            if (capsules == null)
            {
                return null;
            }

            float outwardSign = _side == BrickDuelSide.Top ? 1f : -1f;
            float dropSpeed = _rule.BallSpeed * BrickDuelItemConstants.ItemDropSpeedFactor;
            BrickDuelItemCapsuleState best = null;
            float bestTime = float.PositiveInfinity;
            for (int i = 0; i < capsules.Count; i++)
            {
                BrickDuelItemCapsuleState capsule = capsules[i];
                if (capsule == null || capsule.Side != _side)
                {
                    continue;
                }

                float distance = (paddle.Position.y - capsule.Position.y) * outwardSign;
                float time = Mathf.Max(0f, distance / Mathf.Max(TimeEpsilon, dropSpeed));
                float reachableDistance = _rule.PaddleMoveSpeed * time + paddleHalfWidth +
                                          BrickDuelItemConstants.CapsuleHalfWidth;
                if (Mathf.Abs(capsule.Position.x - paddle.Position.x) > reachableDistance)
                {
                    continue;
                }

                if (time < bestTime - TimeEpsilon ||
                    Mathf.Abs(time - bestTime) <= TimeEpsilon &&
                    (best == null || capsule.CapsuleId < best.CapsuleId))
                {
                    best = capsule;
                    bestTime = time;
                }
            }
            return best;
        }

        private PaddleControlPlan BuildPaddleControlPlan(
            BallIntercept intercept,
            BrickDuelPaddleState paddle,
            BrickDuelBrickState target,
            float tideSpeed,
            float paddleHalfWidth,
            float ballRadius)
        {
            List<ShotPlan> shots = BuildShotPlans(intercept, target, tideSpeed);
            float frameDelta = 1f / Mathf.Max(1, _rule.SimulationFps);
            float finalMoveTime = Mathf.Min(frameDelta, intercept.Time);
            float preparationTime = Mathf.Max(0f, intercept.Time - finalMoveTime);
            float speed = Mathf.Max(TimeEpsilon, _rule.PaddleMoveSpeed);
            float paddleLimit = Mathf.Max(0f, _rule.ArenaHalfWidth - paddleHalfWidth);
            float preparationReach = speed * preparationTime;
            float reachableStartMin = Mathf.Max(-paddleLimit, paddle.Position.x - preparationReach);
            float reachableStartMax = Mathf.Min(paddleLimit, paddle.Position.x + preparationReach);
            PaddleControlPlan best = PaddleControlPlan.None;

            for (int shotIndex = 0; shotIndex < shots.Count; shotIndex++)
            {
                ShotPlan shot = shots[shotIndex];
                for (int axisIndex = -1; axisIndex <= 1; axisIndex++)
                {
                    float collisionAxis = axisIndex;
                    float requestedOffset =
                        (shot.TangentShare - collisionAxis * PaddleMoveInfluence) /
                        HitOffsetInfluence;
                    float hitOffset = Mathf.Clamp(requestedOffset, -1f, 1f);
                    float desiredCenter = ClampPaddleX(
                        intercept.Position.x - hitOffset * paddleHalfWidth,
                        paddleHalfWidth);
                    float reachableStart = FindBestFinalFrameStart(
                        desiredCenter,
                        reachableStartMin,
                        reachableStartMax,
                        collisionAxis,
                        finalMoveTime,
                        frameDelta,
                        speed,
                        paddleLimit);
                    float collisionCenter = ResolveFinalFrameCollisionCenter(
                        reachableStart,
                        collisionAxis,
                        finalMoveTime,
                        frameDelta,
                        speed,
                        paddleLimit);
                    float contactHalfWidth = paddleHalfWidth + ballRadius;
                    if (Mathf.Abs(intercept.Position.x - collisionCenter) >
                        contactHalfWidth + TimeEpsilon)
                    {
                        continue;
                    }

                    float actualOffset = Mathf.Clamp(
                        (intercept.Position.x - collisionCenter) /
                        Mathf.Max(0.001f, paddleHalfWidth),
                        -1f,
                        1f);
                    float actualTangent = actualOffset * HitOffsetInfluence +
                                          collisionAxis * PaddleMoveInfluence;
                    float controlError = Mathf.Abs(actualTangent - shot.TangentShare);
                    float currentMoveAxis = intercept.Time <= frameDelta + TimeEpsilon
                        ? collisionAxis
                        : MoveTowardTarget(
                            paddle.Position.x,
                            reachableStart,
                            paddleHalfWidth);
                    var plan = new PaddleControlPlan(
                        true,
                        collisionCenter,
                        currentMoveAxis,
                        shot.AngularError + controlError,
                        shot.WallBounces,
                        shot.Distance,
                        collisionAxis);
                    if (!best.IsValid || plan.CompareTo(best) < 0)
                    {
                        best = plan;
                    }
                }
            }

            if (best.IsValid)
            {
                return best;
            }

            float fallbackX = ClampPaddleX(intercept.Position.x, paddleHalfWidth);
            ShotPlan fallbackShot = shots.Count > 0
                ? shots[0]
                : new ShotPlan(true, 0f, 0f, 0f, 0);
            return new PaddleControlPlan(
                true,
                fallbackX,
                MoveTowardTarget(paddle.Position.x, fallbackX, paddleHalfWidth),
                float.PositiveInfinity,
                fallbackShot.WallBounces,
                fallbackShot.Distance,
                0f);
        }

        private List<ShotPlan> BuildShotPlans(
            BallIntercept intercept,
            BrickDuelBrickState target,
            float tideSpeed)
        {
            float minX = -_rule.ArenaHalfWidth + intercept.BallRadius;
            float maxX = _rule.ArenaHalfWidth - intercept.BallRadius;
            float[] targetImages =
            {
                target.Position.x,
                2f * minX - target.Position.x,
                2f * maxX - target.Position.x,
            };
            var plans = new List<ShotPlan>(targetImages.Length);
            for (int i = 0; i < targetImages.Length; i++)
            {
                float outwardSign = _side == BrickDuelSide.Top ? 1f : -1f;
                float ballSpeed = Mathf.Max(TimeEpsilon, intercept.Ball.Velocity.magnitude);
                float travelTime = 0f;
                Vector2 delta = Vector2.zero;
                for (int projectionIteration = 0; projectionIteration < 3; projectionIteration++)
                {
                    float projectedY = target.Position.y +
                                       outwardSign * tideSpeed *
                                       (intercept.Time + travelTime);
                    delta = new Vector2(
                        targetImages[i] - intercept.Position.x,
                        projectedY - intercept.Position.y);
                    travelTime = delta.magnitude / ballSpeed;
                }
                float distance = delta.magnitude;
                if (distance <= TimeEpsilon)
                {
                    continue;
                }

                float requestedTangent = delta.x / distance;
                float maxTangent = HitOffsetInfluence + PaddleMoveInfluence;
                float tangent = Mathf.Clamp(requestedTangent, -maxTangent, maxTangent);
                float error = Mathf.Abs(requestedTangent - tangent);
                var plan = new ShotPlan(true, tangent, error, distance, i);
                plans.Add(plan);
            }
            plans.Sort((left, right) => left.CompareTo(right));
            if (plans.Count == 0)
            {
                plans.Add(new ShotPlan(true, 0f, 0f, 0f, 0));
            }
            return plans;
        }

        private static float FindBestFinalFrameStart(
            float desiredCenter,
            float minimumStart,
            float maximumStart,
            float moveAxis,
            float collisionTime,
            float frameDelta,
            float speed,
            float paddleLimit)
        {
            float low = minimumStart;
            float high = maximumStart;
            for (int iteration = 0; iteration < 16; iteration++)
            {
                float middle = (low + high) * 0.5f;
                float center = ResolveFinalFrameCollisionCenter(
                    middle,
                    moveAxis,
                    collisionTime,
                    frameDelta,
                    speed,
                    paddleLimit);
                if (center < desiredCenter)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            float lowError = Mathf.Abs(
                ResolveFinalFrameCollisionCenter(
                    low, moveAxis, collisionTime, frameDelta, speed, paddleLimit) -
                desiredCenter);
            float highError = Mathf.Abs(
                ResolveFinalFrameCollisionCenter(
                    high, moveAxis, collisionTime, frameDelta, speed, paddleLimit) -
                desiredCenter);
            return lowError <= highError ? low : high;
        }

        private static float ResolveFinalFrameCollisionCenter(
            float frameStart,
            float moveAxis,
            float collisionTime,
            float frameDelta,
            float speed,
            float paddleLimit)
        {
            float frameEnd = Mathf.Clamp(
                frameStart + moveAxis * speed * frameDelta,
                -paddleLimit,
                paddleLimit);
            float interpolation = Mathf.Clamp01(
                collisionTime / Mathf.Max(TimeEpsilon, frameDelta));
            return Mathf.Lerp(frameStart, frameEnd, interpolation);
        }

        private bool HasEmergency(List<BrickDuelBrickState> frontBricks)
        {
            for (int i = 0; i < frontBricks.Count; i++)
            {
                if (GetEdgeDistance(frontBricks[i]) <= _aiRule.EmergencyDistance)
                {
                    return true;
                }
            }
            return false;
        }

        private float GetEdgeDistance(BrickDuelBrickState brick)
        {
            float halfHeight = _rule.BrickHeight * 0.5f;
            float distance = _side == BrickDuelSide.Top
                ? _rule.CoreLineY - brick.Position.y - halfHeight
                : brick.Position.y - halfHeight + _rule.CoreLineY;
            return Mathf.Max(0f, distance);
        }

        private int CompareFrontPosition(BrickDuelBrickState left, BrickDuelBrickState right)
        {
            int distance = GetEdgeDistance(left).CompareTo(GetEdgeDistance(right));
            return distance != 0 ? distance : left.BrickId.CompareTo(right.BrickId);
        }

        private int CompareUrgency(BrickDuelBrickState left, BrickDuelBrickState right)
        {
            float leftUrgency = GetEdgeDistance(left) / Mathf.Max(1, left.Health);
            float rightUrgency = GetEdgeDistance(right) / Mathf.Max(1, right.Health);
            int urgency = leftUrgency.CompareTo(rightUrgency);
            if (urgency != 0) return urgency;
            int distance = GetEdgeDistance(left).CompareTo(GetEdgeDistance(right));
            if (distance != 0) return distance;
            return CompareStable(left, right);
        }

        private static int CompareStable(BrickDuelBrickState left, BrickDuelBrickState right)
        {
            int column = left.ColumnId.CompareTo(right.ColumnId);
            return column != 0 ? column : left.BrickId.CompareTo(right.BrickId);
        }

        private static bool ContainsBrick(List<BrickDuelBrickState> bricks, int brickId)
        {
            return FindBrick(bricks, brickId) != null;
        }

        private static BrickDuelBrickState FindBrick(
            List<BrickDuelBrickState> bricks,
            int brickId)
        {
            if (brickId < 0) return null;
            for (int i = 0; i < bricks.Count; i++)
            {
                if (bricks[i].BrickId == brickId) return bricks[i];
            }
            return null;
        }

        private float ClampPaddleX(float x, float paddleHalfWidth)
        {
            float limit = Mathf.Max(0f, _rule.ArenaHalfWidth - paddleHalfWidth);
            return Mathf.Clamp(x, -limit, limit);
        }

        private float MoveTowardTarget(
            float currentX,
            float targetX,
            float paddleHalfWidth)
        {
            float delta = targetX - currentX;
            if (Mathf.Abs(delta) <= _aiRule.MoveDeadZone)
            {
                return 0f;
            }

            float frameDelta = 1f / Mathf.Max(1, _rule.SimulationFps);
            float step = _rule.PaddleMoveSpeed * frameDelta;
            float bestAxis = 0f;
            float bestError = Mathf.Abs(delta);
            for (int axisIndex = -1; axisIndex <= 1; axisIndex += 2)
            {
                float nextX = ClampPaddleX(
                    currentX + axisIndex * step,
                    paddleHalfWidth);
                float error = Mathf.Abs(targetX - nextX);
                if (error < bestError - TimeEpsilon)
                {
                    bestError = error;
                    bestAxis = axisIndex;
                }
            }
            return bestAxis;
        }

        private static float FoldToArena(float x, float minX, float maxX)
        {
            float span = maxX - minX;
            if (span <= TimeEpsilon) return minX;
            float shifted = (x - minX) % (span * 2f);
            if (shifted < 0f) shifted += span * 2f;
            if (shifted > span) shifted = span * 2f - shifted;
            return minX + shifted;
        }

        private readonly struct BallIntercept
        {
            public static readonly BallIntercept None = new BallIntercept();

            public BallIntercept(
                BrickDuelBallState ball,
                float time,
                Vector2 position,
                float ballRadius,
                float paddleStartX)
            {
                IsValid = true;
                Ball = ball;
                Time = time;
                Position = position;
                BallRadius = ballRadius;
                PaddleStartX = paddleStartX;
            }

            public bool IsValid { get; }
            public BrickDuelBallState Ball { get; }
            public float Time { get; }
            public Vector2 Position { get; }
            public float BallRadius { get; }
            public float PaddleStartX { get; }
        }

        private readonly struct PaddleControlPlan
        {
            public static readonly PaddleControlPlan None = new PaddleControlPlan();

            public PaddleControlPlan(
                bool isValid,
                float targetX,
                float moveAxis,
                float totalError,
                int wallBounces,
                float distance,
                float collisionAxis)
            {
                IsValid = isValid;
                TargetX = targetX;
                MoveAxis = moveAxis;
                TotalError = totalError;
                WallBounces = wallBounces;
                Distance = distance;
                CollisionAxis = collisionAxis;
            }

            public bool IsValid { get; }
            public float TargetX { get; }
            public float MoveAxis { get; }
            public float TotalError { get; }
            public int WallBounces { get; }
            public float Distance { get; }
            public float CollisionAxis { get; }

            public int CompareTo(PaddleControlPlan other)
            {
                int error = TotalError.CompareTo(other.TotalError);
                if (error != 0) return error;
                int bounce = WallBounces.CompareTo(other.WallBounces);
                if (bounce != 0) return bounce;
                int distance = Distance.CompareTo(other.Distance);
                if (distance != 0) return distance;
                int movement = Mathf.Abs(MoveAxis).CompareTo(Mathf.Abs(other.MoveAxis));
                if (movement != 0) return movement;
                return CollisionAxis.CompareTo(other.CollisionAxis);
            }
        }

        private readonly struct ShotPlan
        {
            public static readonly ShotPlan None = new ShotPlan();

            public ShotPlan(
                bool isValid,
                float tangentShare,
                float angularError,
                float distance,
                int wallBounces)
            {
                IsValid = isValid;
                TangentShare = tangentShare;
                AngularError = angularError;
                Distance = distance;
                WallBounces = wallBounces == 0 ? 0 : 1;
            }

            public bool IsValid { get; }
            public float TangentShare { get; }
            public float AngularError { get; }
            public float Distance { get; }
            public int WallBounces { get; }

            public int CompareTo(ShotPlan other)
            {
                int error = AngularError.CompareTo(other.AngularError);
                if (error != 0) return error;
                int bounce = WallBounces.CompareTo(other.WallBounces);
                if (bounce != 0) return bounce;
                return Distance.CompareTo(other.Distance);
            }
        }
    }
}
