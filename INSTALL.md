# Backup2FS Installation Guide

This document provides detailed instructions for installing and setting up Backup2FS on your system.

## Prerequisites

Before installing Backup2FS, ensure your system meets the following requirements:

- **Operating System**: Windows 10 or Windows 11
- **.NET Runtime**: .NET 7.0 or later
- **Memory**: 4GB RAM minimum (8GB recommended)
- **Disk Space**: At least 1GB free space plus additional space for extracted backup files
- **Dependencies**: Microsoft Visual C++ Redistributable 2019 or later

## Installation Methods

### Method 1: Installer Package (Recommended)

1. Download the latest installer package (`Backup2FS_Setup.exe`) from the [Releases](https://github.com/eichbaumj/Backup2FS/releases) page
2. Run the installer and follow the on-screen instructions
3. The application will be installed to your Program Files directory by default
4. Shortcuts will be created on your desktop and in the Start menu

### Method 2: Portable ZIP

1. Download the portable ZIP package (`Backup2FS_Portable.zip`) from the [Releases](https://github.com/eichbaumj/Backup2FS/releases) page
2. Extract the ZIP file to a location of your choice
3. Run `Backup2FS.exe` directly from the extracted folder
4. Note: Settings will be saved in the same directory as the executable

## First-Time Setup

When you run Backup2FS for the first time:

1. You'll be prompted to configure default settings:
   - Default output location
   - Preferred hash algorithms (MD5, SHA-1, SHA-256)
   - Log file location

2. These settings can be changed later through the Settings menu

## Common Installation Issues

### Missing .NET Runtime

If you see an error about missing .NET Runtime:

1. Download and install the .NET 7.0 Runtime from the [Microsoft .NET Download Page](https://dotnet.microsoft.com/download/dotnet/7.0)
2. Choose the ".NET Desktop Runtime" installer for your system architecture (x64 for most systems)
3. After installation, restart your computer and try running Backup2FS again

### Windows SmartScreen Warning

If Windows SmartScreen shows a warning when running the installer:

1. Click "More info"
2. Click "Run anyway"
3. This occurs because the application is new and hasn't established a reputation with Microsoft yet

## Building from Source

If you prefer to build the application from source:

1. Clone the repository:
   ```
   git clone https://github.com/eichbaumj/Backup2FS.git
   ```

2. Open the solution in Visual Studio 2022 or later

3. Restore NuGet packages:
   - Right-click on the solution in Solution Explorer
   - Select "Restore NuGet Packages"

4. Build the solution:
   - Select "Build" > "Build Solution" from the menu
   - Or press Ctrl+Shift+B

5. Run the application:
   - Press F5 to run with debugging
   - Or Ctrl+F5 to run without debugging

## Troubleshooting

If you encounter issues during installation or setup:

- Check the application logs in `%APPDATA%\Backup2FS\logs\`
- Ensure your system meets all the prerequisites
- Try running the application as administrator
- For detailed error information, run the application from a command prompt

For additional support, please contact support@elusivedata.io 