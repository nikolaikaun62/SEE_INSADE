# SEE INSADE

SEE INSADE is a Windows desktop application for scanner and detector diagnostics. It emulates an X-ray-style scanning workflow, visualizes scan data, highlights material types, and provides tools for detector health checks, calibration, filtering, and image inspection.

## Features

- Real-time scan simulation with detector readings
- Standard, material, density, and filtered image views
- Detector health visualization and diagnostics
- Calibration and settings dialogs
- Brightness, contrast, material enhancement, edge, and noise filters
- SQLite-backed detector data in the enhanced branch

## Tech Stack

- C# 12
- .NET 8
- WPF
- Entity Framework Core with SQLite

## Getting Started

Requirements:

- Windows
- .NET 8 SDK or newer

Build the project:

```powershell
dotnet build .\SEE_INSADE.csproj
```

Run the application:

```powershell
dotnet run --project .\SEE_INSADE.csproj
```

## Project Structure

- `Core/` - scanner configuration, filters, image processing, utilities, and shared types
- `Services/` - detector, diagnostic, scan emulation, and scan visualization services
- `Data/` - EF Core database context and seed data
- `Models/` - detector and scan session models
- `UI/` - WPF windows and dialogs
- `ViewModels/` - MVVM view models for detector filtering and workflows
- `Views/` - reusable WPF views

## Current Status

The application builds successfully and contains both the original scan visualization workflow and the newer detector diagnostics/data layer. The next useful step is to add automated tests for scan emulation, filters, and detector diagnostics.
