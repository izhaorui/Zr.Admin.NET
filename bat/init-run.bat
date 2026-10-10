@echo off
rem ============================================================================
rem 数据库初始化（自动迁移 + 种子数据）
rem 用法：双击运行，或在仓库根目录执行 bat\init-run.bat
rem
rem 说明：--initdb 只跑初始化、不启动 Web，但 dotnet run 默认会读取
rem       launchSettings.json 的 applicationUrl(localhost:8888) 并做启动探测，
rem       若已有 dotnet watch / 服务实例占用该端口，就会报"端口被占用"。
rem       故此处用 --no-launch-profile 不读 launchProfile，
rem       并以 --urls 指定随机端口(0)，避免与运行中的实例冲突，无需先停服务。
rem ============================================================================

cd /d "%~dp0.."

dotnet run --project ZR.Admin.WebApi --no-launch-profile -- --initdb --urls "http://localhost:0"
pause
