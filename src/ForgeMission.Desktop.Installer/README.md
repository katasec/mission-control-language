---
type: software-component
title: Desktop Installer
description: Windows ARM64 MSI wrapper for the already-published Forge Desktop bundle.
resource: src/ForgeMission.Desktop.Installer
tags: [desktop, windows, installer]
---

# Desktop Installer

## Purpose

Packages the existing published Desktop folder as an ARM64 MSI for installation testing.

## Why this exists

The Desktop bundle needs an installable Windows distribution experiment without giving installer
code ownership of application startup, native hosting, or application content.

## Owns

- WiX package metadata and MSI layout for the contents of `dist/forge-desktop`.

## Does not own

- Publishing the application binaries, code signing, Smart App Control policy, runtime lifecycle,
  Application Host behavior, or the MAUI WebView.

## Use

Publish the Desktop bundle first, then run:

```powershell
dotnet build src/ForgeMission.Desktop.Installer/ForgeMission.Desktop.Installer.wixproj -c Release
```

The MSI is written under this project's `bin\Release` folder. It packages files as published; an
unsigned MSI cannot establish trust for the executable files it contains.
