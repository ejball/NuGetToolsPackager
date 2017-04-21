# NuGetToolsPackager

**NuGetToolsPackager** generates a `.nuspec` file for a NuGet tools package from a Visual Studio 2017 console app project.

## Installation

Use [NuGet](https://www.nuget.org/) to install **NuGetToolsPackager** from its [NuGet package](https://www.nuget.org/packages/XmlDocMarkdown).

For example, `nuget install NuGetToolsPackager -excludeversion` will download the latest version of `NuGetToolsPackager.exe` into `NuGetToolsPackager/tools`.

## Usage

The `NuGetToolsPackager` command-line tool accepts the path to a `.csproj` file, the path to the file or directory where the `.nuspec` file should be created (optional), and a number of options.

For example, `NuGetToolsPackager MyProject.csproj` generates a `.nuspec` file for that project in the current directory.

### Options

* `--configuration <name>`: The project configuration to use. Must match the configuration part of the directory name of the built executable. Defaults to `Release`.
* `--platform <name>`: The project platform to use. Must match the platform part of the directory name of the built executable, if any, e.g. `net46`.
* `--output <directory>`: The output directory for the `.nuspec` file. Defaults to the current directory.
* `--files <filespecs>`: The files to be included in the NuGet package. Defaults to `*.exe;*.dll;*.config`.
* `--quiet`: Suppresses normal console output.
