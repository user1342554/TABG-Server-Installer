using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Core.Services.AI;
using TabgInstaller.Gui.Converters;
using TabgInstaller.Gui.Dialogs;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace TabgInstaller.Gui
{
    public partial class AiChatWindow : Window, INotifyPropertyChanged
    {
        private ISecureKeyStore _keyStore = null!;
        private IProviderModelService _providerService = null!;
        private IUnifiedBackend _unifiedBackend = null!;
        private ILocalModelManager _localModelManager = null!;
        private PromptBuilder _promptBuilder = null!;
        private IToolExecutor _toolExecutor = null!;
        private readonly string _serverPath;
        private Process? _localInferenceProcess;

        private ObservableCollection<ModernChatMessageViewModel> _messages = new();
        private List<ChatMessage> _chatHistory = new();
        private CancellationTokenSource? _currentRequestCts;
        private ObservableCollection<ModelDownloadViewModel> _modelViewModels = new();
        private CancellationTokenSource? _downloadCts;
        private CancellationTokenSource? _validationCts;

        private AiSetupDialog.SetupMode _currentMode = AiSetupDialog.SetupMode.None;
        private string? _currentProvider;
        private string? _currentModel;
        private bool _isLocalMode;

        public ObservableCollection<ModernChatMessageViewModel> Messages => _messages;

        public AiChatWindow(string serverPath)
        {
            InitializeComponent();
            
            _serverPath = serverPath ?? throw new ArgumentNullException(nameof(serverPath));
            
            // Initialize collections first
            _messages = new ObservableCollection<ModernChatMessageViewModel>();
            _chatHistory = new List<ChatMessage>();
            
            DataContext = this;
            
            // Initialize providers and check for existing configuration after window is loaded
            Loaded += AiChatWindow_Loaded;
        }

        private async void AiChatWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize services
                _keyStore = new SecureKeyStore();
                _providerService = new ProviderModelService();
                _unifiedBackend = new UnifiedBackend(_keyStore, _providerService);
                _localModelManager = new LocalModelManager();
                _promptBuilder = new PromptBuilder();
                _toolExecutor = new ToolExecutor(_serverPath);
                
                // Set up UI
                ChatMessages.ItemsSource = Messages;
                
                // Initialize chat
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing AI Chat:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }



        private async Task InitializeAsync()
        {
            // New: inline setup overlay rather than separate dialog
            await ShowInlineSetupAsync();

            if (_currentMode == AiSetupDialog.SetupMode.Local)
            {
                ModelNameText.Text = _currentModel ?? "Local Model";
                ModeText.Text = "Local";
                ModeText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 198));
                StatusText.Text = "Starting local server...";
                
                // Start local inference server
                try
                {
                    _localInferenceProcess = await _localModelManager.StartLocalInferenceServerAsync(_currentModel ?? "gpt-oss-20b");
                    if (_localInferenceProcess != null)
                    {
                        StatusText.Text = "Connected to local AI";
                        StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    }
                    else
                    {
                        throw new Exception("Failed to start local inference server");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to start local AI server: {ex.Message}\n\nPlease ensure the model is properly installed.",
                        "Local AI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }
            }
            else
            {
                _currentModel ??= "gpt-5"; // Default to GPT-5 if available
                ModelNameText.Text = _currentModel;
                ModeText.Text = "Online";
                ModeText.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 107));
                StatusText.Text = $"Connected to {_currentProvider}";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 198));
            }

            // Add system prompt
            var systemPrompt = _promptBuilder.BuildSystemPrompt(_serverPath);
            _chatHistory.Add(ChatMessage.System(systemPrompt));

            // Add welcome message
            AddMessage("Assistant", "🎮 Welcome to TABG AI Assistant!\n\nI'm here to help you configure your server, manage plugins, and optimize your game settings. What would you like to do today?", MessageRole.Assistant);
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear the chat history?", 
                "Clear Chat", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _messages.Clear();
                _chatHistory.Clear();
                
                // Re-add system prompt
                var systemPrompt = _promptBuilder.BuildSystemPrompt(_serverPath);
                _chatHistory.Add(ChatMessage.System(systemPrompt));
                
                AddMessage("System", "Chat cleared. Starting fresh!", MessageRole.System);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            _ = ShowInlineSetupAsync();
        }

        private async Task RestartWithNewSettings()
        {
            // Stop current inference if local
            if (_localInferenceProcess != null)
            {
                await _localModelManager.StopLocalInferenceServerAsync();
                _localInferenceProcess = null;
            }

            _isLocalMode = _currentMode == AiSetupDialog.SetupMode.Local;

            if (_isLocalMode)
            {
                ModelNameText.Text = _currentModel ?? "Local Model";
                ModeText.Text = "Local";
                ModeText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 198));
                
                try
                {
                    _localInferenceProcess = await _localModelManager.StartLocalInferenceServerAsync(_currentModel ?? "gpt-oss-20b");
                    StatusText.Text = "Connected to local AI";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to restart local AI: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                ModelNameText.Text = _currentModel ?? "gpt-5";
                ModeText.Text = "Online";
                ModeText.Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 107));
                StatusText.Text = $"Connected to {_currentProvider}";
            }
        }

        // Inline Setup
        private async Task ShowInlineSetupAsync()
        {
            // Show overlay and default to selection
            SetupOverlay.Visibility = Visibility.Visible;

            // Initialize simple defaults
            _currentMode = AiSetupDialog.SetupMode.None;

            // Wait for user interaction via event-driven handlers; in absence of explicit user click,
            // attempt auto-mode selection: prefer local if any installed, else online.
            try
            {
                // Initialize directory field and available models
                ModelDirectoryTextBox.Text = _localModelManager.GetModelsDirectory();
                await UpdateSpaceInfo();
                await LoadAvailableModels();

                var models = await _localModelManager.GetAvailableModelsAsync();
                var installed = models.FirstOrDefault(m => m.IsInstalled);
                if (installed != null)
                {
                    _currentMode = AiSetupDialog.SetupMode.Local;
                    _currentModel = installed.ModelId;
                }
                else
                {
                    _currentMode = AiSetupDialog.SetupMode.Online;
                    _currentProvider = "OpenAI";
                    _currentModel = "gpt-5";
                }
            }
            catch
            {
                _currentMode = AiSetupDialog.SetupMode.Online;
                _currentProvider = "OpenAI";
                _currentModel = "gpt-5";
            }

            // Hide overlay once decision is made
            SetupOverlay.Visibility = Visibility.Collapsed;
        }

        private async void UseInstalledModel_Click(object sender, RoutedEventArgs e)
        {
            var models = await _localModelManager.GetAvailableModelsAsync();
            var installed = models.FirstOrDefault(m => m.IsInstalled);
            if (installed != null)
            {
                _currentMode = AiSetupDialog.SetupMode.Local;
                _currentModel = installed.ModelId;
                await RestartWithNewSettings();
            }
            else
            {
                MessageBox.Show("No local models found. Please download one.");
            }
            SetupOverlay.Visibility = Visibility.Collapsed;
        }

        private void CancelSetup_Click(object sender, RoutedEventArgs e)
        {
            SetupOverlay.Visibility = Visibility.Collapsed;
        }

        // Inline setup event handlers and helpers (ported from dialog)
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
                    var drive = Path.GetPathRoot(path);
                    if (string.IsNullOrEmpty(drive)) return;
                    var driveInfo = new DriveInfo(drive);
                    var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    Dispatcher.Invoke(() =>
                    {
                        SpaceInfoText.Text = $"Available space: {availableGB:F1} GB";
                        SpaceInfoText.Foreground = availableGB > 250 ? Brushes.Green : availableGB > 50 ? Brushes.Orange : Brushes.Red;
                    });
                }
                catch
                {
                    Dispatcher.Invoke(() =>
                    {
                        SpaceInfoText.Text = "Unable to determine available space";
                        SpaceInfoText.Foreground = Brushes.Gray;
                    });
                }
            });
        }

        private void LocalOption_Click(object sender, MouseButtonEventArgs e)
        {
            SelectionPanel.Visibility = Visibility.Collapsed;
            LocalDownloadPanel.Visibility = Visibility.Visible;
            OnlineSetupPanel.Visibility = Visibility.Collapsed;
        }

        private void OnlineOption_Click(object sender, MouseButtonEventArgs e)
        {
            SelectionPanel.Visibility = Visibility.Collapsed;
            LocalDownloadPanel.Visibility = Visibility.Collapsed;
            OnlineSetupPanel.Visibility = Visibility.Visible;
            // default provider selection to first (OpenAI)
            if (ProviderComboBox != null && ProviderComboBox.Items.Count > 0)
            {
                ProviderComboBox.SelectedIndex = 0;
            }
        }

        private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select directory to store AI models",
                ShowNewFolderButton = true,
                SelectedPath = ModelDirectoryTextBox.Text
            };
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
            if (!selectedModels.Any())
            {
                MessageBox.Show("Please select at least one model to download.", "No Models Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DownloadButton.IsEnabled = false;
            _downloadCts = new CancellationTokenSource();
            try
            {
                foreach (var modelVm in selectedModels)
                {
                    modelVm.IsDownloading = true;
                    var progress = new Progress<(string message, double percentage)>(update =>
                    {
                        modelVm.DownloadProgress = update.percentage;
                        modelVm.ProgressText = update.message;
                    });
                    await _localModelManager.DownloadModelAsync(
                        modelVm.ModelId,
                        ModelDirectoryTextBox.Text,
                        progress,
                        _downloadCts.Token);
                    modelVm.IsInstalled = true;
                    modelVm.IsDownloading = false;
                }
                _currentMode = AiSetupDialog.SetupMode.Local;
                _currentModel = selectedModels.First().ModelId;
                MessageBox.Show("Models downloaded successfully!", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                SetupOverlay.Visibility = Visibility.Collapsed;
                await RestartWithNewSettings();
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Download cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading models: {ex.Message}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadButton.IsEnabled = true;
                foreach (var modelVm in selectedModels) modelVm.IsDownloading = false;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            _validationCts?.Cancel();
            SelectionPanel.Visibility = Visibility.Visible;
            LocalDownloadPanel.Visibility = Visibility.Collapsed;
            OnlineSetupPanel.Visibility = Visibility.Collapsed;
        }

        private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || ProviderComboBox == null || ApiKeyBox == null || HelpText == null || SaveApiKeyButton == null) return;
            if (ProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? "";
                var existingKey = _keyStore.GetKey(provider);
                if (!string.IsNullOrEmpty(existingKey))
                {
                    ApiKeyBox.Password = existingKey;
                }
                switch (provider)
                {
                    case "OpenAI":
                        HelpText.Text = "Get your API key from platform.openai.com";
                        break;
                    case "Anthropic":
                        HelpText.Text = "Get your API key from console.anthropic.com";
                        break;
                    case "Google":
                        HelpText.Text = "Get your API key from makersuite.google.com";
                        break;
                    case "xAI":
                        HelpText.Text = "Get your API key from x.ai/api";
                        break;
                }
                SaveApiKeyButton.IsEnabled = !string.IsNullOrEmpty(ApiKeyBox.Password);
            }
        }

        private async void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || SaveApiKeyButton == null || ValidationMessage == null || ApiKeyBox == null || ProviderComboBox == null) return;
            SaveApiKeyButton.IsEnabled = false;
            ValidationMessage.Visibility = Visibility.Collapsed;
            if (string.IsNullOrEmpty(ApiKeyBox.Password)) return;
            _validationCts?.Cancel();
            _validationCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(500, _validationCts.Token);
                if (ProviderComboBox.SelectedItem is ComboBoxItem item)
                {
                    var provider = item.Tag?.ToString() ?? "";
                    ValidationMessage.Text = "Validating API key...";
                    ValidationMessage.Foreground = Brushes.Gray;
                    ValidationMessage.Visibility = Visibility.Visible;
                    var isValid = await _unifiedBackend.ValidateApiKeyAsync(provider, ApiKeyBox.Password, _validationCts.Token);
                    if (isValid)
                    {
                        ValidationMessage.Text = "✓ API key is valid!";
                        ValidationMessage.Foreground = Brushes.Green;
                        SaveApiKeyButton.IsEnabled = true;
                    }
                    else
                    {
                        ValidationMessage.Text = "✗ Invalid API key";
                        ValidationMessage.Foreground = Brushes.Red;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ValidationMessage.Text = $"Error validating key: {ex.Message}";
                ValidationMessage.Foreground = Brushes.Red;
                ValidationMessage.Visibility = Visibility.Visible;
            }
        }

        private async void SaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (ProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? "";
                _currentProvider = provider;
                if (!string.IsNullOrEmpty(ApiKeyBox.Password))
                {
                    _keyStore.SaveKey(provider, ApiKeyBox.Password);
                    _currentMode = AiSetupDialog.SetupMode.Online;
                    _currentModel = "gpt-5";
                    SetupOverlay.Visibility = Visibility.Collapsed;
                    await RestartWithNewSettings();
                }
            }
        }

        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string content)
            {
                Clipboard.SetText(content);
                
                // Show brief notification
                var originalContent = btn.Content;
                btn.Content = "✓";
                Task.Delay(1000).ContinueWith(_ => 
                {
                    Dispatcher.Invoke(() => btn.Content = originalContent);
                });
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Auto-scroll to bottom when new messages are added
            if (e.ExtentHeightChange > 0)
            {
                ChatScrollViewer.ScrollToEnd();
            }
        }



        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void UserInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await SendMessage();
                e.Handled = true;
            }
        }

        private async Task SendMessage()
        {
            var message = UserInput.Text.Trim();
            if (string.IsNullOrEmpty(message))
                return;

            // Add user message
            AddMessage("You", message, MessageRole.User);
            _chatHistory.Add(ChatMessage.User(message));

            // Clear input and show typing indicator
            UserInput.Clear();
            SendButton.IsEnabled = false;
            TypingIndicator.Visibility = Visibility.Visible;

            // Cancel any ongoing request
            _currentRequestCts?.Cancel();
            _currentRequestCts = new CancellationTokenSource();

            try
            {
                // Get available functions
                var functions = _toolExecutor.GetAvailableFunctions();

                ToolCallResult result;
                
                if (_isLocalMode)
                {
                    // Use local AI backend
                    var localBackend = new LocalAIBackend();
                    result = await localBackend.SendAsync(
                        _chatHistory.ToArray(),
                        functions,
                        _currentModel ?? "gpt-oss-20b",
                        _currentRequestCts.Token);
                }
                else
                {
                    // Use online AI backend
                    result = await _unifiedBackend.SendAsync(
                        _currentProvider ?? "OpenAI",
                        _currentModel ?? "gpt-oss-120b",
                    _chatHistory.ToArray(),
                    functions,
                    _currentRequestCts.Token);
                }

                if (!result.Success)
                {
                    AddMessage("Error", result.ErrorMessage ?? "Unknown error", MessageRole.Error);
                    return;
                }

                // Add assistant response
                if (!string.IsNullOrEmpty(result.AssistantMessage))
                {
                    AddMessage("Assistant", result.AssistantMessage, MessageRole.Assistant);
                    _chatHistory.Add(ChatMessage.Assistant(result.AssistantMessage));
                }

                // Process tool calls
                if (result.ToolCalls.Any())
                {
                    foreach (var toolCall in result.ToolCalls)
                    {
                        var toolResult = _toolExecutor.ExecuteToolCall(toolCall, _serverPath);
                        AddMessage("System", $"Executed {toolCall.Function.Name}: {toolResult}", MessageRole.System);
                        
                        // Add tool result to chat history
                        _chatHistory.Add(new ChatMessage 
                        { 
                            Role = "assistant",
                            Content = $"Tool execution result: {toolResult}"
                        });
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

                Messages.Add(message);

                // Scroll to bottom
                ChatScrollViewer.ScrollToBottom();
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected override void OnClosed(EventArgs e)
        {
            // Clean up local inference server if running
            if (_localInferenceProcess != null)
            {
                _ = _localModelManager.StopLocalInferenceServerAsync();
                _localInferenceProcess = null;
            }
            
            base.OnClosed(e);
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
        
        public Style BubbleStyle
        {
            get
            {
                var window = Application.Current.MainWindow;
                if (window == null) return null;
                
                return MessageRole switch
                {
                    MessageRole.User => window.FindResource("UserBubble") as Style,
                    MessageRole.Assistant => window.FindResource("AssistantBubble") as Style,
                    MessageRole.System => window.FindResource("SystemBubble") as Style,
                    MessageRole.Error => window.FindResource("SystemBubble") as Style,
                    _ => null
                };
            }
        }
        
        public Brush RoleColor => MessageRole switch
        {
            MessageRole.User => new SolidColorBrush(Color.FromRgb(74, 144, 226)),
            MessageRole.Assistant => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            MessageRole.System => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            MessageRole.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            _ => Brushes.White
        };
    }
} 