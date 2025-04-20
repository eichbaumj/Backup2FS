using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Backup2FS.Core.Services;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Provides mapping functionality for iOS backup files to recreate the iOS filesystem structure
    /// </summary>
    public class FileSystemMapper
    {
        /// <summary>
        /// Maps iOS domain prefixes to their corresponding file system paths
        /// </summary>
        private static readonly Dictionary<string, string> DomainMappings = new Dictionary<string, string>
        {
            { "AppDomain-", "private/var/mobile/Containers/Data/Application" },
            { "AppDomainGroup-", "private/var/mobile/Containers/Shared/AppGroup" },
            { "AppDomainPlugin-", "private/var/mobile/Containers/Data/PluginKitPlugin" },
            { "CameraRollDomain", "private/var/mobile" },
            { "DatabaseDomain", "private/var/db" },
            { "HealthDomain", "private/var/mobile/Library/Health" },
            { "HomeDomain", "private/var/mobile" },
            { "HomeKitDomain", "private/var/mobile/" },
            { "InstallDomain", "private/var/installld" },
            { "KeyboardDomain", "private/var/mobile" },
            { "KeychainDomain", "private/var/Keychains" },
            { "ManagedPreferencesDomain", "private/var/Managed Preferences" },
            { "MediaDomain", "private/var/mobile/Media" },
            { "MobileDeviceDomain", "private/var/MobileDevice" },
            { "NetworkDomain", "private/var/networkd" },
            { "ProtectedDomain", "private/var/protected" },
            { "RootDomain", "private/var/root" },
            { "SysContainerDomain-", "private/var/containers/Data/System" },
            { "SysSharedContainerDomain-", "private/var/containers/Shared/SystemGroup" },
            { "SystemPreferencesDomain", "private/var/preferences/" },
            { "TonesDomain", "private/var/mobile" },
            { "WirelessDomain", "private/var/wireless" }
        };

        /// <summary>
        /// Gets the source file path for a backup file
        /// </summary>
        /// <param name="backupPath">Path to the backup directory</param>
        /// <param name="fileId">ID of the file</param>
        /// <returns>Full path to the file in the backup directory</returns>
        public static string GetBackupFilePath(string backupPath, string fileId)
        {
            if (string.IsNullOrEmpty(backupPath) || string.IsNullOrEmpty(fileId))
                return null;

            return Path.Combine(backupPath, fileId.Substring(0, 2), fileId);
        }

        /// <summary>
        /// Gets the output path for a file based on its domain and relative path
        /// </summary>
        /// <param name="baseFolder">Base output folder</param>
        /// <param name="domain">iOS domain</param>
        /// <param name="relativePath">Relative path within the domain</param>
        /// <returns>Full path to where the file should be placed in the output directory</returns>
        public static string GetOutputPath(string baseFolder, string domain, string relativePath)
        {
            if (string.IsNullOrEmpty(baseFolder) || string.IsNullOrEmpty(domain))
                return null;

            string basePath = null;

            foreach (var mapping in DomainMappings)
            {
                if (domain.StartsWith(mapping.Key))
                {
                    basePath = Path.Combine(baseFolder, mapping.Value);
                    
                    // Handle special domains with app-specific subfolders
                    if (mapping.Key is "AppDomain-" or "AppDomainGroup-" or "AppDomainPlugin-" or 
                               "SysContainerDomain-" or "SysSharedContainerDomain-")
                    {
                        string appName = domain.Substring(mapping.Key.Length);
                        basePath = Path.Combine(basePath, appName);
                    }
                    
                    break;
                }
            }

            // If domain wasn't found, use a default path
            if (basePath == null)
            {
                basePath = Path.Combine(baseFolder, "private/var/Other");
            }

            return Path.Combine(basePath, relativePath ?? string.Empty);
        }

        /// <summary>
        /// Copies a file from the backup source to the output destination
        /// </summary>
        /// <param name="sourcePath">Source file path in the backup</param>
        /// <param name="destinationPath">Destination path for the reconstructed file</param>
        /// <param name="log">Action to log messages</param>
        /// <returns>True if copy was successful, false otherwise</returns>
        public static bool CopyFile(string sourcePath, string destinationPath, Action<string> log)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    log($"Source file does not exist: {sourcePath}");
                    return false;
                }
                
                // Create directory structure if it doesn't exist
                string destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }
                
                // Copy the file
                File.Copy(sourcePath, destinationPath, true);
                return true;
            }
            catch (Exception ex)
            {
                log($"Error copying file from {sourcePath} to {destinationPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the file size in human-readable format
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>Human-readable file size</returns>
        public static string GetFileSize(string path)
        {
            if (!File.Exists(path))
                return "0 B";
                
            long bytes = new FileInfo(path).Length;
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }
        
        /// <summary>
        /// Ensures all necessary directories exist for the output path
        /// </summary>
        /// <param name="baseOutputPath">Base output directory</param>
        /// <param name="log">Action to log messages</param>
        public static void EnsureOutputDirectories(string baseOutputPath, Action<string> log)
        {
            try
            {
                if (!Directory.Exists(baseOutputPath))
                {
                    Directory.CreateDirectory(baseOutputPath);
                    log($"Created output directory: {baseOutputPath}");
                }
                
                // Create common iOS directory structure
                string[] commonPaths = {
                    Path.Combine(baseOutputPath, "private", "var", "mobile"),
                    Path.Combine(baseOutputPath, "private", "var", "mobile", "Containers", "Data", "Application"),
                    Path.Combine(baseOutputPath, "private", "var", "mobile", "Containers", "Shared", "AppGroup"),
                    Path.Combine(baseOutputPath, "private", "var", "mobile", "Media"),
                    Path.Combine(baseOutputPath, "private", "var", "mobile", "Library"),
                    Path.Combine(baseOutputPath, "private", "var", "Keychains"),
                    Path.Combine(baseOutputPath, "private", "var", "wireless"),
                    Path.Combine(baseOutputPath, "private", "var", "preferences"),
                    Path.Combine(baseOutputPath, "unknown_domains")
                };
                
                foreach (var path in commonPaths)
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                }
                
                log("Initialized directory structure for iOS filesystem reconstruction");
            }
            catch (Exception ex)
            {
                log($"Error ensuring output directories: {ex.Message}");
            }
        }
    }
} 