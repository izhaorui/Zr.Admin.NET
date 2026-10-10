@echo off
rem ============================================================================
rem Init database (auto migration + seed data): runs --initdb only, no web host.
rem Usage: double-click, or run bat\init-run.bat from the repository root.
rem
rem IMPORTANT: keep this file ASCII-only.
rem    cmd.exe decodes .bat with the OEM/ANSI code page (936 on zh-CN Windows).
rem    UTF-8 Chinese comments get mis-decoded into mojibake; a trailing
rem    double-byte char swallows the CRLF, the next line is executed as a
rem    command ("'xxx' is not recognized as an internal or external command")
rem    and an option can even lose its value, leaving a bare switch such as
rem    --ShowDbLog, which the command-line config provider reads as "true".
rem
rem 1) cd /d "%~dp0.."
rem    Anchor the working directory to the repository root. Otherwise, when
rem    launched from an IDE / the repo root, the relative --project path points
rem    to a non-existent project and the script exits immediately.
rem
rem 2) ASPNETCORE_ENVIRONMENT defaults to Development (kept if already defined)
rem    With --no-launch-profile the env var from launchSettings.json is not
rem    applied, the environment falls back to Production and only the committed
rem    appsettings.json is read (it holds the author's local DB address), so
rem    other developers cannot connect to their own database.
rem    Setting Development also layers each developer's local, gitignored
rem    appsettings.Development.json.
rem
rem 3) ShowDbLog=false, set twice on purpose:
rem    - the env var below is read by the EnvironmentVariables provider, which
rem      is registered after every appsettings file, so it wins. It does not
rem      depend on command-line parsing and cannot be broken by a mangled line.
rem    - --ShowDbLog=false is a single token (no space), so it cannot lose its
rem      value; the command-line provider is registered last of all and wins.
rem    Needed because appsettings.Development.json ships ShowDbLog=true, which
rem    prints every SQL statement to the console and logs\all.log - see
rem    SqlsugarSetup.SetSugarAop / Aop.OnLogExecuting.
rem
rem 4) --no-launch-profile + --urls http://localhost:0
rem    Skip launchSettings.json's applicationUrl (8888) and listen on a random
rem    port, so a running instance / dotnet watch is not disturbed.
rem ============================================================================

setlocal
cd /d "%~dp0.."
if not defined ASPNETCORE_ENVIRONMENT set "ASPNETCORE_ENVIRONMENT=Development"

rem Layer 1: environment variable (independent of argument parsing).
set "ShowDbLog=false"

rem Layer 2: command line, single-token form so the value can never be split off.
dotnet run --project ZR.Admin.WebApi --no-launch-profile -- --initdb --ShowDbLog=false --urls "http://localhost:0"

pause
endlocal
