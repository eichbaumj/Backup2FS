using Backup2FS.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Backup2FS.Core.Services;
using System.Threading;

// Add reference for FolderBrowserDialog
using WinForms = System.Windows.Forms;

namespace Backup2FS.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        #region Properties

        [ObservableProperty]
        private string _backupFolder = string.Empty;

        [ObservableProperty]
        private string _outputFolder = string.Empty;

        [ObservableProperty]
        private string _deviceName = string.Empty;

        [ObservableProperty]
        private string _model = string.Empty;

        [ObservableProperty]
        private string _iosVersion = string.Empty;

        [ObservableProperty]
        private string _buildVersion = string.Empty;

        [ObservableProperty]
        private string _serialNumber = string.Empty;

        [ObservableProperty]
        private string _uniqueDeviceId = string.Empty;

        [ObservableProperty]
        private string _imei = string.Empty;

        [ObservableProperty]
        private string _meid = string.Empty;

        [ObservableProperty]
        private string _iccid = string.Empty;

        [ObservableProperty]
        private string _isEncrypted = "No";

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _lastBackupDate = string.Empty;

        [ObservableProperty]
        private ObservableCollection<InstalledApp> _installedApps = new();

        [ObservableProperty]
        private int _progressValue = 0;

        [ObservableProperty]
        private string _logOutput = string.Empty;

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _isPaused = false;

        [ObservableProperty]
        private bool _isCompleted = false;

        [ObservableProperty]
        private bool _canSaveLog = false;

        [ObservableProperty]
        private bool _usesMD5 = false;

        [ObservableProperty]
        private bool _usesSHA1 = false;

        [ObservableProperty]
        private bool _usesSHA256 = true;

        [ObservableProperty]
        private bool _isOptionsDialogOpen = false;

        [ObservableProperty]
        private bool _isNormalizing;

        [ObservableProperty]
        private ObservableCollection<string> _logs = new();

        [ObservableProperty]
        private int _normalizationProgress = 0;

        [ObservableProperty]
        private int _totalNormalizationFiles = 0;

        [ObservableProperty]
        private int _normalizationProgressPercent = 0;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private ObservableCollection<string> _hashAlgorithms = new() { "MD5", "SHA-1", "SHA-256" };

        private BackgroundWorker? _worker;
        private string? _tempLogPath;
        private StringBuilder _logBuilder = new StringBuilder();
        private bool _cancelRequested = false;

        private readonly BackupExtractorService _backupExtractorService;
        private CancellationTokenSource _extractionCancellationTokenSource;

        [ObservableProperty]
        private string _selectedHashAlgorithm = "sha256";

        // Target progress value to animate towards
        private int _targetProgressValue = 0;
        
        // Animation timer for smooth progress
        private System.Windows.Threading.DispatcherTimer _progressAnimationTimer;

        // Store the last time we changed pause state - for debouncing
        private long _lastPauseOperationTicks = 0;

        // Animate progress changes for visual smoothness
        private void InitializeProgressAnimation()
        {
            _progressAnimationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30) // Update ~33 times per second
            };
            
            _progressAnimationTimer.Tick += (s, e) =>
            {
                // If we're at the target or past it, stop animation
                if (ProgressValue >= _targetProgressValue)
                {
                    _progressAnimationTimer.Stop();
                    return;
                }
                
                // Calculate step size - larger steps when further from target
                int stepSize = Math.Max(1, (_targetProgressValue - ProgressValue) / 15);
                
                // Update progress value with animation step
                ProgressValue += stepSize;
                
                // If we've reached or passed the target, set to exact target
                if (ProgressValue >= _targetProgressValue)
                {
                    ProgressValue = _targetProgressValue;
                    _progressAnimationTimer.Stop();
                }
            };
        }
        
        // Set progress with animation
        private void SetAnimatedProgress(int progressPercent)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // If progress is going backward (like reset to 0), do it instantly
                if (progressPercent < ProgressValue)
                {
                    ProgressValue = progressPercent;
                    _targetProgressValue = progressPercent;
                    return;
                }
                
                // Set the target to animate towards
                _targetProgressValue = progressPercent;
                
                // Start animation if it's not running
                if (!_progressAnimationTimer.IsEnabled)
                {
                    _progressAnimationTimer.Start();
                }
            });
        }

        // Add a private method to update hash algorithm flags based on selected algorithm
        private void UpdateHashAlgorithmFlags()
        {
            UsesMD5 = SelectedHashAlgorithm.Equals("md5", StringComparison.OrdinalIgnoreCase);
            UsesSHA1 = SelectedHashAlgorithm.Equals("sha1", StringComparison.OrdinalIgnoreCase);
            UsesSHA256 = SelectedHashAlgorithm.Equals("sha256", StringComparison.OrdinalIgnoreCase);
        }

        // Add partial method to handle property changes
        partial void OnSelectedHashAlgorithmChanged(string value)
        {
            UpdateHashAlgorithmFlags();
        }

        partial void OnNormalizationProgressPercentChanged(int value)
        {
            // Update animated progress instead of directly setting ProgressValue
            SetAnimatedProgress(value);
        }

        partial void OnUsesMD5Changed(bool value)
        {
            // Update hash algorithms when checkbox state changes
            UpdateBackupExtractorHashAlgorithms();
        }
        
        partial void OnUsesSHA1Changed(bool value)
        {
            // Update hash algorithms when checkbox state changes
            UpdateBackupExtractorHashAlgorithms();
        }
        
        partial void OnUsesSHA256Changed(bool value)
        {
            // Update hash algorithms when checkbox state changes
            UpdateBackupExtractorHashAlgorithms();
        }
        
        // Method to update hash algorithms based on checkbox states
        private void UpdateBackupExtractorHashAlgorithms()
        {
            // Create a list of selected hash algorithms
            var selectedAlgorithms = new List<string>();
            
            if (UsesMD5)
                selectedAlgorithms.Add("md5");
                
            if (UsesSHA1)
                selectedAlgorithms.Add("sha1");
                
            if (UsesSHA256)
                selectedAlgorithms.Add("sha256");
            
            // If no algorithms selected, default to SHA256
            if (selectedAlgorithms.Count == 0)
            {
                UsesSHA256 = true;
                selectedAlgorithms.Add("sha256");
            }
            
            // Update the backup extractor service
            if (_backupExtractorService != null)
            {
                _backupExtractorService.SetHashAlgorithms(selectedAlgorithms);
            }
        }

        [ObservableProperty]
        private bool _canOpenOutputFolder = false;

        #endregion

        #region Commands

        [RelayCommand]
        private void BrowseBackup()
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select iOS Backup Folder",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                BackupFolder = dialog.SelectedPath;
                LoadBackupInfo();
            }
        }

        [RelayCommand]
        private void BrowseOutput()
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select Destination Folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                OutputFolder = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private async Task NormalizeAsync()
        {
            try
            {
                // Reset counters and state
                NormalizationProgress = 0;
                TotalNormalizationFiles = 0;
                NormalizationProgressPercent = 0;
                ProgressValue = 0;
                IsBusy = true;
                IsPaused = false;
                IsNormalizing = true;
                IsProcessing = true;  // Set IsProcessing flag to prevent re-normalization
                
                // Reset logs
                Logs.Clear();
                _logBuilder.Clear();
                LogOutput = string.Empty;
                AddLog($"Starting normalization process at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Force UI refresh
                await Task.Delay(50);
                
                // Validate paths
                if (string.IsNullOrEmpty(BackupFolder))
                {
                    AddLog("Backup folder path is not set. Please select a backup folder.");
                    IsNormalizing = false;
                    IsBusy = false;
                    IsProcessing = false;
                    return;
                }

                if (string.IsNullOrEmpty(OutputFolder))
                {
                    AddLog("Output folder path is not set. Please select an output folder.");
                    IsNormalizing = false;
                    IsBusy = false;
                    IsProcessing = false;
                    return;
                }
                
                // Create cancellation token
                _extractionCancellationTokenSource = new CancellationTokenSource();
                
                // Log hash algorithms being used
                string hashAlgorithms = "";
                if (UsesMD5) hashAlgorithms += "MD5 ";
                if (UsesSHA1) hashAlgorithms += "SHA-1 ";
                if (UsesSHA256) hashAlgorithms += "SHA-256 ";
                AddLog($"Using hash algorithms: {hashAlgorithms.Trim()}");
                
                // Force UI refresh
                await Task.Delay(50);
                
                // Update hash algorithms in the backup extractor service
                UpdateBackupExtractorHashAlgorithms();
                
                // Set up the progress reporting to update the UI thread properly
                _backupExtractorService.ProgressReport += (progress) =>
                {
                    // This will be called from a background thread, so we need to dispatch to UI thread
                    // Use BeginInvoke for immediate handling without waiting
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            NormalizationProgressPercent = progress;
                            // Force immediate update of progress
                            ProgressValue = progress;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating progress: {ex.Message}");
                        }
                    }));
                };
                
                // Start the extraction process (on a background thread via Task.Run)
                AddLog("Starting normalization process");
                
                // Force UI refresh
                await Task.Delay(50);
                
                // Run the actual extraction on a separate thread to keep UI responsive
                bool result = await Task.Run(async () => 
                {
                    try 
                    {
                        return await _backupExtractorService.ExtractBackupAsync(
                            BackupFolder, 
                            OutputFolder, 
                            _extractionCancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        // Log exceptions that happen on the background thread
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AddLog($"Error during background processing: {ex.Message}");
                            AddLog($"Stack trace: {ex.StackTrace}");
                        }));
                        return false;
                    }
                });
                
                // Process the result on the UI thread
                if (result)
                {
                    // The BackupExtractorService already logs a success message, no need to duplicate it here
                    // We'll only enable the open folder button
                    CanOpenOutputFolder = true;
                }
                else
                {
                    if (_extractionCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        AddLog("Normalization was cancelled by user");
                    }
                    else
                    {
                        AddLog("Normalization failed. Check the logs for details");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error during normalization: {ex.Message}");
                AddLog($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Clean up
                try
                {
                    _extractionCancellationTokenSource?.Dispose();
                    _extractionCancellationTokenSource = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing cancellation token: {ex.Message}");
                }
                
                // Reset state flags but keep IsProcessing true to prevent re-normalization
                IsBusy = false;
                IsPaused = false;
                IsNormalizing = false;
                
                // Only set CanSaveLog if we have logs to save
                if (!string.IsNullOrEmpty(LogOutput))
                {
                    CanSaveLog = true;
                }
            }
        }

        /// <summary>
        /// Command to pause or resume the normalization process
        /// </summary>
        [RelayCommand]
        private void PauseResume()
        {
            try
            {
                // Immediately update UI state without waiting for the service
                // Toggle state first to ensure UI responsiveness
                IsPaused = !IsPaused;
                
                // Use BeginInvoke to avoid blocking the UI thread
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Debounce - but don't prevent the UI from updating
                        long currentTicks = DateTime.Now.Ticks;
                        long elapsedMs = (currentTicks - _lastPauseOperationTicks) / TimeSpan.TicksPerMillisecond;
                        
                        // Store last operation time
                        _lastPauseOperationTicks = currentTicks;
                        
                        // Check if we have a service instance
                        if (_backupExtractorService == null)
                        {
                            AddLog("No active normalization process to pause/resume");
                            return;
                        }
                        
                        // Call the appropriate method on the service
                        if (IsPaused)
                        {
                            // We're now paused
                            AddLog("Pausing normalization process...");
                            _backupExtractorService.Pause();
                        }
                        else
                        {
                            // We're now resumed
                            AddLog("Resuming normalization process...");
                            _backupExtractorService.Resume();
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"Error processing pause/resume: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                // Ensure we don't crash even if dispatcher fails
                Debug.WriteLine($"Critical error in PauseResume: {ex.Message}");
                try { AddLog($"Error toggling pause/resume: {ex.Message}"); } catch { }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (!IsNormalizing)
                return;

            // Ask for confirmation
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to cancel the normalization process?",
                "Cancel Normalization",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _backupExtractorService.Cancel();
                _extractionCancellationTokenSource?.Cancel();
                AddLog($"Normalization cancelled at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Reset state flags but keep IsProcessing true to prevent re-normalization
                IsNormalizing = false;
                IsBusy = false;
                // IsProcessing = false; // Keep this true to prevent re-normalization
                IsCompleted = true;
                CanSaveLog = true;
                
                // Reset progress
                SetAnimatedProgress(0);
            }
        }

        [RelayCommand]
        private void SaveLog()
        {
            // Create a save file dialog
            var dialog = new WinForms.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = ".csv",
                Title = "Save Log File"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                try
                {
                    // Check if we have a detailed log file from the extractor service
                    bool detailedLogExists = _backupExtractorService != null && 
                                            !string.IsNullOrEmpty(_backupExtractorService.DetailedLogPath) && 
                                            File.Exists(_backupExtractorService.DetailedLogPath);
                    
                    // Create a combined log file that includes both UI logs and detailed file logs
                    using (StreamWriter writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
                    {
                        // First, write the UI log content
                        writer.WriteLine("===== BACKUP2FS LOG =====");
                        writer.WriteLine($"Log created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine();
                        writer.WriteLine("===== UI LOG =====");
                        writer.WriteLine(LogOutput);
                        writer.WriteLine();
                        
                        // If detailed log exists, append its content
                        if (detailedLogExists)
                        {
                            writer.WriteLine("===== DETAILED FILE LOG =====");
                            writer.WriteLine("Timestamp,FileID,Domain,RelativePath,Hash,Status");
                            
                            // Read and write the detailed log file content (skipping the header)
                            string[] detailedLogLines = File.ReadAllLines(_backupExtractorService.DetailedLogPath);
                            // Skip header line if it exists
                            for (int i = detailedLogLines.Length > 1 ? 1 : 0; i < detailedLogLines.Length; i++)
                            {
                                writer.WriteLine(detailedLogLines[i]);
                            }
                        }
                    }
                    
                    AddLog($"Log saved to: {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    AddLog($"Error saving log: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void OpenFolder()
        {
            if (string.IsNullOrEmpty(OutputFolder))
            {
                AddLog("Output folder not specified.");
                return;
            }

            try
            {
                // Check if folder exists
                if (!Directory.Exists(OutputFolder))
                {
                    AddLog($"Output folder does not exist: {OutputFolder}");
                    return;
                }

                // Try to open the folder
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = OutputFolder,
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                AddLog($"Opened folder: {OutputFolder}");
            }
            catch (Exception ex)
            {
                AddLog($"Error opening folder: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ShowAbout()
        {
            // Create and show the about dialog
            // In a full implementation, we would show a MaterialDesign dialog
            System.Windows.MessageBox.Show(
                "Backup2FS\n\nNormalizes and converts iOS backups into a standard iOS file-system structure for forensic analysis.\n\nCreated by James Eichbaum\n© Elusive Data 2025",
                "About Backup2FS",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        [RelayCommand]
        private void Exit()
        {
            // Clean up resources
            if (_tempLogPath != null && File.Exists(_tempLogPath))
            {
                try
                {
                    File.Delete(_tempLogPath);
                }
                catch
                {
                    // Ignore deletion errors on exit
                }
            }

            // Close the application
            System.Windows.Application.Current.Shutdown();
        }

        [RelayCommand]
        private void OpenOptionsDialog()
        {
            IsOptionsDialogOpen = true;
        }

        [RelayCommand]
        private void CloseOptionsDialog()
        {
            IsOptionsDialogOpen = false;
        }

        [RelayCommand]
        private void SaveOptions()
        {
            // Save options logic would go here
            IsOptionsDialogOpen = false;
        }

        [RelayCommand]
        private void NewSession()
        {
            // Ask for confirmation
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to start a new session? This will clear the current logs and reset the application state.",
                "New Session",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Reset all state flags
                IsProcessing = false;
                IsNormalizing = false;
                IsBusy = false;
                IsPaused = false;
                IsCompleted = false;
                CanSaveLog = false;
                
                // Clear logs
                Logs.Clear();
                _logBuilder.Clear();
                LogOutput = string.Empty;
                
                // Reset progress
                NormalizationProgress = 0;
                TotalNormalizationFiles = 0;
                NormalizationProgressPercent = 0;
                SetAnimatedProgress(0);
                
                AddLog("Started a new session. Ready for normalization.");
            }
        }

        #endregion

        #region Private Methods

        private void LoadBackupInfo()
        {
            if (string.IsNullOrEmpty(BackupFolder) || !Directory.Exists(BackupFolder))
            {
                ClearDeviceInfo();
                return;
            }

            // Clear existing data
            ClearDeviceInfo();
            AddLog("Loading backup information...");

            // Run parsing and initial population asynchronously to keep UI responsive
            Task.Run(() => 
            {
                try
                {
                    // Create a PlistParser to handle the backup files
                    var plistParser = new Backup2FS.Core.Services.PlistParser();

                    // Check for Manifest.db
                    bool hasManifestDb = plistParser.HasValidManifestDb(BackupFolder);
                    if (!hasManifestDb)
                    {
                        AddLog("Error: Manifest.db not found in the backup folder.");
                        return;
                    }

                    // Check for Manifest.plist
                    string manifestPlistPath = Path.Combine(BackupFolder, "Manifest.plist");
                    if (!File.Exists(manifestPlistPath))
                    {
                        AddLog("Error: Manifest.plist not found in the backup folder.");
                        return;
                    }

                    // Check if backup is encrypted
                    bool isEncrypted = plistParser.IsBackupEncrypted(BackupFolder);
                    if (isEncrypted)
                    {
                        // Update UI thread for this property
                         System.Windows.Application.Current?.Dispatcher.Invoke(() => IsEncrypted = "Yes");
                        AddLog("This backup is encrypted. Please decrypt it first to proceed with normalization.");
                        return;
                    }
                    else
                    {
                         System.Windows.Application.Current?.Dispatcher.Invoke(() => IsEncrypted = "No");
                        AddLog("Backup is not encrypted. Processing can continue.");
                    }

                    // Reset the icon repository (if still used, maybe remove if not needed)
                    // Backup2FS.Core.Models.AppIconRepository.Reset(); 
                    
                    // Parse device information (this might still be the slow part)
                    AddLog("Parsing device information and installed apps from backup (this may take a moment)...");
                    var deviceInfo = plistParser.ParseDeviceInfo(BackupFolder, AddLog);
                    AddLog("Finished parsing basic device info and app list.");

                    // Update UI properties on the UI thread
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => 
                    {
                        DeviceName = deviceInfo.DeviceName ?? string.Empty;
                        Model = deviceInfo.Model ?? string.Empty;
                        IosVersion = deviceInfo.IosVersion ?? string.Empty;
                        BuildVersion = deviceInfo.BuildVersion ?? string.Empty;
                        SerialNumber = deviceInfo.SerialNumber ?? string.Empty;
                        UniqueDeviceId = deviceInfo.UniqueDeviceId ?? string.Empty;
                        Imei = deviceInfo.Imei ?? string.Empty;
                        Meid = deviceInfo.Meid ?? string.Empty;
                        Iccid = deviceInfo.Iccid ?? string.Empty;
                        PhoneNumber = deviceInfo.PhoneNumber ?? string.Empty;
                        LastBackupDate = deviceInfo.LastBackupDate ?? string.Empty;
                        
                        // Clear and add apps to the collection first.
                        InstalledApps.Clear();
                        AddLog($"Adding {deviceInfo.InstalledApps.Count} installed applications to list...");
                        foreach (var app in deviceInfo.InstalledApps)
                        {
                            InstalledApps.Add(app); 
                        }

                        // Process icons in the background without detailed logging
                        System.Threading.Thread.Sleep(300);
                        Task.Run(async () => 
                        {
                            try 
                            {
                                await ProcessAppIcons();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error during icon processing: {ex.Message}");
                            }
                        });

                        // Log completion of initial info loading
                        AddLog($"Successfully loaded device information from backup.");
                    });
                }
                catch (Exception ex)
                {
                    AddLog($"Error loading backup info: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        AddLog($"Inner exception: {ex.InnerException.Message}");
                    }
                }
            });
        }

        private void ClearDeviceInfo()
        {
            DeviceName = string.Empty;
            Model = string.Empty;
            IosVersion = string.Empty;
            BuildVersion = string.Empty;
            SerialNumber = string.Empty;
            UniqueDeviceId = string.Empty;
            Imei = string.Empty;
            Meid = string.Empty;
            Iccid = string.Empty;
            IsEncrypted = "No";
            PhoneNumber = string.Empty;
            LastBackupDate = string.Empty;
            InstalledApps.Clear();
        }

        private void AddLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                // Filter out unnecessary technical messages
                if (message.Contains("SQLite interop") || 
                    message.Contains("architecture") || 
                    message.Contains("Using hash algorithm") ||
                    message.Contains("Output folder set to") || 
                    message.Contains("Database version") ||
                    message.Contains("[PlistParser] Adding app") ||
                    (message.Contains("Device model:") && !message.Contains("error")))
                    return;

                // Special messages without timestamps
                bool isSpecialMessage = message == "Normalization paused" || 
                                        message == "Normalization resumed";
                
                string formattedMessage = isSpecialMessage 
                    ? $"{message}" 
                    : $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

                // Use BeginInvoke to avoid blocking the UI thread
                // This allows the UI to stay responsive even when many log messages are being added
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Add to StringBuilder for the log output
                        _logBuilder.AppendLine(formattedMessage);
                        LogOutput = _logBuilder.ToString();
                        
                        // Add to logs collection for future reference
                        Logs.Add(formattedMessage);
                    }
                    catch (Exception ex)
                    {
                        // If we encounter errors updating the log UI, log to debug output
                        Debug.WriteLine($"Error updating log UI: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                // Catch any errors to prevent crashes
                Debug.WriteLine($"Error in AddLog: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes app icons using batch processing for better performance
        /// </summary>
        private async Task ProcessAppIcons()
        {
            if (InstalledApps == null || InstalledApps.Count == 0)
            {
                return;
            }

            // Process with optimized batching
            await ProcessIconsSequentiallyAsync();
        }

        /// <summary>
        /// Processes app icons in batches for better performance
        /// </summary>
        private async Task ProcessIconsSequentiallyAsync()
        {
            try
            {
                // Important: Create a copy to avoid collection modification issues
                var appsList = InstalledApps.ToList();
                
                // Batch size for processing
                const int batchSize = 10;
                
                // Process apps in batches
                for (int i = 0; i < appsList.Count; i += batchSize)
                {
                    if (_cancelRequested)
                        break;
                        
                    // Create a batch of tasks to process multiple icons in parallel
                    var tasks = new List<Task>();
                    var batch = appsList.Skip(i).Take(batchSize).Where(app => app != null && app.RawAppData != null);
                    
                    // Start all tasks in the batch
                    foreach (var app in batch)
                    {
                        tasks.Add(Task.Run(async () => {
                            try
                            {
                                await app.ProcessAndLoadIconAsync();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error processing icon for {app.BundleId}: {ex.Message}");
                            }
                        }));
                    }
                    
                    // Wait for the batch to complete
                    if (tasks.Any())
                        await Task.WhenAll(tasks);
                        
                    // Short pause between batches to let UI update
                    await Task.Delay(10);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during sequential icon processing: {ex.Message}");
            }
        }

        // Helper method for logging
        private void LogMessage(string message, bool isError = false)
        {
            Debug.WriteLine(message);
            AddLog(message);
        }

        public MainViewModel()
        {
            // Initialize hash algorithm options
            HashAlgorithms = new ObservableCollection<string>
            {
                "SHA-1",
                "SHA-256",
                "MD5"
            };
            SelectedHashAlgorithm = "SHA-1";
            
            // Initialize progress animation
            InitializeProgressAnimation();
            
            // Initialize the BackupExtractorService
            _backupExtractorService = new BackupExtractorService();
            _backupExtractorService.LogMessage += AddLog;
            _backupExtractorService.ProgressReport += (progress) =>
            {
                // Update progress percentage
                NormalizationProgressPercent = progress;
            };
            
            // Set default hash algorithms (SHA-256)
            UpdateBackupExtractorHashAlgorithms();
            
            // Initialize the CancellationTokenSource
            _extractionCancellationTokenSource = new CancellationTokenSource();
            
            // Initialize commands
            // ... existing code ...
        }

        // Add a partial method to set CanSaveLog and IsCompleted when normalization completes
        partial void OnIsNormalizingChanged(bool value)
        {
            // When normalization ends (changed from true to false)
            if (!value)
            {
                if (!string.IsNullOrEmpty(OutputFolder))
                {
                    IsCompleted = true;
                }
                
                // Do NOT reset IsProcessing here to keep Normalize button disabled
                // IsProcessing = false;
                
                // Remove redundant notification - the success message is enough
                // if (IsCompleted)
                // {
                //     AddLog($"Normalization complete. You can now open the output folder.");
                // }
            }
        }

        #endregion
    }
} 