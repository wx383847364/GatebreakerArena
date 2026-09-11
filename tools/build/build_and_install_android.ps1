param(
    [string]$Serial = $env:ANDROID_SERIAL,
    [switch]$NoLaunch
)

# Windows PowerShell 5.1; no Python, Git Bash or PowerShell 7 required.
$ErrorActionPreference = 'Stop'
$ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$LogRoot = Join-Path $ProjectRoot 'Logs'
$BuildDirectory = $null

function Quote-NativeArgument([string]$Value) {
    # Windows CommandLineToArgvW quoting, including spaces and trailing backslashes.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    return '"' + [regex]::Replace($escaped, '(\\+)$', '$1$1') + '"'
}

function Invoke-NativeTool([string]$Name, [string]$Executable, [string[]]$Arguments) {
    $outFile = Join-Path $LogRoot "$Name.stdout.log"
    $errFile = Join-Path $LogRoot "$Name.stderr.log"
    $argumentLine = ($Arguments | ForEach-Object { Quote-NativeArgument $_ }) -join ' '
    $process = Start-Process -FilePath $Executable -ArgumentList $argumentLine `
        -WorkingDirectory $ProjectRoot -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    $output = [string](Get-Content -LiteralPath $outFile -Raw -ErrorAction SilentlyContinue)
    $errorOutput = [string](Get-Content -LiteralPath $errFile -Raw -ErrorAction SilentlyContinue)
    if ($output) { Write-Host $output.TrimEnd() }
    if ($errorOutput) { Write-Host $errorOutput.TrimEnd() }
    return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $output; ErrorOutput = $errorOutput }
}

function Find-Editor {
    if ($env:UNITY_BIN) {
        if (Test-Path -LiteralPath $env:UNITY_BIN -PathType Leaf) { return $env:UNITY_BIN }
        throw "UNITY_BIN does not exist: $env:UNITY_BIN"
    }
    $versionText = Get-Content -LiteralPath (Join-Path $ProjectRoot 'ProjectSettings/ProjectVersion.txt') -Raw
    $version = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)').Groups[1].Value
    if (!$version) { throw 'Cannot read the project editor version.' }
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (!$base) { continue }
        foreach ($hub in @('Tuanjie/Hub/Editor', 'Unity/Hub/Editor')) {
            foreach ($exe in @('Editor/Tuanjie.exe', 'Editor/Unity.exe', 'Tuanjie.exe', 'Unity.exe')) {
                $candidate = Join-Path $base "$hub/$version/$exe"
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
            }
        }
    }
    Write-Host "Select the Tuanjie $version editor executable (not the Hub)."
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    try {
        $dialog.Title = "Select Tuanjie $version editor"
        $dialog.Filter = 'Editor executable|Tuanjie.exe;Unity.exe'
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
            throw 'Editor selection cancelled.'
        }
        return $dialog.FileName
    } finally { $dialog.Dispose() }
}

try {
    New-Item -ItemType Directory -Path $LogRoot -Force | Out-Null
    $editor = Find-Editor
    $adb = $env:ADB_BIN
    if (!$adb) {
        $command = Get-Command adb.exe -ErrorAction SilentlyContinue
        if ($command) { $adb = $command.Source }
    }
    if (!$adb) {
        $candidates = @((Join-Path (Split-Path $editor) 'Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'))
        foreach ($sdk in @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME)) {
            if ($sdk) { $candidates += Join-Path $sdk 'platform-tools/adb.exe' }
        }
        if ($env:LOCALAPPDATA) { $candidates += Join-Path $env:LOCALAPPDATA 'Android/Sdk/platform-tools/adb.exe' }
        $adb = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (!$adb -or !(Test-Path -LiteralPath $adb -PathType Leaf)) {
        throw 'adb.exe not found. Install Android Build Support or set ADB_BIN to its full path.'
    }

    $list = Invoke-NativeTool 'android_devices' $adb @('devices', '-l')
    if ($list.ExitCode -ne 0) { throw 'ADB could not list devices.' }
    $devices = @($list.Output -split '\r?\n' | ForEach-Object {
        if ($_ -match '^(\S+)\s+(device|unauthorized|offline|no permissions)\b') {
            [pscustomobject]@{ Serial = $Matches[1]; State = $Matches[2] }
        }
    })
    if (!$Serial) {
        if ($devices.Count -eq 0) { throw 'No phone detected. Connect USB and enable USB debugging.' }
        if ($devices.Count -eq 1) { $Serial = $devices[0].Serial }
        else { $Serial = Read-Host 'Multiple devices connected. Enter the target serial shown above' }
    }
    $device = $devices | Where-Object { $_.Serial -eq $Serial } | Select-Object -First 1
    if (!$device -or $device.State -ne 'device') {
        throw "Device $Serial is unavailable. Unlock the phone and authorize USB debugging."
    }

    $lockPath = Join-Path $ProjectRoot 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        try {
            $lockProbe = [IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None')
            $lockProbe.Dispose()
        } catch { throw 'Project is open in the editor. Save and close it before building.' }
    }
    Write-Host "Editor: $editor"
    Write-Host "Device: $Serial"
    $common = @('-batchmode', '-quit', '-projectPath', $ProjectRoot)
    $runtime = Join-Path $ProjectRoot 'HybridCLRData/LocalIl2CppData-WindowsEditor/il2cpp/libil2cpp/hybridclr'
    if (!(Test-Path -LiteralPath $runtime -PathType Container)) {
        Write-Host 'Installing HybridCLR local runtime...'
        $install = Invoke-NativeTool 'hybridclr_process' $editor ($common + @(
            '-executeMethod', 'GatebreakerSmokeBuildPipeline.InstallHybridClrFromCommandLine',
            '-logFile', (Join-Path $LogRoot 'gatebreaker_hybridclr_install.log')))
        if ($install.ExitCode -ne 0 -or !(Test-Path -LiteralPath $runtime)) {
            throw 'HybridCLR installation failed. See Logs/gatebreaker_hybridclr_install.log.'
        }
    }

    $outputRoot = Join-Path $ProjectRoot 'Builds/Android'
    $BuildDirectory = Join-Path $outputRoot ('.build-install.' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $BuildDirectory -Force | Out-Null
    $temporaryApk = Join-Path $BuildDirectory 'GatebreakerArena-android-smoke.apk'
    $buildLog = Join-Path $LogRoot 'gatebreaker_android_smoke_build.log'
    Write-Host "[1/3] Building APK. Log: $buildLog"
    $build = Invoke-NativeTool 'android_build_process' $editor ($common + @(
        '-executeMethod', 'GatebreakerSmokeBuildPipeline.BuildAndroidSmokeApkFromCommandLine',
        '-gatebreakerOutput', $temporaryApk, '-logFile', $buildLog))
    if ($build.ExitCode -ne 0) { throw "Build failed; no installation attempted. See $buildLog" }
    if (!(Test-Path -LiteralPath $temporaryApk) -or (Get-Item -LiteralPath $temporaryApk).Length -eq 0) {
        throw 'Build produced no APK; no installation attempted.'
    }
    $apk = Join-Path $outputRoot 'GatebreakerArena-android-smoke.apk'
    Move-Item -LiteralPath $temporaryApk -Destination $apk -Force
    Write-Host '[2/3] Installing APK...'
    $install = Invoke-NativeTool 'android_install' $adb @('-s', $Serial, 'install', '-r', $apk)
    if ($install.ExitCode -ne 0 -or $install.Output -notmatch '(?m)^Success\s*$') {
        throw "Installation failed. Enable USB installation and accept phone prompts. APK kept: $apk. Existing app data is not removed."
    }
    if (!$NoLaunch) {
        Write-Host '[3/3] Launching game...'
        $launch = Invoke-NativeTool 'android_launch' $adb @('-s', $Serial, 'shell', 'am', 'start', '-W',
            '-n', 'com.gatebreakerarena.game/com.unity3d.player.UnityPlayerActivity')
        if ($launch.ExitCode -ne 0 -or $launch.Output -notmatch 'Status: ok') {
            throw 'APK installed, but launch failed. See Logs/android_launch.*.log.'
        }
    } else { Write-Host '[3/3] Launch skipped.' }
    Write-Host "Completed: $apk"
    Write-Host 'Please check gameplay on the phone.'
    exit 0
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if ($BuildDirectory -and (Test-Path -LiteralPath $BuildDirectory)) {
        Remove-Item -LiteralPath $BuildDirectory -Recurse -Force
    }
}
