# MelonLoader VS Wizard

An automated Visual Studio template for creating MelonLoader mods and plugins. It supports MelonLoader 0.5.0 to the latest as well as Il2Cpp and Mono games.

## What does it handle?
It handles the creation of the required boilerplate (the `MelonMod`/`MelonPlugin` class, `MelonInfo`, and `MelonGame`) as well as referencing the required assemblies for mod development, mainly MelonLoader, Harmony, and for Il2Cpp, proxy assemblies and the unhollower (Il2CppAssemblyUnhollower or Il2CppInterop). It also handles variation between MelonLoader or Unity versions, such as framework versions or override changes.

## Usage
0. Download MelonLoader to your game and run it once before continuing.
1. Download the VSIX from the [Releases](https://github.com/TrevTV/MelonLoader.VSWizard/releases) tab.
2. Close all instances of Visual Studio and run the VSIX installer (double-clicking it should open it).
3. Open Visual Studio and create a new project.
4. Search for `MelonLoader` and click on either Mod or Plugin.
5. Enter the project info and press Create.
6. Select the EXE of the game you are modding and press Open.
7. Wait for the project creation and it should open a Visual Studio window with a working project.

You may want to change the author in the `MelonInfo` attribute. It defaults to your computer's username.

## Licensing
- [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) is licensed under the MIT License. See [LICENSE](https://github.com/AssetRipper/Primitives/blob/master/License.md) for the full License.
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) is licensed under the MIT License. See [LICENSE](https://github.com/nesrak1/AssetsTools.NET/blob/master/LICENSE) for the full License.
- [StrongNamer](https://github.com/dsplaisted/strongnamer) is licensed under the MIT License. See [LICENSE](https://github.com/dsplaisted/strongnamer/blob/master/LICENSE) for the full License.
- [MelonLoader](https://github.com/LavaGang/MelonLoader) is licensed under the Apache License, Version 2.0. See [LICENSE](https://github.com/LavaGang/MelonLoader/blob/master/LICENSE.md) for the full License.

## Reusable project generation

`MelonLoader.ProjectGeneration` is a .NET Standard 2.0 library with no Visual Studio or UI dependencies. It owns game inspection, framework/reference selection, Unity metadata parsing, and the template contents. The existing template projects package linked library assets for Visual Studio; the wizard handles dialogs and forwards the library's replacements.

Reference its project from another application:

```csharp
using MelonLoader.ProjectGeneration;
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
IReadOnlyDictionary<string, string> files = generator.Generate(options);
```

`files` contains relative filenames and their complete contents. The caller controls previewing and writing them. Use a valid C# namespace and a project name suitable for a filename. `Inspect` reads the installation and throws exceptions for invalid inputs; it never displays UI. `CreateReplacements` is available for hosts using token-based templates. Game inspection still requires MelonLoader to be installed and, for IL2CPP, its assemblies to have been generated.

Build the library independently with `dotnet build MelonLoader.ProjectGeneration`. Building the VSIX also requires Visual Studio SDK build tooling and the repository's strong-name key (`MelonLoader.WizardExtension/key.snk`).

Run the xUnit tests with `dotnet test tests/MelonLoader.ProjectGeneration.Tests`. They use temporary fixture installations rather than requiring a real game.

### Reference selection for your own UI

```csharp
IReadOnlyList<AssemblyReference> available = generator.DiscoverReferences(game);
// Show available in your UI and collect the chosen AssemblyReference objects.
IReadOnlyList<AssemblyReference> selected = selectedByYourUi;
IReadOnlyList<AssemblyReference> resolved = generator.ResolveReferences(game, selected);
IReadOnlyList<ReferenceDiagnostic> diagnostics = generator.ValidateReferences(resolved);
options.References = selected;
IReadOnlyDictionary<string, string> output = generator.Generate(options);
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

The generator determines the directory values from the selected installation; the generated project references `$(GameAssembliesPath)/UnityEngine.CoreModule.dll` or `$(LoaderAssembliesPath)/MelonLoader.dll` directly. Mono and older loader installations get their actual directories instead. Additional reference folders receive `ReferenceAssembliesPath1`, etc. Folders outside the installation retain independent absolute paths.

Moving an installation only requires editing `GamePath`. Each assembly directory can also be overridden independently, including with a project-relative value such as `$(MSBuildThisFileDirectory)references/game`. Set `ProjectOptions.CopyAssemblies` to generate a local reference snapshot instead.

`ProjectOptions.DeployOnBuild` defaults to true for compatibility with the wizard. Set it to false to disable deployment by default, edit the generated props, or run `dotnet build -p:DeployOnBuild=false`. Deployment uses an MSBuild copy task and requires an existing game directory. References remain independent of the deployment destination when their assembly directory properties are overridden.

### Copying assembly references

```csharp
options.CopyAssemblies = true;
options.DeployOnBuild = false;
ProjectGenerationResult result = generator.GeneratePlan(options);

// Your UI can preview result.Files, result.AssemblyCopies, and result.Warnings.
// Write result.Files to the project directory, then explicitly execute the copy plan:
result.CopyAssembliesTo(projectDirectory);
```

Generation never writes files. `AssemblyCopies` records each absolute source path and project-relative destination. `CopyAssembliesTo` copies selected references and the required loader support references, preserving existing files unless `overwrite: true` is supplied. It checks missing sources and existing destinations before beginning; filesystem errors during copying can still leave a partial snapshot. Sources are read when the copy plan is executed, not captured as bytes during generation.

Generated assembly properties point to `$(MSBuildThisFileDirectory)references/Mono/...` or `references/IL2CPP/...`, with separate game, loader, and additional reference folders. Compiler references have `Private=false`, so these DLLs are not automatically copied into build output. The snapshot can move with the project and compilation no longer requires the game installation. Deployment remains independent and still requires `GamePath` when enabled.

The original `Generate(options)` API still returns text files only; when copying is enabled, use `GeneratePlan` to get the matching copy operations too. Copying is disabled by default and the existing Visual Studio wizard continues referencing the installation. No Git ignore policy is imposed. A warning reminds callers to check redistribution permissions before publishing copied DLLs. The snapshot contains resolved selections, not an automatically discovered closure of arbitrary transitive dependencies.

### Mono and IL2CPP projects

The **MelonLoader Mod (Mono and IL2CPP)** Visual Studio template asks for one Mono installation and one IL2CPP installation. These can be different editions of one game or different games used as development targets. Both need valid references. The generated project has configurations `Mono-Debug`, `Mono-Release`, `Il2Cpp-Debug`, and `Il2Cpp-Release`, with platform and CPU target `AnyCPU`. It includes a standalone `.slnx` with exactly the four runtime/build combinations; open that solution when working outside an existing solution.

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
ProjectGenerationResult result = generator.GenerateDualRuntimePlan(options);
```

Each runtime independently chooses its framework, references, copied snapshot, and deployment destination. `Directory.Build.props` keeps those paths in conditional groups. The runtime defines `MONO` or `IL2CPP`; the Debug/Release portion of the configuration selects optimization, debug information, and `DEBUG`/`TRACE`. Output and intermediate directories are separated by configuration and platform to avoid sharing compiler/restore artifacts between targets. The generated entry class shares its initialization body between runtimes. Conditional compilation is added only when the game attributes or initialization method differ between targets. Supporting different game APIs in your own code remains your responsibility. The library also supports dual-runtime plugins through `Kind = ProjectKind.Plugin`.

```powershell
dotnet build MyMod.slnx -c Mono-Debug
dotnet build MyMod.slnx -c Il2Cpp-Release
```

Tests evaluate all four combinations with installation and copied references, then compile all four against a minimal loader API fixture. Those fixtures verify generation and build mechanics, not actual in-game compatibility.
