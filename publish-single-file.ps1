# Lumino 单文件发布脚本
# 用法: .\publish-single-file.ps1 [-RuntimeIdentifier <rid>] [-Configuration <config>]

param(
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputPath = "publish"
)

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $projectRoot "Lumino" "Lumino.csproj"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Lumino 单文件发布" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "项目路径: $projectPath"
Write-Host "运行时标识符: $RuntimeIdentifier"
Write-Host "配置: $Configuration"
Write-Host "输出路径: $OutputPath"
Write-Host ""

# 验证项目文件是否存在
if (!(Test-Path $projectPath)) {
    Write-Host "错误: 项目文件不存在: $projectPath" -ForegroundColor Red
    exit 1
}

# 发布命令
$publishCmd = "dotnet publish `"$projectPath`" -c $Configuration -r $RuntimeIdentifier -o `"$OutputPath`" --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true"

Write-Host "执行命令:" -ForegroundColor Yellow
Write-Host $publishCmd
Write-Host ""

# 执行发布
Invoke-Expression $publishCmd

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ 发布成功!" -ForegroundColor Green
    Write-Host ""
    
    # 获取输出文件大小
    $exePath = Join-Path $OutputPath "Lumino.exe"
    if (Test-Path $exePath) {
        $fileSize = (Get-Item $exePath).Length / 1MB
        Write-Host "输出文件: $exePath"
        Write-Host "文件大小: $([Math]::Round($fileSize, 2)) MB"
    }
} else {
    Write-Host ""
    Write-Host "❌ 发布失败，错误代码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "支持的运行时标识符 (RuntimeIdentifier):" -ForegroundColor Cyan
Write-Host "  - win-x64   : Windows 64 bit (default)"
Write-Host "  - win-x86   : Windows 32 bit"
Write-Host "  - win-arm64 : Windows ARM64"
Write-Host ""
