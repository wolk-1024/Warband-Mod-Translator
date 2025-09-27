using System.Windows;

namespace WarbandAbout
{
    public partial class AboutWindow : Window
    {
        private void InitAboutWindow()
        {
            this.Width = 350;

            this.Height = 250;

            this.ResizeMode = ResizeMode.NoResize;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
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
