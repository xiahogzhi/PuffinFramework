# UPM Editor

Unity Package Manager editor tool for creating, editing, and managing UPM packages.

## Features

- **Directory Conversion**: Move packages between Assets and Packages folders via right-click menu
- **Package Creator**: Create new UPM packages with customizable templates
- **Package Editor**: Edit existing package.json files with a visual interface

## Usage

### Right-Click Context Menu

Right-click on a folder in the Project window to access:

- **Move to Packages**: Move a UPM-structured folder from Assets to Packages (requires package.json)
- **Move to Assets**: Move a local package from Packages to Assets
- **Edit Package**: Open the package editor for an existing package
- **Create Package Here**: Create a new UPM package in the selected folder

### Package Editor Window

Open via `Tools > UPM Editor > Package Editor`

The editor allows you to:
- Create new packages with customizable templates
- Edit package metadata (name, version, description, etc.)
- Manage dependencies
- Generate assembly definition files

## Template Options

When creating a new package, you can optionally include:
- Runtime/ directory with .asmdef
- Editor/ directory with .asmdef
- README.md
- CHANGELOG.md
- LICENSE.md
- Tests/ directory
- Documentation~/ directory
