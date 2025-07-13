using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StarterPackSetup
{
    /// <summary>
    /// Interaction logic for MapScreen.xaml
    /// </summary>
    public partial class MapScreen : Page
    {
        double scale;
        List<double> savedSizes = new List<double> { };
        public MapScreen()
        {
            InitializeComponent();

            if (ScaleText != null)
            {
                ScaleText.Text = "Size: " + scale;
            }
        }
        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point clickPosition = e.GetPosition((IInputElement)sender);
            // Convert to Vector3. You'll need to replace this with your own conversion logic.
            Vector3 position = new Vector3((float)clickPosition.X, 0, (float)clickPosition.Y);
            position -= new Vector3(222, 0, 209); //Image is 456x456
            position *= new Vector3(4, 0, -4);
            // Use position here.
            Debug.WriteLine(string.Format("{0},{1},{2}", position.x, position.y, position.z));
            Debug.WriteLine(string.Format("{0}", scale));
            PositionText.Text = string.Format("{0},{1},{2}", (int)position.x, (int)position.y, (int)position.z);

            Ellipse circle = new Ellipse
            {
                Width = scale / 3.87,
                Height = scale / 3.87,
                Stroke = Brushes.Black,
                Fill = Brushes.Transparent
            };

            // Set the origin point of the circle to match the click position
            Canvas.SetLeft(circle, clickPosition.X - circle.Width / 2);
            Canvas.SetTop(circle, clickPosition.Y - circle.Width / 2);

            // Add the circle to the grid containing the image
            Canvas.SetZIndex(circle, 1); // Ensure the circle is visible above the image
            ((Canvas)((System.Windows.Controls.Image)sender).Parent).Children.Add(circle);
        }
        private void Image_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is Ellipse)
            {
                ((Canvas)sender).Children.Remove((UIElement)e.Source);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new MainMenu());
        }

        private void UpdateEllipseScale(double newScale)
        {
            foreach (UIElement element in MapCanvas.Children)
            {
                if (element is Ellipse)
                {
                    Ellipse ellipse = (Ellipse)element;
                    // Calculate the center point of the ellipse
                    double centerX = Canvas.GetLeft(ellipse) + ellipse.Width / 2;
                    double centerY = Canvas.GetTop(ellipse) + ellipse.Height / 2;

                    // Update the width and height of the ellipse based on the new scale value
                    ellipse.Width = newScale / 4.1; // originalWidth is the initial width of the ellipse
                    ellipse.Height = newScale / 4.1; // originalHeight is the initial height of the ellipse

                    Canvas.SetLeft(ellipse, centerX - ellipse.Width / 2);
                    Canvas.SetTop(ellipse, centerY - ellipse.Height / 2);
                }
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            scale = (int)e.NewValue;
            UpdateEllipseScale(scale);
            if (ScaleText != null)
            {
                ScaleText.Text = "Size: " + scale;
            }
            // Use scale here.
        }

        private void SaveSize_Click(object sender, RoutedEventArgs e)
        {
            savedSizes.Add(scale);
        }

        private void ManageRings_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new ManageRings());
        }

        private void SaveRing_Click(object sender, RoutedEventArgs e)
        {
            string l = "";
            string n = string.Format("{0}:{1}%{2}:", RingNameTextBox.Text, RingRareTextBox.Text, PositionText.Text);
            foreach (double str in savedSizes)
            {
                l += str;
                l += ",";
            }
            if (l.Length > 1)
            {
                l = l.Remove(l.Length - 1);
                l += "/";
                n += l;
                Debug.WriteLine(n.ToString());

                savedSizes.Clear();
                RingRareTextBox.Text = "";
                RingNameTextBox.Text = "";
                PositionText.Text = "";

                string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TheStarterPack.txt");
                //Overwrite config file with new loadout info
                string[] lines = File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("RingSettings="))
                    {
                        lines[i] += n;
                        break;
                    }
                }
                File.WriteAllLines(path, lines);
            }
        }
    }
}
