# Audio Compressor – Professional Edition

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-Framework%204.7.2%2B-purple)
![Language](https://img.shields.io/badge/language-C%23-brightgreen)
![License](https://img.shields.io/badge/license-MIT-orange)

A powerful desktop application for lossy audio compression, built with C# and Windows Forms. It provides an intuitive graphical interface to compress, decompress, and analyze audio files using five different differential coding algorithms, with real-time performance monitoring and quality assessment.

---

## Table of Contents

- [Features](#features)
- [Supported Algorithms](#supported-algorithms)
- [User Interface Overview](#user-interface-overview)
- [Installation & Running](#installation--running)
- [How to Use](#how-to-use)
- [Compression Settings](#compression-settings)
- [Quality Metrics](#quality-metrics)
- [File Formats](#file-formats)
- [Project Structure](#project-structure)
- [Dependencies](#dependencies)
- [License](#license)

---

## Features

- Load audio files via drag-and-drop or file dialog
- Play, pause, and stop the original audio with a seek bar
- Display original and decompressed waveforms side by side
- Show detailed file information (size, duration, sample rate, channels, bit rate, encoding)
- Choose from five compression algorithms with adjustable parameters
- Run compression in the background with a progress bar and live charts
- Cancel compression at any time
- Decompress and play the reconstructed audio
- Save compressed data to a custom binary format (.dat)
- Save decompressed audio as a WAV file
- Load previously compressed files for playback and analysis
- View a detailed compression report (original/compressed size, ratio, time, algorithm settings)
- Calculate and display quality metrics: MSE, RMSE, SNR, PSNR

---

## Supported Algorithms

1. **Delta Modulation (DM)** – 1-bit encoding with fixed step size
2. **Adaptive Delta Modulation (ADM)** – 1-bit encoding with adaptive step size
3. **DPCM (Differential Pulse Code Modulation)** – N-bit quantization of prediction error
4. **μ-law Nonlinear Quantization** – Logarithmic companding using μ-law
5. **Predictive Differential Coding** – Linear predictive coding with fixed coefficients

All algorithms are implemented via a common `IAudioCompressor` interface, making it easy to extend the application with new methods.

---

## User Interface Overview

The main window is divided into several logical areas:

- **Drop Panel** – Displays the original and decompressed waveforms. Use drag-and-drop or the Load button to import audio.
- **Original Player** – Play, pause, and stop the original file with a time display and seek bar.
- **Compressed Player** – Play, pause, and stop the decompressed audio, with seek support and the option to save the WAV file.
- **File Information Panel** – Shows file name, size, duration, sample rate, channels, bit rate, and encoding.
- **Compression Panel** – Select algorithm, target sample rate, and quantization bits; start or cancel compression.
- **Live Charts** – Real-time plots of compression ratio and processing speed.
- **Progress Bar** – Visual feedback during compression.
- **Size Comparison Chart** – Bar chart comparing original and compressed file sizes.
- **Report Area** – Detailed text report after compression, including quality metrics.
- **Status Bar** – Current status messages.

---

## Installation & Running

### Prerequisites

- Windows operating system
- .NET Framework 4.7.2 or later (or .NET 6/8 if you retarget)
- Visual Studio 2019/2022 (Community edition is fine)

### Steps

1. Clone this repository:

```bash
git clone https://github.com/majddakhoul/Audio-Compressor.git
```

2. Open the solution file `Audio_Compressor.sln` in Visual Studio.

3. Restore NuGet packages (Visual Studio does this automatically), or use:

```bash
nuget restore
```

4. Build the solution (`Ctrl + Shift + B`).

5. Run the application (`F5`).

The main dependencies are listed in the Dependencies section below.

---

## How to Use

1. Load an audio file by dragging it onto the waveform panel or clicking the **Load Audio** button.

2. Preview the original audio using the **Play / Pause / Stop** buttons and the seek bar.

3. Configure compression:
   - Choose an algorithm from the dropdown.
   - Set the target sample rate (e.g., 16000 Hz).
   - Set the number of quantization bits (1–16).

4. Click **Compress** to start the process. You can cancel at any time.

5. After compression, the decompressed waveform appears alongside the original.

6. Listen to the decompressed audio using the **Compressed Player** controls.

7. Save the results:
   - Click **Save Compressed** to store the compressed data as a `.dat` file.
   - Click **Save WAV** to export the decompressed audio as a WAV file.

8. View the compression report and quality metrics below the waveforms.

9. Use **Reset Original** to reload the original file and clear all compression results.

10. You can also load a previously compressed file using the **Load Compressed** button.

---

## Compression Settings

### Algorithm

Select one of the five supported algorithms.

### Target Sample Rate (Hz)

Downsample the audio before compression to reduce data size. Typical range:

- 4000 Hz – 44100 Hz

### Bits / Step Size

Number of quantization bits used by:

- DPCM
- μ-law Quantization
- Predictive Coding

Range:

- 1 – 16 bits

For DM and ADM, compression is fixed at **1 bit per sample**.

All settings are applied before compression begins. The original file is never modified.

---

## Quality Metrics

After decompression, the application automatically calculates the following metrics:

### MSE (Mean Squared Error)

Average squared difference between original and reconstructed samples.

### RMSE (Root Mean Squared Error)

Square root of MSE, expressed in the same units as the audio amplitude.

### SNR (Signal-to-Noise Ratio)

Ratio of original signal power to reconstruction error power, expressed in dB.

### PSNR (Peak Signal-to-Noise Ratio)

Ratio of peak signal power to reconstruction error power, expressed in dB.

These metrics provide a quantitative measure of compression fidelity.

---

## File Formats

### Input

Supported through NAudio:

- WAV
- MP3
- FLAC
- AIFF
- M4A

### Output

#### Compressed Format (.dat)

Contains:

- Algorithm name
- Original sample rate
- Maximum amplitude value
- Quantization bits
- Sample count
- Bit-packed compressed data

#### Decompressed Format

- Standard 32-bit floating-point WAV
- Mono audio stream

The custom binary format is compact and self-contained, allowing future loading and playback without requiring the original audio file.

---

## Project Structure

```text
Audio-Compressor/
├── Form1.cs
├── IAudioCompressor.cs
├── CompressedAudio.cs
├── CompressionSettings.cs
├── ProgressInfo.cs
├── BitPackingHelpers.cs
├── DeltaModulationCompressor.cs
├── DPCMCompressor.cs
├── AdaptiveDeltaModulationCompressor.cs
├── NonlinearQuantizationCompressor.cs
├── PredictiveDifferentialCodingCompressor.cs
├── Audio_Compressor.csproj
├── Audio_Compressor.sln
└── README.md
```

### Notes

Currently, all classes are implemented inside `Form1.cs` for simplicity.

In a production-ready version, each class should be moved into its own source file and organized into separate folders.

---

## Dependencies

The application relies on the following NuGet packages:

### NAudio (v2.x or later)

Provides:

- Audio file reading
- Audio playback
- Format conversion support

### System.Windows.Forms.DataVisualization

Provides:

- Compression Ratio Chart
- Processing Speed Chart
- Size Comparison Chart

Packages are automatically restored when building the solution in Visual Studio.

---

## License

This project is released under the MIT License.

See the `LICENSE` file for full licensing details.
