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

# Integrating extension elements

## Introduction

If an extension use Playnite SDK to officially expose its custom UI
elements, then you can use following markups to more easily integrate
those elements. This requires proper support for specific extension,
it\'s not something that\'s generally enabled by default on all
extensions. You should contact extension developer for support in case
you have issue integrating specific element.

## Integrating elements

To actually use plugin control in a view, add `ContentControl` with its
name set in `<SourceName>_<ElementName>` format:

-   `SourceName` is plugin\'s source name.
-   `ElementName` is a specific element name you want to integrate.

Both of these should be provided by an extension developer.

For example, to include `TestUserControl1` control from `TestPlugin`
source:

``` lang-xml
<ContentControl x:Name="TestPlugin_TestUserControl1" />
```

## Detecting if an extension is installed

You can use `PluginStatus` markup to add conditions based on if a plugin
is installed or not.

``` lang-xml
<SomeElement Property="{PluginStatus Plugin=AddonId, Status=Installed}" />
```

`PluginStatus` automatically converts to `Visibility` value if used on
Visibility property, it\'s not needed to use converter in that case. In
other cases it return\'s `bool` value, `true` if a plugin is installed.

`AddonId` should be provided by extension\'s developer.

## Extension settings

If an extension provides support for themes to use its settings, then
you can use `PluginSettings` markup to reference them:

``` lang-xml
<TextBlock Text="{PluginSettings Plugin=SourceName, Path=CustomOption}" />
```

\...where `SourceName` is the plugin source name and `CustomOption` is
the name of a specific settings property (or path in case you want to
reference nested properties).

## Plugin converters

If a plugin provides custom value converters, you can use them via
`PluginConverter` markup:

``` lang-xml
<TextBlock Text="{Binding SomeValue, Converter={PluginConverter Plugin=SourceName, Converter=ConverterName}}" />
```

\...where `SourceName` is the plugin source name and `ConverterName` is
the name of a specific converter class provided by the plugin.

If referenced plugin isn\'t installed or specific converter is not
provided by the plugin, `PluginConverter` will return
[DependencyProperty.UnsetValue](https://docs.microsoft.com/en-us/dotnet/api/system.windows.dependencyproperty.unsetvalue?view=netframework-4.6.2),
which means that you can use `FallbackValue` binding property to set
specific data when converter is missing.

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/PlayniteDocs/blob/main/docs/tutorials/themes/extensionIntegration.md/#L1){.edit-link}
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
