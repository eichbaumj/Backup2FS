namespace Backup2FS.Core.Models
{
    /// <summary>
    /// Represents information about the iOS device that created the backup
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// Device name as set by the user
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Model identifier/name (e.g. "iPhone 14")
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Raw product type (e.g. "iPhone15,2")
        /// </summary>
        public string? ProductType { get; set; }

        /// <summary>
        /// iOS version (e.g. "16.5.1")
        /// </summary>
        public string? IosVersion { get; set; }

        /// <summary>
        /// Build version (e.g. "20F75")
        /// </summary>
        public string? BuildVersion { get; set; }

        /// <summary>
        /// Serial number of the device
        /// </summary>
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Unique device identifier
        /// </summary>
        public string? UniqueDeviceId { get; set; }

        /// <summary>
        /// International Mobile Equipment Identity
        /// </summary>
        public string? Imei { get; set; }
        
        /// <summary>
        /// Integrated Circuit Card Identifier
        /// </summary>
        public string? Iccid { get; set; }
        
        /// <summary>
        /// Mobile Equipment Identifier
        /// </summary>
        public string? Meid { get; set; }
        
        /// <summary>
        /// Phone number associated with the device
        /// </summary>
        public string? PhoneNumber { get; set; }
        
        /// <summary>
        /// Date and time of the last backup
        /// </summary>
        public string? LastBackupDate { get; set; }

        /// <summary>
        /// Whether the backup is encrypted
        /// </summary>
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// List of installed apps on the device
        /// </summary>
        public List<InstalledApp> InstalledApps { get; set; } = new List<InstalledApp>();

        /// <summary>
        /// Gets a friendly display string for the encryption status
        /// </summary>
        public string EncryptionStatus => IsEncrypted ? "Yes" : "No";
    }
} 