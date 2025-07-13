using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;
using TabgInstaller.Core.Services;
using TabgInstaller.Core.Services.AI;
using TabgInstaller.Gui.Dialogs;
using System.Diagnostics;

namespace TabgInstaller.Gui
{
    public partial class AiChatWindow : Window, INotifyPropertyChanged
    {
        private ISecureKeyStore _keyStore = null!;
        private IProviderModelService _providerService = null!;
        private IUnifiedBackend _unifiedBackend = null!;
        private PromptBuilder _promptBuilder = null!;
        private IToolExecutor _toolExecutor = null!;
        private IOllamaBootstrapper _ollamaBootstrapper = null!;
        private readonly string _serverPath;

        private ObservableCollection<ChatMessageViewModel> _messages = new();
        private List<ChatMessage> _chatHistory = new();
        private CancellationTokenSource? _currentRequestCts;

        private List<ProviderConfig> _providers = new();
        private List<ModelInfo> _models = new();
        private ProviderConfig? _selectedProvider;
        private ModelInfo? _selectedModel;
        private bool _isReinstallingOllama = false;

        public ObservableCollection<ChatMessageViewModel> Messages => _messages;
        public List<ProviderConfig> Providers
        {
            get => _providers;
            set { _providers = value; OnPropertyChanged(); }
        }

        public List<ModelInfo> Models
        {
            get => _models;
            set { _models = value; OnPropertyChanged(); }
        }

