using System;

namespace Backup2FS.Core.Models
{
    /// <summary>
    /// Represents a file in an iOS backup
    /// </summary>
    public class BackupFile
    {
        /// <summary>
        /// The file ID in the backup (SHA-1 hash of domain and relativePath)
        /// </summary>
        public string FileId { get; set; }
        
        /// <summary>
        /// The domain the file belongs to (e.g., HomeDomain, AppDomain-com.apple.mobilesafari)
        /// </summary>
        public string Domain { get; set; }
        
        /// <summary>
        /// The relative path of the file within its domain
        /// </summary>
        public string RelativePath { get; set; }
        
        /// <summary>
        /// Flags associated with the file (used to determine file type)
        /// </summary>
        public int Flags { get; set; }
        
        /// <summary>
        /// The full path where the file will be extracted to
        /// </summary>
        public string DestinationPath { get; set; }
        
        /// <summary>
        /// The full path to the file in the backup directory structure
        /// </summary>
        public string SourcePath { get; set; }
        
        /// <summary>
        /// Flag 1 indicates a symbolic link
        /// </summary>
        public bool IsSymbolicLink => (Flags & 1) == 1;
        
        /// <summary>
        /// Flag 2 indicates a directory
        /// </summary>
        public bool IsDirectory => (Flags & 2) == 2;
        
        /// <summary>
        /// Flag 4 indicates a file
        /// </summary>
        public bool IsFile => (Flags & 4) == 4;
        
        /// <summary>
        /// Returns a string representation of the backup file
        /// </summary>
        public override string ToString()
        {
            return $"{Domain}/{RelativePath} -> {DestinationPath}";
        }
    }
} 