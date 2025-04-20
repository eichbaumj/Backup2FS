using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TestPauseButton.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private string _logOutput = string.Empty;

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public MainViewModel()
        {
            AddLog("Application started");
        }

        [RelayCommand]
        private void PauseResume()
        {
            IsPaused = !IsPaused;
            
            if (IsPaused)
            {
                AddLog($"Process paused at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                AddLog($"Process resumed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }

        private void AddLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Add timestamp if not already present
            if (!message.StartsWith("["))
            {
                message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            }
            
            // Update logs collection
            Logs.Add(message);
            
            // Update log output text
            LogOutput = string.Join(Environment.NewLine, Logs);
            
            // Scroll to the bottom of the log view
            Application.Current.Dispatcher.InvokeAsync(() => {
                // This would be handled by a behavior in the actual app
            });
        }
    }
} 