        public ProviderConfig? SelectedProvider
        {
            get => _selectedProvider;
            set { _selectedProvider = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOllamaSelected)); }
        }

        public ModelInfo? SelectedModel
        {
            get => _selectedModel;
            set { _selectedModel = value; OnPropertyChanged(); }
        }

        public bool IsOllamaSelected => SelectedProvider?.Name == "Ollama";

        public AiChatWindow(string serverPath)
        {
            InitializeComponent();
            
            _serverPath = serverPath ?? throw new ArgumentNullException(nameof(serverPath));
            
            // Initialize collections first
            _messages = new ObservableCollection<ChatMessageViewModel>();
            _chatHistory = new List<ChatMessage>();
            _providers = new List<ProviderConfig>();
            _models = new List<ModelInfo>();
            
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
                _promptBuilder = new PromptBuilder();
                _toolExecutor = new ToolExecutor(_serverPath, async () => await ReloadOllama());
                _ollamaBootstrapper = new OllamaBootstrapper();
                
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

        private async Task ReloadOllama()
        {
            // Delete and reinstall Ollama model
            if (_ollamaBootstrapper != null)
            {
                await _ollamaBootstrapper.RemoveModelAsync("deepseek-r1:8b");
                await _ollamaBootstrapper.InstallModelAsync("deepseek-r1:8b");
            }
        }

        private async Task InitializeAsync()
        {
            if (_providerService == null)
                throw new InvalidOperationException("ProviderService is null");

            Providers = _providerService.GetProviders();
            
            if (Providers == null || Providers.Count == 0)
                throw new InvalidOperationException("No providers found");

            // Check if user has any API keys or should use Ollama
            var hasApiKey = false;
            string? configuredProvider = null;

            foreach (var provider in Providers.Where(p => p != null && p.Name != "Ollama"))
            {
                if (provider.Name != null && _keyStore.HasKey(provider.Name))
                {
                    hasApiKey = true;
                    configuredProvider = provider.Name;
                    break;
                }
            }

            if (!hasApiKey)
            {
                // Show API key dialog
                var dialog = new ApiKeyDialog(_keyStore, _unifiedBackend, _ollamaBootstrapper);
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true && dialog.ConfigurationSaved)
                {
                    configuredProvider = dialog.SelectedProvider;
                }
                else
                {
                    // User cancelled, close window
                    Close();
                    return;
                }
            }

            // Select the configured provider
            SelectedProvider = Providers.FirstOrDefault(p => p.Name == configuredProvider);
            if (SelectedProvider != null)
            {
                UpdateModelsForProvider();
            }

            // Add system prompt
            var systemPrompt = _promptBuilder.BuildSystemPrompt(_serverPath);
            _chatHistory.Add(ChatMessage.System(systemPrompt));

            // Add welcome message
            AddMessage("Assistant", "Hello! I'm your TABG server configuration assistant. I can help you configure game settings, manage plugins, and set up your server. What would you like to do?", true);
        }

        private void UpdateModelsForProvider()
        {
            if (SelectedProvider != null)
            {
                Models = _providerService.GetModelsForProvider(SelectedProvider.Name);
                
                // Debug: Show how many models were loaded
                AddMessage("System", $"Loaded {Models.Count} models for {SelectedProvider.Name}", false);
                foreach (var model in Models)
                {
                    AddMessage("System", $"  - {model.DisplayName} ({model.Id})", false);
                }
                
                SelectedModel = Models.FirstOrDefault();
            }
        }

        private void ProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateModelsForProvider();
        }

        private void ChangeApiKey_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ApiKeyDialog(_keyStore, _unifiedBackend, _ollamaBootstrapper);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true && dialog.ConfigurationSaved)
            {
                // Refresh providers after API key change
                SelectedProvider = Providers.FirstOrDefault(p => p.Name == dialog.SelectedProvider);
                UpdateModelsForProvider();
            }
        }

        private void ManageModels_Click(object sender, RoutedEventArgs e)
        {
            // Populate context menu with available models
            ModelsContextMenu.Items.Clear();
            
            foreach (var model in Models)
            {
                var modelMenu = new MenuItem { Header = model.DisplayName };
                
                var downloadItem = new MenuItem 
                { 
                    Header = "Download/Update",
                    Tag = model.Id
                };
                downloadItem.Click += async (s, args) => await DownloadModel(model.Id);
                
                var reinstallItem = new MenuItem 
                { 
                    Header = "Reinstall (Delete & Download)",
                    Tag = model.Id
                };
                reinstallItem.Click += async (s, args) => await ReinstallModel(model.Id);
                
                var deleteItem = new MenuItem 
                { 
                    Header = "Delete",
                    Tag = model.Id
                };
                deleteItem.Click += async (s, args) => await DeleteModel(model.Id);
                
                modelMenu.Items.Add(downloadItem);
                modelMenu.Items.Add(reinstallItem);
                modelMenu.Items.Add(deleteItem);
                
                ModelsContextMenu.Items.Add(modelMenu);
            }
            
            // Open the context menu
            ModelsContextMenu.IsOpen = true;
        }
        
        private async Task DownloadModel(string modelId)
        {
            AddMessage("System", $"Downloading model {modelId}...", false);
            
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = $"pull {modelId}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            
            if (process != null)
            {
                var progressTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            // Remove ANSI escape codes
                            line = System.Text.RegularExpressions.Regex.Replace(line, @"\x1B\[[^@-~]*[@-~]", "");
                            line = System.Text.RegularExpressions.Regex.Replace(line, @"\[\?[0-9]+[hl]", "");
                            line = System.Text.RegularExpressions.Regex.Replace(line, @"\[K", "");
                            line = line.Trim();
                            
                            if (!string.IsNullOrEmpty(line))
                            {
                                await Dispatcher.InvokeAsync(() => AddMessage("System", line, false));
                            }
                        }
                    }
                });
                
                await process.WaitForExitAsync();
                await progressTask;
                
                if (process.ExitCode == 0)
                {
                    AddMessage("System", $"Model {modelId} downloaded successfully!", false);
                }
                else
                {
                    AddMessage("System", $"Failed to download model {modelId}", false);
                }
            }
        }
        
        private async Task DeleteModel(string modelId)
        {
            var result = MessageBox.Show($"Are you sure you want to delete model {modelId}?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                AddMessage("System", $"Deleting model {modelId}...", false);
                
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = $"rm {modelId}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        AddMessage("System", $"Model {modelId} deleted successfully!", false);
                    }
                    else
                    {
                        AddMessage("System", $"Failed to delete model {modelId}", false);
                    }
                }
            }
        }
        
        private async Task ReinstallModel(string modelId)
        {
            await DeleteModel(modelId);
            await Task.Delay(1000); // Brief delay between operations
            await DownloadModel(modelId);
        }
        
        private async void ReinstallOllama_Click(object sender, RoutedEventArgs e)
        {
            // Prevent multiple simultaneous reinstalls
            if (_isReinstallingOllama)
            {
                MessageBox.Show("A reinstall is already in progress. Please wait.", "Reinstall In Progress", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "This will delete and reinstall the Ollama AI model (deepseek-r1:8b).\n\n" +
                "This process may take several minutes.\n\n" +
                "Continue?",
                "Reinstall Ollama Model",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _isReinstallingOllama = true;
                
                try
                {
                    // Disable the button while processing
                    var button = sender as Button;
                    if (button != null)
                        button.IsEnabled = false;

                    var modelName = SelectedModel?.Id ?? "llama3.2:latest";
                    
                    // First, check if Ollama is running
                    AddMessage("System", "Checking if Ollama is running...", false);
                    
                    var checkProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "ollama",
                        Arguments = "list",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    
                    if (checkProcess == null)
                    {
                        throw new Exception("Ollama command not found. Please ensure Ollama is installed.");
                    }
                    
                    await checkProcess.WaitForExitAsync();
                    
                    if (checkProcess.ExitCode != 0)
                    {
                        // Try to start Ollama
                        AddMessage("System", "Starting Ollama service...", false);
                        var startProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ollama",
                            Arguments = "serve",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        
                        // Wait a bit for it to start
                        await Task.Delay(3000);
                    }
                    
                    // Step 1: Delete the model (if it exists)
                    AddMessage("System", $"Checking if model {modelName} exists...", false);
                    
                    var deleteProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "ollama",
                        Arguments = $"rm {modelName}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    
                    if (deleteProcess != null)
                    {
                        var deleteOutput = await deleteProcess.StandardOutput.ReadToEndAsync();
                        var deleteError = await deleteProcess.StandardError.ReadToEndAsync();
                        await deleteProcess.WaitForExitAsync();
                        
                        if (!string.IsNullOrEmpty(deleteError) && !deleteError.Contains("not found"))
                        {
                            AddMessage("System", $"Delete warning: {deleteError}", false);
                        }
                        else
                        {
                            AddMessage("System", "Model deleted successfully.", false);
                        }
                    }
                    
                    // Wait a moment to ensure deletion is processed
                    await Task.Delay(1000);
                    
                    // Step 2: Pull the model fresh
                    AddMessage("System", $"Downloading model {modelName}... This will take several minutes.", false);
                    AddMessage("System", "Progress updates will appear below:", false);
                    
                    var pullProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "ollama",
                        Arguments = $"pull {modelName}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    
                    if (pullProcess != null)
                    {
                        var progressTask = Task.Run(async () =>
                        {
                            string? line;
                            while ((line = await pullProcess.StandardOutput.ReadLineAsync()) != null)
                            {
                                if (!string.IsNullOrEmpty(line))
                                {
                                    // Remove ANSI escape codes
                                    line = System.Text.RegularExpressions.Regex.Replace(line, @"\x1B\[[^@-~]*[@-~]", "");
                                    line = System.Text.RegularExpressions.Regex.Replace(line, @"\[\?[0-9]+[hl]", "");
                                    line = System.Text.RegularExpressions.Regex.Replace(line, @"\[K", "");
                                    line = line.Trim();
                                    
                                    if (!string.IsNullOrEmpty(line))
                                    {
                                        await Dispatcher.InvokeAsync(() => AddMessage("System", line, false));
                                    }
                                }
                            }
                        });
                        
                        var errorTask = Task.Run(async () =>
                        {
                            string? errorLine;
                            while ((errorLine = await pullProcess.StandardError.ReadLineAsync()) != null)
                            {
                                if (!string.IsNullOrEmpty(errorLine))
                                {
                                    // Remove ANSI escape codes
                                    errorLine = System.Text.RegularExpressions.Regex.Replace(errorLine, @"\x1B\[[^@-~]*[@-~]", "");
                                    errorLine = System.Text.RegularExpressions.Regex.Replace(errorLine, @"\[\?[0-9]+[hl]", "");
                                    errorLine = System.Text.RegularExpressions.Regex.Replace(errorLine, @"\[K", "");
                                    errorLine = errorLine.Trim();
                                    
                                    if (!string.IsNullOrEmpty(errorLine))
                                    {
                                        await Dispatcher.InvokeAsync(() => AddMessage("System", $"Error: {errorLine}", false));
                                    }
                                }
                            }
                        });
                        
                        await pullProcess.WaitForExitAsync();
                        await Task.WhenAll(progressTask, errorTask);
                        
                        if (pullProcess.ExitCode == 0)
                        {
                            AddMessage("System", "Model reinstalled successfully!", false);
                        }
                        else
                        {
                            var error = await pullProcess.StandardError.ReadToEndAsync();
                            throw new Exception($"Failed to download model. Error: {error}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddMessage("System", $"Error: {ex.Message}", false);
                    MessageBox.Show(
                        $"Failed to reinstall Ollama model:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    // Re-enable the button
                    var button = sender as Button;
                    if (button != null)
                        button.IsEnabled = true;
                    
                    _isReinstallingOllama = false;
                }
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

            if (SelectedProvider == null || SelectedModel == null)
            {
                MessageBox.Show("Please select a provider and model.", "Configuration Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Add user message
            AddMessage("User", message, false);
            _chatHistory.Add(ChatMessage.User(message));

            // Clear input
            UserInput.Clear();
            SendButton.IsEnabled = false;

            // Cancel any ongoing request
            _currentRequestCts?.Cancel();
            _currentRequestCts = new CancellationTokenSource();

            try
            {
                // Get available functions
                var functions = _toolExecutor.GetAvailableFunctions();

                // Send to AI
                var result = await _unifiedBackend.SendAsync(
                    SelectedProvider.Name,
                    SelectedModel.Id,
                    _chatHistory.ToArray(),
                    functions,
                    _currentRequestCts.Token);

                if (!result.Success)
                {
                    AddMessage("Error", result.ErrorMessage ?? "Unknown error", true);
                    return;
                }

                // Add assistant response
                if (!string.IsNullOrEmpty(result.AssistantMessage))
                {
                    AddMessage("Assistant", result.AssistantMessage, true);
                    _chatHistory.Add(ChatMessage.Assistant(result.AssistantMessage));
                }

                // Process tool calls
                if (result.ToolCalls.Any())
                {
                    var toolCallsJson = JsonConvert.SerializeObject(result.ToolCalls, Formatting.Indented);
                    RawToolCallsTextBox.Text = toolCallsJson;

                    foreach (var toolCall in result.ToolCalls)
                    {
                        var toolResult = _toolExecutor.ExecuteToolCall(toolCall, _serverPath);
                        AddMessage("Tool Result", $"[{toolCall.Function.Name}] {toolResult}", true);
                        
                        // Add tool result to chat history as assistant message (Anthropic doesn't support "tool" role)
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
                AddMessage("System", "Request cancelled", true);
            }
            catch (Exception ex)
            {
                AddMessage("Error", $"Exception: {ex.Message}", true);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private void AddMessage(string role, string content, bool isAssistant)
        {
            Dispatcher.Invoke(() =>
            {
                var message = new ChatMessageViewModel
                {
                    Role = role,
                    Content = content,
                    Background = isAssistant ? new SolidColorBrush(Color.FromRgb(240, 240, 240)) 
                                            : new SolidColorBrush(Color.FromRgb(220, 240, 255)),
                    RoleColor = role switch
                    {
                        "User" => Brushes.Blue,
                        "Assistant" => Brushes.Green,
                        "Error" => Brushes.Red,
                        "Tool Result" => Brushes.Purple,
                        _ => Brushes.Black
                    }
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
    }

    public class ChatMessageViewModel
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public Brush Background { get; set; } = Brushes.White;
        public Brush RoleColor { get; set; } = Brushes.Black;
    }
} 