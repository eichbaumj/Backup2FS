using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Backup2FS
{
    class IconCreator
    {
        // Renamed from Main to CreateIcon to avoid multiple entry points
        public static void CreateIcon()
        {
            Console.WriteLine("Creating icon from logo...");
            
            try
            {
                // Find the root directory of the project
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..")); 
                
                // Path to the logo
                string logoPath = Path.Combine(projectDir, "Resources", "Images", "logo.png");
                string iconOutputPath = Path.Combine(projectDir, "Resources", "Icons", "app_icon.ico");
                
                Console.WriteLine($"Loading logo from: {logoPath}");
                
                // Load the image
                using var bitmap = new Bitmap(logoPath);
                
                // Fix: Use CreateIconFromBitmap directly
                using var icon = CreateIconFromBitmap(bitmap);
                
                // Save the icon to the output path
                using var fs = new FileStream(iconOutputPath, FileMode.Create);
                icon.Save(fs);
                
                Console.WriteLine($"Icon saved to: {iconOutputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static Icon CreateIconFromBitmap(Bitmap bitmap)
        {
            // Create an icon with standard sizes
            var sizes = new[] { 16, 32, 48, 64 };
            
            // Create a bitmap of the desired size (using 32x32)
            using var resizedBitmap = new Bitmap(bitmap, new Size(32, 32));
            
            // Convert bitmap to icon
            using var ms = new MemoryStream();
            resizedBitmap.Save(ms, ImageFormat.Png);
            
            // Reset stream position
            ms.Position = 0;
            
            // Create and return the icon
            return Icon.FromHandle(resizedBitmap.GetHicon());
        }
    }
} 