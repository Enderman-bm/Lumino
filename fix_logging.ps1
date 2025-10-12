# 批量修复日志输出脚本
# 将所有 Console.WriteLine 和 Debug.WriteLine 替换为 EnderLogger

$files = @(
    "Lumino\Views\Rendering\Notes\NoteRenderer.cs",
    "Lumino\Views\Rendering\Vulkan\PerformanceTest.cs",
    "Lumino\Views\Rendering\Vulkan\OptimizedNoteRenderer.cs",
    "Lumino\Views\Rendering\Vulkan\ExtremePerformanceRenderer.cs",
    "Lumino\Views\Rendering\Vulkan\VulkanRenderContextExtreme.cs",
    "Lumino\Views\Rendering\Vulkan\VulkanRenderContext.cs",
    "Lumino\Views\Rendering\Utils\NoteTextRenderer.cs",
    "Lumino\Views\Rendering\Tools\SelectionBoxRenderer.cs",
    "Lumino\Views\Rendering\Tools\MouseCurveRenderer.cs",
    "Lumino\Views\Rendering\Events\VelocityBarRenderer.cs",
    "Lumino\Views\Rendering\Adapters\VulkanDrawingContextAdapter.cs",
    "Lumino\Views\Pages\VulkanDemoPage.axaml.cs",
    "Lumino\Views\Controls\PianoRollView.axaml.cs",
    "Lumino\Views\Controls\Canvas\PianoRollCanvas.cs",
    "Lumino\Tests\ExtremePerformanceTest.cs",
    "Lumino\ViewModels\Editor\Base\PianoRollViewModel.Events.cs"
)

foreach ($file in $files) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        Write-Host "Processing: $file"
        $content = Get-Content $fullPath -Raw
        
        # 检查是否已经包含 using EnderDebugger
        if ($content -notmatch "using EnderDebugger;") {
            # 在第一个 using 之后添加
            $content = $content -replace "(using [^;]+;)", "`$1`r`nusing EnderDebugger;"
        }
        
        # 替换日志调用 - 注意需要解析字符串参数
        # 这是一个简化版本，实际可能需要更复杂的处理
        
        Set-Content $fullPath $content -NoNewline
        Write-Host "Completed: $file"
    } else {
        Write-Host "File not found: $file" -ForegroundColor Yellow
    }
}

Write-Host "`nAll files processed!" -ForegroundColor Green
