using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Service responsible for extracting iOS backup files to a readable file structure
    /// </summary>
    public class BackupExtractorService
    {
        // Delegates for logging and progress reporting
        public delegate void LogMessageHandler(string message);
        public delegate void ProgressReportHandler(int progress);

        // Events
        public event LogMessageHandler LogMessage;
        public event ProgressReportHandler ProgressReport;

        // Private fields
        private volatile int _isPaused; // 0 = not paused, 1 = paused
        private volatile bool _isCancelled;
        private List<string> _hashAlgorithms; // List of hash algorithms to use
        private string _detailedLogPath; // Path to the detailed log file
        
        // Object used for locking operations on the log file
        private readonly object _logLock = new object();
        
        // Lock object for pause/resume operations
        private readonly object _pauseLock = new object();

        /// <summary>
        /// Creates a new instance of the backup extractor service
        /// </summary>
        public BackupExtractorService(LogMessageHandler logMessage = null, ProgressReportHandler progressChanged = null)
        {
            // Initialize events with delegates if provided
            if (logMessage != null)
                LogMessage += logMessage;
            
            if (progressChanged != null)
                ProgressReport += progressChanged;
            
            _isPaused = 0;
            _isCancelled = false;
            
            // Default to using all three hash algorithms for complete logging
            _hashAlgorithms = new List<string> 
            {
                "md5",
                "sha1",
                "sha256"
            };
            
            // Create a unique timestamped log file in the temp directory
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            DetailedLogPath = Path.Combine(Path.GetTempPath(), $"Backup2FS_DetailedLog_{timestamp}.csv");
            
            // Initialize the log file
            InitializeDetailedLogging();
        }
        
        /// <summary>
        /// Sets whether the extraction process is paused
        /// </summary>
        public bool IsPaused
        {
            get => Interlocked.CompareExchange(ref _isPaused, 0, 0) != 0;
            set
            {
                if (value)
                {
                    Pause();
                }
                else
                {
                    Resume();
                }
            }
        }

        /// <summary>
        /// Gets the path to the detailed log file
        /// </summary>
        public string DetailedLogPath
        {
            get => _detailedLogPath;
            private set => _detailedLogPath = value;
        }
        
        /// <summary>
        /// Initializes the detailed log file
        /// </summary>
        private void InitializeDetailedLogging()
        {
            try
            {
                // Ensure we have a unique filename in the temp directory
                if (string.IsNullOrEmpty(_detailedLogPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _detailedLogPath = Path.Combine(Path.GetTempPath(), $"Backup2FS_DetailedLog_{timestamp}.csv");
                }
                
                // Create the log file with headers
                using (var writer = new StreamWriter(_detailedLogPath, false, Encoding.UTF8))
                {
                    // Write header information (lines starting with '#' are comments, not data rows)
                    writer.WriteLine("# Backup2FS Detailed Log File");
                    writer.WriteLine($"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("# One row per Manifest.db entry processed during normalization.");
                    writer.WriteLine("# Status   : Copied | Directory | Symlink | Missing | Error");
                    writer.WriteLine("# Domain   : iOS backup domain (e.g. HomeDomain, AppDomain-<bundle id>)");
                    writer.WriteLine("# FileID   : iOS backup file identifier (folder/filename)");
                    writer.WriteLine("# OutputPath: where the file was written in the normalized file system");
                    writer.WriteLine("# SizeBytes: size of the copied file");
                    writer.WriteLine("# MD5/SHA1/SHA256: file hashes (only for the algorithms you selected)");
                    writer.WriteLine();

                    // Write CSV headers
                    writer.WriteLine("Timestamp,Status,Domain,RelativePath,FileID,OutputPath,SizeBytes,MD5,SHA1,SHA256");
                }
                
                LogMessage?.Invoke($"Detailed log initialized at {_detailedLogPath}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error initializing detailed log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Escapes a value for CSV per RFC 4180: fields containing a comma, quote, or newline are
        /// wrapped in double quotes with embedded quotes doubled. iOS filenames frequently contain
        /// commas, which would otherwise shift the hash columns.
        /// </summary>
        private static string CsvField(string? value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static long TryGetFileSize(string path)
        {
            try { return new FileInfo(path).Length; } catch { return 0; }
        }

        /// <summary>
        /// Writes one structured row to the detailed forensic CSV.
        /// Columns: Timestamp, Status, Domain, RelativePath, FileID, OutputPath, SizeBytes, MD5, SHA1, SHA256.
        /// </summary>
        private void LogFileRecord(string status, string? domain, string? relativePath, string fileId,
                                   string? outputPath, long? size, Dictionary<string, string>? hashes)
        {
            if (string.IsNullOrEmpty(_detailedLogPath))
                return;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string formattedFileId = string.IsNullOrEmpty(fileId) ? string.Empty :
                    (fileId.Length >= 2 ? $"{fileId.Substring(0, 2)}/{fileId}" : fileId);
                // Normalize separators so the output path is consistently Windows-style.
                string outPath = string.IsNullOrEmpty(outputPath) ? string.Empty : outputPath.Replace('/', '\\');
                string md5 = hashes != null && hashes.TryGetValue("md5", out string? m) ? m : string.Empty;
                string sha1 = hashes != null && hashes.TryGetValue("sha1", out string? s1) ? s1 : string.Empty;
                string sha256 = hashes != null && hashes.TryGetValue("sha256", out string? s2) ? s2 : string.Empty;

                string line = string.Join(",",
                    timestamp,
                    CsvField(status),
                    CsvField(domain ?? string.Empty),
                    CsvField(relativePath ?? string.Empty),
                    CsvField(formattedFileId),
                    CsvField(outPath),
                    size.HasValue ? size.Value.ToString() : string.Empty,
                    md5, sha1, sha256);

                lock (_logLock)
                {
                    if (!File.Exists(_detailedLogPath))
                        InitializeDetailedLogging();
                    File.AppendAllText(_detailedLogPath, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error logging detailed info: {ex.Message}");
            }
        }

        private void CloseDetailedLog()
        {
            try
            {
                WriteDetailedLog("Log file closed.");
                LogMessage?.Invoke($"Detailed log file saved to: {_detailedLogPath}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error closing log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the hash algorithms to use for file verification
        /// </summary>
        /// <param name="algorithms">List of hash algorithms (md5, sha1, sha256)</param>
        public void SetHashAlgorithms(List<string> algorithms)
        {
            // Accept empty list without defaulting to SHA-256
            _hashAlgorithms = algorithms ?? new List<string>();
            
            Console.WriteLine($"Hash algorithms set to: {(_hashAlgorithms.Count > 0 ? string.Join(", ", _hashAlgorithms) : "none")}");
            // Don't log this message on startup, only when extraction begins
            // The user will see this in the detailed log
        }

        /// <summary>
        /// Extracts an iOS backup to a readable file structure
        /// </summary>
        /// <param name="backupPath">Path to the iOS backup folder</param>
        /// <param name="outputPath">Path where the extracted files will be saved</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>True if extraction was successful, false otherwise</returns>
        public async Task<bool> ExtractBackupAsync(string backupPath, string outputPath, CancellationToken cancellationToken)
        {
            try
            {
                // Reset state
                _isPaused = 0;
                _isCancelled = false;
                
                if (string.IsNullOrWhiteSpace(backupPath))
                {
                    LogMessage?.Invoke("Error: Backup path is empty.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    LogMessage?.Invoke("Error: Output path is empty.");
                    return false;
                }
                
                if (!Directory.Exists(backupPath))
                {
                    LogMessage?.Invoke($"Error: Backup directory not found: {backupPath}");
                    return false;
                }

                if (!Directory.Exists(outputPath))
                {
                    try
                    {
                        Directory.CreateDirectory(outputPath);
                        LogMessage?.Invoke($"Created output directory: {outputPath}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke($"Error creating output directory: {ex.Message}");
                        return false;
                    }
                }

                string manifestPath = Path.Combine(backupPath, "Manifest.db");
                if (!File.Exists(manifestPath))
                {
                    LogMessage?.Invoke($"Error: Manifest.db not found in backup folder: {manifestPath}");
                    return false;
                }

                // Create a new log file for this extraction run with timestamp to ensure uniqueness
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _detailedLogPath = Path.Combine(outputPath, $"extraction_log_{timestamp}.csv");
                try
                {
                    // Use the initialize method to ensure consistent formatting
                    InitializeDetailedLogging();
                    LogMessage?.Invoke($"Detailed log file created: {_detailedLogPath}");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Error creating detailed log file: {ex.Message}. Using temp log file instead.");
                    // Fall back to temp directory if output directory isn't writable
                    _detailedLogPath = Path.Combine(Path.GetTempPath(), $"Backup2FS_DetailedLog_{timestamp}.csv");
                    InitializeDetailedLogging();
                }

                // Initialize progress at 0% with a placeholder total
                // This ensures the progress bar shows 0% from the very beginning
                ProgressReport?.Invoke(0);
                
                // Log basic information - simplified
                LogMessage?.Invoke("Starting backup extraction process...");
                LogMessage?.Invoke($"Using hash algorithms: {string.Join(", ", _hashAlgorithms).ToUpper()}");
                
                // Set the output base path for domain mapping
                DomainMapper.SetOutputBasePath(outputPath);

                // Extract files from the SQLite database
                try 
                {
                    // Check SQLite libraries without verbose logging
                    CheckSQLiteLibraries();
                    
                    // Extract files
                    bool success = await ExtractFilesFromDatabaseAsync(backupPath, outputPath, cancellationToken);
                    return success;
                }
                catch (DllNotFoundException ex)
                {
                    LogMessage?.Invoke($"Error: SQLite libraries not found. {ex.Message}");
                    WriteDetailedLog($"SQLite DLL not found: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Error reading from database: {ex.Message}");
                    WriteDetailedLog($"Database error: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error during extraction: {ex.Message}");
                return false;
            }
            finally
            {
                CloseDetailedLog();
            }
        }

        /// <summary>
        /// Checks for SQLite libraries and attempts to load them
        /// </summary>
        private bool CheckSQLiteLibraries()
        {
            try
            {
                // Skip verbose logging of SQLite version
                using (var connection = new SQLiteConnection("Data Source=:memory:"))
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error with SQLite: {ex.Message}");
                WriteDetailedLog($"SQLite error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Extracts files from the Manifest.db SQLite database
        /// </summary>
        private async Task<bool> ExtractFilesFromDatabaseAsync(string backupPath, string outputPath, CancellationToken token)
        {
            WriteDetailedLog("Starting to extract files from database...");
            int totalFiles = 0;
            int processedFiles = 0;
            // Classify entries by Manifest.db flags (1=file, 2=directory, 4=symlink) instead of
            // counting directories/symlinks as "failures".
            int filesCopied = 0;
            int dirCount = 0;
            int symlinkCount = 0;
            int missingCount = 0;
            int errorCount = 0;

            try
            {
                // Create SQLite connection
                // Read Only: never mutate the source backup (no -wal/-shm/journal, no write lock).
                using (var connection = new SQLiteConnection($"Data Source={Path.Combine(backupPath, "Manifest.db")};Version=3;Read Only=True;"))
                {
                    connection.Open();

                    // Count total files for progress reporting
                    using (var countCommand = new SQLiteCommand("SELECT COUNT(*) FROM Files", connection))
                    {
                        totalFiles = Convert.ToInt32(countCommand.ExecuteScalar());
                        WriteDetailedLog($"Total files to process: {totalFiles}");
                        ProgressReport?.Invoke(0);
                    }

                    // Get all files from database
                    using (var command = new SQLiteCommand(
                        @"SELECT fileID, domain, relativePath, flags, file 
                          FROM Files 
                          ORDER BY domain, relativePath", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            token.ThrowIfCancellationRequested();
                            CheckIfPaused(token);

                            string fileId = reader["fileID"].ToString();
                            string domain = reader["domain"].ToString();
                            string relativePath = reader["relativePath"].ToString();
                            int flags = reader["flags"] != DBNull.Value ? Convert.ToInt32(reader["flags"]) : 1;

                            processedFiles++;
                            double progressPercentage = (double)processedFiles / totalFiles * 100;

                            // Progress updates (UI only — not the CSV)
                            if (processedFiles % 10 == 0)
                            {
                                ProgressReport?.Invoke((int)Math.Round(progressPercentage));
                                LogMessage?.Invoke($"Processed {processedFiles}/{totalFiles} files ({progressPercentage:F1}%)");
                            }

                            // Process the entry, classified by its Manifest.db flags.
                            try
                            {
                                string destinationPath = DomainMapper.MapPath(domain, relativePath);

                                if (flags == 2)
                                {
                                    // Directory — create it; this is not a "failure".
                                    Directory.CreateDirectory(destinationPath);
                                    dirCount++;
                                    LogFileRecord("Directory", domain, relativePath, fileId, destinationPath, null, null);
                                    continue;
                                }
                                if (flags == 4)
                                {
                                    // Symlink — recorded for completeness; no content to copy.
                                    symlinkCount++;
                                    LogFileRecord("Symlink", domain, relativePath, fileId, null, null, null);
                                    continue;
                                }

                                // flags == 1 (a regular file)
                                string sourcePath = Path.Combine(backupPath, GetBackupFilePath(fileId));
                                if (!File.Exists(sourcePath))
                                {
                                    missingCount++;
                                    LogFileRecord("Missing", domain, relativePath, fileId, null, null, null);
                                    continue;
                                }

                                // No artificial per-file timeout: large files and long pauses must
                                // never cause a file to be silently dropped. Cancellation is driven
                                // solely by the user's token (a genuine OperationCanceledException
                                // propagates and aborts the whole run).
                                var (success, copyHashes) = await ProcessBackupFileAsync(sourcePath, destinationPath, null, token);
                                if (success)
                                {
                                    filesCopied++;

                                    // Reuse the hashes computed during the copy (single read of the
                                    // file). Only recompute when the copy path didn't produce them
                                    // (empty file, or the memory-fallback copy that doesn't hash).
                                    var hashValues = copyHashes ?? CalculateFileHashes(destinationPath, token);
                                    long size = TryGetFileSize(destinationPath);

                                    LogFileRecord("Copied", domain, relativePath, fileId, destinationPath, size, hashValues);
                                }
                                else
                                {
                                    errorCount++;
                                    LogFileRecord("Error", domain, relativePath, fileId, null, null, null);
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ex is OperationCanceledException) throw;

                                errorCount++;
                                LogFileRecord("Error", domain, relativePath, fileId, null, null, null);
                                WriteDetailedLog($"Error processing {domain}/{relativePath} ({fileId}): {ex.Message}");
                            }
                        }
                    }
                }

                WriteDetailedLog($"Done. {processedFiles} entries: {filesCopied} files copied, {dirCount} directories, {symlinkCount} symlinks, {missingCount} missing, {errorCount} errors.");
                LogMessage?.Invoke($"Extraction completed: {filesCopied} files copied, {dirCount} directories, {symlinkCount} symlinks, {missingCount} missing, {errorCount} errors.");
                // Final progress report
                ProgressReport?.Invoke(100);
                return true;
            }
            catch (OperationCanceledException)
            {
                WriteDetailedLog("Extraction was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                WriteDetailedLog($"Error extracting files from database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Processes a single backup file by copying it to the correct destination
        /// </summary>
        private async Task<(bool success, Dictionary<string, string>? hashes)> ProcessBackupFileAsync(string sourcePath, string destinationPath, Dictionary<string, string>? metadata, CancellationToken token)
        {
            try
            {
                // Create directory structure if it doesn't exist
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                    WriteDetailedLog($"Created directory: {destinationDirectory}");
                }

                // Extract fileID from sourcePath - expected format: [backupPath]/[2-char-folder]/[fileID]
                string fileID = "";
                if (sourcePath.Contains("\\"))
                {
                    string[] pathParts = sourcePath.Split('\\');
                    if (pathParts.Length >= 2)
                    {
                        string folderPart = pathParts[pathParts.Length - 2]; // Get the folder (2 characters)
                        string filePart = pathParts[pathParts.Length - 1];  // Get the file part (rest of fileID)
                        
                        // Verify the folder is 2 chars
                        if (folderPart.Length == 2 && filePart.Length > 0)
                        {
                            fileID = filePart;
                        }
                    }
                }

                // Skip if source file doesn't exist (the caller normally classifies this as Missing first).
                if (!File.Exists(sourcePath))
                {
                    WriteDetailedLog($"Source file not found: {sourcePath}");
                    return (false, null);
                }

                var fileInfo = new FileInfo(sourcePath);
                if (fileInfo.Length == 0)
                {
                    // Create empty file and consider it a success
                    using (File.Create(destinationPath)) { }
                    WriteDetailedLog($"Created empty file: {destinationPath}");
                    return (true, null);
                }

                // Check if file is too large (greater than 100MB) and log a warning
                bool isLargeFile = fileInfo.Length > 104857600; // 100MB
                if (isLargeFile)
                {
                    WriteDetailedLog($"Processing large file ({fileInfo.Length / 1048576}MB): {destinationPath}");
                }

                // Copy file with hash verification and respect pause/cancel.
                // The copy returns the hashes it computed while streaming, so the caller
                // doesn't have to read the file a second time.
                Dictionary<string, string>? resultHashes;
                try
                {
                    // Use a memory-efficient approach for copying
                    resultHashes = await CopyFileWithHashVerificationAsync(sourcePath, destinationPath, token);

                    if (resultHashes != null)
                    {
                        WriteDetailedLog($"Successfully processed file: {destinationPath}");
                    }
                    else
                    {
                        WriteDetailedLog($"Failed to verify file integrity: {destinationPath}");
                        return (false, null);
                    }
                }
                catch (OutOfMemoryException)
                {
                    // For extremely large files that cause memory issues, fall back to simple copy.
                    // The fallback copy does not hash, so leave hashes null (the caller computes them).
                    WriteDetailedLog($"Memory error with large file, falling back to direct copy: {destinationPath}");
                    await FallbackFileCopyAsync(sourcePath, destinationPath, token);
                    resultHashes = null;
                }

                return (true, resultHashes);
            }
            catch (IOException ex)
            {
                WriteDetailedLog($"I/O error processing file {sourcePath}: {ex.Message}");
                return (false, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteDetailedLog($"Access denied for file {sourcePath}: {ex.Message}");
                return (false, null);
            }
            catch (OperationCanceledException)
            {
                WriteDetailedLog($"Processing of file {sourcePath} was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                WriteDetailedLog($"Unexpected error processing file {sourcePath}: {ex.Message}");
                return (false, null);
            }
            finally
            {
                // Suggest garbage collection if processing large files
                if (GC.GetTotalMemory(false) > 500000000) // 500MB
                {
                    GC.Collect(2, GCCollectionMode.Optimized, false);
                }
            }
        }

        private async Task FallbackFileCopyAsync(string sourcePath, string destinationPath, CancellationToken token)
        {
            // A simpler copy method that uses less memory for extremely large files
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
            using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                byte[] buffer = new byte[65536]; // 64KB buffer
                int bytesRead;
                
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    CheckIfPaused(token);
                    
                    await destinationStream.WriteAsync(buffer, 0, bytesRead, token);
                }
            }
            
            WriteDetailedLog($"Completed fallback file copy: {destinationPath}");
        }

        /// <summary>
        /// Copies a file with hash verification for all selected algorithms
        /// </summary>
        /// <summary>
        /// Copies a file, computing the selected hashes while streaming. Returns the computed
        /// hashes on success (an empty dictionary when no algorithms are selected), or null on
        /// failure. There is intentionally no per-file timeout: large files and long pauses must
        /// never drop a file — cancellation is driven solely by the caller's token.
        /// </summary>
        private async Task<Dictionary<string, string>?> CopyFileWithHashVerificationAsync(string sourcePath, string destPath, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                CheckIfPaused(token);

                // Check for an existing destination file
                if (File.Exists(destPath))
                {
                    // If the destination already exists, we'll overwrite it
                    File.Delete(destPath);
                }

                // If no hash algorithms are selected, use a simple file copy
                if (_hashAlgorithms == null || _hashAlgorithms.Count == 0)
                {
                    await CopyWithoutHashAsync(sourcePath, destPath, token);
                    return new Dictionary<string, string>();
                }

                // Try to perform a stream copy with hash verification
                using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
                using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.SequentialScan);

                // Create hash algorithm instances
                Dictionary<string, HashAlgorithm> hashers = new Dictionary<string, HashAlgorithm>();
                foreach (var algorithm in _hashAlgorithms)
                {
                    hashers[algorithm] = HashAlgorithm.Create(algorithm);
                }

                // Copy the file in chunks and update hash algorithms
                byte[] buffer = new byte[65536]; // 64KB buffer
                int bytesRead;
                long totalBytesRead = 0;
                long fileSize = new FileInfo(sourcePath).Length;

                // Read in chunks, updating hash and writing to destination
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    CheckIfPaused(token);

                    // Update hash values
                    foreach (var hasher in hashers.Values)
                    {
                        hasher.TransformBlock(buffer, 0, bytesRead, null, 0);
                    }

                    // Write to destination file
                    await destStream.WriteAsync(buffer, 0, bytesRead, token);

                    // Update progress occasionally (every 1MB)
                    totalBytesRead += bytesRead;
                    if (totalBytesRead % 1048576 < buffer.Length && fileSize > 0)
                    {
                        double fileProgress = (double)totalBytesRead / fileSize;
                        WriteDetailedLog($"File progress: {fileProgress:P0} - {Path.GetFileName(destPath)}");
                    }
                }

                // Finalize hash calculations
                foreach (var hasher in hashers.Values)
                {
                    hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                }

                // Flush the destination stream
                await destStream.FlushAsync(token);

                // Collect the hashes computed during the copy (these match the bytes written).
                Dictionary<string, string> calculatedHashes = new Dictionary<string, string>();
                foreach (var algorithm in hashers.Keys)
                {
                    calculatedHashes[algorithm] = BitConverter.ToString(hashers[algorithm].Hash).Replace("-", "").ToLower();
                }

                // Clean up the hash algorithms
                foreach (var hasher in hashers.Values)
                {
                    hasher.Dispose();
                }

                return calculatedHashes;
            }
            catch (OperationCanceledException)
            {
                // Cleanup: delete partial destination file
                if (File.Exists(destPath))
                {
                    try { File.Delete(destPath); } catch { /* Ignore cleanup errors */ }
                }
                throw;
            }
            catch (Exception ex)
            {
                WriteDetailedLog($"Error copying file: {ex.Message}");
                // Cleanup: delete partial destination file
                if (File.Exists(destPath))
                {
                    try { File.Delete(destPath); } catch { /* Ignore cleanup errors */ }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the full path to a backup file based on its file ID
        /// </summary>
        private string GetBackupFilePath(string fileId)
        {
            return Path.Combine(fileId.Substring(0, 2), fileId);
        }

        /// <summary>
        /// Gets the output path for a file based on its domain and relative path
        /// </summary>
        private string GetOutputPath(string domain, string relativePath)
        {
            // Use the DomainMapper to get the correct path
            return DomainMapper.MapPath(domain, relativePath);
        }

        /// <summary>
        /// Pauses the extraction process
        /// </summary>
        public void Pause()
        {
            try
            {
                // Use atomic operation to set the pause flag
                // Set _isPaused to 1 if it's currently 0 (not paused)
                if (Interlocked.CompareExchange(ref _isPaused, 1, 0) == 0)
                {
                    // Only log if we actually changed the state
                    LogMessage?.Invoke("Normalization process paused");
                    WriteDetailedLog("Pause requested");
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw
                Debug.WriteLine($"Error in Pause method: {ex.Message}");
            }
        }

        /// <summary>
        /// Resumes the extraction process
        /// </summary>
        public void Resume()
        {
            try
            {
                // Use atomic operation to set the pause flag
                // Set _isPaused to 0 if it's currently 1 (paused)
                if (Interlocked.CompareExchange(ref _isPaused, 0, 1) == 1)
                {
                    // Only log if we actually changed the state
                    LogMessage?.Invoke("Normalization process resumed");
                    WriteDetailedLog("Resume requested");
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw
                Debug.WriteLine($"Error in Resume method: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancels the extraction process
        /// </summary>
        public void Cancel()
        {
            _isCancelled = true;
            
            // Only write to detailed log - don't send message to UI to avoid duplication
            WriteDetailedLog("Normalization process cancellation requested");
            
            // Don't log additional message to UI, let the ViewModel handle this
            // LogMessage?.Invoke("Normalization cancellation in progress. Please wait...");
        }

        /// <summary>
        /// Writes a message to the detailed log file
        /// </summary>
        public void WriteDetailedLog(string message, bool filterVerbose = true)
        {
            if (string.IsNullOrEmpty(_detailedLogPath))
                return;

            // Skip verbose messages if filtering is enabled
            if (filterVerbose && (
                message.Contains("File already exists") || 
                message.Contains("skipping") ||
                message.Contains("Created directory") ||
                message.Contains("Processing file") ||
                message.Contains("Successfully processed file") ||
                message.Contains("File progress:") ||
                message.Contains("Created empty file:") ||
                (message.Contains("Processed file:") && message.Contains("Hashes:"))
            ))
                return;

            try
            {
                // System/status messages are written as '#' comment lines so they never appear as
                // data rows in the CSV (keeping the table clean: one data row == one file entry).
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string comment = $"# {timestamp}  {message.Replace("\r", " ").Replace("\n", " ")}";
                lock (_logLock)
                {
                    if (!File.Exists(_detailedLogPath))
                        InitializeDetailedLogging();
                    File.AppendAllText(_detailedLogPath, comment + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to write to detailed log: {ex.Message}");
            }
        }

        // Helper method to check if we're paused
        private async Task CheckIfPausedAsync(CancellationToken token)
        {
            const int pauseCheckInterval = 100; // Check every 100ms
            
            try
            {
                while (_isPaused != 0)
                {
                    LogMessage?.Invoke("Extraction paused...");
                    
                    // Check if cancelled while paused
                    if (_isCancelled || token.IsCancellationRequested)
                    {
                        LogMessage?.Invoke("Cancellation requested while paused");
                        throw new OperationCanceledException("Operation was cancelled while paused");
                    }
                    
                    // Wait a short time before checking again
                    await Task.Delay(pauseCheckInterval, token);
                }
            }
            catch (TaskCanceledException)
            {
                // This is expected if token is cancelled during delay
                throw new OperationCanceledException("Operation was cancelled during pause");
            }
        }
        
        // Non-async version for use in synchronous methods
        private bool CheckIfPaused(CancellationToken token)
        {
            // Quick check first
            if (_isPaused == 0)
                return false;
            
            // We're paused, implement delay loop with frequent cancellation checks
            bool wasPaused = false;
            
            try
            {
                while (_isPaused != 0)
                {
                    // IMPORTANT: Always check cancellation first
                    if (_isCancelled || token.IsCancellationRequested)
                        return wasPaused;
                        
                    wasPaused = true;
                    
                    // Use a shorter delay and check more frequently for better responsiveness
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CheckIfPaused: {ex.Message}");
            }
            
            // Return true if we were actually paused at any point
            return wasPaused;
        }

        private void InitializeDetailedLog()
        {
            try
            {
                // Create the directory if it doesn't exist
                string logDirectory = Path.GetDirectoryName(_detailedLogPath);
                if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Write the header, making sure it matches the format in InitializeDetailedLogging
                string header = "Timestamp,FileID,FileCopied,MD5,SHA1,SHA256";
                File.WriteAllText(_detailedLogPath, header + Environment.NewLine);
                
                LogMessage?.Invoke($"Initialized detailed log file at {_detailedLogPath}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to initialize detailed log file: {ex.Message}");
            }
        }

        // Update method for logging file processing results with hash values
        private void LogFileProcessingResult(string fileId, string relativePath, Dictionary<string, string> hashValues, bool success)
        {
            if (string.IsNullOrEmpty(_detailedLogPath))
                return;
                
            // We want to log successful file operations with hash values
            // Removed: if (success) return;

            try
            {
                // Format timestamp as proper human-readable format
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                // Format FileID to show folder/file structure
                string formattedFileID = string.IsNullOrEmpty(fileId) ? string.Empty : 
                    (fileId.Length >= 2 ? $"{fileId.Substring(0, 2)}/{fileId}" : fileId);
                
                // Only include path if copy was successful
                string fileCopied = success ? relativePath : "Failed to copy file";
                
                // Extract hash values, use empty string if not available
                string md5 = hashValues != null && hashValues.ContainsKey("md5") ? hashValues["md5"] : string.Empty;
                string sha1 = hashValues != null && hashValues.ContainsKey("sha1") ? hashValues["sha1"] : string.Empty;
                string sha256 = hashValues != null && hashValues.ContainsKey("sha256") ? hashValues["sha256"] : string.Empty;
                
                // Create the log entry
                string logEntry = $"{timestamp},{formattedFileID},{fileCopied},{md5},{sha1},{sha256}";
                
                // Append to log file
                lock (_logLock)
                {
                    if (!File.Exists(_detailedLogPath))
                    {
                        // Try to recreate the log file if it was deleted
                        InitializeDetailedLogging();
                    }
                    
                    File.AppendAllText(_detailedLogPath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to write to detailed log: {ex.Message}");
            }
        }

        // Helper method to copy files without hash verification
        private async Task CopyWithoutHashAsync(string sourcePath, string destinationPath, CancellationToken token)
        {
            // Use FileOptions.SequentialScan for better performance with large files
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);
            
            // Use a moderately sized buffer for efficient I/O
            byte[] buffer = new byte[81920]; // 80 KB buffer
            int bytesRead;
            
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                // Check if paused
                if (_isPaused != 0)
                {
                    await CheckIfPausedAsync(token);
                }
                
                await destinationStream.WriteAsync(buffer, 0, bytesRead, token);
            }
        }

        private Dictionary<string, string> CalculateFileHashes(string filePath, CancellationToken token)
        {
            var results = new Dictionary<string, string>();
            
            // If no hash algorithms are selected, return an empty dictionary
            if (_hashAlgorithms == null || _hashAlgorithms.Count == 0)
            {
                return results;
            }
            
            try
            {
                // Skip hash calculation for very large files
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 100 * 1024 * 1024) // 100 MB
                {
                    WriteDetailedLog($"Skipping hash calculation for large file ({fileInfo.Length / (1024 * 1024)} MB): {filePath}");
                    foreach (var algo in _hashAlgorithms)
                    {
                        results[algo] = "SKIPPED_LARGE_FILE";
                    }
                    return results;
                }
                
                // Set a timeout for hash calculations
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                
                // Use file stream with minimal buffer to reduce memory usage
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                
                foreach (var algorithm in _hashAlgorithms)
                {
                    // Check for cancellation or pause
                    linkedCts.Token.ThrowIfCancellationRequested();
                    CheckIfPaused(linkedCts.Token);
                    
                    try
                    {
                        HashAlgorithm hashAlgorithm = algorithm.ToLowerInvariant() switch
                        {
                            "md5" => MD5.Create(),
                            "sha1" => SHA1.Create(),
                            "sha256" => SHA256.Create(),
                            _ => throw new ArgumentException($"Unsupported hash algorithm: {algorithm}")
                        };
                        
                        // Reset position to start of file for each algorithm
                        fileStream.Position = 0;
                        
                        // Calculate hash with timeout protection
                        byte[] hashBytes;
                        using (hashAlgorithm)
                        {
                            hashBytes = hashAlgorithm.ComputeHash(fileStream);
                        }
                        
                        string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                        results[algorithm] = hashString;
                    }
                    catch (OperationCanceledException)
                    {
                        if (timeoutCts.IsCancellationRequested)
                        {
                            WriteDetailedLog($"Hash calculation timed out for algorithm {algorithm} on file: {filePath}");
                            results[algorithm] = "TIMEOUT";
                        }
                        else
                        {
                            throw; // Rethrow if it was a normal cancellation
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteDetailedLog($"Error calculating {algorithm} hash for {filePath}: {ex.Message}");
                        results[algorithm] = $"ERROR: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}";
                    }
                }
            }
            catch (IOException ex)
            {
                WriteDetailedLog($"IO exception accessing file {filePath}: {ex.Message}");
                foreach (var algo in _hashAlgorithms)
                {
                    results[algo] = "FILE_ACCESS_ERROR";
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Rethrow cancellation
            }
            catch (Exception ex)
            {
                WriteDetailedLog($"Unexpected exception during hash calculation for {filePath}: {ex.Message}");
                foreach (var algo in _hashAlgorithms)
                {
                    results[algo] = "HASH_ERROR";
                }
            }
            
            return results;
        }
    }
}



