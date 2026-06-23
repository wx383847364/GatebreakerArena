#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gatebreaker.Editor
{
    public static class GatebreakerConfigExportMenu
    {
        private const string MenuRoot = "Gatebreaker/Config";
        private const string ExportScriptPath = "tools/config_export/export_gatebreaker_config.py";
        private const string CollisionLayoutScriptPath = "tools/config_export/extract_collision_layouts.py";

        [MenuItem(MenuRoot + "/Generate Rules Binary")]
        public static void ExportFromMenu()
        {
            RunExporter(dryRun: false, showDialog: true);
        }

        [MenuItem(MenuRoot + "/Validate Rules Config")]
        public static void ValidateFromMenu()
        {
            RunExporter(dryRun: true, showDialog: true);
        }

        [MenuItem(MenuRoot + "/Extract Collision Layouts From Prefabs")]
        public static void ExtractCollisionLayoutsFromMenu()
        {
            RunCollisionLayoutExtractor(showDialog: true);
        }

        public static void ExportFromBatchMode()
        {
            RunExporter(dryRun: false, showDialog: false);
        }

        private static void RunExporter(bool dryRun, bool showDialog)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string scriptPath = ResolveExportScriptPath(projectRoot);
            if (!File.Exists(scriptPath))
            {
                string message = $"找不到 Gatebreaker 配表导出脚本。\n当前 Unity 项目目录: {projectRoot}\n请确认打开的是 Client 工程，或已同步 tools/config_export。";
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Gatebreaker Config Export", message, "OK");
                }

                throw new FileNotFoundException(message, scriptPath);
            }

            string exporterPackagePath = Path.Combine(Path.GetDirectoryName(scriptPath) ?? string.Empty, "gatebreaker_exporter");
            if (!Directory.Exists(exporterPackagePath))
            {
                string message =
                    $"Gatebreaker 配表导出器缺少 Python 包目录: {exporterPackagePath}\n" +
                    "请确认打开的是 GatebreakerArena/Client 工程，或同步完整 tools/config_export 目录。";
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Gatebreaker Config Export", message, "OK");
                }

                throw new DirectoryNotFoundException(message);
            }

            string arguments = Quote(scriptPath) + (dryRun ? " --dry-run" : string.Empty);
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolvePythonExecutable(),
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                string message = $"启动 Gatebreaker 配表导出进程失败，请检查 Python 路径。当前 Python: {startInfo.FileName}\n{ex.Message}";
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Gatebreaker Config Export", message, "OK");
                }

                throw new InvalidOperationException(message, ex);
            }

            using (process)
            {
                if (process == null)
                {
                    throw new InvalidOperationException("启动 Gatebreaker 配表导出进程失败。");
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                stdoutTask.Wait();
                stderrTask.Wait();
                string stdout = stdoutTask.Result;
                string stderr = stderrTask.Result;

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    Debug.Log(stdout.TrimEnd());
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Debug.LogWarning(stderr.TrimEnd());
                }

                if (process.ExitCode != 0)
                {
                    string message = BuildFailureMessage(dryRun, process.ExitCode, stdout, stderr);
                    Debug.LogError(message);
                    if (showDialog)
                    {
                        EditorUtility.DisplayDialog("Gatebreaker Config Export", TruncateForDialog(message), "OK");
                    }

                    throw new InvalidOperationException(message);
                }
            }

            if (!dryRun)
            {
                AssetDatabase.Refresh();
            }

            string summary = dryRun
                ? "Gatebreaker 配表校验完成。"
                : "Gatebreaker 配表二进制生成完成：Assets/HotUpdateContent/Config/gatebreaker_rules.bytes";
            Debug.Log(summary);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Gatebreaker Config Export", summary, "OK");
            }
        }

        private static void RunCollisionLayoutExtractor(bool showDialog)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string scriptPath = ResolveToolScriptPath(projectRoot, CollisionLayoutScriptPath);
            if (!File.Exists(scriptPath))
            {
                string message = $"找不到 Gatebreaker 阻挡线提取脚本。\n当前 Unity 项目目录: {projectRoot}\n请确认已同步 tools/config_export。";
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Gatebreaker Collision Layout", message, "OK");
                }

                throw new FileNotFoundException(message, scriptPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolvePythonExecutable(),
                Arguments = Quote(scriptPath),
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            RunProcess(startInfo, "Gatebreaker 阻挡线提取", showDialog);
            RunExporter(dryRun: false, showDialog: false);

            const string summary = "Gatebreaker 阻挡线配置已从 Scene2P/Scene3P/Scene4P prefab 提取，并已重新生成运行时规则 JSON/bytes。";
            Debug.Log(summary);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Gatebreaker Collision Layout", summary, "OK");
            }
        }

        private static void RunProcess(ProcessStartInfo startInfo, string label, bool showDialog)
        {
            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                string message = $"启动 {label} 进程失败，请检查 Python 路径。当前 Python: {startInfo.FileName}\n{ex.Message}";
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(label, message, "OK");
                }

                throw new InvalidOperationException(message, ex);
            }

            using (process)
            {
                if (process == null)
                {
                    throw new InvalidOperationException($"启动 {label} 进程失败。");
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                stdoutTask.Wait();
                stderrTask.Wait();
                string stdout = stdoutTask.Result;
                string stderr = stderrTask.Result;

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    Debug.Log(stdout.TrimEnd());
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Debug.LogWarning(stderr.TrimEnd());
                }

                if (process.ExitCode != 0)
                {
                    string message = $"{label}失败，退出码: {process.ExitCode}\n{stdout}\n{stderr}".Trim();
                    Debug.LogError(message);
                    if (showDialog)
                    {
                        EditorUtility.DisplayDialog(label, TruncateForDialog(message), "OK");
                    }

                    throw new InvalidOperationException(message);
                }
            }
        }

        private static string ResolvePythonExecutable()
        {
            string configured = Environment.GetEnvironmentVariable("GATEBREAKER_PYTHON");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

#if UNITY_EDITOR_WIN
            return "python";
#else
            string[] candidates =
            {
                "/usr/bin/python3",
                "/opt/homebrew/bin/python3",
                "/usr/local/bin/python3",
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "python3";
#endif
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string ResolveExportScriptPath(string projectRoot)
        {
            return ResolveToolScriptPath(projectRoot, ExportScriptPath);
        }

        private static string ResolveToolScriptPath(string projectRoot, string scriptPath)
        {
            string[] candidates =
            {
                Path.Combine(projectRoot, "Client", scriptPath),
                Path.Combine(projectRoot, scriptPath),
                Path.Combine(Directory.GetParent(projectRoot)?.FullName ?? string.Empty, "Client", scriptPath),
            };

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(projectRoot, scriptPath);
        }

        private static string BuildFailureMessage(bool dryRun, int exitCode, string stdout, string stderr)
        {
            var builder = new StringBuilder();
            builder.Append("Gatebreaker 配表")
                .Append(dryRun ? "校验" : "导出")
                .Append("失败，退出码: ")
                .Append(exitCode)
                .AppendLine();

            AppendProcessOutput(builder, "stdout", stdout);
            AppendProcessOutput(builder, "stderr", stderr);

            if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
            {
                builder.AppendLine("导出脚本没有输出错误详情。请确认 Python 可执行文件、脚本路径和配表文件权限。");
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendProcessOutput(StringBuilder builder, string label, string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            builder.AppendLine()
                .Append("----- ")
                .Append(label)
                .AppendLine(" -----")
                .AppendLine(output.TrimEnd());
        }

        private static string TruncateForDialog(string message)
        {
            const int maxLength = 3500;
            if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            {
                return message;
            }

            return message.Substring(0, maxLength) + "\n...内容过长，完整日志请查看 Console。";
        }
    }
}
#endif
