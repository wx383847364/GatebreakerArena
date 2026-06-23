using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

public static class GatebreakerSmokeBuildPipeline
{
    private const string BootstrapScene = "Assets/Scenes/BootstrapScene.scene";
    private const string ProductName = "Gatebreaker Arena";
    private const string ApplicationIdentifier = "com.gatebreakerarena.game";
    private const string AppVersion = "1.0.0";
    private const int AndroidVersionCode = 1;
    private const string OfflineDefine = "GATEBREAKER_YOO_OFFLINE_PLAYMODE";
    private const string WeChatDefine = "GATEBREAKER_WECHAT_MINIGAME";
    private const string HotUpdateAssemblyName = "App.HotUpdate";
    private const string HotUpdateContentRoot = "Assets/HotUpdateContent/Res/HotUpdate";
    private const string HotUpdateDllLocation = HotUpdateContentRoot + "/App.HotUpdate.dll.bytes";
    private const string MetadataLocation = HotUpdateContentRoot + "/Metadata";
    private const string DefaultAndroidOutput = "Builds/Android/GatebreakerArena-android-smoke.apk";
    private const string DefaultWeixinOutput = "Builds/WeixinMiniGame/GatebreakerArena-minigame-smoke";
    private const string YooPackageName = "DefaultPackage";
    private const string YooPackageVersion = "Smoke_v1";
    private const string YooBuildOutputRoot = "Builds/YooAssets";
    private const string YooCollectPath = "Assets/HotUpdateContent/Res";

    private static readonly string[] AotMetadataAssemblyNames =
    {
        "mscorlib",
        "System",
        "System.Core",
        "UnityEngine.CoreModule",
        "UnityEngine.UI",
        "UnityEngine.UIModule",
        "UnityEngine.TextRenderingModule",
        "UnityEngine.JSONSerializeModule",
        "UnityEngine.InputLegacyModule",
        "Unity.TextMeshPro",
        "App.Shared",
    };

    [MenuItem("Gatebreaker/Build/Android Smoke APK")]
    public static void BuildAndroidSmokeApk()
    {
        BuildAndroidSmokeApkFromCommandLine();
    }

    [MenuItem("Gatebreaker/Build/Weixin MiniGame Smoke")]
    public static void BuildWeixinMiniGameSmoke()
    {
        BuildWeixinMiniGameSmokeFromCommandLine();
    }

    [MenuItem("Gatebreaker/Build/Validate Smoke Build Inputs")]
    public static void ValidateSmokeBuildInputsMenu()
    {
        ValidateSmokeBuildInputs();
    }

    [MenuItem("Gatebreaker/Build/Install HybridCLR Local Runtime")]
    public static void InstallHybridClrFromCommandLine()
    {
        object controller = CreateHybridClrInstallerController();
        if (controller == null)
        {
            throw new InvalidOperationException("HybridCLR InstallerController was not found.");
        }

        bool hasInstalled = Convert.ToBoolean(InvokeInstance(controller, "HasInstalledHybridCLR"));
        if (hasInstalled)
        {
            Debug.Log("Gatebreaker smoke build: HybridCLR local runtime is already installed.");
            return;
        }

        InvokeInstance(controller, "InstallDefaultHybridCLR");
        hasInstalled = Convert.ToBoolean(InvokeInstance(controller, "HasInstalledHybridCLR"));
        if (!hasInstalled)
        {
            throw new InvalidOperationException("HybridCLR local runtime installation did not complete successfully.");
        }
    }

