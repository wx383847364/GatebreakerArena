using System.Linq;
using App.HotUpdate.GatebreakerArena.Chip;
using App.HotUpdate.GatebreakerArena.Mode;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class V1MatchLoadoutTests
    {
        private static V1MatchLoadout CreateLoadout() => new V1MatchLoadout(
            "HERO_FROST_QUEEN", "PATH_FROST_EXTREME", "SIG_FROST_DEEP_FREEZE_TOUCH",
            new[] { "STRIKE_POWER", "GUARD_LENGTH" },
            new[] { "FLOW_SPEED", "STRIKE_SERVE", "GUARD_GOAL" });

        [TestCase(0, 3)]
        [TestCase(449, 3)]
        [TestCase(450, 4)]
        [TestCase(899, 4)]
        [TestCase(900, 5)]
        [TestCase(1349, 5)]
        [TestCase(1350, 6)]
        public void ActivationUsesExactPlayingFramesAndPreservesOrder(int frame, int expectedCount)
        {
            string[] active = V1ActivationSchedule.ResolveActive(CreateLoadout(), frame).ToArray();
            Assert.AreEqual(expectedCount, active.Length);
            CollectionAssert.AreEqual(new[]
            {
                "SIG_FROST_DEEP_FREEZE_TOUCH", "STRIKE_POWER", "GUARD_LENGTH",
                "FLOW_SPEED", "STRIKE_SERVE", "GUARD_GOAL",
            }.Take(expectedCount), active);
        }

        [Test]
        public void LoadoutCopiesInputsAndHashIncludesSlotOrder()
        {
            string[] opening = { "STRIKE_POWER", "GUARD_LENGTH" };
            V1MatchLoadout loadout = CreateLoadout();
            var copied = new V1MatchLoadout(loadout.HeroId, loadout.PathId, loadout.SignatureChipId,
                opening, loadout.ScheduledUniversalChipIds);
            opening[0] = "FLOW_SPEED";

            Assert.AreEqual("STRIKE_POWER", copied.OpeningUniversalChipIds[0]);
            var reordered = new V1MatchLoadout(loadout.HeroId, loadout.PathId, loadout.SignatureChipId,
                loadout.OpeningUniversalChipIds.Reverse(), loadout.ScheduledUniversalChipIds);
            Assert.AreNotEqual(V1ContractHash.ComputeLoadout(loadout), V1ContractHash.ComputeLoadout(reordered));
        }

        [Test]
        public void ValidatorEnforcesSignatureOwnershipAndOpeningResonance()
        {
            GatebreakerModeCatalog catalog = GatebreakerModeCatalog.CreateDefault();
            Assert.IsTrue(V1MatchLoadoutValidator.Validate(catalog, CreateLoadout()).IsValid);

            var wrongSignature = new V1MatchLoadout("HERO_FROST_QUEEN", "PATH_FROST_EXTREME",
                "SIG_MECH_FORTRESS_FOUNDATION", new[] { "STRIKE_POWER", "GUARD_LENGTH" },
                new[] { "FLOW_SPEED", "STRIKE_SERVE", "GUARD_GOAL" });
            Assert.IsFalse(V1MatchLoadoutValidator.Validate(catalog, wrongSignature).IsValid);

            var noOpeningResonance = new V1MatchLoadout("HERO_FROST_QUEEN", "PATH_FROST_EXTREME",
                "SIG_FROST_DEEP_FREEZE_TOUCH", new[] { "FLOW_SPEED", "FLOW_AMMO" },
                new[] { "STRIKE_POWER", "GUARD_LENGTH", "FLOW_CAPACITY" });
            Assert.IsFalse(V1MatchLoadoutValidator.Validate(catalog, noOpeningResonance).IsValid);
        }
    }
}
