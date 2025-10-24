# DICOM Viewer (WPF)

A lightweight DICOM image viewer built in C# using WPF and **fo-dicom**. Supports image rendering, window/level adjustments, zoom/pan, measurements, annotations, and exporting.

---

## Features

### DICOM Loading
- Load single DICOM files (`.dcm`) or an entire series.
- Display metadata like patient name, ID, study date, modality, slice thickness, and pixel spacing.

### Image Rendering
- Supports window width and window center adjustments.
- Zoom in/out, rotate, flip horizontally/vertically.
- Pan using mouse drag.
- Fit-to-screen view.

### Slice Navigation & Cine
- Navigate slices via slider or mouse wheel.
- Play cine sequences (automatic slice playback).

### Measurements
- Distance: Click 2 points to measure distance (in mm).
- Angle: Click 3 points to measure angles (in degrees).
- Area/ROI: Click multiple points and complete with right-click or Space key.
- Displays measurements on the overlay canvas.
- Export measurements as JSON or text report.

### Annotations
- Freehand drawing.
- Arrow tool.
- Text annotations.
- Clear individual or all annotations.

### Export
- Export snapshots as PNG.
- Export measurements and patient info as JSON or text report.

### CT Hounsfield Units (HU)
- Displays HU values for CT images at mouse pointer location.

---

## Dependencies
- [.NET 6 or higher](https://dotnet.microsoft.com/)
- [fo-dicom](https://github.com/fo-dicom/fo-dicom)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)

---

## Usage

1. Clone or download the repository.
2. Open the project in Visual Studio.
3. Restore NuGet packages.
4. Build and run.
5. Use the UI to load DICOM files, navigate slices, adjust window/level, measure, annotate, and export.

---

## Controls / Shortcuts

| Action | Shortcut |
|--------|---------|
| Zoom In | Ctrl + `+` |
| Zoom Out | Ctrl + `-` |
| Reset Transform | Ctrl + R |
| Next Slice | Down Arrow |
| Previous Slice | Up Arrow |
| Deactivate Tools | Esc |
| Complete Area Measurement | Space |

---

## Screenshots

![DICOM Viewer Screenshot](<img width="1891" height="1013" alt="image" src="https://github.com/user-attachments/assets/abba6e0a-e769-4e20-8efa-9bc293101d66" />)  

---

## Notes
- Designed for learning and basic DICOM viewing.  
- Currently supports CT, MR, and other standard modalities.  
- Some advanced features like cine playback speed adjustment may be enhanced in future updates.

---

## License
MIT License
