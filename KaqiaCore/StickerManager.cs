using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KaqiaCore
{
    public class StickerManager
    {
        private static readonly string LibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kaqia", "Stickers");

        public static void EnsureLibraryExists()
        {
            if (!Directory.Exists(LibraryPath))
            {
                Directory.CreateDirectory(LibraryPath);
                
                // Copy the default cat logo if it exists in the app directory
                // (optional: for first-time use experience)
            }
        }

        public static List<string> GetStickers()
        {
            EnsureLibraryExists();
            string[] extensions = { "*.png", "*.jpg", "*.jpeg", "*.bmp" };
            var files = new List<string>();
            foreach (var ext in extensions)
            {
                files.AddRange(Directory.GetFiles(LibraryPath, ext));
            }
            return files.OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        public static string AddSticker(string sourcePath)
        {
            EnsureLibraryExists();
            string extension = Path.GetExtension(sourcePath);
            string destinationName = $"{Guid.NewGuid()}{extension}";
            string destinationPath = Path.Combine(LibraryPath, destinationName);
            
            File.Copy(sourcePath, destinationPath, true);
            return destinationPath;
        }

        public static void DeleteSticker(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
