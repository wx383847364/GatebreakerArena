using System.IO;
using Gatebreaker.Editor;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gatebreaker.Tests.Editor
{
    public sealed class GatebreakerFoundationGitAccessCheckerTests
    {
        [Test]
        public void ExtractRepositoryUrl_RemovesPackagePathAndRevision()
        {
            string repositoryUrl = GatebreakerFoundationGitAccessChecker.ExtractRepositoryUrl(
                "git@github.com:wx383847364/ScrollworksFoundationKit.git?path=/Packages/com.wx.foundation.runtime#v0.1.4");

            Assert.That(repositoryUrl, Is.EqualTo("git@github.com:wx383847364/ScrollworksFoundationKit.git"));
        }

        [Test]
        public void LoadFoundationGitDependencies_ReadsManifestFoundationPackages()
        {
            string manifestPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(
                    manifestPath,
                    "{\n" +
                    "  \"dependencies\": {\n" +
                    "    \"com.wx.foundation.runtime\": \"git@github.com:wx383847364/ScrollworksFoundationKit.git?path=/Packages/com.wx.foundation.runtime#v0.1.4\",\n" +
                    "    \"com.unity.textmeshpro\": \"3.0.9\"\n" +
                    "  }\n" +
                    "}\n");

                var dependencies = GatebreakerFoundationGitAccessChecker.LoadFoundationGitDependencies(manifestPath);

                Assert.That(dependencies, Has.Count.EqualTo(1));
                Assert.That(dependencies[0].PackageName, Is.EqualTo("com.wx.foundation.runtime"));
                Assert.That(dependencies[0].RepositoryUrl, Is.EqualTo("git@github.com:wx383847364/ScrollworksFoundationKit.git"));
            }
            finally
            {
                File.Delete(manifestPath);
            }
        }

        [Test]
        public void BuildFailureMessage_IncludesActionablePermissionHints()
        {
            var dependencies = new[]
            {
                new GatebreakerFoundationGitAccessChecker.FoundationGitDependency(
                    "com.wx.foundation.runtime",
                    "git@github.com:wx383847364/ScrollworksFoundationKit.git?path=/Packages/com.wx.foundation.runtime#v0.1.4",
                    "git@github.com:wx383847364/ScrollworksFoundationKit.git"),
            };
            var failures = new[]
            {
                GatebreakerFoundationGitAccessChecker.GitAccessResult.Failed(
                    "git@github.com:wx383847364/ScrollworksFoundationKit.git",
                    128,
                    string.Empty,
                    "ERROR: Repository not found.\nfatal: Could not read from remote repository."),
            };

            string message = GatebreakerFoundationGitAccessChecker.BuildFailureMessage(dependencies, failures);

            Assert.That(message, Does.Contain(GatebreakerFoundationGitAccessChecker.LogPrefix));
            Assert.That(message, Does.Contain("公共框架 Git 包访问失败"));
            Assert.That(message, Does.Contain("com.wx.foundation.runtime"));
            Assert.That(message, Does.Contain("缺少 wx383847364/ScrollworksFoundationKit 仓库权限"));
            Assert.That(message, Does.Contain("git ls-remote git@github.com:wx383847364/ScrollworksFoundationKit.git HEAD"));
        }

#if UNITY_EDITOR
        public static void RunBatchAssertions()
        {
            try
            {
                var tests = new GatebreakerFoundationGitAccessCheckerTests();
                tests.ExtractRepositoryUrl_RemovesPackagePathAndRevision();
                tests.LoadFoundationGitDependencies_ReadsManifestFoundationPackages();
                tests.BuildFailureMessage_IncludesActionablePermissionHints();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
#endif
    }
}
