using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Prototype;
using NUnit.Framework;
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
    }
}
