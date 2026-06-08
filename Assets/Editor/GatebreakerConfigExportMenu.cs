#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gatebreaker.Editor
{
    public static class GatebreakerConfigExportMenu
    {
        private const string MenuRoot = "Gatebreaker/Config";
        private const string ExportScriptPath = "tools/config_export/export_gatebreaker_config.py";

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

        public static void ExportFromBatchMode()
        {
            RunExporter(dryRun: false, showDialog: false);
        }

        private static void RunExporter(bool dryRun, bool showDialog)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string scriptPath = Path.Combine(projectRoot, ExportScriptPath);
            if (!File.Exists(scriptPath))
            {
                string message = $"找不到 Gatebreaker 配表导出脚本: {scriptPath}";
                Debug.LogError(message);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Gatebreaker Config Export", message, "OK");
                }

                throw new FileNotFoundException(message, scriptPath);
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
                    string message = $"Gatebreaker 配表{(dryRun ? "校验" : "导出")}失败，退出码: {process.ExitCode}";
                    Debug.LogError(message);
                    if (showDialog)
                    {
                        EditorUtility.DisplayDialog("Gatebreaker Config Export", message + "\n请查看 Console 输出。", "OK");
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
    }
}
#endif
