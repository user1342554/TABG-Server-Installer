using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Core.Services.AI;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;

namespace TabgInstaller.Gui.Tabs
{
    public partial class AiChatPanel : UserControl
    {
        public string ServerPath { get; set; } = Environment.CurrentDirectory;
        private ISecureKeyStore _keyStore = null!;
        private IProviderModelService _providerService = null!;
        private IUnifiedBackend _unifiedBackend = null!;
        private ILocalModelManager _localModelManager = null!;
        private PromptBuilder _promptBuilder = null!;
        private IToolExecutor _toolExecutor = null!;
        private System.Diagnostics.Process? _localInferenceProcess;

        private ObservableCollection<ModernChatMessageViewModel> _messages = new();
        private List<ChatMessage> _chatHistory = new();
        private CancellationTokenSource? _currentRequestCts;
        private ObservableCollection<ModelDownloadViewModel> _modelViewModels = new();
        private CancellationTokenSource? _downloadCts;
        private CancellationTokenSource? _validationCts;

        private string? _currentProvider;
        private string? _currentModel;
        private bool _isLocalMode;
        private string _currentChatName = "default";

        public AiChatPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _keyStore = new SecureKeyStore();
            _providerService = new ProviderModelService();
            _unifiedBackend = new UnifiedBackend(_keyStore, _providerService);
            _localModelManager = new LocalModelManager();
            _promptBuilder = new PromptBuilder();
            _toolExecutor = new ToolExecutor(ServerPath);
            ChatMessages.ItemsSource = _messages;

            await ShowInlineSetupAsync();
            // ensure chat starts with only a system prompt
            _messages.Clear();
            _chatHistory.Clear();
            _chatHistory.Add(ChatMessage.System(_promptBuilder.BuildSystemPrompt(ServerPath)));
        }

        private void InitializeConversation()
        {
            var systemPrompt = _promptBuilder.BuildSystemPrompt(ServerPath);
            _chatHistory.Add(ChatMessage.System(systemPrompt));
        }

        private async Task ShowInlineSetupAsync()
        {
            SetupOverlay.Visibility = Visibility.Visible;
            try
            {
                // Local models
                ModelDirectoryTextBox.Text = _localModelManager.GetModelsDirectory();
                await UpdateSpaceInfo();
                await LoadAvailableModels();

                // Providers/models
                var providers = _providerService.GetProviders();
                ProviderComboBox.Items.Clear();
                foreach (var p in providers)
                {
                    ProviderComboBox.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Name });
                }
                if (ProviderComboBox.Items.Count > 0) ProviderComboBox.SelectedIndex = 0;

