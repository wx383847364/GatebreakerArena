using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gatebreaker.Editor
{
    [InitializeOnLoad]
    public static class GatebreakerFoundationGitAccessChecker
    {
        internal const string LogPrefix = "[Gatebreaker Foundation Git Access]";
        private const string SessionKey = "Gatebreaker.FoundationGitAccessChecker.Reported";
        private const string FoundationPackagePrefix = "com.wx.foundation.";
        private const int TimeoutMilliseconds = 10000;

        private static readonly Regex DependencyRegex = new Regex(
            "\"(?<name>com\\.wx\\.foundation\\.[^\"]+)\"\\s*:\\s*\"(?<url>git@[^\"]+)\"",
            RegexOptions.Compiled);

        static GatebreakerFoundationGitAccessChecker()
        {
            EditorApplication.delayCall += CheckOncePerEditorSession;
        }

        [MenuItem("Tools/Gatebreaker/Check Foundation Git Access")]
        private static void CheckFromMenu()
        {
            SessionState.SetBool(SessionKey, true);
            CheckAndReport(logSuccess: true);
        }

        private static void CheckOncePerEditorSession()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            CheckAndReport(logSuccess: false);
        }

        private static void CheckAndReport(bool logSuccess)
        {
            string repoRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(repoRoot))
            {
                return;
            }

            string manifestPath = Path.Combine(repoRoot, "Packages", "manifest.json");
            List<FoundationGitDependency> dependencies = LoadFoundationGitDependencies(manifestPath);
            if (dependencies.Count == 0)
            {
                if (logSuccess)
                {
                    Debug.Log(LogPrefix + " manifest 中没有发现 " + FoundationPackagePrefix + "* Git 依赖。");
                }

                return;
            }

            List<string> repositoryUrls = CollectRepositoryUrls(dependencies);
            var failures = new List<GitAccessResult>();
            foreach (string repositoryUrl in repositoryUrls)
            {
                GitAccessResult result = CheckRepositoryAccess(repoRoot, repositoryUrl);
                if (!result.Ok)
                {
                    failures.Add(result);
                }
            }

            if (failures.Count == 0)
            {
                if (logSuccess)
                {
                    Debug.Log(LogPrefix + " 公共框架 Git 依赖访问正常：" + string.Join(", ", repositoryUrls));
                }

                return;
            }

            Debug.LogError(BuildFailureMessage(dependencies, failures));
        }

        internal static List<FoundationGitDependency> LoadFoundationGitDependencies(string manifestPath)
        {
            var dependencies = new List<FoundationGitDependency>();
            if (!File.Exists(manifestPath))
            {
                return dependencies;
            }

            string manifest = File.ReadAllText(manifestPath);
            MatchCollection matches = DependencyRegex.Matches(manifest);
            foreach (Match match in matches)
            {
                string packageName = match.Groups["name"].Value;
                string packageUrl = match.Groups["url"].Value;
                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageUrl))
                {
                    continue;
                }

                dependencies.Add(new FoundationGitDependency(packageName, packageUrl, ExtractRepositoryUrl(packageUrl)));
            }

            return dependencies;
        }

        internal static List<string> CollectRepositoryUrls(IEnumerable<FoundationGitDependency> dependencies)
        {
            var urls = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (FoundationGitDependency dependency in dependencies)
            {
                if (string.IsNullOrEmpty(dependency.RepositoryUrl) || !seen.Add(dependency.RepositoryUrl))
                {
                    continue;
                }

                urls.Add(dependency.RepositoryUrl);
            }

            return urls;
        }

        internal static string ExtractRepositoryUrl(string packageUrl)
        {
            if (string.IsNullOrEmpty(packageUrl))
            {
                return string.Empty;
            }

            int queryIndex = packageUrl.IndexOf('?');
            int hashIndex = packageUrl.IndexOf('#');
            int endIndex = packageUrl.Length;
            if (queryIndex >= 0)
            {
                endIndex = Math.Min(endIndex, queryIndex);
            }

            if (hashIndex >= 0)
            {
                endIndex = Math.Min(endIndex, hashIndex);
            }

            return packageUrl.Substring(0, endIndex);
        }

        internal static string BuildFailureMessage(
            IReadOnlyList<FoundationGitDependency> dependencies,
            IReadOnlyList<GitAccessResult> failures)
        {
            var packageNames = new List<string>();
            for (int i = 0; i < dependencies.Count; i++)
            {
                packageNames.Add(dependencies[i].PackageName);
            }

            var lines = new List<string>
            {
                LogPrefix + " 公共框架 Git 包访问失败，Unity Package Manager 可能无法解析基础包。",
                "",
                "受影响包：",
                "- " + string.Join("\n- ", packageNames),
                "",
                "失败仓库：",
            };

            for (int i = 0; i < failures.Count; i++)
            {
                GitAccessResult failure = failures[i];
                lines.Add("- " + failure.RepositoryUrl + " (exit=" + failure.ExitCode + ")");
                string detail = BuildFailureDetail(failure);
                if (!string.IsNullOrEmpty(detail))
                {
                    lines.Add("  " + detail.Replace("\n", "\n  "));
                }
            }

            lines.Add("");
            lines.Add("常见原因：");
            lines.Add("- 当前 GitHub 账号缺少 wx383847364/ScrollworksFoundationKit 仓库权限。");
            lines.Add("- SSH key 没有添加到 GitHub，或 Unity/Tuanjie 启动环境无法访问 ssh-agent。");
            lines.Add("- 本机网络、代理或防火墙阻断了 git@github.com 的 SSH 访问。");
            lines.Add("");
            lines.Add("请在终端验证：");
            lines.Add("ssh -T git@github.com");
            for (int i = 0; i < failures.Count; i++)
            {
                lines.Add("git ls-remote " + failures[i].RepositoryUrl + " HEAD");
            }

            return string.Join("\n", lines);
        }

        private static string BuildFailureDetail(GitAccessResult failure)
        {
            string output = string.IsNullOrWhiteSpace(failure.Output) ? string.Empty : failure.Output.Trim();
            string error = string.IsNullOrWhiteSpace(failure.Error) ? string.Empty : failure.Error.Trim();
            if (string.IsNullOrEmpty(output) && string.IsNullOrEmpty(error))
            {
                return failure.TimedOut ? "git ls-remote 执行超时。" : string.Empty;
            }

            if (string.IsNullOrEmpty(output))
            {
                return error;
            }

            if (string.IsNullOrEmpty(error))
            {
                return output;
            }

            return output + "\n" + error;
        }

        private static GitAccessResult CheckRepositoryAccess(string repoRoot, string repositoryUrl)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "ls-remote " + QuoteArgument(repositoryUrl) + " HEAD",
                    WorkingDirectory = repoRoot,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return GitAccessResult.Failed(repositoryUrl, -1, string.Empty, "无法启动 git ls-remote。");
                    }

                    if (!process.WaitForExit(TimeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // The timeout result below is enough for the Editor log.
                        }

                        return GitAccessResult.Timeout(repositoryUrl);
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    return process.ExitCode == 0
                        ? GitAccessResult.Success(repositoryUrl, output, error)
                        : GitAccessResult.Failed(repositoryUrl, process.ExitCode, output, error);
                }
            }
            catch (Exception exception)
            {
                return GitAccessResult.Failed(repositoryUrl, -1, string.Empty, exception.Message);
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        internal readonly struct FoundationGitDependency
        {
            public FoundationGitDependency(string packageName, string packageUrl, string repositoryUrl)
            {
                PackageName = packageName;
                PackageUrl = packageUrl;
                RepositoryUrl = repositoryUrl;
            }

            public string PackageName { get; }
            public string PackageUrl { get; }
            public string RepositoryUrl { get; }
        }

        internal readonly struct GitAccessResult
        {
            private GitAccessResult(
                string repositoryUrl,
                bool ok,
                bool timedOut,
                int exitCode,
                string output,
                string error)
            {
                RepositoryUrl = repositoryUrl;
                Ok = ok;
                TimedOut = timedOut;
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public string RepositoryUrl { get; }
            public bool Ok { get; }
            public bool TimedOut { get; }
            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }

            public static GitAccessResult Success(string repositoryUrl, string output, string error)
            {
                return new GitAccessResult(repositoryUrl, true, false, 0, output, error);
            }

            public static GitAccessResult Failed(string repositoryUrl, int exitCode, string output, string error)
            {
                return new GitAccessResult(repositoryUrl, false, false, exitCode, output, error);
            }

            public static GitAccessResult Timeout(string repositoryUrl)
            {
                return new GitAccessResult(repositoryUrl, false, true, -1, string.Empty, "git ls-remote 执行超时。");
            }
        }
    }
}