    public static void BuildAndroidSmokeApkFromCommandLine()
    {
        string outputPath = GetCommandLineValue("-gatebreakerOutput", DefaultAndroidOutput);
        BuildTargetGroup group = BuildTargetGroup.Android;
        BuildTarget target = BuildTarget.Android;

        PrepareTarget(group, target, new[] { OfflineDefine });
        ConfigureCommonPlayerSettings();
        ConfigureAndroidPlayerSettings();
        PrepareHybridClrContent(target);
        BuildYooAssetsPackage(target);
        ValidateSmokeBuildInputs();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Builds/Android");
        var options = new BuildPlayerOptions
        {
            scenes = new[] { BootstrapScene },
            locationPathName = outputPath,
            target = target,
            targetGroup = group,
            options = BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
        EnsureBuildSucceeded(report, outputPath);
        Debug.Log($"Gatebreaker Android smoke APK built: {Path.GetFullPath(outputPath)}");
    }

    public static void BuildWeixinMiniGameSmokeFromCommandLine()
    {
        string outputPath = GetCommandLineValue("-gatebreakerOutput", DefaultWeixinOutput);
        BuildTarget target = ParseBuildTarget("WeixinMiniGame");
        BuildTargetGroup group = ParseBuildTargetGroup("WeixinMiniGame");

        EnsureWeixinMiniGamePackageInstalled();
        PrepareTarget(group, target, new[] { OfflineDefine, WeChatDefine });
        ConfigureCommonPlayerSettings();
        ConfigureWeixinPlayerSettings(group);
        PrepareHybridClrContent(target);
        BuildYooAssetsPackage(target);
        ValidateSmokeBuildInputs();

        Directory.CreateDirectory(outputPath);
        ConfigureWeixinExportSettings(outputPath);
        InvokeWeixinDoExport(buildWebGL: true);
        EnsureDirectoryHasFiles(outputPath, "Weixin MiniGame output");
        Debug.Log($"Gatebreaker Weixin MiniGame smoke project built: {Path.GetFullPath(outputPath)}");
    }

    public static void ValidateSmokeBuildInputs()
    {
        EnsureSceneInBuildSettings();
        EnsureFileExists(HotUpdateDllLocation, "HotUpdate DLL bytes");
        foreach (string assemblyName in AotMetadataAssemblyNames)
        {
            EnsureFileExists($"{MetadataLocation}/{assemblyName}.dll.bytes", $"AOT metadata {assemblyName}");
        }

        EnsureYooAssetBuiltInFilesExist();
    }

    private static void PrepareTarget(BuildTargetGroup group, BuildTarget target, IEnumerable<string> symbols)
    {
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                throw new InvalidOperationException($"Failed to switch build target to {target}.");
            }
        }

