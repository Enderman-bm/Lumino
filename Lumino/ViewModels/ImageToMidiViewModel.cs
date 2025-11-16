using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToMidi;
using ImageToMidi.Config;
using ImageToMidi.MIDI;
using Lumino.Services.Interfaces;

namespace Lumino.ViewModels
{
    /// <summary>
    /// ImageToMidi ViewModel - 图片转MIDI工具的主ViewModel
    /// </summary>
    public partial class ImageToMidiViewModel : ViewModelBase, IDisposable
    {
        private readonly ILoggingService _loggingService;
        private readonly IDialogService _dialogService;
        private string? _imageFilePath;

        #region 可观察属性

        /// <summary>
        /// 当前加载的图片
        /// </summary>
        [ObservableProperty]
        private Bitmap? _currentImage;

        /// <summary>
        /// 转换配置
        /// </summary>
        [ObservableProperty]
        private ConversionConfig _config = new();

        /// <summary>
        /// 是否正在处理
        /// </summary>
        [ObservableProperty]
        private bool _isProcessing;

        /// <summary>
        /// 处理进度文本
        /// </summary>
        [ObservableProperty]
        private string _progressText = string.Empty;

        /// <summary>
        /// 状态消息
        /// </summary>
        [ObservableProperty]
        private string _statusMessage = "就绪";

        /// <summary>
        /// 图片尺寸文本
        /// </summary>
        [ObservableProperty]
        private string _imageInfo = "未加载图片";

        /// <summary>
        /// 是否启用开始转换按钮
        /// </summary>
        [ObservableProperty]
        private bool _isConvertEnabled;

        #endregion

        public ImageToMidiViewModel(
            ILoggingService loggingService,
            IDialogService dialogService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            _loggingService.LogInfo("ImageToMidiViewModel", "ImageToMidi ViewModel已创建");
        }

        public void Dispose()
        {
            CurrentImage?.Dispose();
        }

        #region 命令

        /// <summary>
        /// 加载图片命令
        /// </summary>
        [RelayCommand]
        public async Task LoadImageAsync()
        {
            try
            {
                var filePath = await _dialogService.ShowOpenFileDialogAsync(
                    "选择图片",
                    new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" }
                );

                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                _imageFilePath = filePath;
                _loggingService.LogInfo("ImageToMidiViewModel", $"加载图片: {filePath}");

                // 加载图片
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                CurrentImage = await Task.Run(() => Bitmap.DecodeToWidth(fileStream, 800));

                // 更新图片信息
                ImageInfo = $"尺寸: {CurrentImage?.Size.Width} x {CurrentImage?.Size.Height}";
                IsConvertEnabled = true;
                StatusMessage = "图片加载成功";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImageToMidiViewModel", $"加载图片失败: {ex.Message}");
                StatusMessage = $"加载失败: {ex.Message}";
                await _dialogService.ShowErrorDialogAsync("加载图片失败", ex.Message);
            }
        }

        /// <summary>
        /// 开始转换命令
        /// </summary>
        [RelayCommand]
        public async Task ConvertAsync()
        {
            if (string.IsNullOrEmpty(_imageFilePath) || CurrentImage == null)
            {
                return;
            }

            try
            {
                IsProcessing = true;
                ProgressText = "正在处理图片...";
                StatusMessage = "转换中...";

                // 选择输出MIDI文件路径
                var outputPath = await _dialogService.ShowSaveFileDialogAsync(
                    "保存MIDI文件",
                    Path.GetFileNameWithoutExtension(_imageFilePath) + ".mid",
                    new[] { "*.mid" }
                );

                if (string.IsNullOrEmpty(outputPath))
                {
                    IsProcessing = false;
                    return;
                }

                // 执行转换
                var success = await Task.Run(() =>
                {
                    try
                    {
                        // 使用ImageToMidi.Core的核心功能
                        var converter = new ImageToMidiConverter(_config);
                        converter.ConvertImageToMidi(_imageFilePath, outputPath);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError("ImageToMidiViewModel", $"转换失败: {ex.Message}");
                        return false;
                    }
                });

                if (success)
                {
                    StatusMessage = "转换成功!";
                    ProgressText = "转换完成";

                    // 询问是否打开生成的MIDI文件
                    var shouldOpen = await _dialogService.ShowConfirmationDialogAsync(
                        "转换完成",
                        "是否加载生成的MIDI文件到Lumino?"
                    );

                    if (shouldOpen)
                    {
                        // TODO: 通过事件通知主窗口加载MIDI文件
                        // 这里可以触发一个事件，让MainWindowViewModel来处理文件加载
                    }
                }
                else
                {
                    StatusMessage = "转换失败";
                    await _dialogService.ShowErrorDialogAsync("转换失败", "图片转换MIDI过程中出现错误");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImageToMidiViewModel", $"转换异常: {ex.Message}");
                StatusMessage = $"错误: {ex.Message}";
                await _dialogService.ShowErrorDialogAsync("转换错误", ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// 关闭窗口命令
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            // 触发关闭请求事件
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region 事件

        /// <summary>
        /// 关闭请求事件
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// MIDI文件生成完成事件
        /// </summary>
        public event EventHandler<string>? MidiFileGenerated;

        #endregion
    }
}
