using System.Windows;
using System.Windows.Input; // Mouse hareketi için gerekli
using UsbMonitorAgent.Helpers;

namespace UsbMonitorAgent.UI.Windows
{
    public partial class UserSettingsWindow : Window
    {
        public UserSettingsWindow()
        {
            InitializeComponent();

            // Mevcut kullanıcı adını otomatik doldur
            UsernameBox.Text = SecurityHelper.GetCurrentUsername();

            // Pencereyi sürükleyebilmek için (Çerçevesiz olduğu için şart)
            this.MouseLeftButtonDown += (s, e) => DragMove();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string newUser = UsernameBox.Text;
            string oldPass = OldPassBox.Password;
            string newPass = NewPassBox.Password;
            string newPass2 = NewPassBox2.Password;

            // Basit Kontroller
            if (string.IsNullOrWhiteSpace(newUser) || string.IsNullOrWhiteSpace(oldPass))
            {
                MessageBox.Show("Lütfen kullanıcı adı ve mevcut şifreyi girin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPass != newPass2)
            {
                MessageBox.Show("Yeni şifreler birbiriyle uyuşmuyor!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Kayıt İşlemi
            bool success = SecurityHelper.ChangeCredentials(oldPass, newUser, newPass);

            if (success)
            {
                MessageBox.Show("Bilgiler başarıyla güncellendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            else
            {
                MessageBox.Show("Mevcut şifreniz hatalı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}