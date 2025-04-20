using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks; // Needed for Task
using Claunia.PropertyList;
using System.Text;
using System.Diagnostics;
using System.Windows.Threading;

namespace Backup2FS.Core.Models
{
    /// <summary>
    /// Represents an installed iOS application. Holds basic info and raw data dictionary initially.
    /// Icon processing is deferred.
    /// </summary>
    public class InstalledApp : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => 
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))
                ));
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private string _bundleId = string.Empty;
        public string BundleId
        {
            get => _bundleId;
            set { if (_bundleId != value) { _bundleId = value; OnPropertyChanged(); } }
        }

        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } }
        }

        // Store the raw plist dictionary for deferred processing
        // Use NSObject as the base type for the dictionary value
        [System.Text.Json.Serialization.JsonIgnore] // Avoid serializing this potentially large object
        public NSDictionary RawAppData { get; set; } 

        // --- Icon related properties --- 
        private byte[] _iconData; 
        private BitmapSource _iconImage; // The actual UI image
        private bool _iconLoadAttempted = false;
        private bool _isLoadingIcon = false;

        public bool HasIcon => _iconData != null && _iconData.Length > 0;

        // Public getter for the UI binding
        public BitmapSource IconImage => _iconImage;

        /// <summary>
        /// Asynchronously extracts IconData from RawAppData and then loads the icon.
        /// </summary>
        public async Task ProcessAndLoadIconAsync()
        {
            // Return early if already processed or currently processing
            lock (this)
            {
                if (_iconLoadAttempted || _isLoadingIcon) return;
                _isLoadingIcon = true;
                _iconLoadAttempted = true;
            }
            
            try
            {
                // Check RawAppData
                if (RawAppData == null)
                    return;
                
                // Extract icon data on background thread
                byte[]? extractedData = await Task.Run(() => ExtractIconDataFromRaw());
                
                // If we got no data, bail out
                if (extractedData == null || extractedData.Length == 0)
                    return;
                
                // Set the icon data (no need for defensive copy as ExtractIconDataFromRaw already creates a new array)
                _iconData = extractedData;
                
                // Create image on background thread with optimized approach
                try
                {
                    var image = await Task.Run(() => 
                    {
                        try
                        {
                            using (var ms = new MemoryStream(_iconData))
                            {
                                ms.Position = 0;
                                
                                // Create a completely new BitmapImage with optimized settings
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // Improves loading performance
                                bmp.StreamSource = ms;
                                bmp.DecodePixelWidth = 24; // We only need small icons, so decode at the display size
                                bmp.DecodePixelHeight = 24;
                                bmp.EndInit();
                                
                                if (bmp.CanFreeze)
                                    bmp.Freeze();
                                
                                return bmp;
                            }
                        }
                        catch
                        {
                            return null;
                        }
                    });
                    
                    // If image creation succeeded, update UI thread
                    if (image != null)
                    {
                        var dispatcher = System.Windows.Application.Current?.Dispatcher;
                        if (dispatcher != null && !dispatcher.CheckAccess())
                        {
                            // Update UI thread with high priority for better responsiveness
                            await dispatcher.InvokeAsync(() => 
                            {
                                _iconImage = image;
                                OnPropertyChanged(nameof(HasIcon));
                                OnPropertyChanged(nameof(IconImage));
                            }, System.Windows.Threading.DispatcherPriority.Normal);
                        }
                        else
                        {
                            // Already on UI thread
                            _iconImage = image;
                            OnPropertyChanged(nameof(HasIcon));
                            OnPropertyChanged(nameof(IconImage));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing icon for {BundleId}: {ex.Message}");
                }
            }
            finally
            {
                _isLoadingIcon = false;
            }
        }

        /// <summary>
        /// Extracts the icon data byte array from the raw NSDictionary.
        /// </summary>
        private byte[] ExtractIconDataFromRaw()
        {
            if (RawAppData == null)
                return null;
            
            try
            {
                // Try to get the PlaceholderIcon key first (most common)
                if (RawAppData.ContainsKey("PlaceholderIcon"))
                {
                    NSObject iconObj = RawAppData["PlaceholderIcon"];
                    
                    if (iconObj is NSData nsData)
                    {
                        byte[] iconData = nsData.Bytes;
                        if (iconData != null && iconData.Length > 0)
                            return iconData;
                    }
                }
                
                // If no PlaceholderIcon, try alternative keys containing "icon"
                var iconKeys = RawAppData.Keys
                    .Where(k => k.ToString().ToLowerInvariant().Contains("icon"))
                    .ToList();
                
                foreach (var key in iconKeys)
                {
                    if (RawAppData[key] is NSData nsData)
                    {
                        var iconData = nsData.Bytes;
                        if (iconData != null && iconData.Length > 0)
                            return iconData;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting icon: {ex.Message}");
            }
            
            return null;
        }
        
        public InstalledApp() { }
    }
} 