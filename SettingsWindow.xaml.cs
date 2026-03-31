using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatBot.Models;
using ChatBot.Services;
using Newtonsoft.Json;

namespace ChatBot
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private ConnectionConfig? _selectedConnection;

        public SettingsWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            LoadConnections();
            LoadSystemPrompt();
        }

        private void LoadConnections()
        {
            ConnectionsList.ItemsSource = _settingsService.Settings.Connections;

            if (_settingsService.Settings.Connections.Count > 0)
            {
                var active = _settingsService.Settings.Connections
                    .FirstOrDefault(c => c.Id == _settingsService.Settings.DefaultConnectionId);
                ConnectionsList.SelectedItem = active ?? _settingsService.Settings.Connections[0];
            }
        }

        private void LoadSystemPrompt()
        {
            SystemPromptInput.Text = _settingsService.Settings.SystemPrompt;
        }

        private void ConnectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConnectionsList.SelectedItem is not ConnectionConfig conn)
            {
                ClearConnectionEditor();
                return;
            }

            _selectedConnection = conn;
            NameInput.Text = conn.Name;
            ProviderCombo.SelectedIndex = (int)conn.Provider;
            ApiKeyInput.Text = conn.ApiKey;
            ModelCombo.Text = conn.Model;
            BaseUrlInput.Text = conn.BaseUrl;
            DefaultConnectionCheck.IsChecked = conn.Id == _settingsService.Settings.DefaultConnectionId;
        }

        private void ClearConnectionEditor()
        {
            _selectedConnection = null;
            NameInput.Text = "";
            ProviderCombo.SelectedIndex = 0;
            ApiKeyInput.Text = "";
            ModelCombo.Text = "";
            BaseUrlInput.Text = "";
            DefaultConnectionCheck.IsChecked = false;
        }

        private void ConnectionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ConnectionsList.SelectedItem is ConnectionConfig conn)
            {
                _settingsService.Settings.DefaultConnectionId = conn.Id;
                _settingsService.Save();
                DefaultConnectionCheck.IsChecked = true;
            }
        }

        private void AddConnectionBtn_Click(object sender, RoutedEventArgs e)
        {
            var newConn = new ConnectionConfig
            {
                Name = "Новое подключение",
                Provider = ProviderType.OpenRouter,
                ApiKey = "",
                Model = "",
                BaseUrl = ""
            };
            _settingsService.Settings.Connections.Add(newConn);
            ConnectionsList.ItemsSource = null;
            ConnectionsList.ItemsSource = _settingsService.Settings.Connections;
            ConnectionsList.SelectedItem = newConn;
        }

        private void DeleteConnectionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                var conn = _settingsService.Settings.Connections.Find(c => c.Id == id);
                if (conn == null) return;

                var result = MessageBox.Show($"Удалить подключение \"{conn.Name}\"?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                _settingsService.Settings.Connections.Remove(conn);

                if (_settingsService.Settings.DefaultConnectionId == conn.Id)
                    _settingsService.Settings.DefaultConnectionId = _settingsService.Settings.Connections.FirstOrDefault()?.Id ?? "";

                if (_selectedConnection?.Id == conn.Id)
                {
                    _selectedConnection = null;
                    ClearConnectionEditor();
                }

                ConnectionsList.ItemsSource = null;
                ConnectionsList.ItemsSource = _settingsService.Settings.Connections;
                _settingsService.Save();
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConnection == null)
                return;

            var result = MessageBox.Show("Удалить это подключение?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            _settingsService.Settings.Connections.Remove(_selectedConnection);

            if (_settingsService.Settings.DefaultConnectionId == _selectedConnection.Id)
                _settingsService.Settings.DefaultConnectionId = _settingsService.Settings.Connections.FirstOrDefault()?.Id ?? "";

            _selectedConnection = null;
            ConnectionsList.ItemsSource = null;
            ConnectionsList.ItemsSource = _settingsService.Settings.Connections;
            ClearConnectionEditor();
        }

        private async void RefreshModelsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConnection == null)
            {
                MessageBox.Show("Сначала выберите или создайте подключение.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshModelsBtn.IsEnabled = false;
            RefreshModelsBtn.Content = "...";

            try
            {
                var models = await FetchModelsAsync(_selectedConnection);
                ModelCombo.ItemsSource = models;
                if (models.Count > 0 && string.IsNullOrEmpty(ModelCombo.Text))
                    ModelCombo.Text = models[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки моделей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshModelsBtn.IsEnabled = true;
                RefreshModelsBtn.Content = "↻";
            }
        }

        private async Task<List<string>> FetchModelsAsync(ConnectionConfig conn)
        {
            using var client = new HttpClient();

            if (conn.Provider == ProviderType.OpenRouter)
            {
                var url = string.IsNullOrEmpty(conn.BaseUrl)
                    ? "https://openrouter.ai/api/v1/models"
                    : conn.BaseUrl.Replace("/chat/completions", "/models").Replace("/v1/chat/completions", "/v1/models");

                if (!url.Contains("/models"))
                    url = url.TrimEnd('/') + "/models";

                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {conn.ApiKey}");
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"HTTP {response.StatusCode}: {json}");

                var data = JsonConvert.DeserializeObject<ModelsResponse>(json);
                return data?.Data?.Select(m => m.Id).OrderBy(s => s).ToList() ?? new List<string>();
            }
            else
            {
                if (string.IsNullOrEmpty(conn.BaseUrl))
                    throw new Exception("Base URL required for LLM provider");

                var url = conn.BaseUrl.TrimEnd('/') + "/models";
                if (!string.IsNullOrEmpty(conn.ApiKey))
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {conn.ApiKey}");

                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"HTTP {response.StatusCode}: {json}");

                var data = JsonConvert.DeserializeObject<ModelsResponse>(json);
                return data?.Data?.Select(m => m.Id).OrderBy(s => s).ToList() ?? new List<string>();
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TabConnection.IsChecked == true)
            {
                if (_selectedConnection == null)
                {
                    MessageBox.Show("Выберите или создайте подключение.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _selectedConnection.Name = NameInput.Text;
                _selectedConnection.Provider = (ProviderType)ProviderCombo.SelectedIndex;
                _selectedConnection.ApiKey = ApiKeyInput.Text;
                _selectedConnection.Model = ModelCombo.Text;
                _selectedConnection.BaseUrl = BaseUrlInput.Text;

                if (DefaultConnectionCheck.IsChecked == true)
                    _settingsService.Settings.DefaultConnectionId = _selectedConnection.Id;
            }
            else
            {
                _settingsService.Settings.SystemPrompt = SystemPromptInput.Text;
            }

            _settingsService.Save();
            MessageBox.Show("Настройки сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TabConnection_Checked(object sender, RoutedEventArgs e)
        {
            if (ConnectionEditor != null) ConnectionEditor.Visibility = Visibility.Visible;
            if (PromptEditor != null) PromptEditor.Visibility = Visibility.Collapsed;
        }

        private void TabPrompt_Checked(object sender, RoutedEventArgs e)
        {
            if (ConnectionEditor != null) ConnectionEditor.Visibility = Visibility.Collapsed;
            if (PromptEditor != null) PromptEditor.Visibility = Visibility.Visible;
        }

        private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedConnection == null) return;
            _selectedConnection.Provider = (ProviderType)ProviderCombo.SelectedIndex;

            if (ProviderCombo.SelectedIndex == 0)
            {
                BaseUrlInput.Text = "";
                if (string.IsNullOrEmpty(ModelCombo.Text))
                    ModelCombo.Text = "google/gemini-2.5-flash-preview-05-20";
            }
            else
            {
                if (string.IsNullOrEmpty(BaseUrlInput.Text))
                    BaseUrlInput.Text = "http://localhost:5001/v1/chat/completions";
            }
        }
    }

    public class ModelsResponse
    {
        public List<ModelData> Data { get; set; } = new();
    }

    public class ModelData
    {
        public string Id { get; set; } = "";
    }
}
