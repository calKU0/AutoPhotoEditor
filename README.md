# AutoPhotoEditor

AutoPhotoEditor is a powerful and automated photo processing pipeline built using Python and WPF. This tool monitors a designated folder for new image files (RAW, JPG, PNG), performs a series of transformations, and integrates with Cloudinary and Comarch ERP XL for streamlined image management.

## Features

- 📂 **Folder Monitoring**: Automatically detects new image files placed into a watched folder.
- 🔄 **File Conversion**:
  - Converts RAW files to JPG format.
- 🖼️ **Image Processing**:
  - Scales images down to a maximum resolution of 1920x1080 while maintaining aspect ratio.
  - Uploads images to Cloudinary for background removal.
  - Crops the image to the largest bounding box.
  - Applies a custom watermark to the image.
- 💾 **File Management**:
  - Saves both watermarked and non-watermarked versions.
  - Moves original files to an archive directory.
- 🖥️ **User Interface (WPF)**:
  - Displays processed image with zoom and pan support.
  - Allows users to save the image to a product card in Comarch ERP XL via `cdn_api`.
  - Provides an option to delete images from the saved folder.

## Technologies Used

- **Python** - Core image processing and automation logic
- **WPF (.NET)** - Desktop UI for displaying and managing images
- **Cloudinary** - Background removal service
- **Comarch ERP XL** - Product card integration via `cdn_api`
- **PIL / OpenCV** - Image processing libraries

## Setup & Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/calKU0/AutoPhotoEditor.git
   cd AutoPhotoEditor
   ```

2. Create and activate a Python virtual environment:

   ```bash
   python -m venv venv
   source venv/bin/activate  # On Windows: venv\Scripts\activate
   ```

3. Install Python dependencies:

   ```bash
   pip install -r requirements.txt
   ```

4. Configure your environment:

   - Set up Cloudinary credentials.
   - Configure paths for watched folder, save directories, and archive.
   - Set up access credentials for Comarch ERP XL.

5. Build the WPF application using Visual Studio (or compatible IDE).

## App Configuration

Configuration values are stored in `App.config`. Below is an example configuration:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
  </startup>
  <appSettings>

    <!-- CDNXL API -->
    <add key="XLApiVersion" value="" />
    <add key="XLProgramName" value="" />
    <add key="XLDatabase" value="" />
    <add key="XLUsername" value="" />
    <add key="XLPassword" value="" />

    <!-- Cloudinary -->
    <add key="CloudinaryCloudName" value="" />
    <add key="CloudinaryApiKey" value="" />
    <add key="CloudinaryApiSecret" value="" />

    <!-- Folders -->
    <add key="InputFolder" value="Images to procces" />
    <add key="TempFolder" value="Temp" />
    <add key="ArchiveFolder" value="Archive" />
    <add key="OutputWithWatermark" value="Images processed" />
    <add key="OutputWithoutWatermark" value="Images processed without logo" />

  </appSettings>
  <connectionStrings>
    <add name="GaskaConnectionString" connectionString="Server='serwer';Database='database';User Id='User';Password='password';Connection Timeout=5 TrustServerCertificate=True"/>
  </connectionStrings>
</configuration>
```

## Folder Structure

```
/Images to procces/               # Watched folder for new image files
/Temp/                            # Temp folder for file transformations
/Images processed without logo/   # Final images with watermark
/Images processed/                # Final images without watermark
/Archive/                         # Archived original files
```

## License

This project is licensed under the MIT License.

---

© 2025 [calKU0](https://github.com/calKU0)
