:::::: {.bg-body .border-bottom}
::::: {.container-xxl .flex-nowrap}
[![](../../logo.svg){#logo .svg}](../../index.html){.navbar-brand}

:::: {#navpanel .collapse .navbar-collapse}
::: {#navbar}
:::
::::
:::::
::::::

:::::::::::: {.container-xxl role="main"}
:::::: toc-offcanvas
::::: {#tocOffcanvas .offcanvas-md .offcanvas-start tabindex="-1" aria-labelledby="tocOffcanvasLabel"}
::: offcanvas-header
##### Table of Contents {#tocOffcanvasLabel .offcanvas-title}
:::

::: offcanvas-body
:::
:::::
::::::

:::::: content
::: actionbar
:::

# Extensions Manifest

Extension manifest files are used by Playnite to load extensions and
display basic information to user on extension settings dialog. Manifest
files are mandatory and extension won\'t be loaded unless valid manifest
file is present inside extension directory under `extension.yaml` file
name.

## Format

Manifest is YAML formatted file with following properties:

  Property   Description
  ---------- -----------------------------------------------------------------------------------------------------------------------
  Id         Unique string identifier for the extension. Must not be shared with any other extension.
  Name       Extension name.
  Author     Extension author.
  Version    Extension version, must be a valid [.NET version string](https://docs.microsoft.com/en-us/dotnet/api/system.version).
  Module     File name of assembly `*.dll` file for plugins, `*.psm1` or `*.psd1` file for scripts.
  Type       Extension type, available values are: `Script`, `GenericPlugin`, `GameLibrary`, `MetadataProvider`.
  Icon       Optional relative file name of extension icon.
  Links      Optional list of links (extension website, changelog etc.)

## Examples

**Example of script extension:**

``` lang-yaml
Id: LibraryExporter_Playnite_Script
Name: Library Exporter
Author: Playnite
Version: 1.0
Module: LibraryExporter.psm1
Type: Script
Links:
    - Name: Website
      Url: https://some.website.nowhere
```

**Example of library plugin:**

``` lang-yaml
Id: SomeLibraryPlugin_Playnite_Plugin
Name: Some Library Plugin
Author: Playnite
Version: 1.0
Module: SomeLibraryPlugin.dll
Type: GameLibrary
Icon: pluginicon.png
Links:
    - Name: Website
      Url: https://some.website.nowhere
```

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/PlayniteDocs/blob/main/docs/tutorials/extensions/extensionsManifest.md/#L1){.edit-link}
:::

::: {#nextArticle .next-article .d-print-none .border-top}
:::
::::::

::: affix
:::
::::::::::::

::: {#search-results .container-xxl .search-results}
:::

:::: container-xxl
::: flex-fill
Made with [docfx](https://dotnet.github.io/docfx)
:::
::::
