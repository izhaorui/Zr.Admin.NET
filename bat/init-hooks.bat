@echo off
rem ============================================================================
rem Enable repository-level git hooks (commit message validation).
rem Run once per machine after clone; running it again is harmless.
rem Usage: double-click, or run bat\init-hooks.bat from the repository root.
rem
rem IMPORTANT: keep this file ASCII-only.
rem    cmd.exe decodes .bat with the OEM/ANSI code page (936 on zh-CN Windows).
rem    UTF-8 Chinese text gets mis-decoded into mojibake and a trailing
rem    double-byte char swallows the CRLF: the next line is then joined to the
rem    previous one, so "pause" inside an error block never runs and the window
rem    closes before the message can be read.
rem ============================================================================

cd /d "%~dp0.."

if not exist ".git" (
    echo [ERROR] .git not found. Run this script from the repository root.
    pause
    exit /b 1
)

if not exist ".githooks\commit-msg" (
    echo [ERROR] .githooks\commit-msg is missing. Please check the repository.
    pause
    exit /b 1
)

git config core.hooksPath .githooks
if errorlevel 1 (
    echo [ERROR] Failed to configure. Make sure Git is installed and on PATH.
    pause
    exit /b 1
)

echo Commit message validation enabled: core.hooksPath = .githooks
echo Messages are checked on every commit, see CONTRIBUTING.md
pause
