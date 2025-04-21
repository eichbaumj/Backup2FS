using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace Backup2FS.Services
{
    public class SettingsManager
    {
        private readonly string _settingsPath;

        public SettingsManager()
        {
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Backup2FS");

            // Ensure the directory exists
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            _settingsPath = Path.Combine(appDataFolder, "settings.json");

            // Create default settings file if it doesn't exist
            if (!File.Exists(_settingsPath))
            {
                WriteDefaultSettings();
            }
        }

        private void WriteDefaultSettings()
        {
            try
            {
                // Create default settings with all hash algorithms set to false
                var settings = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["HashAlgorithms"] = new Dictionary<string, bool>
                        {
                            ["md5"] = false,
                            ["sha1"] = false,
                            ["sha256"] = false
                        }
                    }
                };

                // Serialize to JSON and write to file
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
                Debug.WriteLine($"Created default settings file at: {_settingsPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating default settings: {ex.Message}");
            }
        }

        public Dictionary<string, bool> GetHashAlgorithms()
        {
            var result = new Dictionary<string, bool>
            {
                ["md5"] = false,
                ["sha1"] = false,
                ["sha256"] = false
            };

            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);

                    if (settings != null && settings.Count > 0 && settings[0].TryGetValue("HashAlgorithms", out JsonElement hashAlgosElement))
                    {
                        // Convert JsonElement to Dictionary
                        var hashAlgos = hashAlgosElement.Deserialize<Dictionary<string, bool>>();
                        if (hashAlgos != null)
                        {
                            // Update result with values from settings file
                            foreach (var key in result.Keys)
                            {
                                if (hashAlgos.TryGetValue(key, out bool value))
                                {
                                    result[key] = value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading hash algorithms from settings: {ex.Message}");
                // If the file is corrupted, create a new one with default settings
                WriteDefaultSettings();
            }

            Debug.WriteLine($"GetHashAlgorithms: MD5={result["md5"]}, SHA1={result["sha1"]}, SHA256={result["sha256"]}");
            return result;
        }

        public void SetHashAlgorithms(Dictionary<string, bool> hashAlgorithms)
        {
            try
            {
                Debug.WriteLine($"SetHashAlgorithms called with MD5={hashAlgorithms["md5"]}, SHA1={hashAlgorithms["sha1"]}, SHA256={hashAlgorithms["sha256"]}");
                Console.WriteLine($"SetHashAlgorithms called with MD5={hashAlgorithms["md5"]}, SHA1={hashAlgorithms["sha1"]}, SHA256={hashAlgorithms["sha256"]}");
                Console.WriteLine($"Settings file path: {_settingsPath}");
                
                List<Dictionary<string, object>> settings;

                // Try to read existing settings
                if (File.Exists(_settingsPath))
                {
                    Console.WriteLine($"Settings file exists at: {_settingsPath}");
                    try
                    {
                        string jsonContent = File.ReadAllText(_settingsPath);
                        Console.WriteLine($"Read file content: {jsonContent}");
                        
                        var existingSettings = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);
                        
                        // Convert to the format we can modify
                        settings = new List<Dictionary<string, object>>();
                        
                        if (existingSettings != null && existingSettings.Count > 0)
                        {
                            Console.WriteLine("Successfully parsed existing settings");
                            foreach (var item in existingSettings)
                            {
                                var newItem = new Dictionary<string, object>();
                                foreach (var kvp in item)
                                {
                                    if (kvp.Key == "HashAlgorithms")
                                    {
                                        // We'll replace this with the new values
                                        continue;
                                    }
                                    
                                    // Copy other settings
                                    newItem[kvp.Key] = kvp.Value.Deserialize<object>();
                                }
                                settings.Add(newItem);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Existing settings were null or empty, creating new settings");
                            // Create a new settings list if deserialization returned null or empty
                            settings = new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object>()
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading settings file: {ex.Message}");
                        // If we can't parse the file, create a new settings structure
                        settings = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>()
                        };
                    }
                }
                else
                {
                    Console.WriteLine($"Settings file does not exist, will create: {_settingsPath}");
                    // If the file doesn't exist, create a new settings structure
                    settings = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>()
                    };
                }

                // Ensure we have at least one settings dictionary
                if (settings.Count == 0)
                {
                    settings.Add(new Dictionary<string, object>());
                }

                // Update the hash algorithms - create a direct copy to ensure no reference issues
                var hashAlgorithmsCopy = new Dictionary<string, bool>
                {
                    ["md5"] = hashAlgorithms["md5"],
                    ["sha1"] = hashAlgorithms["sha1"],
                    ["sha256"] = hashAlgorithms["sha256"]
                };
                
                settings[0]["HashAlgorithms"] = hashAlgorithmsCopy;

                // Write the updated settings to file
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"About to write to settings file: {_settingsPath}");
                Console.WriteLine($"JSON content to write: {json}");
                
                // Ensure the directory exists
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(directory))
                {
                    Console.WriteLine($"Creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(_settingsPath, json);
                Console.WriteLine($"Successfully wrote to settings file");
                
                // Verify the file was written correctly by reading it back
                if (File.Exists(_settingsPath))
                {
                    string verifyContent = File.ReadAllText(_settingsPath);
                    Debug.WriteLine($"Verified file content: {verifyContent}");
                    Console.WriteLine($"Verified file content: {verifyContent}");
                }
                else
                {
                    Console.WriteLine($"ERROR: Settings file does not exist after write operation!");
                }
                
                Debug.WriteLine($"Saved hash algorithms: MD5={hashAlgorithms["md5"]}, SHA1={hashAlgorithms["sha1"]}, SHA256={hashAlgorithms["sha256"]}");
                Console.WriteLine($"Saved hash algorithms: MD5={hashAlgorithms["md5"]}, SHA1={hashAlgorithms["sha1"]}, SHA256={hashAlgorithms["sha256"]}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving hash algorithms to settings: {ex.Message}");
                Console.WriteLine($"Error saving hash algorithms to settings: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // For backward compatibility with old string-based format
        public string GetHashAlgorithmsAsString()
        {
            var hashAlgos = GetHashAlgorithms();
            var selectedAlgos = new List<string>();
            
            if (hashAlgos["md5"]) selectedAlgos.Add("md5");
            if (hashAlgos["sha1"]) selectedAlgos.Add("sha1");
            if (hashAlgos["sha256"]) selectedAlgos.Add("sha256");
            
            return string.Join(",", selectedAlgos);
        }

        public void SetHashAlgorithmsFromString(string algorithmsString)
        {
            var hashAlgos = new Dictionary<string, bool>
            {
                ["md5"] = false,
                ["sha1"] = false,
                ["sha256"] = false
            };
            
            if (!string.IsNullOrWhiteSpace(algorithmsString))
            {
                var algorithms = algorithmsString.Split(',');
                foreach (var algo in algorithms)
                {
                    string normalizedAlgo = algo.Trim().ToLower();
                    if (hashAlgos.ContainsKey(normalizedAlgo))
                    {
                        hashAlgos[normalizedAlgo] = true;
                    }
                }
            }
            
            SetHashAlgorithms(hashAlgos);
        }

        public string GetSetting(string settingName, string defaultValue = "")
        {
            // For future expansion
            if (settingName == "HashAlgorithms")
            {
                return GetHashAlgorithmsAsString();
            }
            return defaultValue;
        }

        public void SetSetting(string settingName, string value)
        {
            // For future expansion
            if (settingName == "HashAlgorithms")
            {
                SetHashAlgorithmsFromString(value);
            }
        }
    }
} 