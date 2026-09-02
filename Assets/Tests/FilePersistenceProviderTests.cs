using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using App.AOT.Infrastructure.Persistence;
using NUnit.Framework;

namespace GatebreakerArena.Tests
{
    public sealed class FilePersistenceProviderTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "gatebreaker-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public async Task SaveAsync_ReplacesCommittedValueWithoutLeavingRecoveryFiles()
        {
            var provider = new FilePersistenceProvider(_directory);

            Assert.IsTrue(await provider.SaveAsync("phase.profile", Bytes("old")));
            Assert.IsTrue(await provider.SaveAsync("phase.profile", Bytes("new")));

            CollectionAssert.AreEqual(Bytes("new"), await provider.LoadAsync("phase.profile"));
            Assert.IsFalse(File.Exists(PathFor("phase.profile", ".tmp")));
            Assert.IsFalse(File.Exists(PathFor("phase.profile", ".bak")));
        }

        [Test]
        public async Task LoadAsync_RecoversCompletedTempWhenCommittedFileIsMissing()
        {
            File.WriteAllBytes(PathFor("phase.profile", ".tmp"), Bytes("new"));
            var provider = new FilePersistenceProvider(_directory);

            CollectionAssert.AreEqual(Bytes("new"), await provider.LoadAsync("phase.profile"));
            Assert.IsTrue(File.Exists(PathFor("phase.profile")));
            Assert.IsFalse(File.Exists(PathFor("phase.profile", ".tmp")));
        }

        [Test]
        public async Task LoadAsync_PrefersCommittedFileOverStaleRecoveryFiles()
        {
            File.WriteAllBytes(PathFor("phase.profile"), Bytes("committed"));
            File.WriteAllBytes(PathFor("phase.profile", ".tmp"), Bytes("stale-temp"));
            File.WriteAllBytes(PathFor("phase.profile", ".bak"), Bytes("stale-backup"));
            var provider = new FilePersistenceProvider(_directory);

            CollectionAssert.AreEqual(Bytes("committed"), await provider.LoadAsync("phase.profile"));
        }

        [Test]
        public async Task LoadAsync_RecoversBackupWhenCommittedAndTempAreMissing()
        {
            File.WriteAllBytes(PathFor("phase.profile", ".bak"), Bytes("backup"));
            var provider = new FilePersistenceProvider(_directory);

            CollectionAssert.AreEqual(Bytes("backup"), await provider.LoadAsync("phase.profile"));
            Assert.IsTrue(File.Exists(PathFor("phase.profile")));
        }

        private string PathFor(string key, string suffix = "") =>
            Path.Combine(_directory, FilePersistenceProvider.BuildSafeFileName(key) + ".dat" + suffix);

        private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    }
}
