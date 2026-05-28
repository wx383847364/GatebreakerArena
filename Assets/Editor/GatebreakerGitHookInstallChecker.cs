using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gatebreaker.Editor
{
    [InitializeOnLoad]
    public static class GatebreakerGitHookInstallChecker
    {
        private const string SessionKey = "Gatebreaker.GitHookInstallChecker.Reported";
        private const string ExpectedHooksPath = ".githooks";

        private static readonly string[] RequiredHooks =
        {
            "pre-commit",
            "prepare-commit-msg",
            "commit-msg",
            "post-commit",
        };

        static GatebreakerGitHookInstallChecker()
        {
            EditorApplication.delayCall += CheckOncePerEditorSession;
        }

        private static void CheckOncePerEditorSession()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            CheckAndReport();
        }

        private static void CheckAndReport()
        {
            string repoRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(repoRoot))
            {
                return;
            }

            List<string> problems = CollectProblems(repoRoot);
            if (problems.Count == 0)
            {
                return;
            }

            string installScript = Path.Combine(repoRoot, "tools/repo_maintenance/install_git_hooks.sh");
            string output = string.Empty;
            string error = string.Empty;
            bool installScriptExists = File.Exists(installScript);
            if (installScriptExists && RunInstallScript(repoRoot, installScript, out output, out error))
            {
                problems = CollectProblems(repoRoot);
                if (problems.Count == 0)
                {
                    Debug.Log("Gatebreaker Git hooks 已自动安装完成。");
                    return;
                }

                Debug.LogError(
                    "Gatebreaker Git hooks 自动安装后仍未正确配置。\n" +
                    "当前问题：\n" +
                    string.Join("\n", problems) +
                    BuildProcessOutput(output, error));
                return;
            }

            Debug.LogError(
                "Gatebreaker Git hooks 未正确安装。\n" +
                "Unity 启动时已尝试自动安装，但安装未完成。\n\n" +
                "当前问题：\n" +
                string.Join("\n", problems) +
                BuildMissingInstallScriptMessage(installScript, installScriptExists) +
                BuildProcessOutput(output, error));
        }

        private static List<string> CollectProblems(string repoRoot)
        {
            var problems = new List<string>();
            string hooksPath = ReadGitConfig(repoRoot, "core.hooksPath");
            if (!string.Equals(hooksPath, ExpectedHooksPath, StringComparison.Ordinal))
            {
                problems.Add("- core.hooksPath 未设置为 .githooks");
            }

            string hooksDir = Path.Combine(repoRoot, ExpectedHooksPath);
            for (int i = 0; i < RequiredHooks.Length; i++)
            {
                string hookPath = Path.Combine(hooksDir, RequiredHooks[i]);
                if (!File.Exists(hookPath))
                {
                    problems.Add("- 缺少 .githooks/" + RequiredHooks[i]);
                    continue;
                }

                if (IsUnixLikePlatform() && !IsExecutableFile(hookPath))
                {
                    problems.Add("- .githooks/" + RequiredHooks[i] + " 不可执行");
                }
            }

            return problems;
        }

        private static string ReadGitConfig(string repoRoot, string key)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "config --get " + key,
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
                        return string.Empty;
                    }

                    if (!process.WaitForExit(2000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // Ignore cleanup failures; the caller only needs an empty config value.
                        }

                        return string.Empty;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    return process.ExitCode == 0 ? output.Trim() : string.Empty;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static bool RunInstallScript(string repoRoot, string installScript, out string output, out string error)
        {
            output = string.Empty;
            error = string.Empty;
            string bashPath = ResolveBashPath();
            if (string.IsNullOrEmpty(bashPath))
            {
                error = "未找到 bash，无法自动执行 Git hooks 安装脚本。";
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = bashPath,
                    Arguments = "\"" + installScript + "\"",
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
                        error = "无法启动 bash。";
                        return false;
                    }

                    if (!process.WaitForExit(10000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // Ignore cleanup failures; the error below reports the timeout.
                        }

                        error = "安装脚本执行超时。";
                        return false;
                    }

                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ResolveBashPath()
        {
            if (File.Exists("/bin/bash"))
            {
                return "/bin/bash";
            }

            return "bash";
        }

        private static bool IsUnixLikePlatform()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            return platform == PlatformID.MacOSX || platform == PlatformID.Unix;
        }

        private static bool IsExecutableFile(string path)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = "-c \"test -x \\\"" + path.Replace("\"", "\\\"") + "\\\"\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return false;
                    }

                    if (!process.WaitForExit(2000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // Ignore cleanup failures; the caller only needs the executable status.
                        }

                        return false;
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string BuildMissingInstallScriptMessage(string installScript, bool installScriptExists)
        {
            return installScriptExists
                ? string.Empty
                : "\n\n未找到自动安装脚本：\n" + installScript;
        }

        private static string BuildProcessOutput(string output, string error)
        {
            string trimmedOutput = string.IsNullOrWhiteSpace(output) ? string.Empty : output.Trim();
            string trimmedError = string.IsNullOrWhiteSpace(error) ? string.Empty : error.Trim();
            if (string.IsNullOrEmpty(trimmedOutput) && string.IsNullOrEmpty(trimmedError))
            {
                return string.Empty;
            }

            return "\n\n安装脚本输出：\n" +
                   (string.IsNullOrEmpty(trimmedOutput) ? "-" : trimmedOutput) +
                   "\n\n安装脚本错误：\n" +
                   (string.IsNullOrEmpty(trimmedError) ? "-" : trimmedError);
        }
    }
}
