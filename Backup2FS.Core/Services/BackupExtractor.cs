using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Service to extract and reconstruct iOS backups
    /// </summary>
    public class BackupExtractor
    {
        private readonly ManifestDbReader _manifestDbReader;
        private readonly SHA1 _sha1;
        
        /// <summary>
        /// Initialize a new instance of the BackupExtractor
        /// </summary>
        public BackupExtractor()
        {
            _manifestDbReader = new ManifestDbReader();
            _sha1 = SHA1.Create();
        }
        
        /// <summary>
        /// Extract an iOS backup to a normalized file system structure
        /// </summary>
        /// <param name="backupPath">Path to the iOS backup folder</param>
        /// <param name="outputPath">Path where the normalized backup will be created</param>
        /// <param name="logAction">Action for logging messages</param>
        /// <param name="progressAction">Action for reporting progress (0-100)</param>
        /// <param name="cancelToken">Cancellation token to allow cancelling the operation</param>
        /// <param name="pauseToken">Token to check if the operation should pause</param>
        /// <returns>True if extraction completed successfully</returns>
        public async Task<bool> ExtractBackupAsync(
            string backupPath,
            string outputPath,
            Action<string> logAction,
            Action<int> progressAction,
            CancellationToken cancelToken,
            Func<bool> pauseToken)
        {
            if (string.IsNullOrEmpty(backupPath) || string.IsNullOrEmpty(outputPath))
            {
                logAction?.Invoke("Error: Backup path or output path is empty");
                return false;
            }
            
            if (!Directory.Exists(backupPath))
            {
                logAction?.Invoke($"Error: Backup path does not exist: {backupPath}");
                return false;
            }
            
            logAction?.Invoke($"Starting backup extraction from {backupPath} to {outputPath}");
            
            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                logAction?.Invoke($"Created output directory: {outputPath}");
            }
            
            try
            {
                // Read files from Manifest.db
                var files = await _manifestDbReader.ReadFilesAsync(backupPath, logAction);
                if (files.Count == 0)
                {
                    logAction?.Invoke("No files found in the backup");
                    return false;
                }
                
                logAction?.Invoke($"Found {files.Count} files to extract");
                progressAction?.Invoke(0);
                
                // Extract files
                int processed = 0;
                int successful = 0;
                int errors = 0;
                
                foreach (var file in files)
                {
                    // Check for cancellation
                    if (cancelToken.IsCancellationRequested)
                    {
                        logAction?.Invoke("Operation cancelled by user");
                        return false;
                    }
                    
                    // Check for pause
                    while (pauseToken())
                    {
                        await Task.Delay(500, cancelToken);
                        
                        if (cancelToken.IsCancellationRequested)
                        {
                            logAction?.Invoke("Operation cancelled while paused");
                            return false;
                        }
                    }
                    
                    // Calculate progress
                    processed++;
                    if (processed % 100 == 0)
                    {
                        int progress = (int)((float)processed / files.Count * 100);
                        progressAction?.Invoke(progress);
                        logAction?.Invoke($"Processed {processed} of {files.Count} files");
                    }
                    
                    // Skip files with no destination path
                    if (string.IsNullOrEmpty(file.DestinationPath))
                    {
                        continue;
                    }
                    
                    try
                    {
                        // Extract file
                        string sourcePath = file.SourcePath;
                        string destinationPath = Path.Combine(outputPath, file.DestinationPath);
                        
                        // Check if source file exists
                        if (!File.Exists(sourcePath))
                        {
                            errors++;
                            continue;
                        }
                        
                        // Create destination directory
                        string destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }
                        
                        // Copy file
                        File.Copy(sourcePath, destinationPath, true);
                        successful++;
                        
                        // Verify hash (optional validation)
                        /*
                        if (!VerifyFileHash(sourcePath, file.FileId))
                        {
                            logAction?.Invoke($"Warning: Hash mismatch for file: {file.Domain}/{file.RelativePath}");
                        }
                        */
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        logAction?.Invoke($"Error extracting file {file.Domain}/{file.RelativePath}: {ex.Message}");
                    }
                }
                
                // Final progress update
                progressAction?.Invoke(100);
                logAction?.Invoke($"Extraction completed: {successful} files extracted successfully, {errors} errors");
                
                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error during extraction: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Verifies the SHA1 hash of a file matches the expected fileId
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="expectedHash">Expected SHA1 hash (fileId)</param>
        /// <returns>True if the hash matches</returns>
        private bool VerifyFileHash(string filePath, string expectedHash)
        {
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] hashBytes = _sha1.ComputeHash(fileStream);
                    string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }
    }
} 