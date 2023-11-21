using System.Windows;
using XIVLauncher.Accounts;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class KeepassEntryInputWindow : Window
    {
        public string ResultKeepPassEntryUUID;

        public KeepassEntryInputWindow(XivAccount account)
        {
            InitializeComponent();

            DataContext = new ProfilePictureInputWindowViewModel();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ResultKeepPassEntryUUID = KeepPassEntryUUID.Text;

            Close();
        }
    }
}
