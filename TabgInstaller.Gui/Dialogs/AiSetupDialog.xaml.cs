using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabgInstaller.Core.Services;
using TabgInstaller.Core.Services.AI;
using MessageBox = System.Windows.MessageBox;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace TabgInstaller.Gui.Dialogs
{
    public partial class AiSetupDialog : Window
    {
        private readonly ISecureKeyStore _keyStore;
        private readonly IUnifiedBackend _unifiedBackend;
        private readonly ILocalModelManager _modelManager;
        private ObservableCollection<ModelDownloadViewModel> _modelViewModels;
        private CancellationTokenSource? _downloadCts;
        private CancellationTokenSource? _validationCts;

        public enum SetupMode { None, Local, Online }
        public SetupMode SelectedMode { get; private set; } = SetupMode.None;
        public string? SelectedProvider { get; private set; }
        public string? SelectedModel { get; private set; }
        public bool SetupCompleted { get; private set; }

        public AiSetupDialog(ISecureKeyStore keyStore, IUnifiedBackend unifiedBackend, ILocalModelManager modelManager)
        {
            InitializeComponent();
            _keyStore = keyStore;
            _unifiedBackend = unifiedBackend;
            _modelManager = modelManager;
            _modelViewModels = new ObservableCollection<ModelDownloadViewModel>();
            
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Set default model directory
            var defaultDir = _modelManager.GetModelsDirectory();
            ModelDirectoryTextBox.Text = defaultDir;
            await UpdateSpaceInfo();
            
            // Load available models
            await LoadAvailableModels();
            
            // Set default selection for ProviderComboBox after everything is loaded
            if (ProviderComboBox != null && ProviderComboBox.Items.Count > 0)
            {
                ProviderComboBox.SelectedIndex = 0; // Select OpenAI by default
            }
        }

        private async Task LoadAvailableModels()
        {
            var models = await _modelManager.GetAvailableModelsAsync();
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
                    if (string.IsNullOrEmpty(path))
                        return;

                    var drive = Path.GetPathRoot(path);
                    if (string.IsNullOrEmpty(drive))
                        return;

                    var driveInfo = new DriveInfo(drive);
                    var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    
                    Dispatcher.Invoke(() =>
                    {
                        SpaceInfoText.Text = $"Available space: {availableGB:F1} GB";
                        SpaceInfoText.Foreground = availableGB > 250 ? Brushes.Green : 
                                                  availableGB > 50 ? Brushes.Orange : Brushes.Red;
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
            SelectedMode = SetupMode.Local;
            SelectionPanel.Visibility = Visibility.Collapsed;
            LocalDownloadPanel.Visibility = Visibility.Visible;
        }

        private void OnlineOption_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedMode = SetupMode.Online;
            SelectionPanel.Visibility = Visibility.Collapsed;
            OnlineSetupPanel.Visibility = Visibility.Visible;
        }

        private async void ExistingModels_Click(object sender, RoutedEventArgs e)
        {
            // Check if any models are already installed
            var models = await _modelManager.GetAvailableModelsAsync();
            var installedModels = models.Where(m => m.IsInstalled).ToList();
            
            if (!installedModels.Any())
            {
                MessageBox.Show("No local models found. Please download models first.", 
                    "No Models Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Let user select from installed models
            var dialog = new Window
            {
                Title = "Select Installed Model",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock 
            { 
                Text = "Select an installed model:", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var listBox = new System.Windows.Controls.ListBox { Height = 150 };
            foreach (var model in installedModels)
            {
                listBox.Items.Add(new ListBoxItem 
                { 
                    Content = model.DisplayName,
                    Tag = model.ModelId
                });
            }
            listBox.SelectedIndex = 0;
            panel.Children.Add(listBox);

            var buttonPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal, 
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            var okButton = new System.Windows.Controls.Button 
            { 
                Content = "Use This Model",
                Width = 120,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(10, 5, 10, 5)
            };
            okButton.Click += (s, args) =>
            {
                if (listBox.SelectedItem is ListBoxItem item)
                {
                    SelectedMode = SetupMode.Local;
                    SelectedModel = item.Tag?.ToString();
                    SetupCompleted = true;
                    dialog.DialogResult = true;
                }
            };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new System.Windows.Controls.Button 
            { 
                Content = "Cancel",
                Width = 80,
                Padding = new Thickness(10, 5, 10, 5)
            };
            cancelButton.Click += (s, args) => dialog.DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            if (dialog.ShowDialog() == true)
            {
                DialogResult = true;
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
                _modelManager.SetModelsDirectory(dialog.SelectedPath);
                _ = UpdateSpaceInfo();
            }
        }

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            var selectedModels = _modelViewModels.Where(m => m.IsSelected && !m.IsInstalled).ToList();
            
            if (!selectedModels.Any())
            {
                MessageBox.Show("Please select at least one model to download.", 
                    "No Models Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DownloadButton.IsEnabled = false;
            BackButton.IsEnabled = false;
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

                    await _modelManager.DownloadModelAsync(
                        modelVm.ModelId, 
                        ModelDirectoryTextBox.Text,
                        progress,
                        _downloadCts.Token);

                    modelVm.IsInstalled = true;
                    modelVm.IsDownloading = false;
                }

                // Select the first downloaded model
                SelectedModel = selectedModels.First().ModelId;
                SetupCompleted = true;
                
                MessageBox.Show("Models downloaded successfully! You can now start using the AI assistant.",
                    "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Download cancelled.", "Cancelled", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading models: {ex.Message}", "Download Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadButton.IsEnabled = true;
                BackButton.IsEnabled = true;
                
                foreach (var modelVm in selectedModels)
                {
                    modelVm.IsDownloading = false;
                }
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            _validationCts?.Cancel();
            DialogResult = false;
        }

        private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if controls aren't initialized yet
            if (!IsLoaded || ProviderComboBox == null || ApiKeyBox == null || 
                HelpText == null || SaveApiKeyButton == null)
                return;
                
            if (ProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? "";
                
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

                SaveApiKeyButton.IsEnabled = !string.IsNullOrEmpty(ApiKeyBox.Password);
            }
        }

        private async void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // Skip if controls aren't initialized yet
            if (!IsLoaded || SaveApiKeyButton == null || ValidationMessage == null || 
                ApiKeyBox == null || ProviderComboBox == null)
                return;
                
            SaveApiKeyButton.IsEnabled = false;
            ValidationMessage.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(ApiKeyBox.Password))
                return;

            _validationCts?.Cancel();
            _validationCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(500, _validationCts.Token); // Debounce

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
            catch (OperationCanceledException)
            {
                // Validation was cancelled
            }
            catch (Exception ex)
            {
                ValidationMessage.Text = $"Error validating key: {ex.Message}";
                ValidationMessage.Foreground = Brushes.Red;
                ValidationMessage.Visibility = Visibility.Visible;
            }
        }

        private void SaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (ProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var provider = item.Tag?.ToString() ?? "";
                SelectedProvider = provider;

                if (!string.IsNullOrEmpty(ApiKeyBox.Password))
                {
                    _keyStore.SaveKey(provider, ApiKeyBox.Password);
                    SetupCompleted = true;
                    DialogResult = true;
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _downloadCts?.Cancel();
            _validationCts?.Cancel();
            base.OnClosing(e);
        }
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

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set 
            { 
                _isInstalled = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set 
            { 
                _isDownloading = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ProgressVisibility));
            }
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public string ProgressText
        {
            get => _progressText;
            set { _progressText = value; OnPropertyChanged(); }
        }

        public string StatusText => IsInstalled ? "✓ Installed" : "Not installed";
        public Brush StatusColor => IsInstalled ? Brushes.Green : Brushes.Gray;
        public Visibility ProgressVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
