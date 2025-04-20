using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Backup2FS
{
    /// <summary>
    /// Utility class to create a valid Windows icon file
    /// </summary>
    public class IconFixer
    {
        public static void ConvertLogoToIcon()
        {
            try
            {
                // Load the application logo
                using var stream = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "logo.png"), FileMode.Open, FileAccess.Read);
                using var bitmap = new Bitmap(stream);
                
                // Create icon in multiple sizes (16x16, 32x32, 48x48, 64x64)
                int[] sizes = { 16, 32, 48, 64 };
                
                // Create a temporary file for the icon
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                
                // Save the icon
                using (var icon = new FileStream(iconPath, FileMode.Create))
                {
                    // Write the icon header
                    using var iconWriter = new BinaryWriter(icon);
                    
                    // ICONDIR structure
                    iconWriter.Write((short)0); // Reserved, must be 0
                    iconWriter.Write((short)1); // Type, 1 = ICO
                    iconWriter.Write((short)sizes.Length); // Number of images
                    
                    // Calculate the offset to the actual icon data
                    int offset = 6 + 16 * sizes.Length; // 6 bytes for ICONDIR, 16 bytes for each ICONDIRENTRY
                    
                    // Create and save the different sized icons
                    using var memoryStream = new MemoryStream();
                    foreach (int size in sizes)
                    {
                        // Create a resized bitmap
                        using var resizedBitmap = new Bitmap(bitmap, new Size(size, size));
                        
                        // Save the bitmap to memory
                        memoryStream.Position = 0;
                        memoryStream.SetLength(0);
                        resizedBitmap.Save(memoryStream, ImageFormat.Png);
                        
                        byte[] imageData = memoryStream.ToArray();
                        
                        // ICONDIRENTRY structure
                        iconWriter.Write((byte)size); // Width
                        iconWriter.Write((byte)size); // Height
                        iconWriter.Write((byte)0); // Color palette
                        iconWriter.Write((byte)0); // Reserved
                        iconWriter.Write((short)0); // Color planes
                        iconWriter.Write((short)32); // Bits per pixel
                        iconWriter.Write((int)imageData.Length); // Size of image data
                        iconWriter.Write((int)offset); // Offset to image data
                        
                        offset += imageData.Length;
                    }
                    
                    // Now write the actual image data
                    foreach (int size in sizes)
                    {
                        using var resizedBitmap = new Bitmap(bitmap, new Size(size, size));
                        memoryStream.Position = 0;
                        memoryStream.SetLength(0);
                        resizedBitmap.Save(memoryStream, ImageFormat.Png);
                        byte[] imageData = memoryStream.ToArray();
                        iconWriter.Write(imageData);
                    }
                }
                
                Console.WriteLine($"Icon created successfully at {iconPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating icon: {ex.Message}");
            }
        }
    }
} 