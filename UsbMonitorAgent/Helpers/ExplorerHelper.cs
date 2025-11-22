using System;
using System.IO;
using Shell32;
using SHDocVw;

namespace UsbMonitorAgent
{
    public static class ExplorerHelper
    {
        public static string FindPathInExplorerOrDesktop(string targetName, string excludeDriveRoot)
        {
            // 1. ÖNCELİK: AÇIK PENCERELER (Gerçek kaynak burasıdır)
            // Masaüstü kontrolünü SONA attık ki C:\ klasörleri karışmasın.
            try
            {
                var shellWindows = new ShellWindows();
                foreach (InternetExplorer window in shellWindows)
                {
                    string source = TryGetPathFromWindow(window, targetName, excludeDriveRoot);
                    if (!string.IsNullOrEmpty(source)) return source;
                }
            }
            catch { }

            // 2. ÖNCELİK: MASAÜSTÜ (Hard Check - Eğer pencere kapalıysa)
            string desktopSource = CheckDesktopDirectly(targetName);
            if (!string.IsNullOrEmpty(desktopSource)) return desktopSource;

            return null;
        }

        private static string CheckDesktopDirectly(string folderOrFileName)
        {
            try
            {
                string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string userPath = Path.Combine(userDesktop, folderOrFileName);
                if (Directory.Exists(userPath) || File.Exists(userPath)) return userPath;

                string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string commonPath = Path.Combine(commonDesktop, folderOrFileName);
                if (Directory.Exists(commonPath) || File.Exists(commonPath)) return commonPath;
            }
            catch { }
            return null;
        }

        private static string TryGetPathFromWindow(InternetExplorer window, string targetName, string excludeDrive)
        {
            try
            {
                if (window == null) return null;

                string folderPath = "";
                try
                {
                    string url = window.LocationURL;
                    if (!string.IsNullOrEmpty(url) && url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    {
                        folderPath = new Uri(url).LocalPath;
                    }
                    else
                    {
                        dynamic doc = window.Document;
                        folderPath = doc.Folder.Self.Path;
                    }
                }
                catch { return null; }

                // Filtre: Hedef USB sürücüsünü (F:\) yoksay
                if (!string.IsNullOrEmpty(folderPath) &&
                    folderPath.StartsWith(excludeDrive, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                // 1. Seçili öğe kontrolü
                try
                {
                    dynamic doc = window.Document;
                    dynamic selectedItems = doc.SelectedItems();
                    if (selectedItems != null)
                    {
                        foreach (FolderItem item in selectedItems)
                        {
                            if (item.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                                return item.Path;
                        }
                    }
                }
                catch { }

                // 2. Klasör içeriği kontrolü
                if (!string.IsNullOrEmpty(folderPath))
                {
                    string potentialPath = Path.Combine(folderPath, targetName);
                    if (File.Exists(potentialPath) || Directory.Exists(potentialPath)) return potentialPath;
                }
            }
            catch { }

            return null;
        }
    }
}