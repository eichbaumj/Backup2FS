using Backup2FS.Core.Models;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Normalizes iOS backups by reconstructing the iOS file system structure
    /// </summary>
    public class BackupNormalizer
    {
        private readonly string _backupPath;
        private readonly string _outputPath;
        private readonly ManifestDbReader _manifestDbReader;
        private readonly Action<string> _logAction;
        private readonly CancellationToken _cancellationToken;
        private readonly bool _calculateHashes;
        private readonly HashAlgorithm _hashAlgorithm;
        private readonly IProgress<(int progressPercent, string? message)>? _progress;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isPaused;
        private string _tempLogPath;
        private int _totalFiles = 0;
        private int _processedFiles = 0;
        private int _successFiles = 0;

        /// <summary>
        /// Initializes a new instance of the BackupNormalizer class
        /// </summary>
        /// <param name="backupPath">Path to the iOS backup folder</param>
        /// <param name="outputPath">Path where the normalized file system will be created</param>
        /// <param name="logAction">Action to log messages</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <param name="calculateHashes">Whether to calculate file hashes during normalization</param>
        /// <param name="hashAlgorithm">Hash algorithm to use if calculateHashes is true</param>
        /// <param name="progress">Progress reporter</param>
        public BackupNormalizer(
            string backupPath, 
            string outputPath, 
            Action<string> logAction, 
            CancellationToken cancellationToken,
            bool calculateHashes = false,
            HashAlgorithm hashAlgorithm = null,
            IProgress<(int progressPercent, string? message)>? progress = null)
        {
            _backupPath = backupPath ?? throw new ArgumentNullException(nameof(backupPath));
            _outputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _cancellationToken = cancellationToken;
            _calculateHashes = calculateHashes;
            _hashAlgorithm = hashAlgorithm;
            _progress = progress;
            _isPaused = false;
            _tempLogPath = Path.GetTempFileName();
            _manifestDbReader = new ManifestDbReader();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Set the output base path for the domain mapper
            DomainMapper.SetOutputBasePath(_outputPath);
        }

        /// <summary>
        /// Set or get the paused state
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        /// <summary>
        /// Progress tracking for the normalization process
        /// </summary>
        public event Action<int, int, int> ProgressChanged;

        /// <summary>
        /// Cancels the normalization process
        /// </summary>
        public void Cancel()
        {
            _logAction("Cancelling normalization process...");
            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Normalizes the iOS backup by reconstructing the file system
        /// </summary>
        /// <returns>The number of files successfully processed</returns>
        public async Task<int> NormalizeAsync()
        {
            _logAction($"Starting backup normalization from {_backupPath} to {_outputPath}");
            int processedCount = 0;
            int errorCount = 0;

            try
            {
                // Read all files from the Manifest.db
                var files = await _manifestDbReader.ReadFilesAsync(_backupPath, _logAction);
                _logAction($"Found {files.Count} files in the backup");

                // Create the output directory if it doesn't exist
                if (!Directory.Exists(_outputPath))
                {
                    Directory.CreateDirectory(_outputPath);
                    _logAction($"Created output directory: {_outputPath}");
                }

                // Process each file
                foreach (var file in files)
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        _logAction("Normalization cancelled");
                        break;
                    }

                    try
                    {
                        // Get source file path in the backup
                        string sourcePath = file.SourcePath;
                        
                        // Skip if the source file doesn't exist
                        if (!File.Exists(sourcePath))
                        {
                            _logAction($"Source file not found: {sourcePath}");
                            errorCount++;
                            continue;
                        }

                        // Map the domain and relative path to an output path
                        string destinationPath = file.DestinationPath;
                        
                        // Create the directory structure
                        string destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        // Copy the file
                        File.Copy(sourcePath, destinationPath, true);
                        
                        // Calculate hash if requested
                        if (_calculateHashes && _hashAlgorithm != null)
                        {
                            using (var stream = File.OpenRead(destinationPath))
                            {
                                byte[] hashBytes = _hashAlgorithm.ComputeHash(stream);
                                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                                _logAction($"Hash for {Path.GetFileName(destinationPath)}: {hash}");
                            }
                        }

                        processedCount++;
                        
                        // Log progress periodically
                        if (processedCount % 100 == 0)
                        {
                            _logAction($"Processed {processedCount} files so far");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logAction($"Error processing file {file.FileId}: {ex.Message}");
                        errorCount++;
                    }
                }

                _logAction($"Normalization completed. Processed {processedCount} files successfully with {errorCount} errors");
                return processedCount;
            }
            catch (Exception ex)
            {
                _logAction($"Normalization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Report progress to the progress reporter
        /// </summary>
        private void ReportProgress(int progressPercent, string? message = null)
        {
            _progress?.Report((progressPercent, message));
            
            // Also log to the temporary file if a message is provided
            if (!string.IsNullOrEmpty(message))
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(_tempLogPath, logEntry + Environment.NewLine);
            }
        }

        /// <summary>
        /// Get the path to a file in the backup based on its ID
        /// </summary>
        private static string GetBackupFilePath(string backupPath, string fileId)
        {
            // In iOS backups, files are stored in subdirectories named with the first 2 chars of the file ID
            return Path.Combine(backupPath, fileId.Substring(0, 2), fileId);
        }

        /// <summary>
        /// Get the output path for a file based on its domain and relative path
        /// </summary>
        private static string GetOutputPath(string baseFolder, string domain, string relativePath)
        {
            // Use the DomainMapper to get the correct destination path
            return DomainMapper.GetDestinationPath(baseFolder, domain, relativePath);
        }

        /// <summary>
        /// Compute a hash for a file using the specified algorithm
        /// </summary>
        private string ComputeFileHash(string filePath, string algorithm)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] hashBytes;

            switch (algorithm.ToLower())
            {
                case "md5":
                    using (var md5 = MD5.Create())
                        hashBytes = md5.ComputeHash(fs);
                    break;
                case "sha1":
                    using (var sha1 = SHA1.Create())
                        hashBytes = sha1.ComputeHash(fs);
                    break;
                case "sha256":
                default:
                    using (var sha256 = SHA256.Create())
                        hashBytes = sha256.ComputeHash(fs);
                    break;
            }

            // Convert hash bytes to hexadecimal string
            var sb = new StringBuilder();
            foreach (byte b in hashBytes)
                sb.Append(b.ToString("x2"));
            
            return sb.ToString();
        }
    }
} 