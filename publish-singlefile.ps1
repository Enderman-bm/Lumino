# Lumino 单文件发布脚本
# 使用方法: .\publish-singlefile.ps1 [-Compress] [-Platform <platform>]

param(
    [switch]$Compress,
    [string]$Platform = "win-x64",
    [string]$OutputDir = ".\publish"
)

# 设置错误处理
$ErrorActionPreference = "Stop"

# 脚本路径
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "Lumino"
$ProjectFile = Join-Path $ProjectDir "Lumino.csproj"

# 输出目录
$PublishDir = Join-Path $ScriptDir $OutputDir
$PlatformDir = Join-Path $PublishDir $Platform

Write-Host "=== Lumino 单文件发布脚本 ===" -ForegroundColor Cyan
Write-Host "平台: $Platform" -ForegroundColor Yellow
Write-Host "输出目录: $PublishDir" -ForegroundColor Yellow
Write-Host "启用压缩: $($Compress.ToString())" -ForegroundColor Yellow
Write-Host ""

# 清理旧的发布文件
if (Test-Path $PlatformDir) {
    Write-Host "清理旧的发布文件..." -ForegroundColor Gray
    Remove-Item $PlatformDir -Recurse -Force
}

# 创建输出目录
New-Item -ItemType Directory -Path $PlatformDir -Force | Out-Null

# 发布应用
Write-Host "开始发布 Lumino 为单文件..." -ForegroundColor Green

$publishArgs = @(
    "publish",
    $ProjectFile,
    "-c", "Release",
    "-r", $Platform,
    "-p:IsPublishing=true",
    "--self-contained",
    "--no-restore",
    "-o", $PlatformDir,
    "-v", "minimal"
)

& dotnet $publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "发布失败！"
    exit 1
}

# 获取发布文件信息
$exeFiles = Get-ChildItem $PlatformDir -Filter "*.exe"
if ($exeFiles.Count -eq 0) {
    Write-Error "未找到发布生成的可执行文件！"
    exit 1
}

$exeFile = $exeFiles[0]
$originalSize = (Get-Item $exeFile.FullName).Length
$originalSizeMB = [math]::Round($originalSize / 1MB, 2)

Write-Host "发布成功！" -ForegroundColor Green
Write-Host "原始文件大小: $originalSizeMB MB" -ForegroundColor Cyan
Write-Host "文件位置: $($exeFile.FullName)" -ForegroundColor Cyan

# 如果启用压缩，使用 UPX 压缩
if ($Compress) {
    Write-Host ""
    Write-Host "开始使用 UPX 压缩..." -ForegroundColor Yellow

    # 检查 UPX 是否安装
    $upxPath = Get-Command upx -ErrorAction SilentlyContinue
    if (-not $upxPath) {
        # 尝试在常见位置查找 UPX
        $upxPaths = @(
            "C:\Program Files\UPX\upx.exe",
            "C:\Program Files (x86)\UPX\upx.exe",
            "$env:USERPROFILE\upx\upx.exe",
            "upx.exe"
        )

        foreach ($path in $upxPaths) {
            if (Test-Path $path) {
                $upxPath = Get-Item $path
                break
            }
        }
    }

    if (-not $upxPath) {
        Write-Warning "未找到 UPX 工具。正在下载 UPX..."
        try {
            # 下载 UPX
            $upxUrl = "https://github.com/upx/upx/releases/download/v4.2.4/upx-4.2.4-win64.zip"
            $upxZip = Join-Path $ScriptDir "upx.zip"
            $upxExtractDir = Join-Path $ScriptDir "upx-temp"

            Invoke-WebRequest -Uri $upxUrl -OutFile $upxZip
            Expand-Archive -Path $upxZip -DestinationPath $upxExtractDir

            $upxPath = Get-Item (Join-Path $upxExtractDir "upx-4.2.4-win64\upx.exe")
        }
        catch {
            Write-Warning "无法下载 UPX，跳过压缩步骤。"
            Write-Warning "您可以手动安装 UPX 并重新运行脚本。"
            return
        }
    }

    # 使用 UPX 压缩
    Write-Host "使用 UPX 压缩文件..." -ForegroundColor Gray
    & $upxPath.Source --best --lzma $exeFile.FullName

    if ($LASTEXITCODE -eq 0) {
        $compressedSize = (Get-Item $exeFile.FullName).Length
        $compressedSizeMB = [math]::Round($compressedSize / 1MB, 2)
        $compressionRatio = [math]::Round(($originalSize - $compressedSize) / $originalSize * 100, 1)

        Write-Host "压缩完成！" -ForegroundColor Green
        Write-Host "压缩后大小: $compressedSizeMB MB" -ForegroundColor Cyan
        Write-Host "压缩率: $compressionRatio%" -ForegroundColor Cyan
    }
    else {
        Write-Warning "UPX 压缩失败，继续使用未压缩的文件。"
    }

    # 清理临时文件
    if (Test-Path $upxZip) { Remove-Item $upxZip -Force }
    if (Test-Path $upxExtractDir) { Remove-Item $upxExtractDir -Recurse -Force }
}

Write-Host ""
Write-Host "=== 发布完成 ===" -ForegroundColor Green
Write-Host "可执行文件: $($exeFile.FullName)" -ForegroundColor Cyan

# 显示文件大小摘要
$finalSize = (Get-Item $exeFile.FullName).Length
$finalSizeMB = [math]::Round($finalSize / 1MB, 2)
Write-Host "最终文件大小: $finalSizeMB MB" -ForegroundColor Cyan

# 检查是否还有其他文件
$otherFiles = Get-ChildItem $PlatformDir | Where-Object { $_.Name -ne $exeFile.Name }
if ($otherFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "注意: 发布目录中还包含以下文件:" -ForegroundColor Yellow
    $otherFiles | ForEach-Object {
        $size = [math]::Round($_.Length / 1MB, 2)
        Write-Host "  - $($_.Name) ($size MB)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "发布脚本执行完毕。" -ForegroundColor Green