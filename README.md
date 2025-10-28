# TestBookletProcessor

## Overview
TestBookletProcessor is a .NET8 solution for processing PDF booklets, including deskewing, aligning, and removing red pixels from scanned images. The solution includes a WPF desktop application for user interaction, a core library for interfaces, and service libraries for image and PDF processing.

## Projects
- **TestBookletProcessor.WPF**: Main WPF desktop application. Allows users to select input, template, and output folders, process booklets, and configure settings.
- **TestBookletProcessor.Services**: Contains services for image processing (deskew, align) and red pixel removal, using OpenCvSharp.
- **TestBookletProcessor.Core**: Defines interfaces for extensibility and separation of concerns.
- **TestBookletProcessor.Console**: (Optional) Console entry point for batch or automated processing.

## Features
- **PDF Input/Output**: Select input and template PDFs, process them to generate output booklets.
- **Image Processing**: Deskew and align scanned images using OpenCvSharp.
- **Red Pixel Remover**: Optionally remove red pixels from images based on configurable threshold.
- **Settings Window**: Configure default folders and processing options via a modal settings dialog.
- **Error Handling**: User-friendly error messages for invalid paths, missing files, and processing failures.

## Usage
1. Launch the WPF application.
2. Set default folders in the Settings window.
3. Select input and template PDFs.
4. Click "Process Input File" to start booklet processing.
5. View status and progress in the main window.

## Configuration
- Settings are stored in `appsettings.json`.
- You can configure default input, template, and output folders, red pixel threshold, and DPI.

## Dependencies
- .NET8
- OpenCvSharp (for image processing)
- Microsoft.Extensions.Configuration (for config management)
- Newtonsoft.Json (for settings window)

## Known Issues
- Ensure all folder paths are valid and accessible to avoid dialog errors.


## License
This project is proprietary and not open source.
Cloning, copying, redistributing, or modifying this codebase is not permitted without explicit written permission from the author.
