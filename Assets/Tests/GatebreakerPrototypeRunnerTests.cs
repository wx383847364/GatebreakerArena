using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Prototype;
using NUnit.Framework;
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
                Object.DestroyImmediate(root);
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
                Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }
    }
}
