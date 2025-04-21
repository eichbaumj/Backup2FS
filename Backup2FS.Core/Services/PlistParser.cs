using Backup2FS.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Claunia.PropertyList;
using System.Linq;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Service for parsing iOS property list (plist) files
    /// </summary>
    public class PlistParser
    {
        /// <summary>
        /// Parse device information from Manifest.plist and Info.plist
        /// </summary>
        /// <param name="backupPath">Path to the iOS backup</param>
        /// <param name="logger">Optional logger function to capture debug information</param>
        /// <returns>Device information extracted from the plist files</returns>
        public DeviceInfo ParseDeviceInfo(string backupPath, Action<string>? logger = null)
        {
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
                throw new ArgumentException("Invalid backup path", nameof(backupPath));

            var deviceInfo = new DeviceInfo();
            
            // Parse Manifest.plist
            string manifestPlistPath = Path.Combine(backupPath, "Manifest.plist");
            if (File.Exists(manifestPlistPath))
            {
                try
                {
                    // Use plist-cil library to parse the plist file
                    var rootDict = (NSDictionary)PropertyListParser.Parse(manifestPlistPath);
                    
                    // Check if backup is encrypted
                    if (rootDict.TryGetValue("IsEncrypted", out NSObject encryptedObj) && 
                        encryptedObj is NSNumber encryptedNum)
                    {
                        deviceInfo.IsEncrypted = encryptedNum.ToBool();
                    }
                    
                    // Get device info from Lockdown dictionary
                    if (rootDict.TryGetValue("Lockdown", out NSObject lockdownObj) && 
                        lockdownObj is NSDictionary lockdown)
                    {
                        if (lockdown.TryGetValue("DeviceName", out NSObject deviceNameObj))
                            deviceInfo.DeviceName = deviceNameObj.ToString();
                            
                        if (lockdown.TryGetValue("BuildVersion", out NSObject buildVersionObj))
                            deviceInfo.BuildVersion = buildVersionObj.ToString();
                            
                        if (lockdown.TryGetValue("ProductType", out NSObject productTypeObj))
                        {
                            deviceInfo.ProductType = productTypeObj.ToString();
                            deviceInfo.Model = GetModelName(deviceInfo.ProductType);
                        }
                            
                        if (lockdown.TryGetValue("ProductVersion", out NSObject productVersionObj))
                            deviceInfo.IosVersion = productVersionObj.ToString();
                            
                        if (lockdown.TryGetValue("SerialNumber", out NSObject serialNumberObj))
                            deviceInfo.SerialNumber = serialNumberObj.ToString();
                            
                        if (lockdown.TryGetValue("UniqueDeviceID", out NSObject uniqueDeviceIdObj))
                            deviceInfo.UniqueDeviceId = uniqueDeviceIdObj.ToString();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error parsing Manifest.plist: {ex.Message}", ex);
                }
            }
            
            // Parse Info.plist
            string infoPlistPath = Path.Combine(backupPath, "Info.plist");
            if (File.Exists(infoPlistPath))
            {
                try
                {
                    logger?.Invoke("Parsing Info.plist...");
                    var rootDict = (NSDictionary)PropertyListParser.Parse(infoPlistPath);
                    logger?.Invoke("Info.plist parsed successfully.");
                    
                    // Extract standard device info (remains the same)
                    if (rootDict.TryGetValue("IMEI", out NSObject imeiObj)) deviceInfo.Imei = imeiObj.ToString();
                    if (rootDict.TryGetValue("Phone Number", out NSObject phoneObj)) deviceInfo.PhoneNumber = phoneObj.ToString();
                    if (rootDict.TryGetValue("ICCID", out NSObject iccidObj)) deviceInfo.Iccid = iccidObj.ToString();
                    if (rootDict.TryGetValue("MEID", out NSObject meidObj)) deviceInfo.Meid = meidObj.ToString();
                    if (rootDict.TryGetValue("Last Backup Date", out NSObject dateObj) && dateObj is NSDate nsDate) deviceInfo.LastBackupDate = nsDate.Date.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    deviceInfo.InstalledApps = new List<InstalledApp>();
                    
                    NSDictionary? applicationsDict = null;
                    if (rootDict.TryGetValue("Applications", out NSObject applicationsObj) && 
                        applicationsObj is NSDictionary appDict)
                    {
                        applicationsDict = appDict;
                        
                        // Quickly create InstalledApp objects with BundleId and RawAppData
                        foreach (string bundleId in applicationsDict.Keys)
                        {
                            if (applicationsDict[bundleId] is NSDictionary appDataDict)
                            {
                                var app = new InstalledApp { 
                                    BundleId = bundleId,
                                    DisplayName = GetAppNameFromBundleId(bundleId), // Can still get this quickly
                                    RawAppData = appDataDict // Store the raw dictionary
                                };
                                deviceInfo.InstalledApps.Add(app);
                            }
                        }
                    }
                    else
                    {
                        logger?.Invoke("Applications dictionary not found in Info.plist.");
                    }
                    
                    // Process "Installed Applications" array if needed, but defer icon data extraction
                    if (rootDict.TryGetValue("Installed Applications", out NSObject installedAppsObj) && 
                        installedAppsObj is NSArray installedAppsArray)
                    {
                        logger?.Invoke($"Found {installedAppsArray.Count} apps in Installed Applications array.");
                        var processedBundleIds = new HashSet<string>(deviceInfo.InstalledApps.Select(a => a.BundleId));
                        
                        foreach (var appObj in installedAppsArray)
                        {
                            if (appObj is NSString nsString)
                            {
                                string bundleId = nsString.ToString();
                                if (processedBundleIds.Contains(bundleId)) continue; // Skip if already added

                                // Try to find the raw data from the Applications dictionary if available
                                NSDictionary appDataDict = null;
                                if (applicationsDict != null && 
                                    applicationsDict.TryGetValue(bundleId, out NSObject appDataObj) && 
                                    appDataObj is NSDictionary foundDict)
                                {
                                     appDataDict = foundDict;
                                }
                                
                                var app = new InstalledApp { 
                                    BundleId = bundleId,
                                    DisplayName = GetAppNameFromBundleId(bundleId),
                                    RawAppData = appDataDict // Store raw dict (might be null if not in Applications dict)
                                };
                                
                                deviceInfo.InstalledApps.Add(app);
                                processedBundleIds.Add(bundleId);
                            }
                        }
                        logger?.Invoke($"Processing of Installed Applications array complete.");
                    }
                    else
                    {
                        logger?.Invoke("Installed Applications list not found in Info.plist.");
                    }
                    
                    logger?.Invoke($"Finished parsing Info.plist. Total apps identified: {deviceInfo.InstalledApps.Count}");
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"Error parsing Info.plist: {ex.Message}");
                    // Consider if we should re-throw or just log and continue
                    // throw new Exception($"Error parsing Info.plist: {ex.Message}", ex);
                }
            }
            
            // Parse status.plist for Date field
            string statusPlistPath = Path.Combine(backupPath, "status.plist");
            if (File.Exists(statusPlistPath))
            {
                try
                {
                    logger?.Invoke("Parsing status.plist for backup date...");
                    var statusDict = (NSDictionary)PropertyListParser.Parse(statusPlistPath);
                    
                    // Look for the Date key in status.plist
                    if (statusDict.TryGetValue("Date", out NSObject statusDateObj) && statusDateObj is NSDate statusDate)
                    {
                        // Update the LastBackupDate with the value from status.plist
                        deviceInfo.LastBackupDate = statusDate.Date.ToString("yyyy-MM-dd HH:mm:ss");
                        logger?.Invoke($"Found backup date in status.plist: {deviceInfo.LastBackupDate}");
                    }
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"Error parsing status.plist: {ex.Message}");
                    // Don't rethrow, just log the error as this is not critical
                }
            }
            
            return deviceInfo;
        }
        
        /// <summary>
        /// Map a product type identifier to a user-friendly model name
        /// </summary>
        private string GetModelName(string productType)
        {
            // Mapping of product types to model names
            var productTypeMap = new Dictionary<string, string>
            {
                { "iPhone1,1", "iPhone" },
                { "iPhone1,2", "iPhone 3G" },
                { "iPhone2,1", "iPhone 3GS" },
                { "iPhone3,1", "iPhone 4" },
                { "iPhone3,2", "iPhone 4 (GSM Rev A)" },
                { "iPhone3,3", "iPhone 4 (CDMA)" },
                { "iPhone4,1", "iPhone 4S" },
                { "iPhone5,1", "iPhone 5 (GSM)" },
                { "iPhone5,2", "iPhone 5 (GSM+CDMA)" },
                { "iPhone5,3", "iPhone 5C (GSM)" },
                { "iPhone5,4", "iPhone 5C (Global)" },
                { "iPhone6,1", "iPhone 5S (GSM)" },
                { "iPhone6,2", "iPhone 5S (Global)" },
                { "iPhone7,1", "iPhone 6 Plus" },
                { "iPhone7,2", "iPhone 6" },
                { "iPhone8,1", "iPhone 6s" },
                { "iPhone8,2", "iPhone 6s Plus" },
                { "iPhone8,4", "iPhone SE (GSM)" },
                { "iPhone9,1", "iPhone 7" },
                { "iPhone9,2", "iPhone 7 Plus" },
                { "iPhone9,3", "iPhone 7" },
                { "iPhone9,4", "iPhone 7 Plus" },
                { "iPhone10,1", "iPhone 8" },
                { "iPhone10,2", "iPhone 8 Plus" },
                { "iPhone10,3", "iPhone X (Global)" },
                { "iPhone10,4", "iPhone 8" },
                { "iPhone10,5", "iPhone 8 Plus" },
                { "iPhone10,6", "iPhone X (GSM)" },
                { "iPhone11,2", "iPhone XS" },
                { "iPhone11,4", "iPhone XS Max" },
                { "iPhone11,6", "iPhone XS Max (Global)" },
                { "iPhone11,8", "iPhone XR" },
                { "iPhone12,1", "iPhone 11" },
                { "iPhone12,3", "iPhone 11 Pro" },
                { "iPhone12,5", "iPhone 11 Pro Max" },
                { "iPhone12,8", "iPhone SE (2nd Gen)" },
                { "iPhone13,1", "iPhone 12 Mini" },
                { "iPhone13,2", "iPhone 12" },
                { "iPhone13,3", "iPhone 12 Pro" },
                { "iPhone13,4", "iPhone 12 Pro Max" },
                { "iPhone14,2", "iPhone 13 Pro" },
                { "iPhone14,3", "iPhone 13 Pro Max" },
                { "iPhone14,4", "iPhone 13 Mini" },
                { "iPhone14,5", "iPhone 13" },
                { "iPhone14,6", "iPhone SE (3rd Gen)" },
                { "iPhone14,7", "iPhone 14" },
                { "iPhone14,8", "iPhone 14 Plus" },
                { "iPhone15,2", "iPhone 14 Pro" },
                { "iPhone15,3", "iPhone 14 Pro Max" },
                { "iPhone15,4", "iPhone 15" },
                { "iPhone15,5", "iPhone 15 Plus" },
                { "iPhone16,1", "iPhone 15 Pro" },
                { "iPhone16,2", "iPhone 15 Pro Max" },
                { "iPhone17,1", "iPhone 16 Pro" },
                { "iPhone17,2", "iPhone 16 Pro Max" },
                { "iPhone17,3", "iPhone 16" },
                { "iPhone17,4", "iPhone 16 Plus" },
                { "iPhone17,5", "iPhone 16e" },
            };

            return productTypeMap.TryGetValue(productType, out string? modelName) 
                ? modelName 
                : productType;
        }
        
        /// <summary>
        /// Get a user-friendly name from a bundle ID
        /// </summary>
        private string GetAppNameFromBundleId(string bundleId)
        {
            // For now, just return the full bundle ID as requested by the user
            return bundleId;
        }
        
        /// <summary>
        /// Check if a backup is encrypted by examining its Manifest.plist
        /// </summary>
        /// <param name="backupPath">Path to the backup directory</param>
        /// <returns>True if encrypted, false otherwise. Returns false if unable to determine.</returns>
        public bool IsBackupEncrypted(string backupPath)
        {
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
                return false;
                
            string manifestPlistPath = Path.Combine(backupPath, "Manifest.plist");
            if (!File.Exists(manifestPlistPath))
                return false;
                
            try
            {
                var rootDict = (NSDictionary)PropertyListParser.Parse(manifestPlistPath);
                if (rootDict.TryGetValue("IsEncrypted", out NSObject encryptedObj) && 
                    encryptedObj is NSNumber encryptedNum)
                {
                    return encryptedNum.ToBool();
                }
            }
            catch
            {
                // If we encounter an error reading the plist, assume it's not encrypted
                return false;
            }
            
            return false;
        }
        
        /// <summary>
        /// Verify that a backup folder contains a valid Manifest.db file
        /// </summary>
        /// <param name="backupPath">Path to the backup directory</param>
        /// <returns>True if the Manifest.db file exists, false otherwise</returns>
        public bool HasValidManifestDb(string backupPath)
        {
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
                return false;
                
            string manifestDbPath = Path.Combine(backupPath, "Manifest.db");
            return File.Exists(manifestDbPath);
        }
    }
} 