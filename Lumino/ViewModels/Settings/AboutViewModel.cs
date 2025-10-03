using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnderDebugger;

namespace Lumino.ViewModels.Settings
{
    /// <summary>
    /// 贡献者信息
    /// </summary>
    public class Contributor
    {
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public int Contributions { get; set; }
    }

    /// <summary>
    /// 关于页面ViewModel
    /// </summary>
    public partial class AboutViewModel : ViewModelBase
    {
        private readonly EnderLogger _logger;
        private readonly HttpClient _httpClient;

        [ObservableProperty]
        private string _version = "v1.1-dev";

        [ObservableProperty]
        private bool _isLoadingContributors = false;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public ObservableCollection<Contributor> Contributors { get; } = new();

        public AboutViewModel()
        {
            _logger = new EnderLogger("AboutViewModel");
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Lumino/1.0");

            // 异步加载贡献者数据
            _ = LoadContributorsAsync();
        }

        [RelayCommand]
        private async Task LoadContributorsAsync()
        {
            if (IsLoadingContributors) return;

            IsLoadingContributors = true;
            ErrorMessage = string.Empty;

            try
            {
                // Gitee API获取贡献者信息
                var url = "https://gitee.com/api/v5/repos/Enderman-bm/domino-next-c/contributors";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var contributors = JsonSerializer.Deserialize<Contributor[]>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (contributors != null)
                    {
                        Contributors.Clear();
                        foreach (var contributor in contributors)
                        {
                            Contributors.Add(contributor);
                        }
                        _logger.Info("AboutViewModel", $"成功加载 {contributors.Length} 个贡献者");
                    }
                }
                else
                {
                    ErrorMessage = $"获取贡献者信息失败: {response.StatusCode}";
                    _logger.Error("AboutViewModel", $"获取贡献者信息失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"获取贡献者信息时发生错误: {ex.Message}";
                _logger.Error("AboutViewModel", $"获取贡献者信息时发生错误: {ex.Message}");
            }
            finally
            {
                IsLoadingContributors = false;
            }
        }

        [RelayCommand]
        private void RefreshContributors()
        {
            _ = LoadContributorsAsync();
        }
    }
}