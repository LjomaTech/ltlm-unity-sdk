# Building as DLL

How to compile the LTLM SDK into redistributable DLLs.

---

## Overview

Building the SDK as DLLs provides:
- **Code protection** - Harder to modify
- **Faster compilation** - Pre-compiled assemblies
- **Distribution** - Single file to distribute

---

## Assembly Structure

The SDK is organized into four assemblies:

```
LTLM.Core.dll
├── LTLM.SDK.Core.Communication
├── LTLM.SDK.Core.Models
├── LTLM.SDK.Core.Security
└── LTLM.SDK.Core.Storage

LTLM.Unity.dll
└── LTLM.SDK.Unity (LTLMManager, DeviceID, etc.)

LTLM.Editor.dll (Editor only)
└── LTLM.SDK.Editor (ProjectSettingsEditor)

LTLM.Demos.dll (Optional)
└── LTLM.SDK.Demos (Demo scripts)
```

---

## Prerequisites

1. Unity 2020.3+ installed
2. .NET SDK 6.0+ for standalone compilation
3. The source SDK files

---

## Method 1: Unity Assembly Build

### Step 1: Configure Assembly Definitions

The SDK already includes `.asmdef` files:

```
Scripts/Core/LTLM.Core.asmdef
Scripts/Unity/LTLM.Unity.asmdef
Editor/LTLM.Editor.asmdef
Demos/Scripts/LTLM.Demos.asmdef
```

### Step 2: Build in Unity

1. Open Unity project
2. Go to **Edit → Project Settings → Player**
3. Set **Api Compatibility Level** to **.NET Standard 2.1**
4. Build the project (DLLs auto-generated in Library)

### Step 3: Extract DLLs

Find compiled DLLs in:
```
Library/ScriptAssemblies/
├── LTLM.Core.dll
├── LTLM.Unity.dll
├── LTLM.Editor.dll
└── LTLM.Demos.dll
```

---

## Method 2: Command Line Build

### Create Build Script

```bash
# build_sdk.sh
#!/bin/bash

UNITY_PATH="/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity"
PROJECT_PATH="./unitysdk"
OUTPUT_PATH="./build"

$UNITY_PATH \
  -batchmode \
  -quit \
  -projectPath $PROJECT_PATH \
  -executeMethod BuildScript.BuildDLLs \
  -logFile build.log

# Copy DLLs to output
cp $PROJECT_PATH/Library/ScriptAssemblies/LTLM.*.dll $OUTPUT_PATH/
```

### Build Script (C#)

Create `Assets/Editor/BuildScript.cs`:

```csharp
using UnityEditor;
using UnityEditor.Build;

public class BuildScript
{
    public static void BuildDLLs()
    {
        // Force recompile all assemblies
        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation();
    }
}
```

---

## Method 3: .NET CLI Build

For maximum control, use .NET CLI:

### Step 1: Create .csproj Files

Create `LTLM.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <RootNamespace>LTLM.SDK.Core</RootNamespace>
    <AssemblyName>LTLM.Core</AssemblyName>
  </PropertyGroup>
  
  <ItemGroup>
    <Reference Include="UnityEngine">
      <HintPath>$(UNITY_PATH)/Data/Managed/UnityEngine.dll</HintPath>
    </Reference>
    <PackageReference Include="BouncyCastle.Cryptography" Version="2.2.1" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
  
  <ItemGroup>
    <Compile Include="Scripts/Core/**/*.cs" />
  </ItemGroup>
</Project>
```

### Step 2: Build

```bash
dotnet build LTLM.Core.csproj -c Release
dotnet build LTLM.Unity.csproj -c Release
```

---

## Distribution Package

### Creating a .unitypackage

```csharp
// ExportPackage.cs
using UnityEditor;

public class ExportPackage
{
    [MenuItem("LTLM/Export Package")]
    public static void Export()
    {
        string[] paths = {
            "Assets/LTLM/Plugins",      // DLLs
            "Assets/LTLM/Resources",    // Settings
            "Assets/LTLM/Demos"         // Optional demos
        };
        
        AssetDatabase.ExportPackage(
            paths,
            "LTLM_SDK_v1.2.0.unitypackage",
            ExportPackageOptions.Recurse
        );
    }
}
```

### Package Contents

```
LTLM.unitypackage
├── Plugins/
│   ├── LTLM.Core.dll
│   ├── LTLM.Unity.dll
│   ├── LTLM.Editor.dll
│   ├── BouncyCastle.Cryptography.dll
│   └── Newtonsoft.Json.dll
├── Resources/
│   └── LTLMSettings.asset.template
└── Demos/
    ├── Scripts/
    └── Scenes/
```

---

## Verification

After building, verify:

1. **No compilation errors** when importing to clean project
2. **All namespaces resolve** correctly
3. **Editor tools work** in new project
4. **Activation flow completes** successfully

---

## Obfuscation (Optional)

For additional protection, consider obfuscating the DLLs:

### Using Dotfuscator

```bash
dotfuscator /in:LTLM.Core.dll /out:LTLM.Core.obf.dll
```

### Using ConfuserEx

```xml
<!-- confuser.crproj -->
<project>
  <module path="LTLM.Core.dll">
    <rule pattern="*">
      <protection id="constants" />
      <protection id="ctrl flow" />
      <protection id="rename" />
    </rule>
  </module>
</project>
```

---

## Updating the Backend URL

Before building for production, update `LTLMConstants.cs`:

```csharp
// For production
public const string BackendUrl = "https://api.ltlm.io/api";

// For staging
public const string BackendUrl = "https://staging-api.ltlm.io/api";
```

---

## See Also

- [Installation](../installation.md)
- [Configuration](../configuration.md)
