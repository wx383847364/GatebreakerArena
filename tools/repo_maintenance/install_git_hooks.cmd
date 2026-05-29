@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\\..") do set "REPO_ROOT=%%~fI"

call :FindPython
if errorlevel 1 (
  echo [error] 未找到 Python 3.8+。Git hooks 需要 Python 来检查提交规范和 Unity GUID。
  echo [hint] Windows 可安装 Python 后确保 python 或 py 在 PATH 中。
  exit /b 1
)

if not exist "%REPO_ROOT%\\.githooks\\_common" (
  echo [error] 缺少 .githooks\\_common。
  exit /b 1
)
if not exist "%REPO_ROOT%\\.githooks\\pre-commit" (
  echo [error] 缺少 .githooks\\pre-commit。
  exit /b 1
)
if not exist "%REPO_ROOT%\\.githooks\\prepare-commit-msg" (
  echo [error] 缺少 .githooks\\prepare-commit-msg。
  exit /b 1
)
if not exist "%REPO_ROOT%\\.githooks\\commit-msg" (
  echo [error] 缺少 .githooks\\commit-msg。
  exit /b 1
)
if not exist "%REPO_ROOT%\\.githooks\\post-commit" (
  echo [error] 缺少 .githooks\\post-commit。
  exit /b 1
)

git -C "%REPO_ROOT%" config core.hooksPath .githooks
if errorlevel 1 (
  echo [error] 设置 core.hooksPath 失败。
  exit /b 1
)

echo [ok] 已将当前仓库的 core.hooksPath 设置为 .githooks
echo [ok] 以后提交前会自动检查提交规范、merge 信息和 Unity GUID。
echo [ok] Python: %PYTHON_CMD%
exit /b 0

:FindPython
python3 -c "import sys; raise SystemExit(sys.version_info < (3, 8))" >nul 2>nul
if not errorlevel 1 (
  set "PYTHON_CMD=python3"
  exit /b 0
)

python -c "import sys; raise SystemExit(sys.version_info < (3, 8))" >nul 2>nul
if not errorlevel 1 (
  set "PYTHON_CMD=python"
  exit /b 0
)

py -3 -c "import sys; raise SystemExit(sys.version_info < (3, 8))" >nul 2>nul
if not errorlevel 1 (
  set "PYTHON_CMD=py -3"
  exit /b 0
)

exit /b 1
