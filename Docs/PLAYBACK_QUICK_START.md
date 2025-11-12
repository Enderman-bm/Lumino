# Lumino 播放功能快速开始

## 🎵 5 分钟快速集成

### 第一步：注册服务

在应用启动或 MainWindow 初始化时：

```csharp
// Program.cs 或 App.axaml.cs 中
var serviceCollection = new ServiceCollection();

// 注册播放相关服务
serviceCollection.AddSingleton(new MidiPlaybackService(EnderLogger.Instance));
serviceCollection.AddSingleton<PlaybackService>();
serviceCollection.AddSingleton<NotePlaybackEngine>();
serviceCollection.AddSingleton<PlaybackViewModel>();

// 构建服务容器
var services = serviceCollection.BuildServiceProvider();
```

### 第二步：集成到 ViewModel

在 MainWindowViewModel 中获取播放服务：

```csharp
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PlaybackViewModel _playbackViewModel;
    
    public PlaybackViewModel PlaybackViewModel => _playbackViewModel;

    public MainWindowViewModel(PlaybackViewModel playbackViewModel)
    {
        _playbackViewModel = playbackViewModel;
    }
}
```

### 第三步：添加 UI 控件

在 MainWindow.axaml 中添加播放面板和指示线：

```xml
<Window>
    <DockPanel>
        <!-- 主编辑区 -->
        <Canvas Name="EditorCanvas" DockPanel.Dock="Top">
            <!-- 这里放钢琴卷 -->
            <local:PlayheadIndicator 
                x:Name="Playhead"
                PlayheadX="{Binding PlaybackViewModel.PlayheadX}"
                Height="{Binding $parent[Canvas].Bounds.Height}" />
        </Canvas>

        <!-- 播放控制面板 -->
        <local:PlaybackControlPanel 
            DockPanel.Dock="Bottom"
            DataContext="{Binding PlaybackViewModel}" />
    </DockPanel>
</Window>
```

### 第四步：加载音符并播放

在编辑项目后：

```csharp
// 当用户打开 MIDI 文件或编辑完成时
var notes = _editorService.GetAllNotes();  // 获取编辑器中的所有音符
_playbackViewModel.LoadNotes(notes);

// 用户现在可以点击"播放"按钮或按 Space 键开始播放
```

## 🎯 功能演示

### 功能 1：基本播放

```csharp
// 播放
playbackService.Play();

// 暂停
playbackService.Pause();

// 停止（重置到开头）
playbackService.Stop();

// 跳转到 10 秒处
playbackService.Seek(10.0);
```

### 功能 2：速度控制

```csharp
// 1.5 倍速播放
playbackService.PlaybackSpeed = 1.5;

// 0.8 倍速（慢速）
playbackService.PlaybackSpeed = 0.8;

// 通过 ViewModel 命令
playbackViewModel.IncreaseSpeedCommand.Execute(null);
playbackViewModel.DecreaseSpeedCommand.Execute(null);
playbackViewModel.ResetSpeedCommand.Execute(null);
```

### 功能 3：演奏指示线拖拽

```csharp
// 在 XAML 代码后置中处理拖拽
private void OnPlayheadDragged(object? sender, PlayheadDragEventArgs e)
{
    // e.NewX 是新的像素位置
    double timeScale = 100.0;  // 像素/秒，可根据缩放调整
    double targetTime = e.NewX / timeScale;
    
    // 计算进度百分比
    double progress = targetTime / _playbackViewModel.TotalDuration;
    _playbackViewModel.OnProgressBarDragged(Math.Min(1.0, progress));
}
```

### 功能 4：实时性能监控

```csharp
// 获取当前活跃音符数
int activeNotes = playbackViewModel.ActiveNoteCount;

// 获取总音符数
int totalNotes = playbackViewModel.TotalNoteCount;

// 获取当前播放进度
double progress = playbackService.Progress;  // 0-1

// 获取当前时间
double currentTime = playbackService.CurrentTime;  // 秒
```

## ⌨️ 推荐快捷键设置

添加以下快捷键处理：

```csharp
public void OnKeyDown(KeyEventArgs e)
{
    switch (e.Key)
    {
        case Key.Space:
            // 播放/暂停切换
            if (playbackService.IsPlaying)
                playbackService.Pause();
            else
                playbackService.Play();
            break;

        case Key.S when e.KeyModifiers == KeyModifiers.Control:
            // Ctrl+S: 停止
            playbackService.Stop();
            break;

        case Key.OemPlus:
            // +/= 键: 加速
            playbackViewModel.IncreaseSpeedCommand.Execute(null);
            break;

        case Key.OemMinus:
            // -/_ 键: 减速
            playbackViewModel.DecreaseSpeedCommand.Execute(null);
            break;

        case Key.R when e.KeyModifiers == KeyModifiers.Control:
            // Ctrl+R: 重置速度
            playbackViewModel.ResetSpeedCommand.Execute(null);
            break;
    }
}
```

## 🐛 常见问题

**Q: 播放时没有声音**
- 确认 KDMAPI (OmniMIDI) 已安装
- 检查 MIDI 输出设备是否正确配置
- 查看日志是否有错误信息

**Q: 播放不流畅/跳帧**
- 减少编辑器中的音符数量
- 禁用某些视觉效果（抗锯齿、动画等）
- 检查 CPU 占用率

**Q: 时间显示与实际不符**
- 检查 MusicalFraction 的 TPQ (Ticks Per Quarter) 设置
- 检查 Tempo (BPM) 设置是否正确
- 尝试 Seek 重新同步

**Q: 播放完成后自动返回开头**
- 这是正常行为，可以添加循环模式：

```csharp
if (playbackService.CurrentTime >= playbackService.TotalDuration)
{
    if (_enableLooping)
        playbackService.Seek(0);
    else
        playbackService.Stop();
}
```

## 📊 性能建议

| 场景 | 推荐配置 | 说明 |
|------|--------|------|
| 小型 MIDI (<1000 音符) | 默认设置 | 无需优化 |
| 中型 MIDI (1000-10000 音符) | 启用 LOD 渲染 | 视觉优化 |
| 大型 MIDI (>10000 音符) | 分轨播放、禁用某些效果 | 分解工作量 |

## 🔧 高级定制

### 自定义播放精度

```csharp
// 增加更新频率到 120FPS（可能增加 CPU 占用）
// 修改 PlaybackService 中的 UpdateIntervalMs 常数
const int UpdateIntervalMs = 8;  // 原为 16
```

### 添加音轨控制

```csharp
public class TrackPlaybackControl
{
    public int TrackIndex { get; set; }
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }
    public double Volume { get; set; } = 1.0;  // 0-1
}
```

### 实现循环播放

```csharp
public class LoopSettings
{
    public bool Enabled { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int LoopCount { get; set; } = 0;  // 0 = 无限循环
}
```

## 📚 相关文档

- 详细架构：`PLAYBACK_SYSTEM_GUIDE.md`
- KDMAPI 集成：`MidiPlaybackService.cs` 源代码
- MVVM 绑定：`PlaybackViewModel.cs` 源代码

## ✨ 主要特性总结

✅ FL Studio 风格的播放控制
✅ 实时演奏指示线可视化
✅ 0.5x - 2.0x 速度调整
✅ 低延迟 KDMAPI 集成
✅ 支持 Seek 定位
✅ 实时性能监控
✅ MVVM 模式 UI 绑定
✅ 支持多音轨并发播放

---

**快速联系**：遇到问题？查看 `PLAYBACK_SYSTEM_GUIDE.md` 的完整故障排查部分。
