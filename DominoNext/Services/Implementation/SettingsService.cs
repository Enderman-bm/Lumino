using DominoNext.Models.Settings;
using DominoNext.Services.Interfaces;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 设置服务实现
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsFilePath;
        private ResourceDictionary? _currentThemeResources;

        public SettingsModel Settings { get; private set; }
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public SettingsService()
        {
            // 设置文件保存在用户数据目录
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

                // 如果主题或颜色相关属性变更，立即应用主题设置
                if (IsThemeProperty(e.PropertyName))
                {
                    ApplyThemeSettings();
                }

                // 自动保存设置
                _ = Task.Run(SaveSettingsAsync);
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
                if (!File.Exists(_settingsFilePath))
                {
                    // 首次运行，使用默认设置
                    await SaveSettingsAsync();
                    return;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loadedSettings = JsonSerializer.Deserialize<SettingsModel>(json);
                
                if (loadedSettings != null)
                {
                    // 复制属性值
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

                    // 基础主题颜色
                    Settings.BackgroundColor = loadedSettings.BackgroundColor;
                    Settings.NoteColor = loadedSettings.NoteColor;
                    Settings.GridLineColor = loadedSettings.GridLineColor;
                    Settings.KeyWhiteColor = loadedSettings.KeyWhiteColor;
                    Settings.KeyBlackColor = loadedSettings.KeyBlackColor;
                    Settings.SelectionColor = loadedSettings.SelectionColor;

                    // 扩展界面元素颜色
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

                // 应用设置
                ApplyLanguageSettings();
                ApplyThemeSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
                // 如果加载失败，使用默认设置
                Settings.ResetToDefaults();
            }
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
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            Settings.ResetToDefaults();
            await SaveSettingsAsync();
            
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
                System.Diagnostics.Debug.WriteLine($"应用语言设置失败: {ex.Message}");
            }
        }

        public void ApplyThemeSettings()
        {
            try
            {
                if (Application.Current != null)
                {
                    // 重置资源状态，准备重新加载
                    ResourcePreloadService.Instance.ResetResourceState();

                    // 在UI线程上执行主题更新
                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        Application.Current.RequestedThemeVariant = Settings.Theme;

                        // 加载对应的主题资源字典
                        LoadThemeResourceDictionary();

                        // 如果有自定义颜色，覆盖默认主题颜色
                        ApplyCustomColors();

                        // 等待资源系统稳定
                        await Task.Delay(50);

                        // 标记资源已加载
                        await ResourcePreloadService.Instance.PreloadResourcesAsync();

                        // 强制刷新所有UI元素
                        ForceRefreshAllControls();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载主题资源字典
        /// </summary>
        private void LoadThemeResourceDictionary()
        {
            try
            {
                // 移除当前主题资源
                RemoveCurrentThemeResources();

                // 根据主题选择对应的资源文件
                string themeFileName = Settings.Theme switch
                {
                    var theme when theme == ThemeVariant.Light => "LightTheme.axaml",
                    var theme when theme == ThemeVariant.Dark => "DarkTheme.axaml",
                    _ => "DefaultTheme.axaml"
                };

                // 加载主题资源字典
                var themeUri = new Uri($"avares://DominoNext/Themes/{themeFileName}");
                _currentThemeResources = (ResourceDictionary)AvaloniaXamlLoader.Load(themeUri);

                // 将主题资源添加到应用程序资源中
                if (_currentThemeResources != null && Application.Current != null)
                {
                    Application.Current.Resources.MergedDictionaries.Add(_currentThemeResources);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载主题资源字典失败: {ex.Message}");
                // 回退到手动设置颜色的方式
                ApplyColorsManually();
            }
        }

        /// <summary>
        /// 移除当前主题资源
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
        /// 应用自定义颜色（覆盖主题默认颜色）
        /// </summary>
        private void ApplyCustomColors()
        {
            if (Application.Current?.Resources == null) return;

            var resources = Application.Current.Resources;

            // 只有当颜色不是默认值时才覆盖
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

            // 音符状态颜色
            if (!IsDefaultColor(Settings.NoteSelectedColor, GetDefaultColorForTheme("NoteSelectedColor")))
                SetBrushResource(resources, "NoteSelectedBrush", Settings.NoteSelectedColor, "#FFFF9800");

            if (!IsDefaultColor(Settings.NoteDraggingColor, GetDefaultColorForTheme("NoteDraggingColor")))
                SetBrushResource(resources, "NoteDraggingBrush", Settings.NoteDraggingColor, "#FF2196F3");

            if (!IsDefaultColor(Settings.NotePreviewColor, GetDefaultColorForTheme("NotePreviewColor")))
                SetBrushResource(resources, "NotePreviewBrush", Settings.NotePreviewColor, "#804CAF50");

            // UI元素颜色
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

            // 更新边框笔刷
            UpdatePenBrushes(resources);
        }

        /// <summary>
        /// 获取当前主题的默认颜色
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
            else // Light或Default主题
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
        /// 检查颜色是否为默认值
        /// </summary>
        private bool IsDefaultColor(string currentColor, string defaultColor)
        {
            return string.IsNullOrEmpty(currentColor) || 
                   string.Equals(currentColor, defaultColor, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 更新边框笔刷
        /// </summary>
        private void UpdatePenBrushes(IResourceDictionary resources)
        {
            SetPenBrushResource(resources, "NotePenBrush", Settings.NoteColor, "#FF2E7D32");
            SetPenBrushResource(resources, "NoteSelectedPenBrush", Settings.NoteSelectedColor, "#FFF57C00");
            SetPenBrushResource(resources, "NoteDraggingPenBrush", Settings.NoteDraggingColor, "#FF1976D2");
            SetPenBrushResource(resources, "NotePreviewPenBrush", Settings.NotePreviewColor, "#FF2E7D32");
        }

        /// <summary>
        /// 回退方案：手动设置颜色（保持向后兼容性）
        /// </summary>
        private void ApplyColorsManually()
        {
            if (Application.Current?.Resources == null) return;

            var resources = Application.Current.Resources;

            // 基础颜色
            SetBrushResource(resources, "AppBackgroundBrush", Settings.BackgroundColor, "#FFFAFAFA");
            SetBrushResource(resources, "NoteBrush", Settings.NoteColor, "#FF4CAF50");
            SetBrushResource(resources, "GridLineBrush", Settings.GridLineColor, "#1F000000");
            SetBrushResource(resources, "KeyWhiteBrush", Settings.KeyWhiteColor, "#FFFFFFFF");
            SetBrushResource(resources, "KeyBlackBrush", Settings.KeyBlackColor, "#FF1F1F1F");
            SetBrushResource(resources, "SelectionBrush", Settings.SelectionColor, "#800099FF");

            // 音符状态颜色
            SetBrushResource(resources, "NoteSelectedBrush", Settings.NoteSelectedColor, "#FFFF9800");
            SetBrushResource(resources, "NoteDraggingBrush", Settings.NoteDraggingColor, "#FF2196F3");
            SetBrushResource(resources, "NotePreviewBrush", Settings.NotePreviewColor, "#804CAF50");

            // UI元素颜色
            SetBrushResource(resources, "VelocityIndicatorBrush", Settings.VelocityIndicatorColor, "#FFFFC107");
            SetBrushResource(resources, "MeasureHeaderBackgroundBrush", Settings.MeasureHeaderBackgroundColor, "#FFF5F5F5");
            SetBrushResource(resources, "MeasureLineBrush", Settings.MeasureLineColor, "#FF000080");
            SetBrushResource(resources, "MeasureTextBrush", Settings.MeasureTextColor, "#FF000000");
            SetBrushResource(resources, "SeparatorLineBrush", Settings.SeparatorLineColor, "#FFCCCCCC");
            SetBrushResource(resources, "KeyBorderBrush", Settings.KeyBorderColor, "#FF1F1F1F");
            SetBrushResource(resources, "KeyTextWhiteBrush", Settings.KeyTextWhiteColor, "#FF000000");
            SetBrushResource(resources, "KeyTextBlackBrush", Settings.KeyTextBlackColor, "#FFFFFFFF");

            // 为音符渲染器提供边框笔刷（基于主颜色生成更深的边框色）
            SetPenBrushResource(resources, "NotePenBrush", Settings.NoteColor, "#FF2E7D32");
            SetPenBrushResource(resources, "NoteSelectedPenBrush", Settings.NoteSelectedColor, "#FFF57C00");
            SetPenBrushResource(resources, "NoteDraggingPenBrush", Settings.NoteDraggingColor, "#FF1976D2");
            SetPenBrushResource(resources, "NotePreviewPenBrush", Settings.NotePreviewColor, "#FF2E7D32");

            // 新增：UI界面元素颜色资源
            // 工具栏相关
            SetBrushResource(resources, "ToolbarBackgroundBrush", Settings.MeasureHeaderBackgroundColor, "#FFF0F0F0");
            SetBrushResource(resources, "ToolbarBorderBrush", Settings.SeparatorLineColor, "#FFD0D0D0");
            SetBrushResource(resources, "ButtonBorderBrush", Settings.SeparatorLineColor, "#FFD0D0D0");
            SetBrushResource(resources, "ButtonHoverBrush", Settings.SelectionColor, "#FFE8F4FD");
            SetBrushResource(resources, "ButtonPressedBrush", Settings.SelectionColor, "#FFD0E8FF");
            SetBrushResource(resources, "ButtonActiveBrush", Settings.NoteSelectedColor, "#FF3d80df");
            
            // 滑块和滚动条颜色
            SetBrushResource(resources, "SliderTrackBrush", Settings.SeparatorLineColor, "#FFE0E0E0");
            SetBrushResource(resources, "SliderThumbBrush", Settings.NoteSelectedColor, "#FF3d80df");
            SetBrushResource(resources, "SliderThumbHoverBrush", Settings.NoteDraggingColor, "#FF5a9cff");
            SetBrushResource(resources, "SliderThumbPressedBrush", Settings.NoteColor, "#FF2d6bbf");
            
            // 主区域背景
            SetBrushResource(resources, "PianoKeysBackgroundBrush", Settings.KeyBlackColor, "#FF2F2F2F");
            SetBrushResource(resources, "MainCanvasBackgroundBrush", Settings.BackgroundColor, "#FFFFFFFF");
            SetBrushResource(resources, "PopupBackgroundBrush", Settings.BackgroundColor, "#FFFFFFFF");
            
            // 文字颜色
            SetBrushResource(resources, "StatusTextBrush", Settings.MeasureTextColor, "#FF666666");
            SetBrushResource(resources, "BorderLineBlackBrush", Settings.KeyBlackColor, "#FF000000");
        }

        /// <summary>
        /// 强制刷新所有UI控件
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
                System.Diagnostics.Debug.WriteLine($"强制刷新UI控件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 递归刷新控件及其子控件
        /// </summary>
        private void RefreshControlAndChildren(Control control)
        {
            try
            {
                // 强制重新渲染
                control.InvalidateVisual();
                
                // 强制重新测量和排列
                control.InvalidateMeasure();
                control.InvalidateArrange();

                // 递归处理子控件
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
                    // 对于 ItemsControl，强制重新生成项目
                    itemsControl.InvalidateVisual();
                    
                    // 如果有子项，也递归刷新
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
                System.Diagnostics.Debug.WriteLine($"刷新控件失败: {ex.Message}");
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
                // 忽略解析错误，使用回退颜色
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
                
                // 生成更深的边框颜色
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
                // 忽略解析错误，使用回退颜色
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