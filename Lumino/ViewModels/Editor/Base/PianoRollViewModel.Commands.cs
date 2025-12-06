using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.ViewModels.Editor.Commands;
using Lumino.ViewModels.Editor.Modules;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using EnderDebugger;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel命令定义
    /// 包含所有用户界面命令的实现
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region 工具选择命令
        /// <summary>
        /// 选择铅笔工具命令
        /// </summary>
        [RelayCommand]
        public void SelectPencilTool() => Toolbar.SelectPencilTool();

        /// <summary>
        /// 选择选择工具命令
        /// </summary>
        [RelayCommand]
        public void SelectSelectionTool() => Toolbar.SelectSelectionTool();

        /// <summary>
        /// 选择橡皮擦工具命令
        /// </summary>
        [RelayCommand]
        public void SelectEraserTool() => Toolbar.SelectEraserTool();

        /// <summary>
        /// 选择剪切工具命令
        /// </summary>
        [RelayCommand]
        public void SelectCutTool() => Toolbar.SelectCutTool();
        #endregion

        #region 音符时长相关命令
        /// <summary>
        /// 切换音符时长下拉框显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleNoteDurationDropDown() => Toolbar.ToggleNoteDurationDropDown();

        /// <summary>
        /// 选择指定的音符时长选项
        /// </summary>
        /// <param name="option">音符时长选项</param>
        [RelayCommand]
        private void SelectNoteDuration(NoteDurationOption option) => Toolbar.SelectNoteDuration(option);

        /// <summary>
        /// 应用自定义分数输入
        /// </summary>
        [RelayCommand]
        private void ApplyCustomFraction() => Toolbar.ApplyCustomFraction();
        #endregion

        #region 选择相关命令
        /// <summary>
        /// 全选当前轨道的所有音符
        /// </summary>
        [RelayCommand]
        private void SelectAll() => SelectionModule?.SelectAll(CurrentTrackNotes);
        #endregion

        #region 视图切换命令
        /// <summary>
        /// 切换事件视图显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleEventView() => Toolbar.ToggleEventView();

        /// <summary>
        /// 切换洋葱皮显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleOnionSkin() => Toolbar.ToggleOnionSkin();
        #endregion

        #region 事件类型选择相关命令
        /// <summary>
        /// 切换事件类型选择器显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleEventTypeSelector()
        {
            IsEventTypeSelectorOpen = !IsEventTypeSelectorOpen;
        }

        /// <summary>
        /// 选择指定的事件类型
        /// </summary>
        /// <param name="eventType">要选择的事件类型</param>
        [RelayCommand]
        private void SelectEventType(EventType eventType)
        {
            CurrentEventType = eventType;
            IsEventTypeSelectorOpen = false;
        }

        /// <summary>
        /// 设置CC控制器号
        /// </summary>
        /// <param name="ccNumber">CC控制器号（0-127）</param>
        [RelayCommand]
        private void SetCCNumber(int ccNumber)
        {
            if (ccNumber >= 0 && ccNumber <= 127)
            {
                CurrentCCNumber = ccNumber;
            }
        }

        /// <summary>
        /// 验证并设置CC号（支持字符串输入）
        /// </summary>
        /// <param name="ccNumberText">CC号的字符串表示</param>
        [RelayCommand]
        private void ValidateAndSetCCNumber(string ccNumberText)
        {
            if (int.TryParse(ccNumberText, out int ccNumber))
            {
                ccNumber = Math.Max(0, Math.Min(127, ccNumber)); // 限制在0-127范围内
                CurrentCCNumber = ccNumber;
            }
        }
        #endregion

        #region 撤销重做命令
        /// <summary>
        /// 撤销命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUndoCommand))]
        public void Undo() => _undoRedoService.Undo();

        /// <summary>
        /// 重做命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRedoCommand))]
        public void Redo() => _undoRedoService.Redo();

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        private bool CanUndoCommand => _undoRedoService.CanUndo;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        private bool CanRedoCommand => _undoRedoService.CanRedo;
        #endregion

        #region 复制粘贴命令
        /// <summary>
        /// 复制选中的音符
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopyCommand))]
        public void CopySelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                // 存储选中的音符数据用于粘贴
                _clipboardNotes = selectedNotes.Select(note => new NoteClipboardData
                {
                    StartTime = note.StartPosition,
                    Duration = note.Duration,
                    Pitch = note.Pitch,
                    Velocity = note.Velocity
                }).ToList();
                
                _logger.Debug("PianoRollViewModel", $"已复制 {selectedNotes.Count} 个音符到剪贴板");
            }
        }

        /// <summary>
        /// 粘贴音符
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPasteCommand))]
        public void PasteNotes()
        {
            if (_clipboardNotes == null || !_clipboardNotes.Any()) return;

            // 清除当前选择
            SelectionModule?.ClearSelection(Notes);

            // 计算粘贴位置（基于当前时间轴位置）
            var pasteStartTime = TimelinePosition;
            
            // 创建新的音符
            var newNotes = new List<NoteViewModel>();
            var baseTime = new MusicalFraction((int)TimelinePosition, 1); // 将double转换为MusicalFraction
            foreach (var clipboardNote in _clipboardNotes)
            {
                var relativeStartTime = clipboardNote.StartTime - _clipboardNotes.Min(n => n.StartTime);
                var newNote = new NoteViewModel(_midiConversionService)
                {
                    StartPosition = baseTime + relativeStartTime,
                    Duration = clipboardNote.Duration,
                    Pitch = clipboardNote.Pitch,
                    Velocity = clipboardNote.Velocity,
                    IsSelected = true
                };
                newNotes.Add(newNote);
            }

            // 添加到当前音轨
            foreach (var note in newNotes)
            {
                Notes.Add(note);
            }

            _logger.Debug("PianoRollViewModel", $"已粘贴 {newNotes.Count} 个音符");
        }

        /// <summary>
        /// 全选音符
        /// </summary>
        [RelayCommand]
        public void SelectAllNotes()
        {
            SelectionModule?.SelectAll(Notes);
        }

        /// <summary>
        /// 取消选择所有音符
        /// </summary>
        [RelayCommand]
        public void DeselectAllNotes()
        {
            SelectionModule?.ClearSelection(Notes);
        }

        /// <summary>
        /// 删除选中的音符
        /// </summary>
        [RelayCommand]
        public void DeleteSelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                // 创建包含索引信息的删除列表
                var notesWithIndices = selectedNotes.Select(note => (note, Notes.IndexOf(note))).ToList();
                var deleteOperation = new DeleteNotesOperation(this, notesWithIndices);
                _undoRedoService.ExecuteAndRecord(deleteOperation);

                _logger.Debug("PianoRollViewModel", $"删除了 {selectedNotes.Count} 个音符");
            }
        }

        /// <summary>
        /// 剪切选中的音符（复制到剪贴板然后删除）
        /// </summary>
        [RelayCommand]
        public void CutSelectedNotes()
        {
            CopySelectedNotes();
            DeleteSelectedNotes();
        }

        /// <summary>
        /// 复制选中的音符（创建副本）
        /// </summary>
        [RelayCommand]
        public void DuplicateSelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                var duplicatedNotes = new List<NoteViewModel>();

                foreach (var note in selectedNotes)
                {
                    var newNote = new NoteViewModel
                    {
                        Pitch = note.Pitch,
                        StartPosition = note.StartPosition + new MusicalFraction(1, 4), // 向右偏移一个四分音符
                        Duration = note.Duration,
                        Velocity = note.Velocity,
                        IsSelected = true // 新复制的音符设为选中状态
                    };
                    duplicatedNotes.Add(newNote);
                    Notes.Add(newNote);
                }

                // 创建撤销操作
                var duplicateOperation = new DuplicateNotesOperation(this, duplicatedNotes);
                _undoRedoService.ExecuteAndRecord(duplicateOperation);

                _logger.Debug("PianoRollViewModel", $"复制了 {duplicatedNotes.Count} 个音符");
            }
        }

        /// <summary>
        /// 量化选中的音符
        /// </summary>
        [RelayCommand]
        public void QuantizeSelectedNotes()
        {
            var selectedNotes = Notes.Where(n => n.IsSelected).ToList();
            if (selectedNotes.Any())
            {
                var originalPositions = selectedNotes.ToDictionary(n => n, n => n.StartPosition);

                foreach (var note in selectedNotes)
                {
                    // 根据当前网格量化设置量化音符起始位置
                    var quantizedPosition = MusicalFraction.QuantizeToGrid(note.StartPosition, Toolbar.GridQuantization);
                    note.StartPosition = quantizedPosition;
                }

                // 创建撤销操作
                var quantizeOperation = new QuantizeNotesOperation(selectedNotes, originalPositions);
                _undoRedoService.ExecuteAndRecord(quantizeOperation);

                _logger.Debug("PianoRollViewModel", $"量化了 {selectedNotes.Count} 个音符");
            }
        }

        /// <summary>
        /// 是否可以复制
        /// </summary>
        private bool CanCopyCommand => Notes.Any(n => n.IsSelected);

        /// <summary>
        /// 是否可以粘贴
        /// </summary>
        private bool CanPasteCommand => _clipboardNotes != null && _clipboardNotes.Any();

        /// <summary>
        /// 放大视图
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomInCommand))]
        public void ZoomIn()
        {
            ZoomManager.ZoomIn();
        }

        /// <summary>
        /// 缩小视图
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanZoomOutCommand))]
        public void ZoomOut()
        {
            ZoomManager.ZoomOut();
        }

        /// <summary>
        /// 适应窗口大小
        /// </summary>
        [RelayCommand]
        public void FitToWindow()
        {
            // 重置到默认缩放
            ZoomManager.SetZoomSliderValue(50.0);
            ZoomManager.SetVerticalZoomSliderValue(50.0);
        }

        /// <summary>
        /// 重置缩放
        /// </summary>
        [RelayCommand]
        public void ResetZoom()
        {
            ZoomManager.SetZoomSliderValue(50.0);
            ZoomManager.SetVerticalZoomSliderValue(50.0);
        }

        /// <summary>
        /// 是否可以放大
        /// </summary>
        private bool CanZoomInCommand => !ZoomManager.IsAtMaximumZoom;

        /// <summary>
        /// 是否可以缩小
        /// </summary>
        private bool CanZoomOutCommand => !ZoomManager.IsAtMinimumZoom;
        #endregion

        #region 播放控制命令
        /// <summary>
        /// 开始播放
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPlayCommand))]
        public void Play()
        {
            // TODO: 实现播放逻辑
            _logger.Info("PianoRollViewModel", "播放功能待实现");
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPauseCommand))]
        public void Pause()
        {
            // TODO: 实现暂停逻辑
            _logger.Info("PianoRollViewModel", "暂停功能待实现");
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStopCommand))]
        public void Stop()
        {
            // TODO: 实现停止逻辑
            _logger.Info("PianoRollViewModel", "停止功能待实现");
        }

        /// <summary>
        /// 是否可以播放
        /// </summary>
        private bool CanPlayCommand => true; // TODO: 根据播放状态判断

        /// <summary>
        /// 是否可以暂停
        /// </summary>
        private bool CanPauseCommand => false; // TODO: 根据播放状态判断

        /// <summary>
        /// 是否可以停止
        /// </summary>
        private bool CanStopCommand => false; // TODO: 根据播放状态判断
        #endregion

        #region 频谱背景相关命令
        /// <summary>
        /// 加载频谱数据作为背景
        /// </summary>
        [RelayCommand]
        public void LoadSpectrogramBackground()
        {
            if (HasSpectrogramData)
            {
                _editorStateService.LoadSpectrogramBackground(SpectrogramData!, SpectrogramSampleRate, SpectrogramDuration, SpectrogramMaxFrequency);
                IsSpectrogramVisible = true;
                
                // 确保频谱图像已生成
                GenerateSpectrogramImage();
                
                _logger.Info("PianoRollViewModel",
                    $"频谱背景显示: {SpectrogramData!.GetLength(0)} 帧, {SpectrogramData.GetLength(1)} 频率bin");
            }
            else
            {
                _logger.Warn("PianoRollViewModel", "没有可用的频谱数据");
            }
        }

        /// <summary>
        /// 清除频谱背景
        /// </summary>
        [RelayCommand]
        public void ClearSpectrogramBackground()
        {
            try
            {
                _logger.Info("PianoRollViewModel", "清除频谱背景");
                _editorStateService.ClearSpectrogramBackground();
                SpectrogramData = null;
                SpectrogramSampleRate = 0;
                SpectrogramDuration = 0;
                SpectrogramMaxFrequency = 0;
                IsSpectrogramVisible = false;
            }
            catch (Exception ex)
            {
                _logger.Error("PianoRollViewModel", $"清除频谱背景失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换频谱背景显示状态
        /// </summary>
        [RelayCommand]
        public void ToggleSpectrogramVisibility()
        {
            IsSpectrogramVisible = !IsSpectrogramVisible;
            _editorStateService.SetSpectrogramVisibility(IsSpectrogramVisible);
            _logger.Debug("PianoRollViewModel", $"频谱背景显示状态: {IsSpectrogramVisible}");
        }

        /// <summary>
        /// 设置频谱背景透明度
        /// </summary>
        /// <param name="opacity">透明度值（0.0-1.0）</param>
        [RelayCommand]
        public void SetSpectrogramOpacity(double opacity)
        {
            SpectrogramOpacity = Math.Max(0.0, Math.Min(1.0, opacity));
            _logger.Debug("PianoRollViewModel", $"频谱背景透明度设置为: {SpectrogramOpacity:F2}");
        }

        /// <summary>
        /// 设置频谱颜色映射
        /// </summary>
        /// <param name="colorMap">颜色映射类型</param>
        [RelayCommand]
        public void SetSpectrogramColorMap(SpectrogramColorMap colorMap)
        {
            SpectrogramColorMap = colorMap;
            _logger.Debug("PianoRollViewModel", $"频谱颜色映射设置为: {colorMap}");
        }

        /// <summary>
        /// 导入频谱数据到钢琴卷帘背景
        /// </summary>
        public void ImportSpectrogramData(double[,] spectrogramData, int sampleRate, double duration, double maxFrequency)
        {
            try
            {
                // 检查数据有效性
                if (spectrogramData == null || spectrogramData.Length == 0)
                {
                    _logger.Warn("PianoRollViewModel", "频谱数据为空");
                    return;
                }
                
                int timeFrames = spectrogramData.GetLength(0);
                int frequencyBins = spectrogramData.GetLength(1);
                
                // 检查数据是否全为零
                bool allZero = true;
                double maxValue = 0;
                for (int t = 0; t < Math.Min(10, timeFrames); t++)
                {
                    for (int f = 0; f < Math.Min(10, frequencyBins); f++)
                    {
                        double value = Math.Abs(spectrogramData[t, f]);
                        maxValue = Math.Max(maxValue, value);
                        if (value > 1e-10)
                        {
                            allZero = false;
                            break;
                        }
                    }
                    if (!allZero) break;
                }
                
                _logger.Info("PianoRollViewModel", $"导入频谱数据: {timeFrames}帧 × {frequencyBins}频率bin, 最大值: {maxValue:F4}, 全零: {allZero}");
                
                SpectrogramData = spectrogramData;
                SpectrogramSampleRate = sampleRate;
                SpectrogramDuration = duration;
                SpectrogramMaxFrequency = maxFrequency;
                
                // 将钢琴卷帘长度调整为频谱长度（以秒为单位）
                // 使用默认的每四分音符500000微秒（即120 BPM）进行转换
                SetMidiFileDurationFromSeconds(duration, 500000);
                _logger.Debug("PianoRollViewModel", $"频谱导入后设置钢琴卷帘长度为: {duration:F2}秒");
                
                // 导入数据后立即生成频谱图像
                GenerateSpectrogramImage();
                _logger.Debug("PianoRollViewModel", "频谱数据导入后已调用GenerateSpectrogramImage");
                
                // 确保频谱图可见
                IsSpectrogramVisible = true;
                _logger.Info("PianoRollViewModel", "频谱数据导入后已启用频谱显示");
            }
            catch (Exception ex)
            {
                _logger.Error("PianoRollViewModel", $"导入频谱数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成频谱图像
        /// </summary>
                private void GenerateSpectrogramImage()
                {
                    if (SpectrogramData == null)
                    {
                        _logger.Warn("PianoRollViewModel", "GenerateSpectrogramImage: SpectrogramData 为 null");
                        return;
                    }
                
                    try
                    {
                        int width = SpectrogramData.GetLength(0);
                        int height = SpectrogramData.GetLength(1);
                        
                        _logger.Debug("PianoRollViewModel", $"GenerateSpectrogramImage: 开始生成图像，尺寸: {width}x{height}");
                        
                        // 创建WriteableBitmap
                        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
                            new Avalonia.PixelSize(width, height),
                            new Avalonia.Vector(96, 96),
                            Avalonia.Platform.PixelFormat.Rgba8888,
                            Avalonia.Platform.AlphaFormat.Premul);
                        
                        // 找出频谱数据的最大值，用于归一化
                        double maxValue = 0;
                        int nonZeroCount = 0;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                double value = SpectrogramData[x, y];
                                maxValue = Math.Max(maxValue, value);
                                if (value > 1e-10)
                                    nonZeroCount++;
                            }
                        }
                        
                        _logger.Debug("PianoRollViewModel", $"GenerateSpectrogramImage: 数据统计 - 最大值: {maxValue}, 非零像素: {nonZeroCount}, 总像素: {width * height}");
                        
                        // 如果最大值为0，创建一个空图像
                        if (maxValue <= 0)
                        {
                            _logger.Warn("PianoRollViewModel", "GenerateSpectrogramImage: 最大值为0，无法生成有效图像");
                            SpectrogramImage = null;
                            _logger.Warn("PianoRollViewModel", "频谱数据全为零，无法生成图像");
                            return;
                        }
                        
                        using (var fb = bitmap.Lock())
                        {
                            unsafe
                            {
                                uint* ptr = (uint*)fb.Address;
                                int pixelCount = 0;
                                
                                for (int y = 0; y < height; y++)
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        // 归一化到0-1范围
                                        double normalizedValue = SpectrogramData[x, height - 1 - y] / maxValue;
                                        
                                        // 使用热力图颜色映射
                                        var color = GetHeatMapColor(normalizedValue);
                                        ptr[y * width + x] = color;
                                        
                                        // 统计非零颜色像素
                                        if (color != 0)
                                            pixelCount++;
                                    }
                                }
                                
                                    _logger.Debug("PianoRollViewModel", $"GenerateSpectrogramImage: 图像填充完成，非零颜色像素: {pixelCount}");
                            }
                        }
                        
                        // 验证位图是否成功创建
                        if (bitmap.PixelSize.Width == width && bitmap.PixelSize.Height == height)
                        {
                            SpectrogramImage = bitmap;
                            _logger.Info("PianoRollViewModel", $"频谱图像生成完成: {width}x{height}, 最大值: {maxValue:F4}");
                        }
                        else
                        {
                            _logger.Warn("PianoRollViewModel", $"GenerateSpectrogramImage: 位图创建失败，实际尺寸: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
                        }
                        
                        // 保存频谱图像到文件用于调试
                        SaveSpectrogramImageToFile(bitmap);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, "PianoRollViewModel", "GenerateSpectrogramImage 异常");
                        _logger.Error("PianoRollViewModel", $"生成频谱图像失败: {ex.Message}");
                        SpectrogramImage = null;
                    }
                }

                /// <summary>
                /// 保存频谱图像到文件用于调试
                /// </summary>
                /// <param name="bitmap">要保存的频谱图像</param>
                private void SaveSpectrogramImageToFile(Avalonia.Media.Imaging.WriteableBitmap bitmap)
                {
                    try
                    {
                        // 创建AnalyzerOut目录
                        string analyzerOutDir = Path.Combine(Directory.GetCurrentDirectory(), "AnalyzerOut");
                        if (!Directory.Exists(analyzerOutDir))
                        {
                            Directory.CreateDirectory(analyzerOutDir);
                            _logger.Info("PianoRollViewModel", $"创建AnalyzerOut目录: {analyzerOutDir}");
                        }
                        
                        // 生成文件名（包含时间戳）
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string fileName = $"spectrogram_{timestamp}.png";
                        string filePath = Path.Combine(analyzerOutDir, fileName);
                        
                        // 保存图像
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            bitmap.Save(fileStream);
                        }
                        
                        _logger.Info("PianoRollViewModel", $"频谱图像已保存到: {filePath}");
                        
                        // 同时保存频谱数据为文本文件用于调试
                        SaveSpectrogramDataToFile();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("PianoRollViewModel", $"保存频谱图像失败: {ex.Message}");
                    }
                }

                /// <summary>
                /// 保存频谱数据为文本文件用于调试
                /// </summary>
                private void SaveSpectrogramDataToFile()
                {
                    if (SpectrogramData == null) return;
                    
                    try
                    {
                        // 创建AnalyzerOut目录
                        string analyzerOutDir = Path.Combine(Directory.GetCurrentDirectory(), "AnalyzerOut");
                        if (!Directory.Exists(analyzerOutDir))
                        {
                            Directory.CreateDirectory(analyzerOutDir);
                        }
                        
                        // 生成文件名（包含时间戳）
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string fileName = $"spectrogram_data_{timestamp}.txt";
                        string filePath = Path.Combine(analyzerOutDir, fileName);
                        
                        using (var writer = new StreamWriter(filePath))
                        {
                            writer.WriteLine($"频谱数据信息:");
                            writer.WriteLine($"尺寸: {SpectrogramData.GetLength(0)}x{SpectrogramData.GetLength(1)}");
                            writer.WriteLine($"采样率: {SpectrogramSampleRate} Hz");
                            writer.WriteLine($"时长: {SpectrogramDuration:F2} 秒");
                            writer.WriteLine($"最大频率: {SpectrogramMaxFrequency:F2} Hz");
                            writer.WriteLine();
                            writer.WriteLine("数据预览 (前10x10):");
                            
                            int rows = Math.Min(10, SpectrogramData.GetLength(0));
                            int cols = Math.Min(10, SpectrogramData.GetLength(1));
                            
                            for (int i = 0; i < rows; i++)
                            {
                                for (int j = 0; j < cols; j++)
                                {
                                    writer.Write($"{SpectrogramData[i, j]:F4}\t");
                                }
                                writer.WriteLine();
                            }
                            
                            // 统计信息
                            double min = double.MaxValue;
                            double max = double.MinValue;
                            double sum = 0;
                            int totalCount = 0;
                            int nonZeroCount = 0;
                            
                            for (int i = 0; i < SpectrogramData.GetLength(0); i++)
                            {
                                for (int j = 0; j < SpectrogramData.GetLength(1); j++)
                                {
                                    double value = SpectrogramData[i, j];
                                    min = Math.Min(min, value);
                                    max = Math.Max(max, value);
                                    sum += value;
                                    totalCount++;
                                    
                                    // 检查是否非零（考虑浮点数精度）
                                    if (Math.Abs(value) > 1e-10)
                                    {
                                        nonZeroCount++;
                                    }
                                }
                            }
                            
                            writer.WriteLine();
                            writer.WriteLine($"数据统计:");
                            writer.WriteLine($"最小值: {min:F4}");
                            writer.WriteLine($"最大值: {max:F4}");
                            writer.WriteLine($"平均值: {(sum / totalCount):F4}");
                            writer.WriteLine($"总元素数: {totalCount}");
                            writer.WriteLine($"非零值数量: {nonZeroCount}");
                        }
                        
                        _logger.Info("PianoRollViewModel", $"频谱数据已保存到: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("PianoRollViewModel", $"保存频谱数据失败: {ex.Message}");
                    }
                }

                /// <summary>
                /// 获取热力图颜色
                /// </summary>
                private uint GetHeatMapColor(double value)
                {
                    // 确保值在0-1范围内
                    value = Math.Max(0, Math.Min(1, value));
                    
                    // 热力图颜色映射：蓝色 -> 青色 -> 绿色 -> 黄色 -> 红色
                    byte r, g, b;
                    
                    if (value < 0.25)
                    {
                        // 蓝色到青色
                        r = 0;
                        g = (byte)(value * 4 * 255);
                        b = 255;
                    }
                    else if (value < 0.5)
                    {
                        // 青色到绿色
                        r = 0;
                        g = 255;
                        b = (byte)(255 - (value - 0.25) * 4 * 255);
                    }
                    else if (value < 0.75)
                    {
                        // 绿色到黄色
                        r = (byte)((value - 0.5) * 4 * 255);
                        g = 255;
                        b = 0;
                    }
                    else
                    {
                        // 黄色到红色
                        r = 255;
                        g = (byte)(255 - (value - 0.75) * 4 * 255);
                        b = 0;
                    }
                    
                    return (uint)((255 << 24) | (r << 16) | (g << 8) | b);
                }

                /// <summary>
                /// 导入频谱数据命令（无参版本，用于UI绑定）
                /// </summary>
                [RelayCommand]
                public void ImportSpectrogram()
                {
                    // 这个方法假设频谱数据已经通过其他方式设置到属性中
                    if (HasSpectrogramData)
                    {
                        LoadSpectrogramBackground();
                        _logger.Info("PianoRollViewModel", "通过命令导入频谱数据");
                    }
                    else
                    {
                        _logger.Warn("PianoRollViewModel", "没有可用的频谱数据用于导入");
                    }
                }
                #endregion
            }
        }