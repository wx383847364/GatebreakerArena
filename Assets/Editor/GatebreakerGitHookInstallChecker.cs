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
        private const string InstallBenefits =
            "\n\n安装 Git hooks 后的好处：\n" +
            "- 提交前自动检查 Unity GUID，降低场景、prefab 和 meta 引用损坏风险。\n" +
            "- 自动校验八位编号提交标题，避免不规范提交进入历史。\n" +
            "- merge 提交会自动生成规范标题和正文，减少合并提交格式错误。\n" +
            "- 提交后自动同步提交序号登记，减少多人协作时编号冲突。";

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
            CheckAndReport(autoInstall: true);
        }

        [MenuItem("Tools/Gatebreaker/Check Git Hooks")]
        private static void CheckHooksFromMenu()
        {
            SessionState.SetBool(SessionKey, true);
            CheckAndReport(autoInstall: false);
        }

        [MenuItem("Tools/Gatebreaker/Install Git Hooks")]
        private static void InstallHooksFromMenu()
        {
            string repoRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(repoRoot))
            {
                Debug.LogError("Gatebreaker Git hooks 安装失败：无法定位仓库根目录。");
                return;
            }

            InstallAndReport(repoRoot);
        }

        private static void CheckAndReport(bool autoInstall)
        {
            string repoRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(repoRoot))
            {
                return;
            }

            List<string> problems = CollectProblems(repoRoot);
            if (problems.Count == 0)
            {
                if (!autoInstall)
                {
                    Debug.Log("Gatebreaker Git hooks 已正确安装，提交前会自动检查提交规范、merge 信息和 Unity GUID。");
                }

                return;
            }

            if (autoInstall)
            {
                InstallAndReport(repoRoot);
                return;
            }

            Debug.LogError(
                "Gatebreaker Git hooks 未正确安装。\n\n" +
                "当前问题：\n" +
                string.Join("\n", problems) +
                BuildInstallCommandMessage(repoRoot) +
                InstallBenefits);
        }

        private static void InstallAndReport(string repoRoot)
        {
            string installScript = ResolveInstallScriptPath(repoRoot);
            string output = string.Empty;
            string error = string.Empty;
            bool installScriptExists = File.Exists(installScript);
            if (installScriptExists && RunInstallScript(repoRoot, installScript, out output, out error))
            {
                List<string> problems = CollectProblems(repoRoot);
                if (problems.Count == 0)
                {
                    Debug.Log("Gatebreaker Git hooks 已自动安装完成。");
                    return;
                }

                Debug.LogError(
                    "Gatebreaker Git hooks 自动安装后仍未正确配置。\n" +
                    "当前问题：\n" +
                    string.Join("\n", problems) +
                    BuildProcessOutput(output, error) +
                    BuildInstallCommandMessage(repoRoot) +
                    InstallBenefits);
                return;
            }

            Debug.LogError(
                "Gatebreaker Git hooks 未正确安装。\n" +
                "Unity 启动时已尝试自动安装，但安装未完成。\n\n" +
                "当前问题：\n" +
                string.Join("\n", CollectProblems(repoRoot)) +
                BuildMissingInstallScriptMessage(installScript, installScriptExists) +
                BuildProcessOutput(output, error) +
                BuildInstallCommandMessage(repoRoot) +
                InstallBenefits);
        }

        private static List<string> CollectProblems(string repoRoot)
        {
            var problems = new List<string>();
            if (!CanRunCommand("git", "--version", 2000))
            {
                problems.Add("- 未找到 git 命令；请安装 Git，并确保 Unity/GitHub Desktop 启动环境能访问 git。");
            }

            if (!HasPython3Command())
            {
                problems.Add("- 未找到 Python 3.8+；请安装 Python，并确保 python3、python 或 py -3 至少一个可用。");
            }

            string hooksPath = ReadGitConfig(repoRoot, "core.hooksPath");
            if (!string.Equals(hooksPath, ExpectedHooksPath, StringComparison.Ordinal))
            {
                problems.Add("- core.hooksPath 未设置为 .githooks");
            }

            string hooksDir = Path.Combine(repoRoot, ExpectedHooksPath);
            string commonPath = Path.Combine(hooksDir, "_common");
            if (!File.Exists(commonPath))
            {
                problems.Add("- 缺少 .githooks/_common");
            }

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

        private static string ResolveInstallScriptPath(string repoRoot)
        {
            string scriptName = IsWindowsPlatform()
                ? "install_git_hooks.cmd"
                : "install_git_hooks.sh";
            return Path.Combine(repoRoot, "tools/repo_maintenance", scriptName);
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
            string executable = ResolveInstallerExecutable();
            if (string.IsNullOrEmpty(executable))
            {
                error = IsWindowsPlatform()
                    ? "未找到 cmd.exe，无法自动执行 Windows Git hooks 安装脚本。"
                    : "未找到 bash，无法自动执行 Git hooks 安装脚本。";
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = BuildInstallerArguments(installScript),
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
                        error = "无法启动安装脚本。";
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

        private static string ResolveInstallerExecutable()
        {
            if (IsWindowsPlatform())
            {
                string comSpec = Environment.GetEnvironmentVariable("ComSpec");
                return string.IsNullOrEmpty(comSpec) ? "cmd.exe" : comSpec;
            }

            return ResolveBashPath();
        }

        private static string BuildInstallerArguments(string installScript)
        {
            return IsWindowsPlatform()
                ? "/c \"\"" + installScript + "\"\""
                : "\"" + installScript + "\"";
        }

        private static bool IsWindowsPlatform()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            return platform == PlatformID.Win32NT ||
                   platform == PlatformID.Win32S ||
                   platform == PlatformID.Win32Windows ||
                   platform == PlatformID.WinCE;
        }

        private static bool IsUnixLikePlatform()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            return platform == PlatformID.MacOSX || platform == PlatformID.Unix;
        }

        private static bool HasPython3Command()
        {
            return CanRunCommand("python3", "-c \"import sys; raise SystemExit(sys.version_info < (3, 8))\"", 3000) ||
                   CanRunCommand("python", "-c \"import sys; raise SystemExit(sys.version_info < (3, 8))\"", 3000) ||
                   CanRunCommand("py", "-3 -c \"import sys; raise SystemExit(sys.version_info < (3, 8))\"", 3000);
        }

        private static bool CanRunCommand(string fileName, string arguments, int timeoutMilliseconds)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
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

                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // Ignore cleanup failures; the caller only needs a boolean result.
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

        private static string BuildInstallCommandMessage(string repoRoot)
        {
            string command = IsWindowsPlatform()
                ? "tools\\repo_maintenance\\install_git_hooks.cmd"
                : "bash tools/repo_maintenance/install_git_hooks.sh";
            return "\n\n请在仓库根目录执行：\n" +
                   repoRoot +
                   "\n" +
                   command;
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
