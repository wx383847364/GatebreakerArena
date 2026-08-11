using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public static class BrickDuelItemIds
    {
        public const string WidePaddle = "DUEL_ITEM_WIDE_PADDLE";
        public const string LargeBall = "DUEL_ITEM_LARGE_BALL";
        public const string PhaseDrill = "DUEL_ITEM_PHASE_DRILL";
        public const string SplitBall = "DUEL_ITEM_SPLIT_BALL";
        public const string SpeedBall = "DUEL_ITEM_SPEED_BALL";
        public const string DampingPulse = "DUEL_ITEM_DAMPING_PULSE";
        public const string CoreBuffer = "DUEL_ITEM_CORE_BUFFER";
    }

    public sealed class BrickDuelItemDefinition
    {
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public int BagCopies { get; set; }
        public float DropWeight { get; set; }
        public string IconLocation { get; set; }
        public string PrefabLocation { get; set; }
        public float EffectDurationSeconds { get; set; }
        public float EffectMagnitude { get; set; }
        public string DurationModifierKey { get; set; }
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Prototype item parameters from the duel item design v0.1.
    /// </summary>
    public static class BrickDuelItemConstants
    {
        public const float ItemDropSpeedFactor = 0.65f;
        public const int MaxCapsulesPerSide = 2;
        public const float CapsuleHalfWidth = 0.18f;
        public const float CapsuleHalfHeight = 0.18f;

        public const float WidePaddleWidthMultiplier = 1.25f;
        public const float WidePaddleDurationSeconds = 8f;
        public const float PaddleWidthMultiplierMin = 0.75f;
        public const float PaddleWidthMultiplierMax = 1.50f;

        public const float LargeBallRadiusMultiplier = 1.15f;
        public const float LargeBallDurationSeconds = 8f;
        public const float BallRadiusMultiplierMin = 1.00f;
        public const float BallRadiusMultiplierMax = 1.25f;

        public const int PhaseDrillGrantCharges = 3;
        public const int PhaseDrillMaxCharges = 5;
        public const float PhaseDrillDurationSeconds = 10f;

        public const float DampingTideMultiplier = 0.70f;
        public const float DampingDurationSeconds = 4f;
        public const float TideSpeedMultiplierMin = 0.50f;
        public const float TideSpeedMultiplierMax = 1.50f;

        public const float CoreBufferDurationSeconds = 15f;
        public const int CoreBufferMaxLayers = 1;

        public const int SplitBallBrickHits = 3;
        public const float SplitBallSpawnAngleDegrees = 30f;
        public const float SplitBallSpawnSeparation = 0.02f;

        public const float SpeedBallSpeedMultiplier = 1.30f;
        public const float SpeedBallBaseDurationSeconds = 5f;
        public const float SpeedBallDurationMultiplierMin = 0.10f;
        public const float SpeedBallDurationMultiplierMax = 5f;
        public const float SpeedBallDurationSecondsMin = 0.10f;
        public const float SpeedBallDurationSecondsMax = 60f;
        public const float BallSpeedMultiplierMin = 0.50f;
        public const float BallSpeedMultiplierMax = 2f;
    }

    public sealed class BrickDuelItemDropBag
    {
        private readonly List<string> _pool = new List<string>();
        private readonly List<string> _bag = new List<string>();
        private GatebreakerDeterministicPrng _random;

        public BrickDuelItemDropBag(
            IReadOnlyList<BrickDuelItemDefinition> definitions,
            uint seed)
        {
            _random = new GatebreakerDeterministicPrng(seed);
            if (definitions == null || definitions.Count == 0)
            {
                foreach (BrickDuelItemDefinition item in CreateDefaultDefinitions())
                {
                    AppendCopies(item);
                }
            }
            else
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    BrickDuelItemDefinition item = definitions[i];
                    if (item == null || !item.Enabled || string.IsNullOrWhiteSpace(item.ItemId))
                    {
                        continue;
                    }

                    AppendCopies(item);
                }

                if (_pool.Count == 0)
                {
                    foreach (BrickDuelItemDefinition item in CreateDefaultDefinitions())
                    {
                        AppendCopies(item);
                    }
                }
            }

            RefillBag();
        }

        public uint RandomState => _random.State;

        public string NextItemId()
        {
            if (_bag.Count == 0)
            {
                RefillBag();
            }

            int last = _bag.Count - 1;
            string itemId = _bag[last];
            _bag.RemoveAt(last);
            return itemId;
        }

        public static IReadOnlyList<BrickDuelItemDefinition> CreateDefaultDefinitions()
        {
            return new[]
            {
                new BrickDuelItemDefinition
                {
                    ItemId = BrickDuelItemIds.WidePaddle,
                    ItemName = "宽幅组件",
                    BagCopies = 2,
                    DropWeight = 1f / 7f,
                    IconLocation =
                        "Assets/HotUpdateContent/Res/textures/items/duel/duel_item_wide_paddle.png",
                },
                new BrickDuelItemDefinition
                {
                    ItemId = BrickDuelItemIds.LargeBall,
                    ItemName = "扩容球体",
                    BagCopies = 2,
                    DropWeight = 1f / 7f,
                    IconLocation =
                        "Assets/HotUpdateContent/Res/textures/items/duel/duel_item_large_ball.png",
                },
                new BrickDuelItemDefinition
                {
                    ItemId = BrickDuelItemIds.PhaseDrill,
                    ItemName = "相位钻头",
                    BagCopies = 2,
                    DropWeight = 1f / 7f,
                    IconLocation =
                        "Assets/HotUpdateContent/Res/textures/items/duel/duel_item_phase_drill.png",
                },
                new BrickDuelItemDefinition
                {
                    ItemId = BrickDuelItemIds.SplitBall,
                    ItemName = "裂变球体",
                    BagCopies = 2,
                    DropWeight = 1f / 7f,
                    IconLocation =
                        "Assets/HotUpdateContent/Res/textures/items/duel/duel_item_split_ball.png",
                    PrefabLocation =
                        "Assets/HotUpdateContent/Res/prefabs/Item06.prefab",
                },
                new BrickDuelItemDefinition
                {
                    ItemId = BrickDuelItemIds.SpeedBall,
                    ItemName = "弹球加速",
                    BagCopies = 2,
                    DropWeight = 1f / 7f,
                    IconLocation =
                        "Assets/HotUpdateContent/Res/textures/items/duel/duel_item_speed_ball.png",
                    PrefabLocation =
                        "Assets/HotUpdateContent/Res/prefabs/Item07.prefab",
                    EffectDurationSeconds = BrickDuelItemConstants.SpeedBallBaseDurationSeconds,
                    EffectMagnitude = BrickDuelItemConstants.SpeedBallSpeedMultiplier,
                    DurationModifierKey = "DUEL_ITEM_SPEED_BALL_DURATION",
                },
                new BrickDuelItemDefinition
                {
                    ItemId = BrickDuelItemIds.DampingPulse,
                    ItemName = "阻尼脉冲",
                    BagCopies = 2,
                    DropWeight = 1f / 7f,
                    IconLocation =
                        "Assets/HotUpdateContent/Res/textures/items/duel/duel_item_damping_pulse.png",
                },
                new BrickDuelItemDefinition
                {
                    ItemId = BrickDuelItemIds.CoreBuffer,
                    ItemName = "核心缓冲",
                    BagCopies = 2,
                    DropWeight = 1f / 7f,
                    IconLocation =
                        "Assets/HotUpdateContent/Res/textures/items/duel/duel_item_core_buffer.png",
                },
            };
        }

        public static IReadOnlyList<BrickDuelItemDefinition> ResolveDefinitions(
            IReadOnlyList<BrickDuelItemDropDefinition> dropTable)
        {
            if (dropTable == null || dropTable.Count == 0)
            {
                return CreateDefaultDefinitions();
            }

            var definitions = new List<BrickDuelItemDefinition>(dropTable.Count);
            for (int i = 0; i < dropTable.Count; i++)
            {
                BrickDuelItemDropDefinition row = dropTable[i];
                if (row == null || !row.Enabled || string.IsNullOrWhiteSpace(row.ItemId))
                {
                    continue;
                }

                definitions.Add(new BrickDuelItemDefinition
                {
                    ItemId = row.ItemId,
                    ItemName = row.ItemName,
                    BagCopies = Math.Max(1, row.BagCopies),
                    DropWeight = row.DropWeight,
                    IconLocation = row.IconLocation,
                    PrefabLocation = row.PrefabLocation,
                    EffectDurationSeconds = row.EffectDurationSeconds,
                    EffectMagnitude = row.EffectMagnitude,
                    DurationModifierKey = row.DurationModifierKey,
                    Enabled = row.Enabled,
                });
            }

            return definitions.Count > 0 ? definitions : CreateDefaultDefinitions();
        }

        private void AppendCopies(BrickDuelItemDefinition item)
        {
            int copies = Math.Max(1, item.BagCopies);
            for (int i = 0; i < copies; i++)
            {
                _pool.Add(item.ItemId);
            }
        }

        private void RefillBag()
        {
            _bag.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                _bag.Add(_pool[i]);
            }

            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.NextInt(i + 1);
                string temporary = _bag[i];
                _bag[i] = _bag[swapIndex];
                _bag[swapIndex] = temporary;
            }
        }
    }
}
