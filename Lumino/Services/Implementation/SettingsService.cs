using Lumino.Models.Settings;
using Lumino.Services.Interfaces;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using EnderDebugger;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// ���÷���ʵ��
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsFilePath;
        private ResourceDictionary? _currentThemeResources;
        private readonly EnderLogger _logger;

        public SettingsModel Settings { get; private set; }
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        // 添加同步锁和防抖定时器
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _debounceCts;
        private const int DebounceDelayMs = 500; // 防抖延迟时间

        public SettingsService()
        {
            _logger = new EnderLogger("SettingsService");
            
            // �����ļ��������û�����Ŀ¼
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "Lumino");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, SettingsFileName);

            Settings = new SettingsModel();
            Settings.PropertyChanged += OnSettingsPropertyChanged;
        }

        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null)
            {
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
                {
                    PropertyName = e.PropertyName
                });

                // ����������ɫ������Ա��������Ӧ����������
                if (IsThemeProperty(e.PropertyName))
                {
                    ApplyThemeSettings();
                }

                // 使用防抖机制延迟保存，避免短时间内多次保存
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                
                _ = Task.Delay(DebounceDelayMs, _debounceCts.Token).ContinueWith(async task =>
                {
                    if (!task.IsCanceled)
                    {
                        await SaveSettingsWithRetryAsync();
                    }
                }, _debounceCts.Token);
            }
        }

        private bool IsThemeProperty(string propertyName)
        {
            return propertyName == nameof(Settings.Theme) || 
                   propertyName.EndsWith("Color");
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                // 获取锁，确保在加载设置时没有其他线程正在写入
                await _saveLock.WaitAsync();
                
                if (!File.Exists(_settingsFilePath))
                {
                    // �״����У�ʹ��Ĭ������
                    await SaveSettingsAsync();
                    return;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loadedSettings = JsonSerializer.Deserialize<SettingsModel>(json);
                
                if (loadedSettings != null)
                {
                    // ��������ֵ
                    Settings.Language = loadedSettings.Language;
                    Settings.Theme = loadedSettings.Theme;
                    Settings.AutoSave = loadedSettings.AutoSave;
                    Settings.AutoSaveInterval = loadedSettings.AutoSaveInterval;
                    Settings.ShowGridLines = loadedSettings.ShowGridLines;
                    Settings.SnapToGrid = loadedSettings.SnapToGrid;
                    Settings.DefaultZoom = loadedSettings.DefaultZoom;
                    Settings.UseNativeMenuBar = loadedSettings.UseNativeMenuBar;
                    Settings.MaxUndoSteps = loadedSettings.MaxUndoSteps;
                    Settings.ConfirmBeforeDelete = loadedSettings.ConfirmBeforeDelete;
                    Settings.ShowVelocityBars = loadedSettings.ShowVelocityBars;
                    Settings.PianoKeyWidth = loadedSettings.PianoKeyWidth;
                    Settings.EnableKeyboardShortcuts = loadedSettings.EnableKeyboardShortcuts;
                    Settings.CustomShortcutsJson = loadedSettings.CustomShortcutsJson;

                    // ����������ɫ
                    Settings.BackgroundColor = loadedSettings.BackgroundColor;
                    Settings.NoteColor = loadedSettings.NoteColor;
                    Settings.GridLineColor = loadedSettings.GridLineColor;
                    Settings.KeyWhiteColor = loadedSettings.KeyWhiteColor;
                    Settings.KeyBlackColor = loadedSettings.KeyBlackColor;
                    Settings.SelectionColor = loadedSettings.SelectionColor;

                    // ��չ����Ԫ����ɫ
                    Settings.NoteSelectedColor = loadedSettings.NoteSelectedColor;
                    Settings.NoteDraggingColor = loadedSettings.NoteDraggingColor;
                    Settings.NotePreviewColor = loadedSettings.NotePreviewColor;
                    Settings.VelocityIndicatorColor = loadedSettings.VelocityIndicatorColor;
                    Settings.MeasureHeaderBackgroundColor = loadedSettings.MeasureHeaderBackgroundColor;
                    Settings.MeasureLineColor = loadedSettings.MeasureLineColor;
                    Settings.MeasureTextColor = loadedSettings.MeasureTextColor;
                    Settings.SeparatorLineColor = loadedSettings.SeparatorLineColor;
                    Settings.KeyBorderColor = loadedSettings.KeyBorderColor;
                    Settings.KeyTextWhiteColor = loadedSettings.KeyTextWhiteColor;
                    Settings.KeyTextBlackColor = loadedSettings.KeyTextBlackColor;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsService", $"加载设置失败: {ex.Message}");
                // 如果加载失败，使用默认设置
                Settings.ResetToDefaults();
            }
            finally
            {
                _saveLock.Release();
            }

            // Ӧ������
            ApplyLanguageSettings();
            ApplyThemeSettings();
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(Settings, options);
                await File.WriteAllTextAsync(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsService", $"保存设置失败: {ex.Message}");
                throw; // 重新抛出异常，让调用者知道保存失败
            }
        }

        /// <summary>
        /// 添加重试逻辑的保存方法，解决文件被占用问题
        /// </summary>
        private async Task SaveSettingsWithRetryAsync()
        {
            const int maxRetries = 3;
            const int retryDelayMs = 100;
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    // 获取锁，确保只有一个线程可以执行保存操作
                    await _saveLock.WaitAsync();
                    
                    try
                    {
                        await SaveSettingsAsync();
                        return; // 保存成功，退出方法
                    }
                    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                    {
                        // 如果是文件被占用错误，记录日志并继续循环
                        _logger.Debug("SettingsService", $"保存设置时文件被占用，正在重试 ({retry + 1}/{maxRetries})");
                        
                        if (retry < maxRetries - 1)
                        {
                            // 等待一段时间后重试
                            await Task.Delay(retryDelayMs * (retry + 1));
                        }
                        else
                        {
                            // 达到最大重试次数，记录错误
                            _logger.Error("SettingsService", $"保存设置失败: 达到最大重试次数，文件被占用");
                        }
                    }
                    finally
                    {
                        _saveLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("SettingsService", $"保存设置时发生异常: {ex.Message}");
                    break;
                }
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            try
            {
                await _saveLock.WaitAsync();
                
                Settings.ResetToDefaults();
                await SaveSettingsAsync();
            }
            finally
            {
                _saveLock.Release();
            }
            
            ApplyLanguageSettings();
            ApplyThemeSettings();
        }

        public void ApplyLanguageSettings()
        {
            try
            {
                var culture = new CultureInfo(Settings.Language);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsService", $"应用语言设置失败: {ex.Message}");
            }
        }

        public void ApplyThemeSettings()
        {
            try
            {
                if (Application.Current != null)
                {
                    // ������Դ״̬��׼�����¼���
                    ResourcePreloadService.Instance.ResetResourceState();

                    // ��UI�߳���ִ���������
                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        // 仅在用户显式选择 Light 或 Dark 时设置 RequestedThemeVariant，
                        // 当选择 Default（跟随系统）时避免显式设置，让 Avalonia 的 FluentTheme
                        // 根据系统偏好决定 ActualThemeVariant。
                        if (Settings.Theme == ThemeVariant.Light || Settings.Theme == ThemeVariant.Dark)
                        {
                            Application.Current.RequestedThemeVariant = Settings.Theme;
                        }

                        // 载入主题资源字典（如果是 Default，会根据实际系统主题选择对应文件）
                        LoadThemeResourceDictionary();

                        // ������Զ�����ɫ������Ĭ��������ɫ
                        ApplyCustomColors();

                        // �ȴ���Դϵͳ�ȶ�
                        await Task.Delay(50);

                        // �����Դ�Ѽ���
                        await ResourcePreloadService.Instance.PreloadResourcesAsync();

                        // 清除各 Canvas/渲染器的内部缓存，确保使用新主题画刷
                        ClearCanvasRendererCachesAcrossWindows();

                        // ǿ��ˢ������UIԪ��
                        ForceRefreshAllControls();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsService", $"应用主题设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ����������Դ�ֵ�
        /// </summary>
        private void LoadThemeResourceDictionary()
        {
            try
            {
                // �Ƴ���ǰ������Դ
                RemoveCurrentThemeResources();

                // 清理渲染画刷缓存，确保渲染器在下一次取资源时能获取到新主题的画刷
                try
                {
                    RenderingUtils.ClearBrushCache();
                }
                catch { }

                // ��������ѡ���Ӧ����Դ�ļ�
                string themeFileName = Settings.Theme switch
                {
                    var theme when theme == ThemeVariant.Light => "LightTheme.axaml",
                    var theme when theme == ThemeVariant.Dark => "DarkTheme.axaml",
                    _ => GetSystemThemeFileName() // 自动检测系统主题
                };

                // ����������Դ�ֵ�
                var themeUri = new Uri($"avares://Lumino/Themes/{themeFileName}");
                _currentThemeResources = (ResourceDictionary)AvaloniaXamlLoader.Load(themeUri);

                // ��������Դ���ӵ�Ӧ�ó�����Դ��
                if (_currentThemeResources != null && Application.Current != null)
                {
                    // 标记该字典为主题字典，便于后续移除时识别
                    try
                    {
                        if (!_currentThemeResources.ContainsKey("__LuminoThemeMarker"))
                        {
                            _currentThemeResources["__LuminoThemeMarker"] = themeFileName;
                        }
                    }
                    catch { }
                    Application.Current.Resources.MergedDictionaries.Add(_currentThemeResources);

                    // 诊断信息：记录当前合并字典和关键画刷值，便于调试主题切换未生效的问题
                    try
                    {
                        var markers = new System.Text.StringBuilder();
                        foreach (var md in Application.Current.Resources.MergedDictionaries)
                        {
                            try
                            {
                                if (md is ResourceDictionary rd && rd.ContainsKey("__LuminoThemeMarker"))
                                {
                                    markers.AppendLine(rd["__LuminoThemeMarker"]?.ToString());
                                }
                            }
                            catch { }
                        }

                        _logger.Debug("SettingsService", $"Merged theme markers:\n{markers}");

                        // 关键画刷值
                        if (Application.Current.Resources.TryGetResource("MainCanvasBackgroundBrush", null, out var mainObj) && mainObj is Avalonia.Media.ISolidColorBrush mainBrush)
                        {
                            _logger.Debug("SettingsService", $"MainCanvasBackgroundBrush resolved: {mainBrush.Color}");
                        }
                        else
                        {
                            _logger.Debug("SettingsService", "MainCanvasBackgroundBrush not found or not solid brush");
                        }

                        if (Application.Current.Resources.TryGetResource("PianoKeysBackgroundBrush", null, out var pianoObj) && pianoObj is Avalonia.Media.ISolidColorBrush pianoBrush)
                        {
                            _logger.Debug("SettingsService", $"PianoKeysBackgroundBrush resolved: {pianoBrush.Color}");
                        }
                        else
                        {
                            _logger.Debug("SettingsService", "PianoKeysBackgroundBrush not found or not solid brush");
                        }
                    }
                    catch { }

                    // 为确保主题切换后关键画刷能立即生效（避免旧字典覆盖），
                    // 将常用的主题画刷直接写入 Application.Current.Resources，覆盖同名项。
                    try
                    {
                        string[] importantKeys = new[]
                        {
                            "AppBackgroundBrush",
                            "MainCanvasBackgroundBrush",
                            "PianoKeysBackgroundBrush",
                            "NoteBrush",
                            "GridLineBrush",
                            "KeyWhiteBrush",
                            "KeyBlackBrush",
                            "SelectionBrush",
                            "MeasureHeaderBackgroundBrush",
                            "SeparatorLineBrush",
                            "StatusTextBrush",
                        };

                        foreach (var key in importantKeys)
                        {
                            if (_currentThemeResources.ContainsKey(key))
                            {
                                var val = _currentThemeResources[key];

                                // 如果已经存在相同键并且都是 SolidColorBrush，直接更新现有画刷的颜色以便
                                // 所有引用该画刷的对象自动获得更新（避免替换对象导致已有引用仍指向旧画刷）。
                                try
                                {
                                    if (Application.Current.Resources.ContainsKey(key))
                                    {
                                        var existing = Application.Current.Resources[key];
                                        if (existing is Avalonia.Media.SolidColorBrush existingSolid && val is Avalonia.Media.SolidColorBrush newSolid)
                                        {
                                            existingSolid.Color = newSolid.Color;
                                            // 同步不透明度（SolidColorBrush 上有可写的 Opacity）
                                            existingSolid.Opacity = newSolid.Opacity;
                                        }
                                        else
                                        {
                                            Application.Current.Resources[key] = val;
                                        }
                                    }
                                    else
                                    {
                                        Application.Current.Resources.Add(key, val);
                                    }
                                }
                                catch
                                {
                                    // 直接替换以确保资源存在
                                    try
                                    {
                                        if (Application.Current.Resources.ContainsKey(key))
                                            Application.Current.Resources[key] = val;
                                        else
                                            Application.Current.Resources.Add(key, val);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsService", $"加载主题资源字典失败: {ex.Message}");
                // 回退到手动应用颜色的方式
                ApplyColorsManually();
            }
        }

        /// <summary>
        /// �Ƴ���ǰ������Դ
        /// </summary>
        private void RemoveCurrentThemeResources()
        {
                if (Application.Current?.Resources?.MergedDictionaries == null) return;

                try
                {
                    // 删除我们之前标记过的主题字典（通过 __LuminoThemeMarker 标记）
                    for (int i = Application.Current.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                    {
                        var dict = Application.Current.Resources.MergedDictionaries[i];
                        try
                        {
                            if (dict is ResourceDictionary rd2 && rd2.ContainsKey("__LuminoThemeMarker"))
                            {
                                Application.Current.Resources.MergedDictionaries.RemoveAt(i);
                            }
                        }
                        catch
                        {
                            // 忽略任何检查异常，继续清理其他字典
                        }
                    }
                }
                finally
                {
                    _currentThemeResources = null;
                }
        }

        /// <summary>
        /// Ӧ���Զ�����ɫ����������Ĭ����ɫ��
        /// </summary>
        private void ApplyCustomColors()
        {
            if (Application.Current?.Resources == null) return;

            var resources = Application.Current.Resources;

            // ֻ�е���ɫ����Ĭ��ֵʱ�Ÿ���
            if (!IsDefaultColor(Settings.BackgroundColor, "BackgroundColor"))
                SetBrushResource(resources, "AppBackgroundBrush", Settings.BackgroundColor, "#FFFAFAFA");

            if (!IsDefaultColor(Settings.NoteColor, "NoteColor"))
                SetBrushResource(resources, "NoteBrush", Settings.NoteColor, "#FF4CAF50");

            if (!IsDefaultColor(Settings.GridLineColor, "GridLineColor"))
                SetBrushResource(resources, "GridLineBrush", Settings.GridLineColor, "#1F000000");

            if (!IsDefaultColor(Settings.KeyWhiteColor, "KeyWhiteColor"))
                SetBrushResource(resources, "KeyWhiteBrush", Settings.KeyWhiteColor, "#FFFFFFFF");

            if (!IsDefaultColor(Settings.KeyBlackColor, "KeyBlackColor"))
                SetBrushResource(resources, "KeyBlackBrush", Settings.KeyBlackColor, "#FF1F1F1F");

            if (!IsDefaultColor(Settings.SelectionColor, "SelectionColor"))
                SetBrushResource(resources, "SelectionBrush", Settings.SelectionColor, "#800099FF");

            // ����״̬��ɫ
            if (!IsDefaultColor(Settings.NoteSelectedColor, "NoteSelectedColor"))
                SetBrushResource(resources, "NoteSelectedBrush", Settings.NoteSelectedColor, "#FFFF9800");

            if (!IsDefaultColor(Settings.NoteDraggingColor, "NoteDraggingColor"))
                SetBrushResource(resources, "NoteDraggingBrush", Settings.NoteDraggingColor, "#FF2196F3");

            if (!IsDefaultColor(Settings.NotePreviewColor, "NotePreviewColor"))
                SetBrushResource(resources, "NotePreviewBrush", Settings.NotePreviewColor, "#804CAF50");

            // UIԪ����ɫ
            if (!IsDefaultColor(Settings.VelocityIndicatorColor, "VelocityIndicatorColor"))
                SetBrushResource(resources, "VelocityIndicatorBrush", Settings.VelocityIndicatorColor, "#FFFFC107");

            if (!IsDefaultColor(Settings.MeasureHeaderBackgroundColor, "MeasureHeaderBackgroundColor"))
                SetBrushResource(resources, "MeasureHeaderBackgroundBrush", Settings.MeasureHeaderBackgroundColor, "#FFF5F5F5");

            if (!IsDefaultColor(Settings.MeasureLineColor, "MeasureLineColor"))
                SetBrushResource(resources, "MeasureLineBrush", Settings.MeasureLineColor, "#FF000080");

            if (!IsDefaultColor(Settings.MeasureTextColor, "MeasureTextColor"))
                SetBrushResource(resources, "MeasureTextBrush", Settings.MeasureTextColor, "#FF000000");

            if (!IsDefaultColor(Settings.SeparatorLineColor, "SeparatorLineColor"))
                SetBrushResource(resources, "SeparatorLineColor", Settings.SeparatorLineColor, "#FFCCCCCC");

            if (!IsDefaultColor(Settings.KeyBorderColor, "KeyBorderColor"))
                SetBrushResource(resources, "KeyBorderBrush", Settings.KeyBorderColor, "#FF1F1F1F");

            if (!IsDefaultColor(Settings.KeyTextWhiteColor, "KeyTextWhiteColor"))
                SetBrushResource(resources, "KeyTextWhiteBrush", Settings.KeyTextWhiteColor, "#FF000000");

            if (!IsDefaultColor(Settings.KeyTextBlackColor, "KeyTextBlackColor"))
                SetBrushResource(resources, "KeyTextBlackBrush", Settings.KeyTextBlackColor, "#FFFFFFFF");

            // ���±߿��ˢ
            UpdatePenBrushes(resources);
        }

        /// <summary>
        /// <summary>
        /// 根据指定的 ThemeVariant 获取默认颜色
        /// </summary>
        private string GetDefaultColorForThemeVariant(string propertyName, ThemeVariant variant)
        {
            if (variant == ThemeVariant.Dark)
            {
                return propertyName switch
                {
                    "BackgroundColor" => "#FF1E1E1E",
                    "NoteColor" => "#FF66BB6A",
                    "GridLineColor" => "#40FFFFFF",
                    "KeyWhiteColor" => "#FF2D2D30",
                    "KeyBlackColor" => "#FF0F0F0F",
                    "SelectionColor" => "#8064B5F6",
                    "NoteSelectedColor" => "#FFFFB74D",
                    "NoteDraggingColor" => "#FF64B5F6",
                    "NotePreviewColor" => "#8066BB6A",
                    "VelocityIndicatorColor" => "#FFFFCA28",
                    "MeasureHeaderBackgroundColor" => "#FF252526",
                    "MeasureLineColor" => "#FF6495ED",
                    "MeasureTextColor" => "#FFE0E0E0",
                    "SeparatorLineColor" => "#FF3E3E42",
                    "KeyBorderColor" => "#FF404040",
                    "KeyTextWhiteColor" => "#FFCCCCCC",
                    "KeyTextBlackColor" => "#FF999999",
                    "PianoKeysBackgroundBrush" => "#FF252526",
                    _ => "#FFFFFFFF"
                };
            }

            // Light / Default
            return propertyName switch
            {
                "BackgroundColor" => "#FFFAFAFA",
                "NoteColor" => "#FF4CAF50",
                "GridLineColor" => "#1F000000",
                "KeyWhiteColor" => "#FFFFFFFF",
                "KeyBlackColor" => "#FF1F1F1F",
                "SelectionColor" => "#800099FF",
                "NoteSelectedColor" => "#FFFF9800",
                "NoteDraggingColor" => "#FF2196F3",
                "NotePreviewColor" => "#804CAF50",
                "VelocityIndicatorColor" => "#FFFFC107",
                "MeasureHeaderBackgroundColor" => "#FFF5F5F5",
                "MeasureLineColor" => "#FF000080",
                "MeasureTextColor" => "#FF000000",
                "SeparatorLineColor" => "#FFCCCCCC",
                "KeyBorderColor" => "#FF1F1F1F",
                "KeyTextWhiteColor" => "#FF000000",
                "KeyTextBlackColor" => "#FFFFFFFF",
                // 钢琴键背景（为兼容手动颜色覆盖）
                "PianoKeysBackgroundBrush" => "#FFE8E8E8",
                _ => "#FF000000"
            };
        }

        /// <summary>
        /// ��ȡ��ǰ�����Ĭ����ɫ
        /// </summary>
        private string GetDefaultColorForTheme(string propertyName)
        {
            return GetDefaultColorForThemeVariant(propertyName, Settings.Theme == ThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light);
        }

        /// <summary>
        /// �����ɫ�Ƿ�ΪĬ��ֵ
        /// </summary>
        private bool IsDefaultColor(string currentColor, string propertyName)
        {
            if (string.IsNullOrEmpty(currentColor)) return true;

            // If theme is Default (follow system), accept either light or dark defaults as 'default'
            if (Settings.Theme == ThemeVariant.Default)
            {
                var lightDefault = GetDefaultColorForThemeVariant(propertyName, ThemeVariant.Light);
                var darkDefault = GetDefaultColorForThemeVariant(propertyName, ThemeVariant.Dark);

                if (string.Equals(currentColor, lightDefault, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentColor, darkDefault, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Otherwise compare to the default for the current theme
            var defaultForCurrent = GetDefaultColorForTheme(propertyName);
            return string.Equals(currentColor, defaultForCurrent, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ���±߿��ˢ
        /// </summary>
        private void UpdatePenBrushes(IResourceDictionary resources)
        {
            SetPenBrushResource(resources, "NotePenBrush", Settings.NoteColor, "#FF2E7D32");
            SetPenBrushResource(resources, "NoteSelectedPenBrush", Settings.NoteSelectedColor, "#FFF57C00");
            SetPenBrushResource(resources, "NoteDraggingPenBrush", Settings.NoteDraggingColor, "#FF1976D2");
            SetPenBrushResource(resources, "NotePreviewPenBrush", Settings.NotePreviewColor, "#FF2E7D32");
        }

        /// <summary>
        /// ���˷������ֶ�������ɫ�������������ԣ�
        /// </summary>
        private void ApplyColorsManually()
        {
            if (Application.Current?.Resources == null) return;

            var resources = Application.Current.Resources;

            // ������ɫ
            SetBrushResource(resources, "AppBackgroundBrush", Settings.BackgroundColor, "#FFFAFAFA");
            SetBrushResource(resources, "NoteBrush", Settings.NoteColor, "#FF4CAF50");
            SetBrushResource(resources, "GridLineBrush", Settings.GridLineColor, "#1F000000");
            SetBrushResource(resources, "KeyWhiteBrush", Settings.KeyWhiteColor, "#FFFFFFFF");
            SetBrushResource(resources, "KeyBlackBrush", Settings.KeyBlackColor, "#FF1F1F1F");
            SetBrushResource(resources, "SelectionBrush", Settings.SelectionColor, "#800099FF");

            // ����״̬��ɫ
            SetBrushResource(resources, "NoteSelectedBrush", Settings.NoteSelectedColor, "#FFFF9800");
            SetBrushResource(resources, "NoteDraggingBrush", Settings.NoteDraggingColor, "#FF2196F3");
            SetBrushResource(resources, "NotePreviewBrush", Settings.NotePreviewColor, "#804CAF50");

            // UIԪ����ɫ
            SetBrushResource(resources, "VelocityIndicatorBrush", Settings.VelocityIndicatorColor, "#FFFFC107");
            SetBrushResource(resources, "MeasureHeaderBackgroundBrush", Settings.MeasureHeaderBackgroundColor, "#FFF5F5F5");
            SetBrushResource(resources, "MeasureLineBrush", Settings.MeasureLineColor, "#FF000080");
            SetBrushResource(resources, "MeasureTextBrush", Settings.MeasureTextColor, "#FF000000");
            SetBrushResource(resources, "SeparatorLineBrush", Settings.SeparatorLineColor, "#FFCCCCCC");
            SetBrushResource(resources, "KeyBorderBrush", Settings.KeyBorderColor, "#FF1F1F1F");
            SetBrushResource(resources, "KeyTextWhiteBrush", Settings.KeyTextWhiteColor, "#FF000000");
            SetBrushResource(resources, "KeyTextBlackBrush", Settings.KeyTextBlackColor, "#FFFFFFFF");

            // Ϊ������Ⱦ���ṩ�߿��ˢ����������ɫ���ɸ���ı߿�ɫ��
            SetPenBrushResource(resources, "NotePenBrush", Settings.NoteColor, "#FF2E7D32");
            SetPenBrushResource(resources, "NoteSelectedPenBrush", Settings.NoteSelectedColor, "#FFF57C00");
            SetPenBrushResource(resources, "NoteDraggingPenBrush", Settings.NoteDraggingColor, "#FF1976D2");
            SetPenBrushResource(resources, "NotePreviewPenBrush", Settings.NotePreviewColor, "#FF2E7D32");

            // ������UI����Ԫ����ɫ��Դ
            // ���������
            SetBrushResource(resources, "ToolbarBackgroundBrush", Settings.MeasureHeaderBackgroundColor, "#FFF0F0F0");
            SetBrushResource(resources, "ToolbarBorderBrush", Settings.SeparatorLineColor, "#FFD0D0D0");
            SetBrushResource(resources, "ButtonBorderBrush", Settings.SeparatorLineColor, "#FFD0D0D0");
            SetBrushResource(resources, "ButtonHoverBrush", Settings.SelectionColor, "#FFE8F4FD");
            SetBrushResource(resources, "ButtonPressedBrush", Settings.SelectionColor, "#FFD0E8FF");
            SetBrushResource(resources, "ButtonActiveBrush", Settings.NoteSelectedColor, "#FF3d80df");
            
            // ����͹�������ɫ
            SetBrushResource(resources, "SliderTrackBrush", Settings.SeparatorLineColor, "#FFE0E0E0");
            SetBrushResource(resources, "SliderThumbBrush", Settings.NoteSelectedColor, "#FF3d80df");
            SetBrushResource(resources, "SliderThumbHoverBrush", Settings.NoteDraggingColor, "#FF5a9cff");
            SetBrushResource(resources, "SliderThumbPressedBrush", Settings.NoteColor, "#FF2d6bbf");
            
            // �����򱳾�
            // 使用主题相关的默认钢琴键背景色作为 fallback（避免把 KeyBlackColor 用作背景色）
            SetBrushResource(resources, "PianoKeysBackgroundBrush", Settings.KeyBlackColor, GetDefaultColorForTheme("PianoKeysBackgroundBrush"));
            SetBrushResource(resources, "MainCanvasBackgroundBrush", Settings.BackgroundColor, "#FFFFFFFF");
            SetBrushResource(resources, "PopupBackgroundBrush", Settings.BackgroundColor, "#FFFFFFFF");
            
            // ������ɫ
            SetBrushResource(resources, "StatusTextBrush", Settings.MeasureTextColor, "#FF666666");
            SetBrushResource(resources, "BorderLineBlackBrush", Settings.KeyBlackColor, "#FF000000");
        }

        /// <summary>
        /// ǿ��ˢ������UI�ؼ�
        /// </summary>
        private void ForceRefreshAllControls()
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    foreach (var window in desktop.Windows)
                    {
                        RefreshControlAndChildren(window);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "ForceRefreshAllControls", "强制刷新所有UI控件失败");
            }
        }

        /// <summary>
        /// �ݹ�ˢ�¿ؼ������ӿؼ�
        /// </summary>
        private void RefreshControlAndChildren(Control control)
        {
            try
            {
                // ǿ��������Ⱦ
                control.InvalidateVisual();
                
                // ǿ�����²���������
                control.InvalidateMeasure();
                control.InvalidateArrange();

                // �ݹ鴦���ӿؼ�
                if (control is Panel panel)
                {
                    foreach (Control child in panel.Children)
                    {
                        RefreshControlAndChildren(child);
                    }
                }
                else if (control is ContentControl contentControl && contentControl.Content is Control childControl)
                {
                    RefreshControlAndChildren(childControl);
                }
                else if (control is ItemsControl itemsControl)
                {
                    // ���� ItemsControl��ǿ������������Ŀ
                    itemsControl.InvalidateVisual();
                    
                    // ��������Ҳ�ݹ�ˢ��
                    foreach (var item in itemsControl.GetRealizedContainers())
                    {
                        if (item is Control itemControl)
                        {
                            RefreshControlAndChildren(itemControl);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "RefreshControlAndChildren", "刷新控件及其子控件失败");
            }
        }

        private void SetBrushResource(IResourceDictionary resources, string key, string colorHex, string fallbackHex)
        {
            try
            {
                var hex = string.IsNullOrEmpty(colorHex) ? fallbackHex : colorHex;
                var color = Avalonia.Media.Color.Parse(hex);
                var brush = new SolidColorBrush(color);

                if (resources.ContainsKey(key))
                {
                    resources[key] = brush;
                }
                else
                {
                    resources.Add(key, brush);
                }
            }
            catch
            {
                // ���Խ�������ʹ�û�����ɫ
                try
                {
                    var color = Avalonia.Media.Color.Parse(fallbackHex);
                    var brush = new SolidColorBrush(color);
                    if (resources.ContainsKey(key))
                    {
                        resources[key] = brush;
                    }
                    else
                    {
                        resources.Add(key, brush);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 获取系统主题对应的主题文件名
        /// </summary>
        private string GetSystemThemeFileName()
        {
            try
            {
                // 检测系统主题偏好
                var actualThemeVariant = Application.Current?.ActualThemeVariant;
                if (actualThemeVariant == ThemeVariant.Dark)
                {
                    return "DarkTheme.axaml";
                }
            }
            catch (Exception ex)
            {
                _logger.Error("SettingsService", $"检测系统主题失败: {ex.Message}");
            }

            // 默认返回亮色主题
            return "LightTheme.axaml";
        }

        private void SetPenBrushResource(IResourceDictionary resources, string key, string baseColorHex, string fallbackHex)
        {
            try
            {
                var hex = string.IsNullOrEmpty(baseColorHex) ? fallbackHex : baseColorHex;
                var color = Avalonia.Media.Color.Parse(hex);
                
                // ���ɸ���ı߿���ɫ
                var darkerColor = Color.FromArgb(
                    color.A,
                    (byte)(color.R * 0.7),
                    (byte)(color.G * 0.7),
                    (byte)(color.B * 0.7)
                );
                
                var brush = new SolidColorBrush(darkerColor);

                if (resources.ContainsKey(key))
                {
                    resources[key] = brush;
                }
                else
                {
                    resources.Add(key, brush);
                }
            }
            catch
            {
                // ���Խ�������ʹ�û�����ɫ
                try
                {
                    var color = Avalonia.Media.Color.Parse(fallbackHex);
                    var brush = new SolidColorBrush(color);
                    if (resources.ContainsKey(key))
                    {
                        resources[key] = brush;
                    }
                    else
                    {
                        resources.Add(key, brush);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 遍历当前打开的窗口，调用 Canvas 控件上的 ClearRendererCaches 方法（如果存在）
        /// </summary>
        private void ClearCanvasRendererCachesAcrossWindows()
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    foreach (var window in desktop.Windows)
                    {
                        try
                        {
                            if (window is Avalonia.Controls.Control ctrl)
                            {
                                ClearRendererCachesRecursively(ctrl);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void ClearRendererCachesRecursively(Avalonia.Controls.Control control)
        {
            try
            {
                var mi = control.GetType().GetMethod("ClearRendererCaches");
                mi?.Invoke(control, null);
            }
            catch { }

            try
            {
                if (control is Avalonia.Controls.Panel panel)
                {
                    foreach (Avalonia.Controls.Control child in panel.Children)
                    {
                        ClearRendererCachesRecursively(child);
                    }
                }
                else if (control is Avalonia.Controls.ContentControl contentControl && contentControl.Content is Avalonia.Controls.Control childControl)
                {
                    ClearRendererCachesRecursively(childControl);
                }
                else if (control is Avalonia.Controls.ItemsControl itemsControl)
                {
                    foreach (var item in itemsControl.GetRealizedContainers())
                    {
                        if (item is Avalonia.Controls.Control itemControl)
                        {
                            ClearRendererCachesRecursively(itemControl);
                        }
                    }
                }
            }
            catch { }
        }
    }
}