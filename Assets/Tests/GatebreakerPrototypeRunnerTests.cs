using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Prototype;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerPrototypeRunnerTests
    {
        [Test]
        public void SetLocalInputFrameDoesNotThrowWhenInputServiceIsMissing()
        {
            var root = new GameObject("Gatebreaker Prototype Runner Test");
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                MethodInfo setFrameMethod = typeof(GatebreakerPrototypeRunner).GetMethod(
                    "SetLocalInputFrame",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNotNull(setFrameMethod);
                LogAssert.Expect(
                    LogType.Warning,
                    "GatebreakerPrototypeRunner: input service is missing; local prototype continues with direct runtime input.");

                Assert.DoesNotThrow(() => setFrameMethod.Invoke(
                    runner,
                    new object[] { new PlayerInputFrame(1, 0f, false, Vector2.zero) }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HostLeaveThenOnlineMenuCanCreateFreshLanRoom()
        {
            var root = new GameObject("Gatebreaker Prototype Runner LAN Test");
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                var roomService = new LanRoomService();
                var transport = new GatebreakerLanTestTransport();
                SetPrivateField(runner, "_lanRoomService", roomService);
                SetPrivateField(runner, "_lanTransport", transport);

                InvokePrivate(runner, "ShowOnlineBattleMenu");
                InvokePrivate(runner, "CreateLanHost");
                RoomSnapshot firstRoom = roomService.CurrentSnapshot;
                Assert.AreEqual(LanRoomState.Lobby, firstRoom.State);
                Assert.IsTrue(firstRoom.IsHost);
                Assert.AreEqual(0, firstRoom.LocalSlotIndex);
                Assert.IsTrue(transport.Stats.IsTcpHostActive);

                InvokePrivate(runner, "LeaveLanRoom");
                RoomSnapshot afterLeave = roomService.CurrentSnapshot;
                Assert.AreEqual(LanRoomState.Idle, afterLeave.State);
                Assert.IsFalse(afterLeave.IsHost);
                Assert.AreEqual(-1, afterLeave.LocalSlotIndex);
                Assert.AreEqual(string.Empty, afterLeave.RoomCode);
                Assert.IsFalse(transport.Stats.IsTcpHostActive);
                Assert.IsFalse(transport.Stats.IsDiscoveryActive);

                InvokePrivate(runner, "ShowOnlineBattleMenu");
                InvokePrivate(runner, "CreateLanHost");
                RoomSnapshot secondRoom = roomService.CurrentSnapshot;
                Assert.AreEqual(LanRoomState.Lobby, secondRoom.State);
                Assert.IsTrue(secondRoom.IsHost);
                Assert.AreEqual(0, secondRoom.LocalSlotIndex);
                Assert.AreEqual(1, secondRoom.Players.Count(player => player.IsLocal));
                Assert.IsTrue(transport.Stats.IsTcpHostActive);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReusedBallViewStartsAtSpawnPositionWithFreshTrail()
        {
            var root = new GameObject("Gatebreaker Prototype Runner Trail Test");
            GameObject pooledBall = null;
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                SetPrivateField(runner, "_visualRoot", root.transform);
                SetPrivateField(runner, "_poolRoot", root.transform);

                pooledBall = new GameObject("Pooled Ball");
                pooledBall.transform.SetParent(root.transform, false);
                pooledBall.transform.localPosition = new Vector3(7f, 8f, 9f);
                GameObject trailObject = new GameObject("Trail");
                trailObject.transform.SetParent(pooledBall.transform, false);
                TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
                trail.emitting = true;
                Rigidbody2D body = pooledBall.AddComponent<Rigidbody2D>();
                body.gravityScale = 1f;
                body.velocity = new Vector2(3f, -4f);
                CircleCollider2D collider = pooledBall.AddComponent<CircleCollider2D>();
                pooledBall.SetActive(false);

                Dictionary<int, Stack<GameObject>> pools = GetPrivateField<Dictionary<int, Stack<GameObject>>>(runner, "_ballViewPools");
                pools[3] = new Stack<GameObject>();
                pools[3].Push(pooledBall);

                var ball = new BallRuntimeState
                {
                    BallId = 42,
                    OwnerPlayerId = 3,
                };
                Vector3 spawnPosition = new Vector3(1.25f, 2.5f, 0.35f);
                Transform view = InvokePrivate<Transform>(runner, "EnsureBallView", ball, spawnPosition);

                Assert.AreSame(pooledBall.transform, view);
                Assert.IsTrue(pooledBall.activeSelf);
                Assert.AreEqual(spawnPosition, pooledBall.transform.localPosition);
                Assert.IsTrue(trail.emitting);
                Assert.AreEqual(0, trail.positionCount);
                Assert.IsFalse(body.simulated);
                Assert.AreEqual(0f, body.gravityScale);
                Assert.AreEqual(Vector2.zero, body.velocity);
                Assert.IsFalse(collider.enabled);

                InvokePrivate(runner, "ReleaseBallViewObject", ball.BallId, pooledBall);

                Assert.IsFalse(pooledBall.activeSelf);
                Assert.IsFalse(trail.emitting);
                Assert.AreEqual(0, trail.positionCount);
                pooledBall = null;
            }
            finally
            {
                if (pooledBall != null)
                {
                    UnityEngine.Object.DestroyImmediate(pooledBall);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalCountdownHidesExistingBallViews()
        {
            var root = new GameObject("Gatebreaker Prototype Runner Countdown Ball Test");
            GameObject ballObject = null;
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                ballObject = new GameObject("Ball 9");
                ballObject.transform.SetParent(root.transform, false);
                TrailRenderer trail = ballObject.AddComponent<TrailRenderer>();
                trail.emitting = true;
                Rigidbody2D body = ballObject.AddComponent<Rigidbody2D>();
                body.simulated = true;

                GetPrivateField<Dictionary<int, Transform>>(runner, "_ballViews")[9] = ballObject.transform;
                SetStartupUiState(runner, "LocalCountdown");

                InvokePrivate(runner, "SyncBallViews");

                Assert.IsFalse(ballObject.activeSelf);
                Assert.IsFalse(trail.emitting);
                Assert.IsFalse(body.simulated);
                ballObject = null;
            }
            finally
            {
                if (ballObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ballObject);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ModeSelectHidesExistingBallViewsBeforeGameStarts()
        {
            var root = new GameObject("Gatebreaker Prototype Runner Mode Select Ball Test");
            GameObject ballObject = null;
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                ballObject = new GameObject("Ball 10");
                ballObject.transform.SetParent(root.transform, false);
                ballObject.SetActive(true);

                GetPrivateField<Dictionary<int, Transform>>(runner, "_ballViews")[10] = ballObject.transform;
                SetStartupUiState(runner, "ModeSelect");

                InvokePrivate(runner, "SyncBallViews");

                Assert.IsFalse(ballObject.activeSelf);
                ballObject = null;
            }
            finally
            {
                if (ballObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ballObject);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HiddenBallViewReactivatesWhenReusedAfterCountdown()
        {
            var root = new GameObject("Gatebreaker Prototype Runner Ball Reactivate Test");
            GameObject ballObject = null;
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                ballObject = new GameObject("Ball 12");
                ballObject.transform.SetParent(root.transform, false);
                TrailRenderer trail = ballObject.AddComponent<TrailRenderer>();
                trail.emitting = false;
                ballObject.SetActive(false);

                GetPrivateField<Dictionary<int, Transform>>(runner, "_ballViews")[12] = ballObject.transform;
                var ball = new BallRuntimeState
                {
                    BallId = 12,
                    OwnerPlayerId = 1,
                };

                Transform view = InvokePrivate<Transform>(
                    runner,
                    "EnsureBallView",
                    ball,
                    new Vector3(0.5f, 1f, 0.35f));

                Assert.AreSame(ballObject.transform, view);
                Assert.IsTrue(ballObject.activeSelf);
                Assert.IsTrue(trail.emitting);
                ballObject = null;
            }
            finally
            {
                if (ballObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ballObject);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BallGoalContactRadiusUsesPrefabCircleColliderAndScale()
        {
            var root = new GameObject("Gatebreaker Prototype Runner Ball Radius Test");
            GameObject prefab = null;
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                prefab = new GameObject("Ball Prefab Probe");
                prefab.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                CircleCollider2D collider = prefab.AddComponent<CircleCollider2D>();
                collider.radius = 0.2f;

                object[] arguments = { prefab, 0f };
                bool calculated = InvokePrivate<bool>(runner, "TryCalculateBallGoalContactRadius", arguments);
                float radius = (float)arguments[1];
                float expectedSceneScale = InvokePrivate<float>(runner, "GetPrefabVisualUniformScale");

                Assert.IsTrue(calculated);
                Assert.AreEqual(0.2f * 0.4f / expectedSceneScale, radius, 0.0001f);
            }
            finally
            {
                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StaleScoredBallViewHoldsBeforeReturningToPool()
        {
            var root = new GameObject("Gatebreaker Prototype Runner Scored Visual Test");
            GameObject ballObject = null;
            try
            {
                GatebreakerPrototypeRunner runner = root.AddComponent<GatebreakerPrototypeRunner>();
                SetPrivateField(runner, "_visualRoot", root.transform);
                SetPrivateField(runner, "_poolRoot", root.transform);

                ballObject = new GameObject("Ball 77");
                ballObject.transform.SetParent(root.transform, false);
                GameObject trailObject = new GameObject("Trail");
                trailObject.transform.SetParent(ballObject.transform, false);
                TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
                trail.emitting = true;
                Rigidbody2D body = ballObject.AddComponent<Rigidbody2D>();
                body.gravityScale = 1f;
                body.velocity = new Vector2(0f, -5f);
                CircleCollider2D collider = ballObject.AddComponent<CircleCollider2D>();
                Vector3 currentPosition = new Vector3(0f, 0f, 0.35f);
                ballObject.transform.localPosition = currentPosition;

                GetPrivateField<Dictionary<int, Transform>>(runner, "_ballViews")[77] = ballObject.transform;
                GetPrivateField<Dictionary<int, int>>(runner, "_ballViewSlots")[77] = 1;

                InvokePrivate(runner, "RemoveStaleBallViews");

                Assert.IsTrue(ballObject.activeSelf);
                Assert.AreEqual(currentPosition, ballObject.transform.localPosition);
                Assert.IsFalse(trail.emitting);
                Assert.IsFalse(body.simulated);
                Assert.AreEqual(0f, body.gravityScale);
                Assert.AreEqual(Vector2.zero, body.velocity);
                Assert.IsFalse(collider.enabled);

                for (int i = 0; i < 4; i++)
                {
                    InvokePrivate(runner, "RemoveStaleBallViews");
                }

                Assert.IsFalse(ballObject.activeSelf);
                ballObject = null;
            }
            finally
            {
                if (ballObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ballObject);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void SetStartupUiState(object target, string stateName)
        {
            Type stateType = target.GetType().GetNestedType("StartupUiState", BindingFlags.NonPublic);
            Assert.IsNotNull(stateType);
            SetPrivateField(target, "_startupUiState", Enum.Parse(stateType, stateName));
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, arguments);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            return (T)method.Invoke(target, arguments);
        }
    }
}
