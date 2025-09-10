using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace TabgInstaller.Gui.Windows
{
    public partial class SigmaOverlayWindow : Window
    {
        private DispatcherTimer _animationTimer;
        private int _dotCount = 0;
        private bool _isPrimary;

        public SigmaOverlayWindow(Screen screen, bool isPrimary = false)
        {
            InitializeComponent();
            _isPrimary = isPrimary;
            
            // Position window on specified screen
            Left = screen.Bounds.Left;
            Top = screen.Bounds.Top;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
            
            // Show welcome panel only on primary monitor
            if (_isPrimary)
            {
                WelcomePanel.Visibility = Visibility.Visible;
                StartLoadingAnimation();
            }
            
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure window is always on top and non-interactive
            Topmost = true;
        }

        public void SetWelcomeName(string userName)
        {
            if (_isPrimary)
            {
                WelcomeText.Text = $"Welcome, {userName}";
            }
        }

        private void StartLoadingAnimation()
        {
            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(500);
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            _dotCount = (_dotCount + 1) % 4;
            LoadingDots.Text = new string('.', Math.Max(1, _dotCount));
        }

        public async Task FadeOutAsync(int durationMs = 300)
        {
            var fadeSteps = 20;
            var stepDelay = durationMs / fadeSteps;
            
            for (int i = fadeSteps; i >= 0; i--)
            {
                Opacity = (double)i / fadeSteps;
                await Task.Delay(stepDelay);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _animationTimer?.Stop();
            _animationTimer = null;
            base.OnClosed(e);
        }
    }
}
