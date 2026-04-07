using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ChatBot.Models;
using ChatBot.Services;

namespace ChatBot
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly AiService _aiService;
        private readonly ChatHistoryService _chatHistoryService;
        private readonly List<ChatMessage> _chatHistory = new();
        private bool _isProcessing;
        private bool _sidebarVisible;
        private string? _currentSessionId;

        public MainWindow()
        {
            InitializeComponent();
            _settingsService = new SettingsService();
            _aiService = new AiService();
            _chatHistoryService = new ChatHistoryService();
            UpdateConnectionStatus();
            LoadChatHistorySidebar();
        }

        private void UpdateConnectionStatus()
        {
            var conn = _settingsService.GetActiveConnection();
            if (conn != null)
            {
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ADE80"));
                ModelNameLabel.Text = conn.Name;
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4854A"));
                ModelNameLabel.Text = "Нет подключения";
            }
        }

        private void ToggleSidebarBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleSidebar();
        }

        private void OpenSidebarBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_sidebarVisible)
                ToggleSidebar();
        }

        private void ToggleSidebar()
        {
            _sidebarVisible = !_sidebarVisible;
            GridLengthAnimationHelper.AnimateWidth(SidebarColumn, _sidebarVisible ? 280.0 : 0.0, 300);
        }

        private void NewChatBtn_Click(object sender, RoutedEventArgs e)
        {
            StartNewChat();
        }

        private void StartNewChat()
        {
            _chatHistory.Clear();
            _currentSessionId = null;
            ChatMessages.Children.Clear();
            EmptyStateText.Visibility = Visibility.Visible;
            MessageInput.Text = "";
        }

        private void LoadChatHistorySidebar()
        {
            ChatHistoryList.Children.Clear();
            foreach (var session in _chatHistoryService.Sessions)
            {
                var item = CreateChatHistoryItem(session);
                ChatHistoryList.Children.Add(item);
            }
        }

        private Border CreateChatHistoryItem(ChatSession session)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 8, 10),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand,
                Tag = session.Id
            };

            border.MouseLeftButtonUp += (s, e) =>
            {
                var id = (s as Border)?.Tag as string;
                if (!string.IsNullOrEmpty(id))
                    LoadSession(id);
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textPanel = new StackPanel();
            var titleText = new TextBlock
            {
                Text = session.Title,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0D6CC")),
                FontSize = 14,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var dateText = new TextBlock
            {
                Text = FormatDate(session.UpdatedAt),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8A9A")),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            };
            textPanel.Children.Add(titleText);
            textPanel.Children.Add(dateText);
            Grid.SetColumn(textPanel, 0);

            var deleteBtn = new Button
            {
                Width = 28,
                Height = 28,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = session.Id,
                Margin = new Thickness(4, 0, 0, 0)
            };
            deleteBtn.Click += DeleteSessionBtn_Click;

            var deleteIcon = new Path
            {
                Data = Geometry.Parse("M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19M8,9H16V19H8V9M15.5,4L14.5,3H9.5L8.5,4H5V6H19V4H15.5Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8A9A")),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform
            };
            deleteBtn.Content = deleteIcon;
            Grid.SetColumn(deleteBtn, 1);

            grid.Children.Add(textPanel);
            grid.Children.Add(deleteBtn);
            border.Child = grid;

            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A50"));
            border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;

            return border;
        }

        private string FormatDate(long timestamp)
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
            var now = DateTime.Now;
            if (date.Date == now.Date)
                return $"Сегодня, {date:HH:mm}";
            if (date.Date == now.Date.AddDays(-1))
                return $"Вчера, {date:HH:mm}";
            return date.ToString("dd.MM.yy HH:mm");
        }

        private void DeleteSessionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                var session = _chatHistoryService.GetSession(id);
                if (session == null) return;

                var result = MessageBox.Show($"Удалить чат \"{session.Title}\"?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                _chatHistoryService.DeleteSession(id);
                LoadChatHistorySidebar();

                if (_currentSessionId == id)
                {
                    StartNewChat();
                }
            }
        }

        private void LoadSession(string sessionId)
        {
            var session = _chatHistoryService.GetSession(sessionId);
            if (session == null) return;

            _currentSessionId = sessionId;
            _chatHistory.Clear();
            ChatMessages.Children.Clear();

            EmptyStateText.Visibility = session.Messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (var msg in session.Messages)
            {
                if (msg.Role == "user")
                    AddUserMessage(msg.Content);
                else
                    AddBotMessageWithoutRegen(msg.Content);
            }

            if (_sidebarVisible)
                ToggleSidebar();
        }

        private void AddBotMessageWithoutRegen(string text)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A50")),
                CornerRadius = new CornerRadius(16, 16, 16, 4),
                Padding = new Thickness(16, 12, 16, 12),
                MaxWidth = 600
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0D6CC")),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            panel.Children.Add(border);
            AddDeleteButton(panel);
            ChatMessages.Children.Add(panel);
            ScrollToBottom();
        }

        private void SaveCurrentSession()
        {
            if (_chatHistory.Count == 0) return;

            var title = "Новый чат";
            if (_chatHistory.Count > 0)
            {
                var firstUserMsg = _chatHistory.FirstOrDefault(m => m.Role == "user");
                if (firstUserMsg != null)
                {
                    title = firstUserMsg.Content.Length > 30 ? firstUserMsg.Content.Substring(0, 30) + "..." : firstUserMsg.Content;
                }
            }

            var session = new ChatSession
            {
                Id = _currentSessionId ?? Guid.NewGuid().ToString(),
                Title = title,
                Messages = new List<ChatMessage>(_chatHistory),
                CreatedAt = _currentSessionId == null ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : 0,
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            if (_currentSessionId == null)
            {
                _chatHistoryService.AddSession(session);
                _currentSessionId = session.Id;
            }
            else
            {
                _chatHistoryService.UpdateSession(session);
            }

            LoadChatHistorySidebar();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            var text = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text) || _isProcessing)
                return;

            var conn = _settingsService.GetActiveConnection();
            if (conn == null)
            {
                MessageBox.Show("Настройте подключение в настройках.", "Нет подключения", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageInput.Text = "";
            _isProcessing = true;
            SendButton.IsEnabled = false;
            EmptyStateText.Visibility = Visibility.Collapsed;

            AddUserMessage(text);
            _chatHistory.Add(new ChatMessage { Role = "user", Content = text });

            var botPanel = AddBotMessagePlaceholder();
            ShowLoading();

            try
            {
                var response = await _aiService.SendMessageAsync(conn, _settingsService.Settings.SystemPrompt, _chatHistory);
                var cleanedResponse = StripThinkTags(response);
                HideLoading();
                UpdateBotMessage(botPanel, cleanedResponse);
                _chatHistory.Add(new ChatMessage { Role = "assistant", Content = response });
                SaveCurrentSession();
            }
            catch (Exception ex)
            {
                HideLoading();
                UpdateBotMessage(botPanel, $"Ошибка: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                SendButton.IsEnabled = true;
                MessageInput.Focus();
            }
        }

        private void AddUserMessage(string text)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4854A")),
                CornerRadius = new CornerRadius(16, 16, 4, 16),
                Padding = new Thickness(16, 12, 16, 12),
                MaxWidth = 600
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E")),
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            panel.Children.Add(border);
            AddDeleteButton(panel);
            ChatMessages.Children.Add(panel);
            ScrollToBottom();
        }

        private StackPanel AddBotMessagePlaceholder()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A50")),
                CornerRadius = new CornerRadius(16, 16, 16, 4),
                Padding = new Thickness(16, 12, 16, 12),
                MaxWidth = 600
            };

            var textBlock = new TextBlock
            {
                Text = "...",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0D6CC")),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            panel.Children.Add(border);
            ChatMessages.Children.Add(panel);
            ScrollToBottom();
            return panel;
        }

        private void UpdateBotMessage(StackPanel panel, string text)
        {
            if (panel.Children.Count >= 1 && panel.Children[0] is Border border)
            {
                if (border.Child is TextBlock textBlock)
                {
                    textBlock.Text = text;
                }
            }

            var regenBtn = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(8, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Bottom,
                ToolTip = "Регенерировать"
            };

            var icon = new Path
            {
                Data = Geometry.Parse("M17.65,6.35C16.2,4.9 14.21,4 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20C15.73,20 18.84,17.45 19.73,14H17.65C16.83,16.33 14.61,18 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6C13.66,6 15.14,6.69 16.22,7.78L13,11H20V4L17.65,6.35Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8A9A")),
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform
            };

            regenBtn.Content = icon;
            regenBtn.Click += async (s, e) => await RegenerateLastMessage(panel);
            panel.Children.Add(regenBtn);
            AddDeleteButton(panel);
            ScrollToBottom();
        }

        private async Task RegenerateLastMessage(StackPanel panel)
        {
            if (_isProcessing || _chatHistory.Count == 0)
                return;

            var conn = _settingsService.GetActiveConnection();
            if (conn == null) return;

            _isProcessing = true;
            SendButton.IsEnabled = false;
            ShowLoading();

            TextBlock? targetTextBlock = null;
            if (panel.Children[0] is Border border && border.Child is TextBlock tb)
            {
                tb.Text = "...";
                targetTextBlock = tb;
            }

            try
            {
                if (_chatHistory[_chatHistory.Count - 1].Role == "assistant")
                    _chatHistory.RemoveAt(_chatHistory.Count - 1);

                var response = await _aiService.SendMessageAsync(conn, _settingsService.Settings.SystemPrompt, _chatHistory);
                var cleanedResponse = StripThinkTags(response);
                HideLoading();
                if (targetTextBlock != null)
                    targetTextBlock.Text = cleanedResponse;
                _chatHistory.Add(new ChatMessage { Role = "assistant", Content = response });
                SaveCurrentSession();
            }
            catch (Exception ex)
            {
                HideLoading();
                if (targetTextBlock != null)
                    targetTextBlock.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
                SendButton.IsEnabled = true;
            }
        }

        private void AddDeleteButton(StackPanel panel)
        {
            var deleteBtn = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(8, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Bottom,
                ToolTip = "Удалить"
            };

            var icon = new Path
            {
                Data = Geometry.Parse("M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19M8,9H16V19H8V9M15.5,4L14.5,3H9.5L8.5,4H5V6H19V4H15.5Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A8A9A")),
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform
            };

            deleteBtn.Content = icon;
            deleteBtn.Click += (s, e) => DeleteMessage(panel);
            panel.Children.Add(deleteBtn);
        }

        private void DeleteMessage(StackPanel panel)
        {
            var index = ChatMessages.Children.IndexOf(panel);
            ChatMessages.Children.Remove(panel);
            if (index >= 0 && index < _chatHistory.Count)
                _chatHistory.RemoveAt(index);
            if (ChatMessages.Children.Count == 0)
                EmptyStateText.Visibility = Visibility.Visible;
            SaveCurrentSession();
        }

        private void ShowLoading()
        {
            LoadingIndicator.Visibility = Visibility.Visible;
            StartLoadingAnimation();
        }

        private void HideLoading()
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            StopLoadingAnimation();
        }

        private void StartLoadingAnimation()
        {
            var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

            for (int i = 0; i < 3; i++)
            {
                var da = new DoubleAnimation
                {
                    From = 0.5,
                    To = 1.2,
                    Duration = TimeSpan.FromMilliseconds(600),
                    BeginTime = TimeSpan.FromMilliseconds(i * 150),
                    AutoReverse = true
                };

                var name = i switch { 0 => "Dot1", 1 => "Dot2", 2 => "Dot3", _ => "" };
                Storyboard.SetTargetName(da, name);
                Storyboard.SetTargetProperty(da, new PropertyPath(ScaleTransform.ScaleYProperty));
                storyboard.Children.Add(da);
            }

            storyboard.Begin(this);
        }

        private void StopLoadingAnimation()
        {
            Dot1.ScaleY = 1;
            Dot2.ScaleY = 1;
            Dot3.ScaleY = 1;
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ChatScrollViewer.ScrollToEnd();
            }));
        }

        private string StripThinkTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var result = System.Text.RegularExpressions.Regex.Replace(text, @"<think>.*?", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            return result.Trim();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settingsService) { Owner = this };
            settingsWindow.ShowDialog();
            UpdateConnectionStatus();
        }
    }
}