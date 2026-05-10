using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Serve;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class ServeResourceSystemTests
    {
        [Test]
        public void EvaluateCanServeUsesGddPriorityOrder()
        {
            var system = new ServeResourceSystem();
            ServeResourceState state = system.CreateState(1, 2, 1, 6f);

            Assert.AreEqual(ServeBlockReason.PlayerDisabled, system.EvaluateCanServe(state, 0, 4, true));

            state.ServeCooldownRemaining = 1f;
            Assert.AreEqual(ServeBlockReason.CoolingDown, system.EvaluateCanServe(state, 0, 4, false));

            state.ServeCooldownRemaining = 0f;
            state.CurrentServeAmmo = 0;
            Assert.AreEqual(ServeBlockReason.NoAmmo, system.EvaluateCanServe(state, 0, 4, false));

            state.CurrentServeAmmo = 1;
            state.OwnedBallsInField = 1;
            Assert.AreEqual(ServeBlockReason.OwnedBallLimit, system.EvaluateCanServe(state, 0, 4, false));

            state.OwnedBallsInField = 0;
            Assert.AreEqual(ServeBlockReason.MatchBallLimit, system.EvaluateCanServe(state, 4, 4, false));
        }

        [Test]
        public void TryServeConsumesAmmoStartsCooldownAndOwnedBallCount()
        {
            var system = new ServeResourceSystem();
            ServeResourceState state = system.CreateState(1, 2, 1, 6f);

            bool served = system.TryServe(state, 0, 4, false, 1f, out ServeBlockReason reason);

            Assert.IsTrue(served);
            Assert.AreEqual(ServeBlockReason.None, reason);
            Assert.AreEqual(0, state.CurrentServeAmmo);
            Assert.AreEqual(1, state.OwnedBallsInField);
            Assert.AreEqual(6f, state.ServeCooldownRemaining);
        }

        [Test]
        public void CooldownRestoresOneAmmoWithoutOverflow()
        {
            var system = new ServeResourceSystem();
            ServeResourceState state = system.CreateState(1, 2, 1, 6f);
            system.TryServe(state, 0, 4, false, 1f, out _);

            system.Tick(state, 10f);
            system.Tick(state, 10f);

            Assert.AreEqual(1, state.CurrentServeAmmo);
            Assert.AreEqual(0f, state.ServeCooldownRemaining);
        }

        [Test]
        public void OwnedBallRemovedNeverGoesBelowZero()
        {
            var system = new ServeResourceSystem();
            ServeResourceState state = system.CreateState(1, 2, 1, 6f);

            system.OnOwnedBallRemoved(state);

            Assert.AreEqual(0, state.OwnedBallsInField);
        }
    }
}
