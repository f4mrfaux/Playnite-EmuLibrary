:::::: {.bg-body .border-bottom}
::::: {.container-xxl .flex-nowrap}
[![](../logo.svg){#logo .svg}](../index.html){.navbar-brand}

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

# Extension Toolbox utility

## Introduction

Toolbox is a Playnite utility that can be used for various tasks, mainly
for creating extensions and themes. Toolbox is distributed with every
Playnite installation and can be found in Playnite\'s installation
directory.

## Creating new extensions

### Themes

``` lang-text
Toolbox.exe new <themetype> <themename>
```

`<themetype>` available options:

-   **DesktopTheme**
-   **FullscreenTheme**

`<themename>` - name of the theme.

#### Example

``` lang-text
Toolbox.exe new desktoptheme "New Desktop Theme"
```

### Scripts

``` lang-text
Toolbox.exe new <scripttype> <scriptname> <targetfolder>
```

`<scripttype>` available options:

-   **PowerShellScript**

`<scriptname>` - name of the new script extension.

`<targetfolder>` - folder to create script in.

#### Example

``` lang-text
Toolbox.exe new PowerShellScript "Testing Script" "d:\somefolder"
```

### Plugins

``` lang-text
Toolbox.exe new <plugintype> <pluginname> <targetfolder>
```

`<plugintype>` available options:

-   **GenericPlugin**
-   **MetadataPlugin**
-   **LibraryPlugin**

`<pluginname>` - name of the new plugin extension.

`<targetfolder>` - folder to create plugin in.

#### Example

``` lang-text
Toolbox.exe new MetadataPlugin "GameDatabase metadata provider" "d:\somefolder"
```

## Packing extensions

``` lang-text
Toolbox.exe pack <extensionfolder> <targetfolder>
```

`<extensionfolder>` - extension directory (theme, script or plugin) to
pack (in case of plugins it has to be folder with built binaries).

`<targetfolder>` - target directory where to save packed file.

#### Example

``` lang-text
Toolbox.exe pack "C:\Playnite\Themes\Fullscreen\TestingFullscreen" "c:\somefolder"
```

\... will create `c:\somefolder\TestingFullscreen.pthm` package.

## Verify manifests

``` lang-text
Toolbox.exe verify <manifest_type> <manifest_path>
```

`<manifest_type>` - `addon` for addon browser manifest or `installer`
for package installer manifest. `addon` verifies linked installer
manifest automatically.

`<manifest_path>` - Path to manifest yaml file. Local full path or HTTP
URLs are supported.

``` lang-text
Toolbox.exe verify <manifest_type> <manifest_path>
```

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/PlayniteDocs/blob/main/docs/tutorials/toolbox.md/#L1){.edit-link}
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
