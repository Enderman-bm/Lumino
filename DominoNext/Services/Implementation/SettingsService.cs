using DominoNext.Models.Settings;
using DominoNext.Services.Interfaces;
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

namespace DominoNext.Services.Implementation
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
            var appFolder = Path.Combine(appDataPath, "DominoNext");
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
                        Application.Current.RequestedThemeVariant = Settings.Theme;

                        // ���ض�Ӧ��������Դ�ֵ�
                        LoadThemeResourceDictionary();

                        // ������Զ�����ɫ������Ĭ��������ɫ
                        ApplyCustomColors();

                        // �ȴ���Դϵͳ�ȶ�
                        await Task.Delay(50);

                        // �����Դ�Ѽ���
                        await ResourcePreloadService.Instance.PreloadResourcesAsync();

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

                // ��������ѡ���Ӧ����Դ�ļ�
                string themeFileName = Settings.Theme switch
                {
                    var theme when theme == ThemeVariant.Light => "LightTheme.axaml",
                    var theme when theme == ThemeVariant.Dark => "DarkTheme.axaml",
                    _ => "DefaultTheme.axaml"
                };

                // ����������Դ�ֵ�
                var themeUri = new Uri($"avares://DominoNext/Themes/{themeFileName}");
                _currentThemeResources = (ResourceDictionary)AvaloniaXamlLoader.Load(themeUri);

                // ��������Դ���ӵ�Ӧ�ó�����Դ��
                if (_currentThemeResources != null && Application.Current != null)
                {
                    Application.Current.Resources.MergedDictionaries.Add(_currentThemeResources);
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
            if (_currentThemeResources != null && Application.Current != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(_currentThemeResources);
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
            if (!IsDefaultColor(Settings.BackgroundColor, GetDefaultColorForTheme("BackgroundColor")))
                SetBrushResource(resources, "AppBackgroundBrush", Settings.BackgroundColor, "#FFFAFAFA");

            if (!IsDefaultColor(Settings.NoteColor, GetDefaultColorForTheme("NoteColor")))
                SetBrushResource(resources, "NoteBrush", Settings.NoteColor, "#FF4CAF50");

            if (!IsDefaultColor(Settings.GridLineColor, GetDefaultColorForTheme("GridLineColor")))
                SetBrushResource(resources, "GridLineBrush", Settings.GridLineColor, "#1F000000");

            if (!IsDefaultColor(Settings.KeyWhiteColor, GetDefaultColorForTheme("KeyWhiteColor")))
                SetBrushResource(resources, "KeyWhiteBrush", Settings.KeyWhiteColor, "#FFFFFFFF");

            if (!IsDefaultColor(Settings.KeyBlackColor, GetDefaultColorForTheme("KeyBlackColor")))
                SetBrushResource(resources, "KeyBlackBrush", Settings.KeyBlackColor, "#FF1F1F1F");

            if (!IsDefaultColor(Settings.SelectionColor, GetDefaultColorForTheme("SelectionColor")))
                SetBrushResource(resources, "SelectionBrush", Settings.SelectionColor, "#800099FF");

            // ����״̬��ɫ
            if (!IsDefaultColor(Settings.NoteSelectedColor, GetDefaultColorForTheme("NoteSelectedColor")))
                SetBrushResource(resources, "NoteSelectedBrush", Settings.NoteSelectedColor, "#FFFF9800");

            if (!IsDefaultColor(Settings.NoteDraggingColor, GetDefaultColorForTheme("NoteDraggingColor")))
                SetBrushResource(resources, "NoteDraggingBrush", Settings.NoteDraggingColor, "#FF2196F3");

            if (!IsDefaultColor(Settings.NotePreviewColor, GetDefaultColorForTheme("NotePreviewColor")))
                SetBrushResource(resources, "NotePreviewBrush", Settings.NotePreviewColor, "#804CAF50");

            // UIԪ����ɫ
            if (!IsDefaultColor(Settings.VelocityIndicatorColor, GetDefaultColorForTheme("VelocityIndicatorColor")))
                SetBrushResource(resources, "VelocityIndicatorBrush", Settings.VelocityIndicatorColor, "#FFFFC107");

            if (!IsDefaultColor(Settings.MeasureHeaderBackgroundColor, GetDefaultColorForTheme("MeasureHeaderBackgroundColor")))
                SetBrushResource(resources, "MeasureHeaderBackgroundBrush", Settings.MeasureHeaderBackgroundColor, "#FFF5F5F5");

            if (!IsDefaultColor(Settings.MeasureLineColor, GetDefaultColorForTheme("MeasureLineColor")))
                SetBrushResource(resources, "MeasureLineBrush", Settings.MeasureLineColor, "#FF000080");

            if (!IsDefaultColor(Settings.MeasureTextColor, GetDefaultColorForTheme("MeasureTextColor")))
                SetBrushResource(resources, "MeasureTextBrush", Settings.MeasureTextColor, "#FF000000");

            if (!IsDefaultColor(Settings.SeparatorLineColor, GetDefaultColorForTheme("SeparatorLineColor")))
                SetBrushResource(resources, "SeparatorLineColor", Settings.SeparatorLineColor, "#FFCCCCCC");

            if (!IsDefaultColor(Settings.KeyBorderColor, GetDefaultColorForTheme("KeyBorderColor")))
                SetBrushResource(resources, "KeyBorderBrush", Settings.KeyBorderColor, "#FF1F1F1F");

            if (!IsDefaultColor(Settings.KeyTextWhiteColor, GetDefaultColorForTheme("KeyTextWhiteColor")))
                SetBrushResource(resources, "KeyTextWhiteBrush", Settings.KeyTextWhiteColor, "#FF000000");

            if (!IsDefaultColor(Settings.KeyTextBlackColor, GetDefaultColorForTheme("KeyTextBlackColor")))
                SetBrushResource(resources, "KeyTextBlackBrush", Settings.KeyTextBlackColor, "#FFFFFFFF");

            // ���±߿��ˢ
            UpdatePenBrushes(resources);
        }

        /// <summary>
        /// ��ȡ��ǰ�����Ĭ����ɫ
        /// </summary>
        private string GetDefaultColorForTheme(string propertyName)
        {
            if (Settings.Theme == ThemeVariant.Dark)
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
                    _ => "#FFFFFFFF"
                };
            }
            else // Light��Default����
            {
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
                    _ => "#FF000000"
                };
            }
        }

        /// <summary>
        /// �����ɫ�Ƿ�ΪĬ��ֵ
        /// </summary>
        private bool IsDefaultColor(string currentColor, string defaultColor)
        {
            return string.IsNullOrEmpty(currentColor) || 
                   string.Equals(currentColor, defaultColor, StringComparison.OrdinalIgnoreCase);
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
            SetBrushResource(resources, "PianoKeysBackgroundBrush", Settings.KeyBlackColor, "#FF2F2F2F");
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
    }
}