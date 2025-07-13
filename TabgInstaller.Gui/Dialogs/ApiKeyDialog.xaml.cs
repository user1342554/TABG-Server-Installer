using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Core.Services;
using TabgInstaller.Core.Services.AI;

namespace TabgInstaller.Gui.Dialogs
{
    public partial class ApiKeyDialog : Window
    {
        private readonly ISecureKeyStore _keyStore;
        private readonly IUnifiedBackend _unifiedBackend;
        private readonly IOllamaBootstrapper _ollamaBootstrapper;
        private CancellationTokenSource? _validationCts;

        public string? SelectedProvider { get; private set; }
        public bool ConfigurationSaved { get; private set; }

        public ApiKeyDialog(ISecureKeyStore keyStore, IUnifiedBackend unifiedBackend, IOllamaBootstrapper ollamaBootstrapper)
        {
            InitializeComponent();
            _keyStore = keyStore;
            _unifiedBackend = unifiedBackend;
            _ollamaBootstrapper = ollamaBootstrapper;
            
            // Handle initial selection after window is loaded
            Loaded += (s, e) =>
            {
                if (ProviderComboBox?.SelectedItem is ComboBoxItem item)
                {
                    var provider = item.Tag?.ToString() ?? "";
                    UpdateUIForProvider(provider);
                }
            };
        }

        private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if controls aren't initialized yet
            if (!IsLoaded) return;
            
            if (ProviderComboBox?.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? "";
                UpdateUIForProvider(provider);
            }
        }

        private void UpdateUIForProvider(string provider)
        {
            // Ensure all controls are initialized
            if (ApiKeyLabel == null || ApiKeyBox == null || HelpText == null || 
                SaveButton == null || ValidationMessage == null) 
                return;
                
            if (provider == "Ollama")
            {
                ApiKeyLabel.Visibility = Visibility.Collapsed;
                ApiKeyBox.Visibility = Visibility.Collapsed;
                HelpText.Visibility = Visibility.Visible;
                HelpText.Text = "No API key required for local AI. Click Save to install Ollama.";
                SaveButton.IsEnabled = true;
                ValidationMessage.Visibility = Visibility.Collapsed;
            }
            else
            {
                ApiKeyLabel.Visibility = Visibility.Visible;
                ApiKeyBox.Visibility = Visibility.Visible;
                HelpText.Visibility = Visibility.Visible;
                
                // Load existing key if available
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

                SaveButton.IsEnabled = !string.IsNullOrEmpty(ApiKeyBox.Password);
            }
        }

        private async void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            ValidationMessage.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(ApiKeyBox.Password))
                return;

            // Cancel previous validation
            _validationCts?.Cancel();
            _validationCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(500, _validationCts.Token); // Debounce

                if (ProviderComboBox.SelectedItem is ComboBoxItem item)
                {
                    var provider = item.Tag?.ToString() ?? "";
                    ValidationMessage.Text = "Validating API key...";
                    ValidationMessage.Foreground = System.Windows.Media.Brushes.Gray;
                    ValidationMessage.Visibility = Visibility.Visible;

                    var isValid = await _unifiedBackend.ValidateApiKeyAsync(provider, ApiKeyBox.Password, _validationCts.Token);

                    if (isValid)
                    {
                        ValidationMessage.Text = "API key is valid!";
                        ValidationMessage.Foreground = System.Windows.Media.Brushes.Green;
                        SaveButton.IsEnabled = true;
                    }
                    else
                    {
                        ValidationMessage.Text = "Invalid API key";
                        ValidationMessage.Foreground = System.Windows.Media.Brushes.Red;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Validation was cancelled
            }
            catch (Exception ex)
            {
                ValidationMessage.Text = $"Error validating key: {ex.Message}";
                ValidationMessage.Foreground = System.Windows.Media.Brushes.Red;
                ValidationMessage.Visibility = Visibility.Visible;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? "";
                SelectedProvider = provider;

                if (provider == "Ollama")
                {
                    // Handle Ollama installation
                    var progressDialog = new ProgressDialog();
                    progressDialog.Owner = this;
                    
                    var progress = new Progress<string>(msg => 
                    {
                        Dispatcher.Invoke(() => progressDialog.UpdateMessage(msg));
                    });

                    var installTask = Task.Run(async () =>
                    {
                        if (!await _ollamaBootstrapper.EnsureOllamaInstalledAsync(progress))
                        {
                            throw new Exception("Failed to install Ollama");
                        }

                        await _ollamaBootstrapper.StartOllamaServerAsync();
                        
                        if (!await _ollamaBootstrapper.EnsureModelInstalledAsync("llama3.2:latest", progress))
                        {
                            throw new Exception("Failed to install model");
                        }
                    });

                    try
                    {
                        progressDialog.ShowDialog();
                        await installTask;
                        ConfigurationSaved = true;
                        DialogResult = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error setting up Ollama: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Save API key
                    if (!string.IsNullOrEmpty(ApiKeyBox.Password))
                    {
                        _keyStore.SaveKey(provider, ApiKeyBox.Password);
                        ConfigurationSaved = true;
                        DialogResult = true;
                    }
                }
            }
        }
    }

    // Simple progress dialog
    public partial class ProgressDialog : Window
    {
        private TextBlock _messageBlock;

        public ProgressDialog()
        {
            Title = "Setting up AI";
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            _messageBlock = new TextBlock 
            { 
                Text = "Initializing...",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Children.Add(_messageBlock);
            Content = grid;
        }

        public void UpdateMessage(string message)
        {
            _messageBlock.Text = message;
        }
    }
} 