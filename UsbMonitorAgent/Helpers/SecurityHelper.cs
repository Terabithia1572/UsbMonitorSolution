using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using UsbMonitorAgent.UI.Windows;

namespace UsbMonitorAgent.Helpers
{
    public static class SecurityHelper
    {
        private const string RegistryPath = @"SOFTWARE\UsbMonitorAgent\Auth";
        private const string DefaultUser = "yonet";
        private const string DefaultPass = "Qq123456";

        // Şifreyi Hash'le (SHA256)
        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        // Kullanıcı Doğrulama
        public static bool ValidateUser(string username, string password)
        {
            EnsureDefaults(); // Kayıt yoksa varsayılanları oluştur

            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key == null) return false;

            string storedUser = key.GetValue("Username")?.ToString() ?? "";
            string storedHash = key.GetValue("PasswordHash")?.ToString() ?? "";

            string inputHash = HashPassword(password);

            return storedUser == username && storedHash == inputHash;
        }

        // Şifre Değiştirme
        public static bool ChangeCredentials(string oldPassword, string newUser, string newPassword)
        {
            // Önce mevcut şifreyi doğrula
            if (!ValidateUser(GetCurrentUsername(), oldPassword)) return false;

            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key.SetValue("Username", newUser);
            key.SetValue("PasswordHash", HashPassword(newPassword));
            return true;
        }

        // Mevcut kullanıcı adını getir (UI için)
        public static string GetCurrentUsername()
        {
            EnsureDefaults();
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue("Username")?.ToString() ?? DefaultUser;
        }

        // Varsayılan ayarları oluştur (İlk kurulum)
        private static void EnsureDefaults()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key == null)
            {
                using var newKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
                newKey.SetValue("Username", DefaultUser);
                newKey.SetValue("PasswordHash", HashPassword(DefaultPass));
            }
        }

        // Login Ekranını Çağırma (Eski kodunla uyumlu)
        public static bool EnsureAuthenticated()
        {
            if (LoginWindow.IsAuthenticated) return true;

            var login = new LoginWindow();
            bool? result = login.ShowDialog();
            return result == true && LoginWindow.IsAuthenticated;
        }
    }
}