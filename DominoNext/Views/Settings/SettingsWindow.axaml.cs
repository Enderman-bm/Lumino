using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Lumino.ViewModels.Settings;

namespace Lumino.Views.Settings
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        // ���Ӵ��ļ��������õİ�ť����¼�
        private async void LoadSettingsFromFile_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                try
                {
                    // �����ļ�ѡ��Ի���
                    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "ѡ�������ļ�",
                        FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                        AllowMultiple = false
                    });

                    if (files.Count > 0)
                    {
                        var file = files[0];
                        var filePath = file.TryGetLocalPath();

                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            // ʹ��SettingsModel���Զ���·�����ط���
                            viewModel.Settings.LoadFromFile(filePath);

                            // ���¼�⵱ǰ����
                            viewModel.UpdateCurrentSelections();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"���ļ���������ʧ��: {ex.Message}");
                }
            }
        }

        // ���ӱ������õ��ļ��İ�ť����¼�
        private async void SaveSettingsToFile_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsWindowViewModel viewModel)
            {
                try
                {
                    // �����ļ�����Ի���
                    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "���������ļ�",
                        FileTypeChoices = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                        DefaultExtension = "json",
                        SuggestedFileName = "settings.json"
                    });

                    if (file != null)
                    {
                        var filePath = file.TryGetLocalPath();

                        if (!string.IsNullOrEmpty(filePath))
                        {
                            // ʹ��SettingsModel���Զ���·�����淽��
                            viewModel.Settings.SaveToFile(filePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"�������õ��ļ�ʧ��: {ex.Message}");
                }
            }
        }
    }
}