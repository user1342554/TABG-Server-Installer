using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TabgInstaller.Gui.Services;

namespace TabgInstaller.Gui.Controls
{
    public partial class ToastNotification : UserControl
    {
        private readonly DispatcherTimer _dismissTimer = new();

        public ToastNotification()
        {
            InitializeComponent();
            _dismissTimer.Tick += (_, _) => Hide();
        }

        public void Show(string message, ToastType type, int durationMs = 4000)
        {
            _dismissTimer.Stop();

            TxtMessage.Text = message;

            var (bg, icon) = type switch
            {
                ToastType.Success => ("#2E7D32", "OK"),
                ToastType.Error   => ("#C62828", "!!"),
                ToastType.Warning => ("#E65100", "!"),
                ToastType.Info    => ("#1565C0", "i"),
                _ => ("#323232", "")
            };

            ToastBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            TxtIcon.Text = icon;

            Visibility = Visibility.Visible;
            Opacity = 1;
            BeginAnimation(OpacityProperty, null); // Clear any running animation

            _dismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _dismissTimer.Start();
        }

        private void Hide()
        {
            _dismissTimer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (_, _) => Visibility = Visibility.Collapsed;
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e) => Hide();
    }
}
