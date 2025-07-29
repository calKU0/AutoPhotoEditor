# AutoPhotoEditor

AutoPhotoEditor is a powerful and automated photo processing pipeline built using Python and WPF. This tool monitors a designated folder for new image files (RAW, JPG, PNG), performs a series of transformations, and integrates with Comarch ERP XL for streamlined image management.

## Features

- 📂 **Folder Monitoring**: Automatically detects new image files placed into a watched folder.
- 🔄 **File Conversion**:
  - Converts RAW files to JPG format.
- 🖼️ **Image Processing**:
  - Scales images down to a maximum resolution of 1920x1080 while maintaining aspect ratio.
  - Removes background using python library `rembg`.
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
- **Comarch ERP XL** - Product card integration via `cdn_api`
- **PIL / OpenCV / Rembg** - Image processing libraries

## License

This project is proprietary and confidential. See the [LICENSE](LICENSE) file for more information.

---

© 2025 [calKU0](https://github.com/calKU0)
