using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Linq;
using Backup2FS.Services;

namespace Backup2FS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private Mutex _instanceMutex;
        private const string MutexName = "Backup2FS_SingleInstanceMutex";
        private bool _mutexOwned = false;
        private SettingsManager _settingsManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // Initialize settings manager
                _settingsManager = new SettingsManager();
                
                // Check for existing instance
                _instanceMutex = new Mutex(true, MutexName, out _mutexOwned);
                
                if (!_mutexOwned)
                {
                    // Another instance is running, notify the user and exit
                    System.Windows.MessageBox.Show("Backup2FS is already running.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
                
                // Initialize SQLite - try initialization but don't show error popup
                try
                {
                    InitializeSQLite();
                }
                catch (Exception ex)
                {
                    // Log error but don't show popup since application works anyway
                    Console.WriteLine($"SQLite initialization warning (non-fatal): {ex.Message}");
                }
                
                // Set up exception handlers
                AppDomain.CurrentDomain.UnhandledException += (s, args) => 
                {
                    HandleException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
                };
                    
                Current.DispatcherUnhandledException += (s, args) => 
                {
                    HandleException(args.Exception, "Application.Current.DispatcherUnhandledException");
                    args.Handled = true;
                };
            }
            catch (Exception ex)
            {
                HandleException(ex, "Application Startup");
                Shutdown(1);
            }
        }
        
        /// <summary>
        /// Initialize SQLite and ensure native libraries are properly loaded
        /// </summary>
        private void InitializeSQLite()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Architecture-specific DLLs
                string sqliteX86Dll = Path.Combine(appDir, "SQLite.Interop.x86.dll");
                string sqliteX64Dll = Path.Combine(appDir, "SQLite.Interop.x64.dll");
                
                // Target SQLite.Interop.dll
                string sqliteInteropDll = Path.Combine(appDir, "SQLite.Interop.dll");
                
                // Architecture detection
                bool is64BitProcess = Environment.Is64BitProcess;
                string sourceDll = is64BitProcess ? sqliteX64Dll : sqliteX86Dll;
                string architectureType = is64BitProcess ? "x64" : "x86";
                
                // Check if source DLL exists
                if (!File.Exists(sourceDll))
                {
                    // Check runtime folders
                    string runtimeSourceDll = Path.Combine(appDir, "runtimes", $"win-{architectureType}", "native", "SQLite.Interop.dll");
                    if (File.Exists(runtimeSourceDll))
                    {
                        sourceDll = runtimeSourceDll;
                    }
                    else
                    {
                        // Try to find it in the packages folder
                        string nugetFolder = Path.Combine(appDir, "packages");
                        if (Directory.Exists(nugetFolder))
                        {
                            var sqlitePackageDirs = Directory.GetDirectories(nugetFolder, "System.Data.SQLite.Core*");
                            foreach (var dir in sqlitePackageDirs)
                            {
                                string potentialDll = Path.Combine(dir, "runtimes", $"win-{architectureType}", "native", "SQLite.Interop.dll");
                                if (File.Exists(potentialDll))
                                {
                                    sourceDll = potentialDll;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // If we still don't have the source DLL, log it but don't show error since app works anyway
                if (!File.Exists(sourceDll))
                {
                    Console.WriteLine($"SQLite.Interop DLL for {architectureType} not found, but application may still work.");
                    return;
                }
                
                // Copy architecture-specific DLL to SQLite.Interop.dll if it doesn't exist or is different
                if (!File.Exists(sqliteInteropDll) || !AreFilesSame(sourceDll, sqliteInteropDll))
                {
                    try
                    {
                        // Make sure target directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(sqliteInteropDll));
                        
                        // Copy source to destination
                        File.Copy(sourceDll, sqliteInteropDll, true);
                        
                        Console.WriteLine($"Copied {sourceDll} to {sqliteInteropDll}");
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't show popup
                        Console.WriteLine($"Warning: Failed to copy SQLite library: {ex.Message}");
                        return;
                    }
                }
                
                // Test SQLite connection - if this works, the rest of the application should work too
                try
                {
                    using (var connection = new SQLiteConnection("Data Source=:memory:"))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT sqlite_version()";
                            string version = command.ExecuteScalar().ToString();
                            Console.WriteLine($"SQLite version: {version}");
                        }
                    }
                    
                    Console.WriteLine("SQLite initialization completed successfully.");
                }
                catch (Exception ex)
                {
                    // If we can't connect, log it but don't show error - application might still work
                    Console.WriteLine($"Warning: SQLite connection test failed: {ex.Message}");
                }
            }
            catch (DllNotFoundException dllEx)
            {
                // Log the error but don't show popup since it works anyway
                Console.WriteLine($"Warning: SQLite.Interop.dll not found: {dllEx.Message}");
            }
            catch (Exception ex)
            {
                // Log the error but don't show popup
                Console.WriteLine($"Warning: SQLite initialization warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Compares two files to check if they are identical
        /// </summary>
        private bool AreFilesSame(string file1, string file2)
        {
            if (!File.Exists(file1) || !File.Exists(file2))
                return false;
            
            if (new FileInfo(file1).Length != new FileInfo(file2).Length)
                return false;
            
            try
            {
                using (var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read))
                using (var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read))
                {
                    byte[] hash1;
                    byte[] hash2;
                    
                    using (var md5 = MD5.Create())
                    {
                        hash1 = md5.ComputeHash(fs1);
                        hash2 = md5.ComputeHash(fs2);
                    }
                    
                    return hash1.SequenceEqual(hash2);
                }
            }
            catch
            {
                return false;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Release the mutex if we own it
            if (_mutexOwned && _instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
            }
            
            base.OnExit(e);
        }

        private void HandleException(Exception ex, string source)
        {
            // Add proper error handling
            try
            {
                string errorMessage = $"An error occurred in {source}:\n{ex?.Message}";
                
                // Show error message
                System.Windows.MessageBox.Show(errorMessage, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Last resort if even the error handler fails
                System.Windows.MessageBox.Show("A critical error occurred in the application.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 