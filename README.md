# Backup2FS

A modern Windows application for normalizing iOS backups into standard file system structures for forensic analysis.

![Backup2FS Logo](Backup2FS/Resources/Images/logo.png)

## Features

- Convert iOS backup structures into standard file system structures for forensic analysis
- Extract and normalize iOS backups with intelligent domain mapping
- Display detailed device information from backups
- Show installed applications with app icons when available
- Support for different hashing algorithms (MD5, SHA1, SHA256)
- Real-time progress tracking with pause/resume capability
- Detailed logging with CSV export
- Modern UI with Elusive Data branding

## Technical Details

Backup2FS is built using:

- .NET 7.0 with WPF for the UI
- MVVM architecture with CommunityToolkit.Mvvm
- Material Design for WPF
- SQLite for parsing iOS backup databases
- Asynchronous processing for responsive UI

## Architecture

The solution is divided into two main projects:

1. **Backup2FS** - The WPF UI application
   - Views: XAML UI components
   - ViewModels: UI logic and data binding
   - Resources: Styles, images, and other UI assets

2. **Backup2FS.Core** - The business logic library
   - Models: Data structures for backups and device info
   - Services: Core processing functionality
   - Helpers: Utility classes

## iOS Backup Structure

iOS backups consist of:

- **Manifest.db**: SQLite database containing file mappings
- **Manifest.plist**: Device information and backup metadata
- **Info.plist**: Additional backup information
- **Files**: Stored with hash-based filenames in subdirectories

The application converts these to a standard iOS file system structure, making it easy for forensic analysts to navigate and examine the data.

### Domain Mapping

iOS backup files are organized by domains, which represent different parts of the iOS filesystem. Backup2FS maps these domains to their corresponding filesystem paths:

- **HomeDomain**: User's home directory
- **MediaDomain**: Media files (photos, videos, etc.)
- **CameraRollDomain**: Camera photos and videos
- **AppDomain-***: Individual application data
- **KeychainDomain**: Stored credentials (if unencrypted)
- And many more...

The extraction process:
1. Reads the backup's Manifest.db SQLite database
2. Maps each file to its proper filesystem location
3. Recreates the iOS directory structure
4. Copies files with hash verification

## Development

### Prerequisites

- Visual Studio 2022 or newer
- .NET 7.0 SDK

### Building

1. Clone the repository
2. Open the solution in Visual Studio
3. Restore NuGet packages
4. Build the solution

## Improvements Over Original Python Version

- Modern UI with Material Design
- Better multithreading support
- Improved error handling
- App icon extraction and display
- Full MVVM architecture for better maintainability
- Native Windows integration

## License

© Elusive Data 2025. All rights reserved.

## Contact

Developed by James Eichbaum at Elusive Data. 