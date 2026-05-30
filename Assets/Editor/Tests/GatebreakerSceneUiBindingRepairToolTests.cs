using Gatebreaker.Editor;
using NUnit.Framework;

namespace Gatebreaker.Tests.Editor
{
    public sealed class GatebreakerSceneUiBindingRepairToolTests
    {
        [Test]
        public void ValidateBootstrapScene_DoesNotRequireLegacyHudSummaryFields()
        {
            System.Collections.Generic.List<string> errors =
                GatebreakerSceneUiBindingRepairTool.ValidateBootstrapScene();

            Assert.That(errors, Is.Empty);
        }
    }
}
