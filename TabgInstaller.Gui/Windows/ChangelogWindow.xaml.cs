using System;
using System.Windows;
using System.Windows.Controls;
using TabgInstaller.Gui.Converters;

namespace TabgInstaller.Gui.Windows
{
    public partial class ChangelogWindow : Window
    {
        public string? SkippedVersion { get; private set; }

        private readonly string _tagName;

        public ChangelogWindow(Version currentVersion, Version newVersion, string? releaseNotes, string tagName)
        {
            InitializeComponent();

            _tagName = tagName;

            VersionLabel.Text = $"v{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}  \u2192  v{newVersion.Major}.{newVersion.Minor}.{newVersion.Build}";

            if (string.IsNullOrWhiteSpace(releaseNotes))
            {
                NotesPanel.Children.Add(new TextBlock
                {
                    Text = "No release notes available for this version.",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                foreach (var element in MarkdownRenderer.RenderMarkdown(releaseNotes))
                {
                    NotesPanel.Children.Add(element);
                }
            }
        }

        private void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void SkipVersion_Click(object sender, RoutedEventArgs e)
        {
            SkippedVersion = _tagName;
            DialogResult = false;
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
