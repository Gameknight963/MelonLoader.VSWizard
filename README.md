# MelonLoader VS Wizard

Visual Studio templates and an independent project-generation library for MelonLoader mods and plugins. Supports Mono and IL2CPP installations with MelonLoader 0.5.0 or newer, including projects with separate targets for both runtimes.

## What does it handle?
It handles the creation of the required boilerplate (the `MelonMod`/`MelonPlugin` class, `MelonInfo`, and `MelonGame`) as well as referencing the required assemblies for mod development, mainly MelonLoader, Harmony, and for Il2Cpp, proxy assemblies and the unhollower (Il2CppAssemblyUnhollower or Il2CppInterop). It also handles variation between MelonLoader or Unity versions, such as framework versions or override changes.

## Usage
0. Install MelonLoader to your game. If targeting Il2Cpp, run it once before continuing (to generate Il2CppInterop assemblies).
1. Download the VSIX from the [Releases](https://github.com/TrevTV/MelonLoader.VSWizard/releases) tab.
2. Close all instances of Visual Studio and run the VSIX installer (double-clicking it should open it).
3. Open Visual Studio and create a new project.
4. Search for `MelonLoader` and choose Mod, Plugin, or Mod (Mono and IL2CPP).
5. Enter the project info and press Create.
6. Select the game executable. For the dual-runtime template, select a Mono installation first, then an IL2CPP installation.
7. The generated project opens with references to the selected installation or installations.

You may want to change the author in the `MelonInfo` attribute. It defaults to your computer's username.

### Differences from TrevTV's version

 - Removed Manual templates. They're not very useful in practice
 - Independent generation library: Game inspection, reference selection, and project generation live in `MelonLoader.VSWizard.ProjectGeneration`, with no Visual Studio or UI dependency
- Dual-runtime mod template: Separate Mono and IL2CPP installations, references, frameworks, and deployment destinations
- Proper build configurations: `Mono-Debug`, `Mono-Release`, `Il2Cpp-Debug`, and `Il2Cpp-Release`
- Target runtime symbols: Dual-runtime configurations define `MONO` or `IL2CPP`
- Explicit assembly paths: `Directory.Build.props` exposes assembly directories independently of `GamePath`
- `.slnx` solutions: It's just better lol
- Better cancellation: Cancelling a picker or dismissing a validation error ends the wizard instead of being annoying and reopening dialogs
- Automated tests: xUnit coverage for generation, references, copying, and compilation across all four runtime configurations

#### The following features require usage of the generation library:

- Custom reference selection: Your tools can discover assemblies, choose references, add custom DLLs, and validate missing files or conflicting names
- Optional assembly copying: Copy references into the project so compilation can work without the original game installation
- Optional deployment: Copying can be disabled now

## Reusable project generation

`MelonLoader.VSWizard.ProjectGeneration` is a .NET Standard 2.0 library with no Visual Studio or UI dependencies. It owns game inspection, framework/reference selection, Unity metadata parsing, and the template contents. The existing template projects package linked library assets for Visual Studio; the wizard handles dialogs and forwards the library's replacements.

Reference its project from another application:

```csharp
using MelonLoader.VSWizard.ProjectGeneration;
using System.Collections.Generic;

GameInspector inspector = new();
GameInfo game = inspector.Inspect(gameExecutable);
ProjectGenerator generator = new();
ProjectOptions options = new()
{
    ProjectName = "MyMod",
    RootNamespace = "MyMod",
    Author = "Me",
    Kind = ProjectKind.Mod,
    Game = game
};
ProjectGenerationResult result = generator.Generate(options);
IReadOnlyDictionary<string, string> files = result.Files;
```

`files` contains relative filenames and their complete contents. The caller controls previewing and writing them. Use a valid C# namespace and a project name suitable for a filename. `Inspect` reads the installation and throws exceptions for invalid inputs; it never displays UI. `TemplateRenderer.CreateReplacements` and `TemplateRenderer.CreateDualRuntimeReplacements` are available for hosts using token-based templates. Game inspection still requires MelonLoader to be installed and, for IL2CPP, its assemblies to have been generated.

`GameInfo` is an immutable record. Inspection populates `GameDirectory`, `ExecutablePath`, `DataDirectory`, `LoaderVersion`, `UnityVersion`, `IsIl2Cpp`, and game metadata. Custom integrations can construct it with an object initializer or create a modified copy with `with`. `UnityVersion` belongs to this library; callers do not need to use AssetRipper types. It supports parsing Unity version strings, comparisons, and `Major`, `Minor`, and `Patch` properties. `UnityVersion.Unknown` represents unavailable version metadata.

Build the library independently with `dotnet build MelonLoader.VSWizard.ProjectGeneration`. Building the VSIX also requires Visual Studio SDK build tooling and the repository's strong-name key (`MelonLoader.VSWizard.WizardExtension/key.snk`).

Run the xUnit tests with `dotnet test tests/MelonLoader.VSWizard.ProjectGeneration.Tests`. They use temporary fixture installations rather than requiring a real game.

### Reference selection for your own UI

```csharp
IReadOnlyList<AssemblyReference> available = generator.DiscoverReferences(game);
// Show available in your UI and collect the chosen AssemblyReference objects.
IReadOnlyList<AssemblyReference> selected = selectedByYourUi;
IReadOnlyList<AssemblyReference> resolved = generator.ResolveReferences(game, selected);
IReadOnlyList<ReferenceDiagnostic> diagnostics = generator.ValidateReferences(resolved);
options.References = selected;
ProjectGenerationResult output = generator.Generate(options);
```

Each reference exposes `Name`, absolute `Path`, `Category`, `Exists`, `IsRequired`, and `IsRecommended`. Framework DLLs are visible but excluded from the default recommendations. Loader support references are included automatically. A null selection preserves the automatic defaults; an empty selection includes only required support references. Advanced callers can set `IncludeRequiredReferences = false` to manage every reference themselves. Custom DLLs can be supplied with `new AssemblyReference(path)`.

Discovery includes expected support references even when missing, so the UI can display them. Validation returns structured missing-file and conflicting-name diagnostics. Generation rejects those errors. Validation checks file availability and filename conflicts; it does not inspect assembly metadata, resolve arbitrary transitive dependencies, or establish API compatibility. Missing generated IL2CPP assemblies prevent discovery and are reported as an exception.

The Visual Studio wizard keeps its existing automatic selection; interactive selection is exposed for external tool integration.

### Generated paths and deployment

`Directory.Build.props` contains the exact assembly directories, with their shared installation prefix factored into `GamePath`:

```xml
<Project>
  <PropertyGroup>
    <GamePath>D:\Games\Example</GamePath>
    <GameAssembliesPath>$(GamePath)/MelonLoader/Il2CppAssemblies</GameAssembliesPath>
    <LoaderAssembliesPath>$(GamePath)/MelonLoader/net6</LoaderAssembliesPath>
    <DeployOnBuild>true</DeployOnBuild>
  </PropertyGroup>
</Project>
```

The generator determines the directory values from the selected installation. The generated project references `$(GameAssembliesPath)/UnityEngine.CoreModule.dll` or `$(LoaderAssembliesPath)/MelonLoader.dll` directly. 

Mono and older loader installations get their actual directories instead. Additional reference folders receive `ReferenceAssembliesPath1`, etc. Folders outside the installation retain independent absolute paths.

Moving an installation only requires editing `GamePath`. Each assembly directory can also be overridden independently, including with a project-relative value such as `$(MSBuildThisFileDirectory)references/game`. Set `ProjectOptions.CopyAssemblies` to generate a local reference snapshot instead.

`ProjectOptions.DeployOnBuild` defaults to true for compatibility with the wizard. Set it to false to disable deployment by default, edit the generated props, or run `dotnet build -p:DeployOnBuild=false`. Deployment uses an MSBuild copy task and requires an existing game directory. References remain independent of the deployment destination when their assembly directory properties are overridden.

### Copying assembly references

```csharp
options.CopyAssemblies = true;
options.DeployOnBuild = false;
ProjectGenerationResult result = generator.Generate(options);

// Your UI can preview result.Files, result.AssemblyCopies, and result.Warnings.
// Write result.Files to the project directory, then explicitly execute the copy plan:
result.CopyAssembliesTo(projectDirectory);
```

Generation never writes files. `AssemblyCopies` records each absolute source path and project-relative destination. `CopyAssembliesTo` copies selected references and the required loader support references, refusing to overwrite existing files unless `overwrite: true` is supplied. It checks missing sources and existing destinations before beginning. Filesystem errors during copying can still leave a partial snapshot. Sources are read when the copy plan is executed, not captured as bytes during generation.

Generated assembly properties point to `$(MSBuildThisFileDirectory)references/Mono/...` or `references/IL2CPP/...`, with separate game, loader, and additional reference folders. Compiler references have `Private=false`, so these DLLs are not automatically copied into build output. The snapshot can move with the project and compilation no longer requires the game installation. Deployment remains independent and still requires `GamePath` when enabled.

`Generate` returns a `ProjectGenerationResult` containing `Files`, `AssemblyCopies`, and `Warnings`. It has overloads for `ProjectOptions` and `DualRuntimeOptions`; both generate without writing files.

### Mono and IL2CPP projects

The **MelonLoader Mod (Mono and IL2CPP)** Visual Studio template asks for one Mono installation and one IL2CPP installation. These can be different editions of one game or different games used as development targets. The generated project has configurations `Mono-Debug`, `Mono-Release`, `Il2Cpp-Debug`, and `Il2Cpp-Release`, with platform `AnyCPU` like normal.

Your own tool can generate the same project without Visual Studio:

```csharp
DualRuntimeOptions options = new()
{
    ProjectName = "MyMod",
    RootNamespace = "MyMod",
    Author = "Me",
    Mono = new RuntimeTargetOptions
    {
        Game = monoGame,
        References = monoSelection,
        CopyAssemblies = true,
        DeployOnBuild = false
    },
    Il2Cpp = new RuntimeTargetOptions
    {
        Game = il2CppGame,
        References = il2CppSelection,
        CopyAssemblies = true,
        DeployOnBuild = false
    }
};
ProjectGenerationResult result = generator.Generate(options);
```

Each runtime independently chooses its framework, references, copied snapshot, and deployment destination. `Directory.Build.props` keeps those paths in conditional groups. Supporting different game APIs in your own code remains your responsibility. 

The library also supports dual-runtime plugins through `Kind = ProjectKind.Plugin`.

```powershell
dotnet build MyMod.slnx -c Mono-Debug
dotnet build MyMod.slnx -c Il2Cpp-Release
```

Tests evaluate all four combinations with installation and copied references, then compile all four against minimal loader API fixtures with both matching and differing initialization methods. Those fixtures verify generation and build mechanics, not actual in-game compatibility.

### Editing the templates

Template files live in [`MelonLoader.VSWizard.ProjectGeneration/Templates`](MelonLoader.VSWizard.ProjectGeneration/Templates), in the `Mod`, `Plugin`, and `DualRuntime` directories. Edit `Core.cs` for the entry class, `ProjectTemplate.csproj` for project settings, and `Directory.Build.props` for assembly paths and deployment defaults. The dual-runtime template also has `Solution.slnx` for solution configuration mappings.

These files are embedded into the library and linked into the Visual Studio template packages at build time. Rebuild the library after editing them; rebuild and reinstall the VSIX to update the installed Visual Studio templates.

The dual-runtime `Core.cs` is its own shared-class template. Its `$GAME_ATTRIBUTE$` and `$INIT_METHOD$` placeholders are populated by [`DualRuntimeGenerator.cs`](MelonLoader.VSWizard.ProjectGeneration/DualRuntimeGenerator.cs), which emits conditional lines only when the two targets differ. It does not insert separate copies of the Mod or Plugin entry class.

## Licensing
- [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) is licensed under the MIT License. See [LICENSE](https://github.com/AssetRipper/Primitives/blob/master/License.md) for the full License.
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) is licensed under the MIT License. See [LICENSE](https://github.com/nesrak1/AssetsTools.NET/blob/master/LICENSE) for the full License.
- [StrongNamer](https://github.com/dsplaisted/strongnamer) is licensed under the MIT License. See [LICENSE](https://github.com/dsplaisted/strongnamer/blob/master/LICENSE) for the full License.
- [MelonLoader](https://github.com/LavaGang/MelonLoader) is licensed under the Apache License, Version 2.0. See [LICENSE](https://github.com/LavaGang/MelonLoader/blob/master/LICENSE.md) for the full License.
