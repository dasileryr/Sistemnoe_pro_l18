using System.Windows;

namespace UserMonitorApp
{
    public partial class ReportViewWindow : Window
    {
        public ReportViewWindow(string title, string content)
        {
            InitializeComponent();
            TitleTextBlock.Text = title;
            ContentTextBox.Text = content;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

