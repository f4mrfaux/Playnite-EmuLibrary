:::::: {.bg-body .border-bottom}
::::: {.container-xxl .flex-nowrap}
[![](../../logo.svg){#logo .svg}](../../index.html){.navbar-brand}

:::: {#navpanel .collapse .navbar-collapse}
::: {#navbar}
:::
::::
:::::
::::::

::::::::::::::::: {.container-xxl role="main"}
:::::: toc-offcanvas
::::: {#tocOffcanvas .offcanvas-md .offcanvas-start tabindex="-1" aria-labelledby="tocOffcanvasLabel"}
::: offcanvas-header
##### Table of Contents {#tocOffcanvasLabel .offcanvas-title}
:::

::: offcanvas-body
:::
:::::
::::::

::::::::::: content
::: actionbar
:::

# Introduction to Themes

## General information

Playnite\'s user interface is implemented using Windows Presentation
Framework (WPF) and UI definition is done using
[XAML](https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/xaml-overview-wpf)
files. Custom themes in Playnite are implemented using [standard
template and
styling](https://docs.microsoft.com/en-us/dotnet/framework/wpf/controls/styling-and-templating)
support that WPF provides, therefore any tutorial that applies to
styling in WPF also applies to Playnite.

## Fullscreen and Desktop modes

Playnite offers two separate modes of operation. Standard `Desktop` mode
designed for keyboard and mouse and `Fullscreen` mode designed to be
controlled with gamepad. These two modes are implemented completely
separately and therefore themes are also completely separate.

## Learning resources

Since Playnite themes are essentially just set of [template and
style](https://docs.microsoft.com/en-us/dotnet/framework/wpf/controls/styling-and-templating)
files, general editing rules and tutorials that apply to WPF also apply
to Playnite.

Recommended WPF resources:

-   <https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/xaml-overview-wpf>
-   <https://www.wpftutorial.net/GettingStarted.html>
-   <https://www.tutorialspoint.com/wpf/>

::: NOTE
##### Note

There\'s currently very active community around theme/extension
development on our [Discord server](https://discord.gg/hSFvmN6). We
highly recommend joining if you plan to develop add-ons for Playnite!
:::

## Creating Playnite themes

::: WARNING
##### Warning

Do not edit built-in default themes. Always create new copy of theme
files (ideally using Toolbox utility) are edit those. Broken edits to
default theme files could lead to Playnite not being able to start
anymore.
:::

::: WARNING
##### Warning

Please read the documentation carefully, especially section about
[distribution and theme updates](distributionAndUpdates.html). Not
updating your theme regularly could cause issues to theme users, for
example they might not be able to use newly added features. Or they
might not be able to load the theme at all in newer version of Playnite,
in the worst case scenario.
:::

There are generally two approaches to theme creation in Playnite.

1.  **[Manually editing](manualEditing.html)** XAML files using any text
    editor.

2.  **[Using Blend/Visual Studio](usingBlend.html)** designer.

Option #1 doesn\'t require installation of any additional applications
and themes can be generally created even using Notepad. However this
approach has major disadvantages:

-   You don\'t get live preview of changes your are making.
-   You have to restart Playnite every time a change is made to theme
    files.
-   There\'s not autocompletion or error checking for XAML syntax.

Option #2 requires installation of [Visual Studio
IDE](https://visualstudio.microsoft.com/), Community edition is free to
download and includes
[Blend](https://docs.microsoft.com/en-us/visualstudio/designers/creating-a-ui-by-using-blend-for-visual-studio?view=vs-2019)
editor. This approach takes some time to set up, but offers all
advantages that manual editing lacks, like live preview, autocompletion
of XAML properties, visual editor etc.

::: WARNING
##### Warning

The latest Visual Studio **2022** is currently not supported (live
previews will not work)! Use [2019 or 2017
version](https://download.visualstudio.microsoft.com/download/pr/93f24e82-778c-46ae-92f9-8d3010ecd011/a5da04d78b1f94ab145a365733476df7a1ec6219fa17f09c7e2f3c7cd74d9c9e/vs_Community.exe)
instead.
:::

**Using Blend editor is recommended option.**

::: WARNING
##### Warning

Theme installation and update always replaces the entire theme directory
completely. Meaning that any files that are not part of the installation
package will be lost during installation process! If your theme includes
some custom functionality that requires user to replace/add files to
theme\'s directory, make sure they know that they will loose those
changes after an update!
:::

## Integrating with plugins

Plugins can provide custom UI elements that can be integration into a
theme. See [extension integration page](extensionIntegration.html) for
more details.

## Theme files and directories

This section explains contents and purpose of specific theme files.

  Directory/File    Description
  ----------------- ----------------------------------------------------------------------------------------------------------------------------------------------------------------
  DefaultControls   Styles for standard (built-in WPF) controls like button, checkbox etc.
  DerivedStyles     Styles for standard (built-in WPF) controls like button, checkbox etc., that are used in specific cases. For example Play button, list item for Grid view etc.
  CustomControls    Styles for custom Playnite controls.
  Views             Styles for library views and panels.
  Common.xaml       Base styles that are inherited by other styles from the theme.
  Constants.xaml    Colors, brushes, sizes and other constants used by styles form the theme.
  Media.xaml        Various icons, texts and image specifications used by styles form the theme.

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/PlayniteDocs/blob/main/docs/tutorials/themes/introduction.md/#L1){.edit-link}
:::

::: {#nextArticle .next-article .d-print-none .border-top}
:::
:::::::::::

::: affix
:::
:::::::::::::::::

::: {#search-results .container-xxl .search-results}
:::

:::: container-xxl
::: flex-fill
Made with [docfx](https://dotnet.github.io/docfx)
:::
::::
