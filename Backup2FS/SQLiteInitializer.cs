using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Backup2FS
{
    public static class SQLiteInitializer
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        public static void Initialize()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                
                Console.WriteLine($"SQLite initialization - looking in directory: {assemblyDirectory}");

                // Try loading from various locations
                string[] searchPaths = new[]
                {
                    // Current directory
                    Path.Combine(assemblyDirectory, "SQLite.Interop.dll"),
                    
                    // x64 subdirectory
                    Path.Combine(assemblyDirectory, "x64", "SQLite.Interop.dll"),
                    
                    // x86 subdirectory
                    Path.Combine(assemblyDirectory, "x86", "SQLite.Interop.dll"),
                    
                    // Standard .NET runtime locations
                    Path.Combine(assemblyDirectory, "runtimes", "win-x64", "native", "SQLite.Interop.dll"),
                    Path.Combine(assemblyDirectory, "runtimes", "win-x86", "native", "SQLite.Interop.dll")
                };

                // Log which paths we're checking
                foreach (string path in searchPaths)
                {
                    Console.WriteLine($"Checking for SQLite.Interop.dll at: {path} - Exists: {File.Exists(path)}");
                }

                Exception lastException = null;
                foreach (string path in searchPaths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            IntPtr handle = LoadLibrary(path);
                            if (handle != IntPtr.Zero)
                            {
                                Console.WriteLine($"Successfully loaded SQLite from: {path}");
                                return;
                            }
                            else
                            {
                                int error = Marshal.GetLastWin32Error();
                                Console.WriteLine($"Failed to load {path}: Error code {error}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        Console.WriteLine($"Exception trying to load {path}: {ex.Message}");
                    }
                }

                if (lastException != null)
                {
                    // Log the exception but don't throw - the application may still work
                    Console.WriteLine($"Warning: Failed to load SQLite.Interop.dll: {lastException.Message}");
                    Console.WriteLine("Continuing without SQLite initialization - application may still work.");
                }
                else
                {
                    Console.WriteLine("Warning: Could not find SQLite.Interop.dll in any expected location.");
                    Console.WriteLine("Continuing without SQLite initialization - application may still work.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing SQLite: {ex.Message}");
                // Don't rethrow - allow the application to continue
            }
        }
    }
} 