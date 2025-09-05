# P3k Editor Tools

A lightweight Unity editor window for common project tasks including file import, script organization, bundle building, and GitHub package scaffolding.

## Installation

Add via git url in Unity Package Manager or add manually to your Unity project's `manifest.json`:

```json
{
  "dependencies": {
    "com.p3k.unityeditortools": "https://github.com/p3k22/UnityEditorTools.git"
  }
}
```

**Option 2:** Copy the Editor folder into your project. 

**Requirements:** Unity 2020+

## Usage

Open the tool via **Window → P3k's Editor Tools**

## Features

### File Management
- **Import From File**: Add `.cs`, `.txt`, or `.asset` files to the currently active Project window folder
- **Export Selected**: Export checked files to `C:\UnityScriptExports\<ProductName>`

### Code Organization  
- **Sort Namespaces**: Automatically rename script namespaces based on their folder structure (with option to remove spaces)

### Asset Building
- **Build AssetBundles**: Export Unity AssetBundles using custom extension, builds to `Assets/AssetBundles`

### GitHub Package Creation
- **Create Empty GitHub Package**: Generate a complete package structure including:
  - `package.json` with your specified name, author, and version
  - `.gitignore` 
  - `README.md`
  - `REPO_INIT.bat` - Initialize and force-push fresh repository
  - `REPO_UPDATE.bat` - Commit, rebase, push, and auto-tag versions

## Configuration

- **Root Directory**: Fixed to `Assets`
- **File Filter**: Defaults to `.cs .asset` files
- **Active Folder Targeting**: New assets are placed in the currently selected Project window folder

## Batch Scripts

The generated package includes helper scripts:

- **REPO_INIT.bat**: Sets up a new Git repository and pushes to remote
- **REPO_UPDATE.bat**: Handles commits, rebasing, pushing, and automatic version tagging based on `package.json`