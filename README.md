> [!NOTE]
> 🚧 Work in progress 🚧

# TinyIcon

[![GitHub License](https://badgen.net/github/license/pzychotic/TinyIcon)](https://github.com/pzychotic/TinyIcon/blob/main/LICENSE)

A tiny windows icon file (.ico) creator.
Define your wanted resolutions, import image, save. Done!

## Build
### From the command line

Prerequisites:
- .NET SDK 10.0 - install from https://dotnet.microsoft.com/

Build and run:
1. Build:
   ```
   dotnet build
   ```
2. Run:
   ```
   dotnet run --project Source\TinyIcon\TinyIcon.csproj
   ```

### From Visual Studio 2026

Prerequisites:
- .Net Desktop development workload
- .Net 10.0 Runtime
- .Net SDK

Just open ```TinyIcon.slnx``` build and run.

## Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [Microsoft.Xaml.Behaviors.Wpf](https://github.com/microsoft/XamlBehaviorsWpf)
