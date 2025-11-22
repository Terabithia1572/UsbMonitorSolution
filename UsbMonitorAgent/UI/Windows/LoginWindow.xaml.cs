using System;
using System.Windows;
using System.Windows.Input;
using UsbMonitorAgent.Helpers;

namespace UsbMonitorAgent.UI.Windows
{
    public partial class LoginWindow : Window
    {
        public static bool IsAuthenticated { get; private set; } = false;

        public LoginWindow()
        {
            InitializeComponent();
            UsernameBox.Text = SecurityHelper.GetCurrentUsername();

            // Pencereyi sürükleyebilmek için
            this.MouseLeftButtonDown += (s, e) => DragMove();

            UsernameBox.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string user = UsernameBox.Text;
            string pass = PasswordBox.Password;

            if (SecurityHelper.ValidateUser(user, pass))
            {
                IsAuthenticated = true;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                ErrorText.Text = "Hatalı kullanıcı adı veya şifre!";
                // Şifre kutusunu temizle ve odakla
                PasswordBox.Password = "";
                PasswordBox.Focus();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}