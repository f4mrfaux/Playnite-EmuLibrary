:::::: {.bg-body .border-bottom}
::::: {.container-xxl .flex-nowrap}
[![](../../logo.svg){#logo .svg}](../../index.html){.navbar-brand}

:::: {#navpanel .collapse .navbar-collapse}
::: {#navbar}
:::
::::
:::::
::::::

::::::::::::: {.container-xxl role="main"}
:::::: toc-offcanvas
::::: {#tocOffcanvas .offcanvas-md .offcanvas-start tabindex="-1" aria-labelledby="tocOffcanvasLabel"}
::: offcanvas-header
##### Table of Contents {#tocOffcanvasLabel .offcanvas-title}
:::

::: offcanvas-body
:::
:::::
::::::

::::::: content
::: actionbar
:::

# Use VSCode to develop Playnite extensions in Windows

::: WARNING
##### Warning

This guide is not up to date with the latest changes made to C#
development in VS Code and therefore not all steps might be correct.

Proper VS Code development doc will be made after Playnite transitions
to modern .NET runtime with Playnite 11.
:::

This is a step by step tutorial to build the Playnite extensions using
VSCode.

## Download and setup dependencies

1.  Install [VSCode](https://code.visualstudio.com/Download)
2.  Install [.NET SDK for
    VSCode](https://dotnet.microsoft.com/en-us/download/dotnet/sdk-for-vs-code)
3.  Install [.NET Framework 4.6.2 Developer
    Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462)
4.  Install [Visual Build Tools for Visual Studio 2015 with Update
    3](https://my.visualstudio.com/Downloads?q=%22Visual%20Build%20Tools%20for%20Visual%20Studio%202015%20with%20Update%203%22)
    (Requires registration)
5.  Install [C# extension in
    VSCode](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)
6.  Install [nuget](https://www.nuget.org/downloads) (If in doubt,
    follow the instruction in [Microsoft
    Docs](https://docs.microsoft.com/en-us/nuget/install-nuget-client-tools#nugetexe-cli),
    and be sure to add it to the path!)

## Prepare your project folder

1.  [Create the project using
    toolbox.exe](https://playnite.link/docs/master/tutorials/toolbox.html#plugins)
2.  Open the project folder in VSCode
3.  In VSCode, press `ctrl+shit+P`, type `Terminal: Create New Terminal`
    and press `Enter`
4.  Create a new `AssemblyInfo.cs` file:

``` lang-powershell
Get-Content .\Properties\AssemblyInfo.cs | Where-Object {$_ -notmatch '^\[assembly: Assembly.+'} | Set-Content .\AssemblyInfo.cs
Remove-Item -path .\Properties\ -recurse -force
```

5.  Install PlayniteSDK package using `nuget`:

``` lang-shell
nuget restore -SolutionDirectory . -Verbosity normal
```

6.  Replace the csproj file content in your project folder for this:

``` lang-xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net4.6.2</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
        <DefineConstants>DEBUG;NET462;TRACE</DefineConstants>
        <DebugType>portable</DebugType>
  </PropertyGroup> 
  <ItemGroup>
    <ApplicationDefinition Remove="App.xaml" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="PlayniteSDK" Version="6.2.0" />
  </ItemGroup>
  <ItemGroup>
    <None Include="extension.yaml">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="packages.config" />
  </ItemGroup>
  <ItemGroup>
    <None Include="icon.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

7.  In VSCode, press `ctrl+shift+P`, type
    `.NET: Generate Assets for Build and Debug` and press `Enter`
8.  In VSCode, press `ctrl+shift+P`, type `Tasks: Run Task > publish`
    and press `Enter`
9.  Output folder should be in `.\bin\Debug\net4.6.2\publish`. Add the
    full path (including drive letter) to the external extensions list
    in Playnite, and restart it.
10. In Playnite, you should see your extension running in the Add-Ons
    window.

As soon as build VSCode debugger are not support x86 application debug
and execution, but Playnite x86 officially only, You have to prepare x64
version of Playnite yourself to be able to debug. To do it you have:

1.  Install Windows PowerShell version 7.0.
2.  Open Powershell version 7.0 and in PlayniteSources call
    `.\build\build.ps1 Debug x64 c:\playnite` this will build binaries
    and move to c:\\playnite. Please be aware that it may affect already
    existed Playnite installation at this location.
3.  You have to download and replace x86 to x64 versions of SDL2.dll and
    SDL2_mixer.dll. Obtain it from official releases
    <https://github.com/libsdl-org/SDL/releases> and
    <https://github.com/libsdl-org/SDL_mixer/releases>
4.  Tune you .vscode/tasks.json:

``` lang-json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build debug",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary;ForceNoAlign"
            ],
            "problemMatcher": "$msCompile",
            "group": {
                "kind": "build",
                "isDefault": true
            }
        },
        {
            "label": "build release",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary;ForceNoAlign",
                "-c",
                "Release"
            ],
            "problemMatcher": "$msCompile"
        },
        {
            "label": "pack",
            "command": "C:\\Playnite\\Toolbox.exe",
            "type": "process",
            "args": [
                "pack",
                "${workspaceFolder}\\bin\\Release\\net4.6.2",
                "${workspaceFolder}\\bin\\Package"
            ],
            "problemMatcher": "$msCompile",
            "dependsOn": [
                "build release"
            ]
        }
    ]
}
```

and .vscode/launch.json:

``` lang-json
{
    "configurations": [
        {
            "name": "Debug PlayniteSounds Desktop",
            "type": "clr",
            "request": "launch",
            "preLaunchTask": "build debug",
            "program": "C:\\Playnite\\Playnite.DesktopApp.exe",
            "args": [],
            "cwd": "${workspaceFolder}",
            "stopAtEntry": false,
            "console": "internalConsole",
            "logging": {
                "programOutput": true,
                "moduleLoad": false,
                "processExit": false
            }
        },
        {
            "name": "Debug PlayniteSounds Fullscreen",
            "type": "clr",
            "request": "launch",
            "preLaunchTask": "build debug",
            "program": "C:\\Playnite\\Playnite.FullscreenApp.exe",
            "args": [],
            "cwd": "${workspaceFolder}",
            "stopAtEntry": false,
            "console": "internalConsole",
            "logging": {
                "programOutput": true,
                "moduleLoad": false,
                "processExit": false
            }
        }
    ]
}
```

You can use something like [XAML Studio](https://aka.ms/xamlstudio) to
edit single XAML files graphically.

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/PlayniteDocs/blob/main/docs/tutorials/extensions/vscodeWindows.md/#L1){.edit-link}
:::

::: {#nextArticle .next-article .d-print-none .border-top}
:::
:::::::

::: affix
:::
:::::::::::::

::: {#search-results .container-xxl .search-results}
:::

:::: container-xxl
::: flex-fill
Made with [docfx](https://dotnet.github.io/docfx)
:::
::::
