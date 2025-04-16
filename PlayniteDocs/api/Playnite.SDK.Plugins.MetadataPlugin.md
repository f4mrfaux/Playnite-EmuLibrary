:::::: {.bg-body .border-bottom}
::::: {.container-xxl .flex-nowrap}
[![](../logo.svg){#logo .svg}](../index.html){.navbar-brand}

:::: {#navpanel .collapse .navbar-collapse}
::: {#navbar}
:::
::::
:::::
::::::

:::::::::::::::::::::::::::::: {.container-xxl role="main"}
:::::: toc-offcanvas
::::: {#tocOffcanvas .offcanvas-md .offcanvas-start tabindex="-1" aria-labelledby="tocOffcanvasLabel"}
::: offcanvas-header
##### Table of Contents {#tocOffcanvasLabel .offcanvas-title}
:::

::: offcanvas-body
:::
:::::
::::::

:::::::::::::::::::::::: content
::: actionbar
:::

# Class MetadataPlugin [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/Plugins/MetadataPlugin.cs/#L335 "View source"){.header-action .link-secondary} {#Playnite_SDK_Plugins_MetadataPlugin .text-break uid="Playnite.SDK.Plugins.MetadataPlugin"}

::: {.facts .text-secondary}

Namespace
:   [Playnite](Playnite.html){.xref}.[SDK](Playnite.SDK.html){.xref}.[Plugins](Playnite.SDK.Plugins.html){.xref}

```{=html}
<!-- -->
```

Assembly
:   Playnite.SDK.dll
:::

::: {.markdown .summary}
Represents plugin providing game metadata.
:::

::: {.markdown .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
public abstract class MetadataPlugin : Plugin, IDisposable, IIdentifiable
```
:::

Inheritance

:   <div>

    [object](https://learn.microsoft.com/dotnet/api/system.object){.xref}

    </div>

    <div>

    [Plugin](Playnite.SDK.Plugins.Plugin.html){.xref}

    </div>

    <div>

    [MetadataPlugin]{.xref}

    </div>

```{=html}
<!-- -->
```

Implements

:   <div>

    [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable){.xref}

    </div>

    <div>

    [IIdentifiable](Playnite.SDK.Models.IIdentifiable.html){.xref}

    </div>

```{=html}
<!-- -->
```

Inherited Members

:   <div>

    [Plugin.Searches](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_Searches){.xref}

    </div>

    <div>

    [Plugin.PlayniteApi](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_PlayniteApi){.xref}

    </div>

    <div>

    [Plugin.Id](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_Id){.xref}

    </div>

    <div>

    [Plugin.Dispose()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_Dispose){.xref}

    </div>

    <div>

    [Plugin.GetSettings(bool)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetSettings_System_Boolean_){.xref}

    </div>

    <div>

    [Plugin.GetSettingsView(bool)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetSettingsView_System_Boolean_){.xref}

    </div>

    <div>

    [Plugin.OnGameStarting(OnGameStartingEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnGameStarting_Playnite_SDK_Events_OnGameStartingEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnGameStarted(OnGameStartedEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnGameStarted_Playnite_SDK_Events_OnGameStartedEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnGameStopped(OnGameStoppedEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnGameStopped_Playnite_SDK_Events_OnGameStoppedEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnGameStartupCancelled(OnGameStartupCancelledEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnGameStartupCancelled_Playnite_SDK_Events_OnGameStartupCancelledEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnGameInstalled(OnGameInstalledEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnGameInstalled_Playnite_SDK_Events_OnGameInstalledEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnGameUninstalled(OnGameUninstalledEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnGameUninstalled_Playnite_SDK_Events_OnGameUninstalledEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnGameSelected(OnGameSelectedEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnGameSelected_Playnite_SDK_Events_OnGameSelectedEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnApplicationStarted(OnApplicationStartedEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnApplicationStarted_Playnite_SDK_Events_OnApplicationStartedEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnApplicationStopped(OnApplicationStoppedEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnApplicationStopped_Playnite_SDK_Events_OnApplicationStoppedEventArgs_){.xref}

    </div>

    <div>

    [Plugin.OnLibraryUpdated(OnLibraryUpdatedEventArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OnLibraryUpdated_Playnite_SDK_Events_OnLibraryUpdatedEventArgs_){.xref}

    </div>

    <div>

    [Plugin.GetGameMenuItems(GetGameMenuItemsArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetGameMenuItems_Playnite_SDK_Plugins_GetGameMenuItemsArgs_){.xref}

    </div>

    <div>

    [Plugin.GetMainMenuItems(GetMainMenuItemsArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetMainMenuItems_Playnite_SDK_Plugins_GetMainMenuItemsArgs_){.xref}

    </div>

    <div>

    [Plugin.GetPluginUserDataPath()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetPluginUserDataPath){.xref}

    </div>

    <div>

    [Plugin.GetPluginConfiguration\<TConfig\>()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetPluginConfiguration__1){.xref}

    </div>

    <div>

    [Plugin.LoadPluginSettings\<TSettings\>()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_LoadPluginSettings__1){.xref}

    </div>

    <div>

    [Plugin.SavePluginSettings\<TSettings\>(TSettings)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_SavePluginSettings__1___0_){.xref}

    </div>

    <div>

    [Plugin.OpenSettingsView()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_OpenSettingsView){.xref}

    </div>

    <div>

    [Plugin.GetPlayActions(GetPlayActionsArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetPlayActions_Playnite_SDK_Plugins_GetPlayActionsArgs_){.xref}

    </div>

    <div>

    [Plugin.GetInstallActions(GetInstallActionsArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetInstallActions_Playnite_SDK_Plugins_GetInstallActionsArgs_){.xref}

    </div>

    <div>

    [Plugin.GetUninstallActions(GetUninstallActionsArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetUninstallActions_Playnite_SDK_Plugins_GetUninstallActionsArgs_){.xref}

    </div>

    <div>

    [Plugin.GetGameViewControl(GetGameViewControlArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetGameViewControl_Playnite_SDK_Plugins_GetGameViewControlArgs_){.xref}

    </div>

    <div>

    [Plugin.AddCustomElementSupport(AddCustomElementSupportArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_AddCustomElementSupport_Playnite_SDK_Plugins_AddCustomElementSupportArgs_){.xref}

    </div>

    <div>

    [Plugin.AddSettingsSupport(AddSettingsSupportArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_AddSettingsSupport_Playnite_SDK_Plugins_AddSettingsSupportArgs_){.xref}

    </div>

    <div>

    [Plugin.AddConvertersSupport(AddConvertersSupportArgs)](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_AddConvertersSupport_Playnite_SDK_Plugins_AddConvertersSupportArgs_){.xref}

    </div>

    <div>

    [Plugin.GetSidebarItems()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetSidebarItems){.xref}

    </div>

    <div>

    [Plugin.GetTopPanelItems()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetTopPanelItems){.xref}

    </div>

    <div>

    [Plugin.GetSearchGlobalCommands()](Playnite.SDK.Plugins.Plugin.html#Playnite_SDK_Plugins_Plugin_GetSearchGlobalCommands){.xref}

    </div>

    <div>

    [object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring){.xref}

    </div>

    <div>

    [object.Equals(object)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals(system-object)){.xref}

    </div>

    <div>

    [object.Equals(object,
    object)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals(system-object-system-object)){.xref}

    </div>

    <div>

    [object.ReferenceEquals(object,
    object)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals){.xref}

    </div>

    <div>

    [object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode){.xref}

    </div>

    <div>

    [object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype){.xref}

    </div>

    <div>

    [object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone){.xref}

    </div>

## Constructors {#constructors .section}

[]{#Playnite_SDK_Plugins_MetadataPlugin__ctor_
uid="Playnite.SDK.Plugins.MetadataPlugin.#ctor*"}

### MetadataPlugin(IPlayniteAPI) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/Plugins/MetadataPlugin.cs/#L356 "View source"){.header-action .link-secondary} {#Playnite_SDK_Plugins_MetadataPlugin__ctor_Playnite_SDK_IPlayniteAPI_ uid="Playnite.SDK.Plugins.MetadataPlugin.#ctor(Playnite.SDK.IPlayniteAPI)"}

::: {.markdown .level1 .summary}
Creates new instance of
[MetadataPlugin](Playnite.SDK.Plugins.MetadataPlugin.html){.xref}.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
public MetadataPlugin(IPlayniteAPI playniteAPI)
```
:::

#### Parameters {#parameters .section}

`playniteAPI` [IPlayniteAPI](Playnite.SDK.IPlayniteAPI.html){.xref}

:   

## Properties {#properties .section}

[]{#Playnite_SDK_Plugins_MetadataPlugin_Name_
uid="Playnite.SDK.Plugins.MetadataPlugin.Name*"}

### Name [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/Plugins/MetadataPlugin.cs/#L345 "View source"){.header-action .link-secondary} {#Playnite_SDK_Plugins_MetadataPlugin_Name uid="Playnite.SDK.Plugins.MetadataPlugin.Name"}

::: {.markdown .level1 .summary}
Gets metadata source name.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
public abstract string Name { get; }
```
:::

#### Property Value {#property-value .section}

[string](https://learn.microsoft.com/dotnet/api/system.string){.xref}

:   

[]{#Playnite_SDK_Plugins_MetadataPlugin_Properties_
uid="Playnite.SDK.Plugins.MetadataPlugin.Properties*"}

### Properties [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/Plugins/MetadataPlugin.cs/#L340 "View source"){.header-action .link-secondary} {#Playnite_SDK_Plugins_MetadataPlugin_Properties uid="Playnite.SDK.Plugins.MetadataPlugin.Properties"}

::: {.markdown .level1 .summary}
Gets plugin\'s properties.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
public MetadataPluginProperties Properties { get; protected set; }
```
:::

#### Property Value {#property-value-1 .section}

[MetadataPluginProperties](Playnite.SDK.Plugins.MetadataPluginProperties.html){.xref}

:   

[]{#Playnite_SDK_Plugins_MetadataPlugin_SupportedFields_
uid="Playnite.SDK.Plugins.MetadataPlugin.SupportedFields*"}

### SupportedFields [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/Plugins/MetadataPlugin.cs/#L350 "View source"){.header-action .link-secondary} {#Playnite_SDK_Plugins_MetadataPlugin_SupportedFields uid="Playnite.SDK.Plugins.MetadataPlugin.SupportedFields"}

::: {.markdown .level1 .summary}
Gets list of game fields this metadata provider can provide.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
public abstract List<MetadataField> SupportedFields { get; }
```
:::

#### Property Value {#property-value-2 .section}

[List](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1){.xref}\<[MetadataField](Playnite.SDK.Plugins.MetadataField.html){.xref}\>

:   

## Methods {#methods .section}

[]{#Playnite_SDK_Plugins_MetadataPlugin_GetMetadataProvider_
uid="Playnite.SDK.Plugins.MetadataPlugin.GetMetadataProvider*"}

### GetMetadataProvider(MetadataRequestOptions) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/Plugins/MetadataPlugin.cs/#L365 "View source"){.header-action .link-secondary} {#Playnite_SDK_Plugins_MetadataPlugin_GetMetadataProvider_Playnite_SDK_Plugins_MetadataRequestOptions_ uid="Playnite.SDK.Plugins.MetadataPlugin.GetMetadataProvider(Playnite.SDK.Plugins.MetadataRequestOptions)"}

::: {.markdown .level1 .summary}
Gets metadata provider.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
public abstract OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
```
:::

#### Parameters {#parameters-1 .section}

`options` [MetadataRequestOptions](Playnite.SDK.Plugins.MetadataRequestOptions.html){.xref}

:   

#### Returns {#returns .section}

[OnDemandMetadataProvider](Playnite.SDK.Plugins.OnDemandMetadataProvider.html){.xref}

:   

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/Plugins/MetadataPlugin.cs/#L335){.edit-link}
:::
::::::::::::::::::::::::

::: affix
:::
::::::::::::::::::::::::::::::

::: {#search-results .container-xxl .search-results}
:::

:::: container-xxl
::: flex-fill
Made with [docfx](https://dotnet.github.io/docfx)
:::
::::
