using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Backup2FS.Core.Models;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Reads backup file metadata from the iOS backup Manifest.db SQLite database
    /// </summary>
    public class ManifestDbReader : IDisposable
    {
        private SQLiteConnection _connection;
        private bool _isDisposed;
        
        /// <summary>
        /// Initializes a new instance of the ManifestDbReader
        /// </summary>
        public ManifestDbReader()
        {
        }
        
        /// <summary>
        /// Opens a connection to the Manifest.db file in the specified backup directory
        /// </summary>
        /// <param name="backupPath">Path to the iOS backup directory</param>
        /// <returns>True if connection was successful, false otherwise</returns>
        public bool OpenConnection(string backupPath)
        {
            try
            {
                if (string.IsNullOrEmpty(backupPath))
                {
                    return false;
                }
                
                string manifestDbPath = Path.Combine(backupPath, "Manifest.db");
                if (!File.Exists(manifestDbPath))
                {
                    return false;
                }
                
                // Create and open the SQLite connection
                string connectionString = $"Data Source={manifestDbPath};Version=3;Read Only=True;";
                _connection = new SQLiteConnection(connectionString);
                _connection.Open();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening Manifest.db: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Closes the SQLite connection if open
        /// </summary>
        public void CloseConnection()
        {
            if (_connection != null && _connection.State != System.Data.ConnectionState.Closed)
            {
                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }
        
        /// <summary>
        /// Gets the count of files in the backup
        /// </summary>
        /// <returns>Number of files in the backup, or -1 if error</returns>
        public int GetFileCount()
        {
            try
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                {
                    return -1;
                }
                
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM Files";
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting file count: {ex.Message}");
                return -1;
            }
        }
        
        /// <summary>
        /// Retrieves all backup files from the Manifest.db
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Enumerable of backup file metadata</returns>
        public async Task<IEnumerable<BackupFile>> GetBackupFilesAsync(CancellationToken cancellationToken = default)
        {
            var files = new List<BackupFile>();
            
            try
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                {
                    return files;
                }
                
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "SELECT fileID, domain, relativePath, flags, file FROM Files";
                    
                    using (var reader = await Task.Run(() => command.ExecuteReader(), cancellationToken))
                    {
                        while (await Task.Run(() => reader.Read(), cancellationToken))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }
                            
                            string fileId = reader.GetString(0);
                            string domain = reader.GetString(1);
                            string relativePath = reader.GetString(2);
                            int flags = reader.GetInt32(3);
                            
                            // Skip symlinks (flags = 4) and files with empty paths
                            if (flags == 4 || string.IsNullOrEmpty(relativePath))
                            {
                                continue;
                            }
                            
                            var file = new BackupFile
                            {
                                FileId = fileId,
                                Domain = domain,
                                RelativePath = relativePath,
                                Flags = flags
                            };
                            
                            files.Add(file);
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Console.WriteLine($"Error retrieving backup files: {ex.Message}");
            }
            
            return files;
        }
        
        /// <summary>
        /// Retrieves backup files with progress reporting
        /// </summary>
        /// <param name="progressCallback">Callback for progress updates</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Enumerable of backup file metadata</returns>
        public async Task<IEnumerable<BackupFile>> GetBackupFilesWithProgressAsync(
            Action<int, int> progressCallback,
            CancellationToken cancellationToken = default)
        {
            var files = new List<BackupFile>();
            
            try
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                {
                    return files;
                }
                
                // Get total file count for progress reporting
                int totalFiles = GetFileCount();
                int processedFiles = 0;
                
                if (totalFiles <= 0)
                {
                    return files;
                }
                
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "SELECT fileID, domain, relativePath, flags, file FROM Files";
                    
                    using (var reader = await Task.Run(() => command.ExecuteReader(), cancellationToken))
                    {
                        while (await Task.Run(() => reader.Read(), cancellationToken))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }
                            
                            string fileId = reader.GetString(0);
                            string domain = reader.GetString(1);
                            string relativePath = reader.GetString(2);
                            int flags = reader.GetInt32(3);
                            
                            // Skip symlinks (flags = 4) and files with empty paths
                            if (flags == 4 || string.IsNullOrEmpty(relativePath))
                            {
                                continue;
                            }
                            
                            var file = new BackupFile
                            {
                                FileId = fileId,
                                Domain = domain,
                                RelativePath = relativePath,
                                Flags = flags
                            };
                            
                            files.Add(file);
                            
                            // Report progress
                            processedFiles++;
                            if (processedFiles % 100 == 0 || processedFiles == totalFiles)
                            {
                                progressCallback?.Invoke(processedFiles, totalFiles);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Console.WriteLine($"Error retrieving backup files: {ex.Message}");
            }
            
            return files;
        }
        
        /// <summary>
        /// Gets the path to a backup file in the backup directory
        /// </summary>
        /// <param name="backupPath">Path to the iOS backup directory</param>
        /// <param name="fileId">File ID (SHA1 hash) of the file</param>
        /// <returns>Path to the file in the backup directory</returns>
        public static string GetBackupFilePath(string backupPath, string fileId)
        {
            if (string.IsNullOrEmpty(backupPath) || string.IsNullOrEmpty(fileId))
            {
                return null;
            }
            
            // In iOS backups, files are stored with their first 2 characters as a subdirectory
            string firstTwoChars = fileId.Substring(0, 2);
            return Path.Combine(backupPath, firstTwoChars, fileId);
        }

        /// <summary>
        /// Retrieves all backup files with paths mapped for normalization
        /// </summary>
        /// <param name="backupPath">Path to the iOS backup directory</param>
        /// <param name="logAction">Action to log messages</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>List of backup files with source and destination paths</returns>
        public async Task<List<BackupFile>> ReadFilesAsync(string backupPath, Action<string> logAction, CancellationToken cancellationToken = default)
        {
            try
            {
                logAction?.Invoke("Opening connection to Manifest.db...");
                if (!OpenConnection(backupPath))
                {
                    logAction?.Invoke("Failed to open connection to Manifest.db");
                    return new List<BackupFile>();
                }

                logAction?.Invoke("Reading backup files from database...");
                var files = await GetBackupFilesAsync(cancellationToken);
                var result = new List<BackupFile>();

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // Set the source path (where the file is in the backup)
                    string sourcePath = GetBackupFilePath(backupPath, file.FileId);
                    
                    // Set the destination path (where the file will be normalized to)
                    string destinationPath = DomainMapper.MapPath(file.Domain, file.RelativePath);
                    
                    // Only include files that exist
                    if (File.Exists(sourcePath))
                    {
                        file.SourcePath = sourcePath;
                        file.DestinationPath = destinationPath;
                        result.Add(file);
                    }
                }

                logAction?.Invoke($"Found {result.Count} valid files in backup");
                return result;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error reading files from backup: {ex.Message}");
                return new List<BackupFile>();
            }
            finally
            {
                CloseConnection();
            }
        }

        /// <summary>
        /// Disposes resources used by the ManifestDbReader
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Disposes resources used by the ManifestDbReader
        /// </summary>
        /// <param name="disposing">Whether this is being called from Dispose or a finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    CloseConnection();
                }
                
                _isDisposed = true;
            }
        }
        
        /// <summary>
        /// Finalizer for ManifestDbReader
        /// </summary>
        ~ManifestDbReader()
        {
            Dispose(false);
        }
    }
} 