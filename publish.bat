@echo off
REM Lumino 单文件发布批处理脚本
REM 使用方法: publish.bat [compress] [platform]

setlocal enabledelayedexpansion

set "COMPRESS="
set "PLATFORM=win-x64"

if "%1"=="compress" (
    set "COMPRESS=-Compress"
)
if "%2"=="" (
    set "PLATFORM=%2"
) else if "%1" neq "compress" (
    set "PLATFORM=%1"
)

echo === Lumino 单文件发布 ===
echo 平台: %PLATFORM%
echo 压缩: %COMPRESS%
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0publish-singlefile.ps1" %COMPRESS% -Platform %PLATFORM%

pause