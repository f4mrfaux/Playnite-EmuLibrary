:::::: {.bg-body .border-bottom}
::::: {.container-xxl .flex-nowrap}
[![](../logo.svg){#logo .svg}](../index.html){.navbar-brand}

:::: {#navpanel .collapse .navbar-collapse}
::: {#navbar}
:::
::::
:::::
::::::

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::: {.container-xxl role="main"}
:::::: toc-offcanvas
::::: {#tocOffcanvas .offcanvas-md .offcanvas-start tabindex="-1" aria-labelledby="tocOffcanvasLabel"}
::: offcanvas-header
##### Table of Contents {#tocOffcanvasLabel .offcanvas-title}
:::

::: offcanvas-body
:::
:::::
::::::

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::: content
::: actionbar
:::

# Interface IPlayniteAPI [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L14 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI .text-break uid="Playnite.SDK.IPlayniteAPI"}

::: {.facts .text-secondary}

Namespace
:   [Playnite](Playnite.html){.xref}.[SDK](Playnite.SDK.html){.xref}

```{=html}
<!-- -->
```

Assembly
:   Playnite.SDK.dll
:::

::: {.markdown .summary}
Describes object providing Playnite API.
:::

::: {.markdown .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
public interface IPlayniteAPI
```
:::

## Properties {#properties .section}

[]{#Playnite_SDK_IPlayniteAPI_Addons_
uid="Playnite.SDK.IPlayniteAPI.Addons*"}

### Addons [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L69 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_Addons uid="Playnite.SDK.IPlayniteAPI.Addons"}

::: {.markdown .level1 .summary}
Gets addons API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IAddons Addons { get; }
```
:::

#### Property Value {#property-value .section}

[IAddons](Playnite.SDK.IAddons.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_ApplicationInfo_
uid="Playnite.SDK.IPlayniteAPI.ApplicationInfo*"}

### ApplicationInfo [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L44 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_ApplicationInfo uid="Playnite.SDK.IPlayniteAPI.ApplicationInfo"}

::: {.markdown .level1 .summary}
Gets application info API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IPlayniteInfoAPI ApplicationInfo { get; }
```
:::

#### Property Value {#property-value-1 .section}

[IPlayniteInfoAPI](Playnite.SDK.IPlayniteInfoAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_ApplicationSettings_
uid="Playnite.SDK.IPlayniteAPI.ApplicationSettings*"}

### ApplicationSettings [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L64 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_ApplicationSettings uid="Playnite.SDK.IPlayniteAPI.ApplicationSettings"}

::: {.markdown .level1 .summary}
Get application settings API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IPlayniteSettingsAPI ApplicationSettings { get; }
```
:::

#### Property Value {#property-value-2 .section}

[IPlayniteSettingsAPI](Playnite.SDK.IPlayniteSettingsAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_Database_
uid="Playnite.SDK.IPlayniteAPI.Database*"}

### Database [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L24 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_Database uid="Playnite.SDK.IPlayniteAPI.Database"}

::: {.markdown .level1 .summary}
Gets database API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IGameDatabaseAPI Database { get; }
```
:::

#### Property Value {#property-value-3 .section}

[IGameDatabaseAPI](Playnite.SDK.IGameDatabaseAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_Dialogs_
uid="Playnite.SDK.IPlayniteAPI.Dialogs*"}

### Dialogs [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L29 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_Dialogs uid="Playnite.SDK.IPlayniteAPI.Dialogs"}

::: {.markdown .level1 .summary}
Gets dialog API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IDialogsFactory Dialogs { get; }
```
:::

#### Property Value {#property-value-4 .section}

[IDialogsFactory](Playnite.SDK.IDialogsFactory.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_Emulation_
uid="Playnite.SDK.IPlayniteAPI.Emulation*"}

### Emulation [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L74 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_Emulation uid="Playnite.SDK.IPlayniteAPI.Emulation"}

::: {.markdown .level1 .summary}
Gets emulation API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IEmulationAPI Emulation { get; }
```
:::

#### Property Value {#property-value-5 .section}

[IEmulationAPI](Playnite.SDK.IEmulationAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_MainView_
uid="Playnite.SDK.IPlayniteAPI.MainView*"}

### MainView [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L19 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_MainView uid="Playnite.SDK.IPlayniteAPI.MainView"}

::: {.markdown .level1 .summary}
Gets main view API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IMainViewAPI MainView { get; }
```
:::

#### Property Value {#property-value-6 .section}

[IMainViewAPI](Playnite.SDK.IMainViewAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_Notifications_
uid="Playnite.SDK.IPlayniteAPI.Notifications*"}

### Notifications [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L39 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_Notifications uid="Playnite.SDK.IPlayniteAPI.Notifications"}

::: {.markdown .level1 .summary}
Gets notification API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
INotificationsAPI Notifications { get; }
```
:::

#### Property Value {#property-value-7 .section}

[INotificationsAPI](Playnite.SDK.INotificationsAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_Paths_
uid="Playnite.SDK.IPlayniteAPI.Paths*"}

### Paths [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L34 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_Paths uid="Playnite.SDK.IPlayniteAPI.Paths"}

::: {.markdown .level1 .summary}
Gets paths API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IPlaynitePathsAPI Paths { get; }
```
:::

#### Property Value {#property-value-8 .section}

[IPlaynitePathsAPI](Playnite.SDK.IPlaynitePathsAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_Resources_
uid="Playnite.SDK.IPlayniteAPI.Resources*"}

### Resources [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L54 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_Resources uid="Playnite.SDK.IPlayniteAPI.Resources"}

::: {.markdown .level1 .summary}
Gets resources API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IResourceProvider Resources { get; }
```
:::

#### Property Value {#property-value-9 .section}

[IResourceProvider](Playnite.SDK.IResourceProvider.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_UriHandler_
uid="Playnite.SDK.IPlayniteAPI.UriHandler*"}

### UriHandler [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L59 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_UriHandler uid="Playnite.SDK.IPlayniteAPI.UriHandler"}

::: {.markdown .level1 .summary}
Gets URI handler API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IUriHandlerAPI UriHandler { get; }
```
:::

#### Property Value {#property-value-10 .section}

[IUriHandlerAPI](Playnite.SDK.IUriHandlerAPI.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_WebViews_
uid="Playnite.SDK.IPlayniteAPI.WebViews*"}

### WebViews [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L49 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_WebViews uid="Playnite.SDK.IPlayniteAPI.WebViews"}

::: {.markdown .level1 .summary}
Gets web view API.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
IWebViewFactory WebViews { get; }
```
:::

#### Property Value {#property-value-11 .section}

[IWebViewFactory](Playnite.SDK.IWebViewFactory.html){.xref}

:   

## Methods {#methods .section}

[]{#Playnite_SDK_IPlayniteAPI_AddConvertersSupport_
uid="Playnite.SDK.IPlayniteAPI.AddConvertersSupport*"}

### AddConvertersSupport(Plugin, AddConvertersSupportArgs) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L138 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_AddConvertersSupport_Playnite_SDK_Plugins_Plugin_Playnite_SDK_Plugins_AddConvertersSupportArgs_ uid="Playnite.SDK.IPlayniteAPI.AddConvertersSupport(Playnite.SDK.Plugins.Plugin,Playnite.SDK.Plugins.AddConvertersSupportArgs)"}

::: {.markdown .level1 .summary}
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args)
```
:::

#### Parameters {#parameters .section}

`source` [Plugin](Playnite.SDK.Plugins.Plugin.html){.xref}

:   

`args` [AddConvertersSupportArgs](Playnite.SDK.Plugins.AddConvertersSupportArgs.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_AddCustomElementSupport_
uid="Playnite.SDK.IPlayniteAPI.AddCustomElementSupport*"}

### AddCustomElementSupport(Plugin, AddCustomElementSupportArgs) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L124 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_AddCustomElementSupport_Playnite_SDK_Plugins_Plugin_Playnite_SDK_Plugins_AddCustomElementSupportArgs_ uid="Playnite.SDK.IPlayniteAPI.AddCustomElementSupport(Playnite.SDK.Plugins.Plugin,Playnite.SDK.Plugins.AddCustomElementSupportArgs)"}

::: {.markdown .level1 .summary}
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
void AddCustomElementSupport(Plugin source, AddCustomElementSupportArgs args)
```
:::

#### Parameters {#parameters-1 .section}

`source` [Plugin](Playnite.SDK.Plugins.Plugin.html){.xref}

:   

`args` [AddCustomElementSupportArgs](Playnite.SDK.Plugins.AddCustomElementSupportArgs.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_AddSettingsSupport_
uid="Playnite.SDK.IPlayniteAPI.AddSettingsSupport*"}

### AddSettingsSupport(Plugin, AddSettingsSupportArgs) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L131 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_AddSettingsSupport_Playnite_SDK_Plugins_Plugin_Playnite_SDK_Plugins_AddSettingsSupportArgs_ uid="Playnite.SDK.IPlayniteAPI.AddSettingsSupport(Playnite.SDK.Plugins.Plugin,Playnite.SDK.Plugins.AddSettingsSupportArgs)"}

::: {.markdown .level1 .summary}
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
void AddSettingsSupport(Plugin source, AddSettingsSupportArgs args)
```
:::

#### Parameters {#parameters-2 .section}

`source` [Plugin](Playnite.SDK.Plugins.Plugin.html){.xref}

:   

`args` [AddSettingsSupportArgs](Playnite.SDK.Plugins.AddSettingsSupportArgs.html){.xref}

:   

[]{#Playnite_SDK_IPlayniteAPI_ExpandGameVariables_
uid="Playnite.SDK.IPlayniteAPI.ExpandGameVariables*"}

### ExpandGameVariables(Game, GameAction) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L99 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_ExpandGameVariables_Playnite_SDK_Models_Game_Playnite_SDK_Models_GameAction_ uid="Playnite.SDK.IPlayniteAPI.ExpandGameVariables(Playnite.SDK.Models.Game,Playnite.SDK.Models.GameAction)"}

::: {.markdown .level1 .summary}
Expands dynamic game variables in specified game action.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
GameAction ExpandGameVariables(Game game, GameAction action)
```
:::

#### Parameters {#parameters-3 .section}

`game` [Game](Playnite.SDK.Models.Game.html){.xref}

:   Game to use dynamic variables from.

`action` [GameAction](Playnite.SDK.Models.GameAction.html){.xref}

:   Game action to expand variables to.

#### Returns {#returns .section}

[GameAction](Playnite.SDK.Models.GameAction.html){.xref}

:   Game action with expanded variables.

[]{#Playnite_SDK_IPlayniteAPI_ExpandGameVariables_
uid="Playnite.SDK.IPlayniteAPI.ExpandGameVariables*"}

### ExpandGameVariables(Game, string) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L82 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_ExpandGameVariables_Playnite_SDK_Models_Game_System_String_ uid="Playnite.SDK.IPlayniteAPI.ExpandGameVariables(Playnite.SDK.Models.Game,System.String)"}

::: {.markdown .level1 .summary}
Expands dynamic game variables in specified string.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
string ExpandGameVariables(Game game, string inputString)
```
:::

#### Parameters {#parameters-4 .section}

`game` [Game](Playnite.SDK.Models.Game.html){.xref}

:   Game to use dynamic variables from.

`inputString` [string](https://learn.microsoft.com/dotnet/api/system.string){.xref}

:   String containing dynamic variables.

#### Returns {#returns-1 .section}

[string](https://learn.microsoft.com/dotnet/api/system.string){.xref}

:   String with replaces variables.

[]{#Playnite_SDK_IPlayniteAPI_ExpandGameVariables_
uid="Playnite.SDK.IPlayniteAPI.ExpandGameVariables*"}

### ExpandGameVariables(Game, string, string) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L91 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_ExpandGameVariables_Playnite_SDK_Models_Game_System_String_System_String_ uid="Playnite.SDK.IPlayniteAPI.ExpandGameVariables(Playnite.SDK.Models.Game,System.String,System.String)"}

::: {.markdown .level1 .summary}
Expands dynamic game variables in specified string.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
string ExpandGameVariables(Game game, string inputString, string emulatorDir)
```
:::

#### Parameters {#parameters-5 .section}

`game` [Game](Playnite.SDK.Models.Game.html){.xref}

:   Game to use dynamic variables from.

`inputString` [string](https://learn.microsoft.com/dotnet/api/system.string){.xref}

:   String containing dynamic variables.

`emulatorDir` [string](https://learn.microsoft.com/dotnet/api/system.string){.xref}

:   String to be used to expand {EmulatorDir} variable if present.

#### Returns {#returns-2 .section}

[string](https://learn.microsoft.com/dotnet/api/system.string){.xref}

:   String with replaces variables.

[]{#Playnite_SDK_IPlayniteAPI_InstallGame_
uid="Playnite.SDK.IPlayniteAPI.InstallGame*"}

### InstallGame(Guid) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L111 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_InstallGame_System_Guid_ uid="Playnite.SDK.IPlayniteAPI.InstallGame(System.Guid)"}

::: {.markdown .level1 .summary}
Installs game.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
void InstallGame(Guid gameId)
```
:::

#### Parameters {#parameters-6 .section}

`gameId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid){.xref}

:   Game\'s database ID.

[]{#Playnite_SDK_IPlayniteAPI_StartGame_
uid="Playnite.SDK.IPlayniteAPI.StartGame*"}

### StartGame(Guid) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L105 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_StartGame_System_Guid_ uid="Playnite.SDK.IPlayniteAPI.StartGame(System.Guid)"}

::: {.markdown .level1 .summary}
Starts game.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
void StartGame(Guid gameId)
```
:::

#### Parameters {#parameters-7 .section}

`gameId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid){.xref}

:   Game\'s database ID.

[]{#Playnite_SDK_IPlayniteAPI_UninstallGame_
uid="Playnite.SDK.IPlayniteAPI.UninstallGame*"}

### UninstallGame(Guid) [](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L117 "View source"){.header-action .link-secondary} {#Playnite_SDK_IPlayniteAPI_UninstallGame_System_Guid_ uid="Playnite.SDK.IPlayniteAPI.UninstallGame(System.Guid)"}

::: {.markdown .level1 .summary}
Uninstalls game.
:::

::: {.markdown .level1 .conceptual}
:::

::: codewrapper
``` {.lang-csharp .hljs}
void UninstallGame(Guid gameId)
```
:::

#### Parameters {#parameters-8 .section}

`gameId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid){.xref}

:   Game\'s database ID.

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/Playnite/blob/main/source/PlayniteSDK/IPlayniteAPI.cs/#L14){.edit-link}
:::
::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

::: affix
:::
::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

::: {#search-results .container-xxl .search-results}
:::

:::: container-xxl
::: flex-fill
Made with [docfx](https://dotnet.github.io/docfx)
:::
::::
