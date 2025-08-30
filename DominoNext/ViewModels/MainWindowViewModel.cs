using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using DominoNext.Services.Interfaces;
using DominoNext.Services.Implementation;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Settings;
using DominoNext.Views.Settings;

namespace DominoNext.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly MidiProcessingService _midiProcessingService;
        private readonly IPlaybackService _playbackService;
        private readonly ICoordinateService _coordinateService; // 新增：坐标服务

        public string Greeting { get; } = "Welcome to Avalonia!";

        public PianoRollViewModel PianoRoll { get; }

        public MainWindowViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _playbackService = new PlaybackService();
            _midiProcessingService = new MidiProcessingService();
            _coordinateService = new CoordinateService(); // 修复：创建坐标服务实例
            
            // 修复：传入正确的坐标服务
            PianoRoll = new PianoRollViewModel(_coordinateService, _playbackService);
            
            System.Diagnostics.Debug.WriteLine("MainWindowViewModel 构造完成");
        }

        // 无参构造函数用于设计时
        public MainWindowViewModel() : this(new DominoNext.Services.Implementation.SettingsService())
        {
        }

        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            var settingsViewModel = new SettingsWindowViewModel(_settingsService);
            var settingsWindow = new SettingsWindow
            {
                DataContext = settingsViewModel
            };

            // 安全地获取主窗口
            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                await settingsWindow.ShowDialog(desktop.MainWindow);
            }
            else
            {
                // 如果没有主窗口，就作为独立窗口显示
                settingsWindow.Show();
            }
        }

        [RelayCommand]
        private void NewFile()
        {
            // 清除当前项目
            PianoRoll.Cleanup();
            PianoRoll.Tracks.Clear();
            PianoRoll.Notes.Clear();
            PianoRoll.SelectedTrack = null;
            
            // 修复：重置为默认设置 - 与PianoRollViewModel的默认值保持一致
            PianoRoll.TotalMeasures = 16; // 修复：改为16与默认值一致
            PianoRoll.TicksPerBeat = Models.Music.MusicalFraction.QUARTER_NOTE_TICKS; // 默认96 PPQ
            PianoRoll.BeatsPerMeasure = 4; // 4/4拍
            
            // 修复：为新项目设置合适的默认缩放
            PianoRoll.SetDefaultZoomForEmptyProject();
        }

        [RelayCommand]
        private async Task OpenFileAsync()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow?.StorageProvider is { } storageProvider)
                {
                    var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "打开MIDI文件",
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("MIDI Files")
                            {
                                Patterns = new[] { "*.mid", "*.midi" }
                            },
                            new FilePickerFileType("All Files")
                            {
                                Patterns = new[] { "*.*" }
                            }
                        },
                        AllowMultiple = false
                    });

                    if (files.Count > 0)
                    {
                        var file = files[0];
                        var filePath = file.TryGetLocalPath();

                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            await LoadMidiFileAsync(filePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开文件失败: {ex.Message}");
                // TODO: 显示错误对话框
            }
        }

        [RelayCommand]
        private async Task LoadMidiFileAsync(string filePath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"开始加载MIDI文件: {filePath}");
                
                var success = await _midiProcessingService.LoadMidiFileAsync(filePath, PianoRoll);
                
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("MIDI文件加载成功");
                    // TODO: 显示成功消息
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("MIDI文件加载失败");
                    // TODO: 显示错误消息
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载MIDI文件异常: {ex.Message}");
                // TODO: 显示错误对话框
            }
        }

        [RelayCommand]
        private async Task SaveFileAsync()
        {
            try
            {
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow?.StorageProvider is { } storageProvider)
                {
                    var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "保存MIDI文件",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("MIDI Files")
                            {
                                Patterns = new[] { "*.mid" }
                            }
                        },
                        DefaultExtension = "mid",
                        SuggestedFileName = "untitled.mid"
                    });

                    if (file != null)
                    {
                        var filePath = file.TryGetLocalPath();
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            await SaveMidiFileAsync(filePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存文件失败: {ex.Message}");
                // TODO: 显示错误对话框
            }
        }

        private async Task SaveMidiFileAsync(string filePath)
        {
            try
            {
                // TODO: 实现MIDI文件保存功能
                await Task.CompletedTask;
                System.Diagnostics.Debug.WriteLine($"保存文件到: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存MIDI文件失败: {ex.Message}");
                throw;
            }
        }

        [RelayCommand]
        private void ExitApplication()
        {
            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        /// <summary>
        /// 获取MIDI文件信息（用于状态显示）
        /// </summary>
        public async Task<string> GetMidiFileInfoAsync(string filePath)
        {
            try
            {
                var info = await _midiProcessingService.GetMidiFileInfoAsync(filePath);
                if (info != null)
                {
                    return $"轨道: {info.TrackCount}, 音符: {info.NoteCount}, PPQ: {info.TicksPerBeat}, 拍号: {info.TimeSignature}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取MIDI文件信息失败: {ex.Message}");
            }
            
            return "无法获取文件信息";
        }
    }
}