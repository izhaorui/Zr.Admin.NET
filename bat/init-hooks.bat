@echo off
rem ============================================================================
rem 启用仓库级 git hooks（提交信息校验）
rem 每台机器 clone 后执行一次即可；重复执行无害。
rem 用法：双击运行，或在仓库根目录执行 bat\init-hooks.bat
rem ============================================================================

cd /d "%~dp0.."

if not exist ".git" (
    echo [错误] 未找到 .git 目录，请在仓库根目录运行本脚本。
    pause
    exit /b 1
)

if not exist ".githooks\commit-msg" (
    echo [错误] 缺少 .githooks\commit-msg，请确认仓库完整。
    pause
    exit /b 1
)

git config core.hooksPath .githooks
if errorlevel 1 (
    echo [错误] 配置失败，请确认已安装 Git 并加入 PATH。
    pause
    exit /b 1
)

echo 已启用提交信息校验：core.hooksPath = .githooks
echo 提交时会自动校验格式，规则见 CONTRIBUTING.md
pause