                StatusText.Text = "Setup required";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Setup error: {ex.Message}";
            }
        }

        private async Task LoadAvailableModels()
        {
            var models = await _localModelManager.GetAvailableModelsAsync();
            _modelViewModels.Clear();
            foreach (var model in models)
            {
                _modelViewModels.Add(new ModelDownloadViewModel
                {
                    ModelId = model.ModelId,
                    DisplayName = model.DisplayName,
                    FileSize = model.TotalSize,
                    IsInstalled = model.IsInstalled,
                    IsSelected = false
                });
            }
            ModelsList.ItemsSource = _modelViewModels;
        }

        private async Task UpdateSpaceInfo()
        {
            await Task.Run(() =>
            {
                try
                {
                    var path = ModelDirectoryTextBox.Text;
                    if (string.IsNullOrEmpty(path)) return;
                    var drive = System.IO.Path.GetPathRoot(path);
                    if (string.IsNullOrEmpty(drive)) return;
                    var driveInfo = new System.IO.DriveInfo(drive);
                    var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    Dispatcher.Invoke(() =>
                    {
                        SpaceInfoText.Text = $"Available space: {availableGB:F1} GB";
                        SpaceInfoText.Foreground = availableGB > 250 ? Brushes.Green : availableGB > 50 ? Brushes.Orange : Brushes.Red;
                    });
                }
                catch
                {
                    Dispatcher.Invoke(() => { SpaceInfoText.Text = "Unable to determine available space"; SpaceInfoText.Foreground = Brushes.Gray; });
                }
            });
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            _messages.Clear();
            _chatHistory.Clear();
            _chatHistory.Add(ChatMessage.System(_promptBuilder.BuildSystemPrompt(ServerPath)));
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SelectionPanel.Visibility = Visibility.Visible;
            LocalDownloadPanel.Visibility = Visibility.Collapsed;
            OnlineSetupPanel.Visibility = Visibility.Collapsed;
            SetupOverlay.Visibility = Visibility.Visible;
            // Make sure model directory textbox reflects persisted dir (handles external drive)
            ModelDirectoryTextBox.Text = _localModelManager.GetModelsDirectory();
            _ = UpdateSpaceInfo();
        }

        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange > 0)
            {
                ChatScrollViewer.ScrollToEnd();
            }
        }

        private async void UserInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await SendMessage();
                e.Handled = true;
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async Task SendMessage()
        {
            var message = UserInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;
            AddMessage("You", message, MessageRole.User);
            _chatHistory.Add(ChatMessage.User(message));
            UserInput.Clear();
            SendButton.IsEnabled = false;
            TypingIndicator.Visibility = Visibility.Visible;
            _currentRequestCts?.Cancel();
            _currentRequestCts = new CancellationTokenSource();

            try
            {
                var functions = _toolExecutor.GetAvailableFunctions();
                ToolCallResult result;
                if (_isLocalMode)
                {
                    var localBackend = new LocalAIBackend();
                    result = await localBackend.SendAsync(_chatHistory.ToArray(), functions, _currentModel ?? "gpt-oss-20b", _currentRequestCts.Token);
                }
                else
                {
                    var provider = _currentProvider ?? (ProviderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OpenAI";
                    var model = _currentModel ?? (OnlineModelComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "gpt-5";
                    result = await _unifiedBackend.SendAsync(provider, model, _chatHistory.ToArray(), functions, _currentRequestCts.Token);
                }

                if (!result.Success)
                {
                    AddMessage("Error", result.ErrorMessage ?? "Unknown error", MessageRole.Error);
                    return;
                }
                if (!string.IsNullOrEmpty(result.AssistantMessage))
                {
                    // Simulate streaming typing
                    await StreamAssistantAsync(result.AssistantMessage, _currentRequestCts.Token);
                }
                if (result.ToolCalls.Any())
                {
                    foreach (var toolCall in result.ToolCalls)
                    {
                        AddMessage("System", $"Tool call: {toolCall.Function.Name}({toolCall.Function.Arguments})", MessageRole.System);
                        var toolResult = _toolExecutor.ExecuteToolCall(toolCall, ServerPath);
                        AddMessage("System", $"Tool result: {toolResult}", MessageRole.System);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AddMessage("System", "Request cancelled", MessageRole.System);
            }
            catch (Exception ex)
            {
                AddMessage("Error", $"Exception: {ex.Message}", MessageRole.Error);
            }
            finally
            {
                TypingIndicator.Visibility = Visibility.Collapsed;
                SendButton.IsEnabled = true;
            }
        }

        private async Task StreamAssistantAsync(string fullText, CancellationToken ct)
        {
            var builder = new System.Text.StringBuilder();
            var messageVm = new ModernChatMessageViewModel
            {
                Role = "Assistant",
                Content = string.Empty,
                MessageRole = MessageRole.Assistant,
                Timestamp = DateTime.Now.ToString("HH:mm")
            };
            Dispatcher.Invoke(() => _messages.Add(messageVm));
            foreach (var ch in fullText)
            {
                ct.ThrowIfCancellationRequested();
                builder.Append(ch);
                var current = builder.ToString();
                Dispatcher.Invoke(() => messageVm.Content = current);
                await Task.Delay(10, ct);
            }
            _chatHistory.Add(ChatMessage.Assistant(fullText));
        }

        // Removed Edit/Reload and chat history persistence per request

        private async void UseInstalledModel_Click(object sender, RoutedEventArgs e)
        {
            var models = await _localModelManager.GetAvailableModelsAsync();
            var installed = models.FirstOrDefault(m => m.IsInstalled);
            if (installed != null)
            {
                _isLocalMode = true; _currentModel = installed.ModelId;
                ModelNameText.Text = _currentModel;
                ModeText.Text = "Local";
                await RestartLocalAsync();
            }
            else
            {
                MessageBox.Show("No local models found. Please download one.");
            }
            SetupOverlay.Visibility = Visibility.Collapsed;
            StatusText.Text = "Ready";
        }

        private async Task RestartLocalAsync()
        {
            if (_localInferenceProcess != null)
            {
                await _localModelManager.StopLocalInferenceServerAsync();
                _localInferenceProcess = null;
            }
            _localInferenceProcess = await _localModelManager.StartLocalInferenceServerAsync(_currentModel ?? "gpt-oss-20b");
            StatusText.Text = "Ready";
        }

        private void CancelSetup_Click(object sender, RoutedEventArgs e)
        {
            SetupOverlay.Visibility = Visibility.Collapsed;
        }

        private void LocalOption_Click(object sender, RoutedEventArgs e)
        {
            SelectionPanel.Visibility = Visibility.Collapsed;
            LocalDownloadPanel.Visibility = Visibility.Visible;
            OnlineSetupPanel.Visibility = Visibility.Collapsed;
        }

        private void OnlineOption_Click(object sender, RoutedEventArgs e)
        {
            SelectionPanel.Visibility = Visibility.Collapsed;
            LocalDownloadPanel.Visibility = Visibility.Collapsed;
            OnlineSetupPanel.Visibility = Visibility.Visible;
            if (ProviderComboBox != null && ProviderComboBox.Items.Count > 0) ProviderComboBox.SelectedIndex = 0;
        }

        private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog { Description = "Select directory to store AI models", ShowNewFolderButton = true, SelectedPath = ModelDirectoryTextBox.Text };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ModelDirectoryTextBox.Text = dialog.SelectedPath;
                _localModelManager.SetModelsDirectory(dialog.SelectedPath);
                _ = UpdateSpaceInfo();
            }
        }

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            var selectedModels = _modelViewModels.Where(m => m.IsSelected && !m.IsInstalled).ToList();
            if (!selectedModels.Any()) { MessageBox.Show("Please select at least one model to download."); return; }
            DownloadButton.IsEnabled = false; _downloadCts = new CancellationTokenSource();
            try
            {
                foreach (var modelVm in selectedModels)
                {
                    modelVm.IsDownloading = true;
                    var progress = new Progress<(string message, double percentage)>(u => { modelVm.DownloadProgress = u.percentage; modelVm.ProgressText = u.message; });
                    await _localModelManager.DownloadModelAsync(modelVm.ModelId, ModelDirectoryTextBox.Text, progress, _downloadCts.Token);
                    modelVm.IsInstalled = true; modelVm.IsDownloading = false;
                }
                _isLocalMode = true; _currentModel = selectedModels.First().ModelId;
                SetupOverlay.Visibility = Visibility.Collapsed;
                await RestartLocalAsync();
            }
            catch (OperationCanceledException) { MessageBox.Show("Download cancelled."); }
            catch (Exception ex) { MessageBox.Show($"Error downloading models: {ex.Message}"); }
            finally { DownloadButton.IsEnabled = true; foreach (var m in selectedModels) m.IsDownloading = false; }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            _validationCts?.Cancel();
            SelectionPanel.Visibility = Visibility.Visible;
            LocalDownloadPanel.Visibility = Visibility.Collapsed;
            OnlineSetupPanel.Visibility = Visibility.Collapsed;
        }

        private async void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || SaveApiKeyButton == null || ValidationMessage == null || ApiKeyBox == null || ProviderComboBox == null) return;
            SaveApiKeyButton.IsEnabled = false; ValidationMessage.Visibility = Visibility.Collapsed;
            if (string.IsNullOrEmpty(ApiKeyBox.Password)) return;
            _validationCts?.Cancel(); _validationCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(500, _validationCts.Token);
                if (ProviderComboBox.SelectedItem is ComboBoxItem item)
                {
                    var provider = item.Tag?.ToString() ?? "";
                    ValidationMessage.Text = "Validating API key..."; ValidationMessage.Foreground = Brushes.Gray; ValidationMessage.Visibility = Visibility.Visible;
                    var isValid = await _unifiedBackend.ValidateApiKeyAsync(provider, ApiKeyBox.Password, _validationCts.Token);
                    if (isValid) { ValidationMessage.Text = "✓ API key is valid!"; ValidationMessage.Foreground = Brushes.Green; SaveApiKeyButton.IsEnabled = true; }
                    else { ValidationMessage.Text = "✗ Invalid API key"; ValidationMessage.Foreground = Brushes.Red; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ValidationMessage.Text = $"Error validating key: {ex.Message}"; ValidationMessage.Foreground = Brushes.Red; ValidationMessage.Visibility = Visibility.Visible; }
        }

        private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || ProviderComboBox == null || ApiKeyBox == null || HelpText == null || SaveApiKeyButton == null) return;
            if (ProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? "";
                var existingKey = _keyStore.GetKey(provider);
                if (!string.IsNullOrEmpty(existingKey)) ApiKeyBox.Password = existingKey;
                if (OnlineModelComboBox != null)
                {
                    OnlineModelComboBox.Items.Clear();
                    var models = _providerService.GetModelsForProvider(provider);
                    foreach (var m in models)
                    {
                        OnlineModelComboBox.Items.Add(new ComboBoxItem { Content = m.DisplayName, Tag = m.Id });
                    }
                    if (OnlineModelComboBox.Items.Count > 0) OnlineModelComboBox.SelectedIndex = 0;
                }
                HelpText.Text = $"Provider: {provider}";
                SaveApiKeyButton.IsEnabled = !string.IsNullOrEmpty(ApiKeyBox.Password);
            }
        }

        private async void SaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (ProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? ""; _currentProvider = provider;
                if (!string.IsNullOrEmpty(ApiKeyBox.Password))
                {
                    _keyStore.SaveKey(provider, ApiKeyBox.Password);
                    _isLocalMode = false; _currentModel = "gpt-5";
                    SetupOverlay.Visibility = Visibility.Collapsed;
                    ModelNameText.Text = _currentModel; ModeText.Text = "Online"; StatusText.Text = $"Connected to {_currentProvider}";
                    await Task.CompletedTask;
                }
            }
        }

        private void AddMessage(string role, string content, MessageRole messageRole)
        {
            Dispatcher.Invoke(() =>
            {
                var message = new ModernChatMessageViewModel
                {
                    Role = role,
                    Content = content,
                    MessageRole = messageRole,
                    Timestamp = DateTime.Now.ToString("HH:mm")
                };
                _messages.Add(message);
                ChatScrollViewer.ScrollToBottom();
            });
        }

        // Chat history removed
    }
}