        AddScriptingDefines(group, symbols);
    }

    private static void ConfigureCommonPlayerSettings()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.applicationIdentifier = ApplicationIdentifier;
        PlayerSettings.bundleVersion = AppVersion;
    }

    private static void ConfigureAndroidPlayerSettings()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.bundleVersionCode = AndroidVersionCode;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
    }

    private static void ConfigureWeixinPlayerSettings(BuildTargetGroup group)
    {
        PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
        PlayerSettings.runInBackground = false;
    }

    private static void PrepareHybridClrContent(BuildTarget target)
    {
        InvokeHybridClrPrebuild(target);
        CopyHybridClrArtifacts(target);
        AssetDatabase.Refresh();
    }

    private static void BuildYooAssetsPackage(BuildTarget target)
    {
        EnsureYooAssetCollectorConfig();

        var buildParameters = new ScriptableBuildParameters
        {
            BuildOutputRoot = YooBuildOutputRoot,
            BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
            BuildPipeline = EBuildPipeline.ScriptableBuildPipeline.ToString(),
            BuildBundleType = (int)EBuildBundleType.AssetBundle,
            BuildTarget = target,
            PackageName = YooPackageName,
            PackageVersion = YooPackageVersion,
            EnableSharePackRule = true,
            VerifyBuildingResult = true,
            FileNameStyle = EFileNameStyle.HashName,
            BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll,
            BuildinFileCopyParams = string.Empty,
            CompressOption = ECompressOption.LZ4,
            ClearBuildCacheFiles = true,
            UseAssetDependencyDB = true,
            BuiltinShadersBundleName = $"{YooPackageName}_unityshaders",
        };

        var pipeline = new ScriptableBuildPipeline();
        YooAsset.Editor.BuildResult result = pipeline.Run(buildParameters, true);
        if (!result.Success)
        {
            throw new InvalidOperationException($"YooAssets build failed: {result.ErrorInfo}");
        }

        Debug.Log($"Gatebreaker smoke build: YooAssets built at {result.OutputPackageDirectory}");
    }

    private static void InvokeHybridClrPrebuild(BuildTarget target)
    {
        Debug.Log($"Gatebreaker smoke build: running HybridCLR prebuild for {target}.");
        InvokeStatic("HybridCLR.Editor.Commands.CompileDllCommand, HybridCLR.Editor", "CompileDll", target, true);
        InvokeStatic("HybridCLR.Editor.Commands.LinkGeneratorCommand, HybridCLR.Editor", "GenerateLinkXml", target);
        EnsureHybridClrLocalRuntimeInstalled();
        InvokeStatic("HybridCLR.Editor.Commands.StripAOTDllCommand, HybridCLR.Editor", "GenerateStripedAOTDlls", target);
        InvokeStatic("HybridCLR.Editor.Commands.MethodBridgeGeneratorCommand, HybridCLR.Editor", "GenerateMethodBridgeAndReversePInvokeWrapper", target);
        InvokeStatic("HybridCLR.Editor.Commands.AOTReferenceGeneratorCommand, HybridCLR.Editor", "GenerateAOTGenericReference", target);
    }

    private static void EnsureHybridClrLocalRuntimeInstalled()
    {
        object controller = CreateHybridClrInstallerController();
        if (controller == null)
        {
            return;
        }

        bool hasInstalled = Convert.ToBoolean(InvokeInstance(controller, "HasInstalledHybridCLR"));
        if (!hasInstalled)
        {
            throw new InvalidOperationException(
                "HybridCLR local runtime is not installed. Run tools/build/install_hybridclr.sh " +
                "or Unity menu Gatebreaker/Build/Install HybridCLR Local Runtime before smoke player builds.");
        }
    }

    private static object CreateHybridClrInstallerController()
    {
        Type type = Type.GetType("HybridCLR.Editor.Installer.InstallerController, HybridCLR.Editor");
        return type == null ? null : Activator.CreateInstance(type);
    }

    private static void CopyHybridClrArtifacts(BuildTarget target)
    {
        string hotUpdateDll = Path.Combine("HybridCLRData", "HotUpdateDlls", target.ToString(), HotUpdateAssemblyName + ".dll");
        EnsureFileExists(hotUpdateDll, "compiled HotUpdate DLL");
        CopyFile(hotUpdateDll, HotUpdateDllLocation);

        string strippedAotDir = Path.Combine("HybridCLRData", "AssembliesPostIl2CppStrip", target.ToString());
        EnsureDirectoryExists(strippedAotDir, "HybridCLR stripped AOT directory");
        foreach (string assemblyName in AotMetadataAssemblyNames)
        {
            CopyFile(Path.Combine(strippedAotDir, assemblyName + ".dll"), $"{MetadataLocation}/{assemblyName}.dll.bytes");
        }
    }

    private static void ConfigureWeixinExportSettings(string outputPath)
    {
        object config = InvokeStatic("WeChatWASM.UnityUtil, WxEditor", "GetEditorConf");
        if (config == null)
        {
            config = InvokeStatic("WeChatWASM.UnityUtil, com.qq.weixin.minigame.Editor", "GetEditorConf");
        }

        if (config == null)
        {
            throw new InvalidOperationException("Unable to load Weixin MiniGame editor config from com.qq.weixin.minigame.");
        }

        object projectConf = GetFieldValue(config, "ProjectConf");
        if (projectConf == null)
        {
            throw new InvalidOperationException("Weixin MiniGame ProjectConf is missing.");
        }

        string relativeOutput = ToProjectRelativePath(outputPath);
        SetFieldValue(projectConf, "projectName", "GatebreakerArena");
        SetFieldValue(projectConf, "Appid", GetCommandLineValue("-wechatAppId", "wx-test-gatebreaker"));
        SetFieldValue(projectConf, "CDN", GetCommandLineValue("-wechatCdn", string.Empty));
        SetFieldValue(projectConf, "relativeDST", relativeOutput);
        SetFieldValue(projectConf, "DST", Path.GetFullPath(outputPath));
        SetFieldValue(projectConf, "MemorySize", 64);
        EditorUtility.SetDirty(config as UnityEngine.Object);
        AssetDatabase.SaveAssets();
    }

    private static void InvokeWeixinDoExport(bool buildWebGL)
    {
        object result = InvokeStatic("WeChatWASM.WXConvertCore, WxEditor", "DoExport", buildWebGL);
        if (result == null)
        {
            result = InvokeStatic("WeChatWASM.WXConvertCore, com.qq.weixin.minigame.Editor", "DoExport", buildWebGL);
        }

        if (result == null)
        {
            throw new InvalidOperationException("Weixin MiniGame export API WeChatWASM.WXConvertCore.DoExport was not found.");
        }

        if (Convert.ToInt32(result) != 0)
        {
            throw new InvalidOperationException($"Weixin MiniGame export failed. WXExportError={result}");
        }
    }

    private static void EnsureYooAssetCollectorConfig()
    {
        AssetBundleCollectorPackage package = AssetBundleCollectorSettingData.Setting.Packages
            .FirstOrDefault(item => item.PackageName == YooPackageName);
        if (package == null)
        {
            package = AssetBundleCollectorSettingData.CreatePackage(YooPackageName);
            package.PackageDesc = "Gatebreaker smoke built-in package";
        }

        package.EnableAddressable = false;
        package.SupportExtensionless = true;
        package.LocationToLower = false;
        package.IncludeAssetGUID = false;
        package.AutoCollectShaders = true;

        AssetBundleCollectorGroup group = package.Groups.FirstOrDefault(item => item.GroupName == "Smoke Builtin");
        if (group == null)
        {
            group = AssetBundleCollectorSettingData.CreateGroup(package, "Smoke Builtin");
        }

        AssetBundleCollector collector = group.Collectors.FirstOrDefault(item => item.CollectPath == YooCollectPath);
        if (collector == null)
        {
            collector = new AssetBundleCollector();
            AssetBundleCollectorSettingData.CreateCollector(group, collector);
        }

        collector.CollectPath = YooCollectPath;
        collector.CollectorGUID = AssetDatabase.AssetPathToGUID(YooCollectPath);
        collector.CollectorType = ECollectorType.MainAssetCollector;
        collector.AddressRuleName = nameof(AddressByFileName);
        collector.PackRuleName = nameof(PackDirectory);
        collector.FilterRuleName = nameof(CollectAll);
        collector.AssetTags = string.Empty;
        collector.UserData = string.Empty;

        AssetBundleCollectorSettingData.ModifyCollector(group, collector);
        AssetBundleCollectorSettingData.SaveFile();
        package.CheckConfigError();
    }

    private static void EnsureYooAssetBuiltInFilesExist()
    {
        string root = Path.Combine("Assets", "StreamingAssets", "yoo", YooPackageName);
        EnsureDirectoryExists(root, "YooAssets built-in package directory");
        if (!Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
        {
            throw new InvalidOperationException($"YooAssets built-in package directory is empty: {root}");
        }

        bool hasManifest = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Any(path => path.IndexOf(YooPackageVersion, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         Path.GetFileName(path).IndexOf("PackageManifest", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!hasManifest)
        {
            throw new InvalidOperationException($"YooAssets built-in package manifest was not found under: {root}");
        }
    }

    private static bool HasYooAssetCollectorPackage(string packageName)
    {
        return AssetBundleCollectorSettingData.Setting.Packages.Any(item =>
        {
            if (item.PackageName != packageName)
            {
                return false;
            }

            return item.Groups.Any(group => group.Collectors.Any(collector => collector.CollectPath == YooCollectPath));
        });
    }

    private static void EnsureWeixinMiniGamePackageInstalled()
    {
        if (Type.GetType("WeChatWASM.WXConvertCore, WxEditor") == null &&
            Type.GetType("WeChatWASM.WXConvertCore, com.qq.weixin.minigame.Editor") == null)
        {
            throw new InvalidOperationException(
                "Weixin MiniGame SDK is not installed or has not compiled. " +
                "Restore packages and ensure com.qq.weixin.minigame is available from Packages/manifest.json.");
        }
    }

    private static object InvokeStatic(string assemblyQualifiedTypeName, string methodName, params object[] args)
    {
        Type type = Type.GetType(assemblyQualifiedTypeName);
        if (type == null)
        {
            return null;
        }

        MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(item => item.Name == methodName && ParametersMatch(item.GetParameters(), args));
        if (method == null)
        {
            throw new MissingMethodException(type.FullName, methodName);
        }

        return method.Invoke(null, args);
    }

    private static object InvokeInstance(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(item => item.Name == methodName && ParametersMatch(item.GetParameters(), args));
        if (method == null)
        {
            throw new MissingMethodException(target.GetType().FullName, methodName);
        }

        return method.Invoke(target, args);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object[] args)
    {
        if (parameters.Length != args.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static object GetFieldValue(object target, string fieldName)
    {
        return target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }

        field.SetValue(target, value);
    }

    private static void EnsureSceneInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length != 1 || scenes[0] != BootstrapScene)
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScene, true) };
        }
    }

    private static void AddScriptingDefines(BuildTargetGroup group, IEnumerable<string> symbols)
    {
        var current = new HashSet<string>(
            PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        bool changed = false;
        foreach (string symbol in symbols)
        {
            changed |= current.Add(symbol);
        }

        if (changed)
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", current.OrderBy(item => item)));
        }
    }

    private static BuildTarget ParseBuildTarget(string name)
    {
        return (BuildTarget)Enum.Parse(typeof(BuildTarget), name);
    }

    private static BuildTargetGroup ParseBuildTargetGroup(string name)
    {
        return (BuildTargetGroup)Enum.Parse(typeof(BuildTargetGroup), name);
    }

    private static string GetCommandLineValue(string key, string defaultValue)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key)
            {
                return args[i + 1];
            }
        }

        return defaultValue;
    }

    private static string ToProjectRelativePath(string path)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string fullPath = Path.GetFullPath(path);
        Uri rootUri = new Uri(projectRoot.EndsWith(Path.DirectorySeparatorChar.ToString()) ? projectRoot : projectRoot + Path.DirectorySeparatorChar);
        Uri pathUri = new Uri(fullPath);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('\\', '/');
    }

    private static void CopyFile(string sourcePath, string destinationPath)
    {
        EnsureFileExists(sourcePath, Path.GetFileName(sourcePath));
        string directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, destinationPath, true);
    }

    private static void EnsureFileExists(string path, string label)
    {
        if (!File.Exists(path) || new FileInfo(path).Length <= 0)
        {
            throw new FileNotFoundException($"{label} is missing or empty: {path}", path);
        }
    }

    private static void EnsureDirectoryExists(string path, string label)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"{label} is missing: {path}");
        }
    }

    private static void EnsureDirectoryHasFiles(string path, string label)
    {
        EnsureDirectoryExists(path, label);
        if (!Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any())
        {
            throw new InvalidOperationException($"{label} is empty: {path}");
        }
    }

    private static void EnsureBuildSucceeded(UnityEditor.Build.Reporting.BuildReport report, string outputPath)
    {
        if (report == null || report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Build failed. Result={report?.summary.result}");
        }

        EnsureFileExists(outputPath, "build output");
    }
}
