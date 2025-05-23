using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Maps iOS backup domains to file system paths
    /// </summary>
    public static class DomainMapper
    {
        /// <summary>
        /// Domain to path mappings for iOS backup domains
        /// </summary>
        private static readonly Dictionary<string, string> DomainMappings = new Dictionary<string, string>
        {
            { "AppDomain-", "private/var/mobile/Containers/Data/Application" },
            { "AppDomainGroup-", "private/var/mobile/Containers/Shared/AppGroup" },
            { "AppDomainPlugin-", "private/var/mobile/Containers/Data/PluginKitPlugin" },
            { "CameraRollDomain", "private/var/mobile" },
            { "DatabaseDomain", "private/var/db" },
            { "HealthDomain", "private/var/mobile/Library/" },
            { "HomeDomain", "private/var/mobile" },
            { "HomeKitDomain", "private/var/mobile/" },
            { "InstallDomain", "private/var/installd" },
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
        /// Base output path for all file mappings
        /// </summary>
        private static string _outputBasePath;

        /// <summary>
        /// Sets the base output path for all file mappings
        /// </summary>
        /// <param name="outputPath">Base output path</param>
        public static void SetOutputBasePath(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));
                
            _outputBasePath = outputPath;
        }

        /// <summary>
        /// Gets the destination path for a file based on its domain and relative path
        /// </summary>
        /// <param name="outputRootPath">Root path where files should be extracted</param>
        /// <param name="domain">iOS domain of the file</param>
        /// <param name="relativePath">Relative path within the domain</param>
        /// <returns>Full destination path for the file</returns>
        public static string GetDestinationPath(string outputRootPath, string domain, string relativePath)
        {
            if (string.IsNullOrEmpty(outputRootPath))
                throw new ArgumentNullException(nameof(outputRootPath));
            
            if (string.IsNullOrEmpty(domain))
                throw new ArgumentNullException(nameof(domain));
            
            string basePath = null;
            
            // First check for exact matches (domains without hyphens)
            if (DomainMappings.TryGetValue(domain, out string exactMatch))
            {
                basePath = exactMatch;
            }
            else
            {
                // Check for domains with prefixes (like AppDomain-)
                foreach (var mapping in DomainMappings)
                {
                    string key = mapping.Key;
                    if (key.EndsWith("-") && domain.StartsWith(key))
                    {
                        // For domain patterns with a dash, extract the app name
                        string appName = domain.Substring(key.Length);
                        basePath = Path.Combine(mapping.Value, appName);
                        break;
                    }
                }
            }
            
            // Default fallback for unknown domains
            if (basePath == null)
            {
                basePath = "private/var/Other";
            }
            
            // Sanitize the relative path to remove illegal characters
            string sanitizedPath = SanitizeFilePath(relativePath);
            
            // Combine paths and normalize slashes
            string normalizedPath = sanitizedPath.Replace(':', '/');
            string fullPath = Path.Combine(basePath, normalizedPath).Replace('\\', '/');
            
            return Path.Combine(outputRootPath, fullPath);
        }

        /// <summary>
        /// Sanitizes a file path by removing illegal characters
        /// </summary>
        /// <param name="path">The path to sanitize</param>
        /// <returns>Sanitized path</returns>
        private static string SanitizeFilePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
                
            // Get the directory part and file name
            string directoryPath = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            
            // If path doesn't have a directory component, just sanitize the filename
            if (string.IsNullOrEmpty(directoryPath))
                return SanitizeFileName(fileName);
                
            // Otherwise sanitize both parts
            string sanitizedDirectory = string.Join(
                Path.DirectorySeparatorChar.ToString(), 
                directoryPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Select(SanitizeFileName));
                    
            return Path.Combine(sanitizedDirectory, SanitizeFileName(fileName));
        }
        
        /// <summary>
        /// Sanitizes a file name by removing illegal characters
        /// </summary>
        /// <param name="fileName">The file name to sanitize</param>
        /// <returns>Sanitized file name</returns>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;
                
            // Characters that are invalid in Windows filenames
            char[] invalidChars = Path.GetInvalidFileNameChars();
            
            // Remove pipe characters (|) which are causing issues with the files
            // from the examples (also included in invalidChars)
            
            // Remove all invalid characters
            return string.Join("", fileName.Where(c => !invalidChars.Contains(c)));
        }

        /// <summary>
        /// Maps a domain and relative path to an output path using the stored output base path
        /// </summary>
        /// <param name="domain">iOS domain of the file</param>
        /// <param name="relativePath">Relative path within the domain</param>
        /// <returns>Full destination path for the file</returns>
        public static string MapPath(string domain, string relativePath)
        {
            if (_outputBasePath == null)
                throw new InvalidOperationException("Output base path not set. Call SetOutputBasePath first.");
            
            return GetDestinationPath(_outputBasePath, domain, relativePath);
        }
    }
} 