public enum MessageRole
{
    User,
    Assistant,
    System,
    Error
}

public class ModernChatMessageViewModel
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public MessageRole MessageRole { get; set; }
    public string Timestamp { get; set; } = "";

    public string AvatarText => MessageRole switch
    {
        MessageRole.Assistant => "AI",
        MessageRole.User => "U",
        MessageRole.System => "S",
        _ => "?"
    };

    public bool ShowAvatar => MessageRole == MessageRole.Assistant;
    public bool ShowCopyButton => MessageRole != MessageRole.System;
    public int BubbleColumn => MessageRole == MessageRole.User ? 2 : 1;



    public Brush RoleColor => MessageRole switch
    {
        MessageRole.User => new SolidColorBrush(Color.FromRgb(74, 144, 226)),
        MessageRole.Assistant => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        MessageRole.System => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
        MessageRole.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
        _ => Brushes.White
    };
}

public class ModelDownloadViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isInstalled;
    private bool _isDownloading;
    private double _downloadProgress;
    private string _progressText = "";

    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long FileSize { get; set; }

    public string SizeText => $"Size: {FileSize / (1024.0 * 1024.0 * 1024.0):F1} GB";

    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public bool IsInstalled { get => _isInstalled; set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); } }
    public bool IsDownloading { get => _isDownloading; set { _isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressVisibility)); } }
    public double DownloadProgress { get => _downloadProgress; set { _downloadProgress = value; OnPropertyChanged(); } }
    public string ProgressText { get => _progressText; set { _progressText = value; OnPropertyChanged(); } }

    public string StatusText => IsInstalled ? "✓ Installed" : "Not installed";
    public Brush StatusColor => IsInstalled ? Brushes.Green : Brushes.Gray;
    public Visibility ProgressVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


