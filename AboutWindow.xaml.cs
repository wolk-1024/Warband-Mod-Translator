using System.Windows;
using System.Windows.Controls;

namespace WarbandAbout
{
    public partial class AboutWindow : Window
    {
        private void InitAboutWindow()
        {
            this.Width = 650;

            this.Height = 400;

            this.ResizeMode = ResizeMode.NoResize;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.ButtonAboutOk.Click += ButtonAboutOk_Click;
        }

        public AboutWindow()
        {
            InitializeComponent();

            InitAboutWindow();
        }

        private void ButtonAboutOk_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
