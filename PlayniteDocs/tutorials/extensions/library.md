:::::: {.bg-body .border-bottom}
::::: {.container-xxl .flex-nowrap}
[![](../../logo.svg){#logo .svg}](../../index.html){.navbar-brand}

:::: {#navpanel .collapse .navbar-collapse}
::: {#navbar}
:::
::::
:::::
::::::

:::::::::::::::::::::::::::::::::::: {.container-xxl role="main"}
:::::: toc-offcanvas
::::: {#tocOffcanvas .offcanvas-md .offcanvas-start tabindex="-1" aria-labelledby="tocOffcanvasLabel"}
::: offcanvas-header
##### Table of Contents {#tocOffcanvasLabel .offcanvas-title}
:::

::: offcanvas-body
:::
:::::
::::::

:::::::::::::::::::::::::::::: content
::: actionbar
:::

# Working with game library

## Introduction

Game database API allows you to access and modify game library and its
objects (including platforms and emulators). Database API can be
accessed via `PlayniteAPI.Database` property, which provides
[IDatabaseAPI](../../api/Playnite.SDK.IGameDatabaseAPI.html){.xref}
interface.

## Handling Games

### Getting Games

To get list of all games in library use
[Database](../../api/Playnite.SDK.IPlayniteAPI.html#Playnite_SDK_IPlayniteAPI_Database){.xref}
property from `IPlayniteAPI` and
[Games](../../api/Playnite.SDK.IGameDatabase.html#Playnite_SDK_IGameDatabase_Games){.xref}
collection.

::::: {#tabgroup_1 .tabGroup}
-   [C#](#tabpanel_1_csharp){role="tab"
    aria-controls="tabpanel_1_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_1_tabpowershell){role="tab"
    aria-controls="tabpanel_1_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_1_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
foreach (var game in PlayniteApi.Database.Games)
{
    // Do stuff with a game
}

// Get a game with known Id
var game = PlayniteApi.Database.Games[SomeGuidId];
```
:::

::: {#tabpanel_1_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
# Get all games
$games = $PlayniteApi.Database.Games
# Get a game with known Id
$game = $PlayniteApi.Database.Games[$SomeGuidId]
```
:::
:::::

### Adding New Game

To add a new game create new instance of
[Game](../../api/Playnite.SDK.Models.Game.html){.xref} class and call
`Add` method from
[Games](../../api/Playnite.SDK.IGameDatabase.html#Playnite_SDK_IGameDatabase_Games){.xref}
collection.

::::: {#tabgroup_2 .tabGroup}
-   [C#](#tabpanel_2_csharp){role="tab"
    aria-controls="tabpanel_2_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_2_tabpowershell){role="tab"
    aria-controls="tabpanel_2_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_2_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
var newGame = new Game("New Game");
PlayniteApi.Database.Games.Add(newGame);
```
:::

::: {#tabpanel_2_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
$newGame = New-Object "Playnite.SDK.Models.Game"
$newGame.Name = "New Game"
$PlayniteApi.Database.Games.Add($newGame)
```
:::
:::::

### Changing Game Data

Changing properties on a `Game` object doesn\'t automatically update the
game in Playnite\'s database and changes are lost with application
restart. To make permanent changes game object must be updated in
database manually using `Update` method from
[Games](../../api/Playnite.SDK.IGameDatabase.html#Playnite_SDK_IGameDatabase_Games){.xref}
collection.

::::: {#tabgroup_3 .tabGroup}
-   [C#](#tabpanel_3_csharp){role="tab"
    aria-controls="tabpanel_3_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_3_tabpowershell){role="tab"
    aria-controls="tabpanel_3_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_3_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
var game = PlayniteApi.Database.Games[SomeId];
game.Name = "Changed Name";
PlayniteApi.Database.Games.Update(game);
```
:::

::: {#tabpanel_3_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
$game = $PlayniteApi.Database.Games[$SomeId]
$game.Name = "Changed Name"
$PlayniteApi.Database.Games.Update($game)
```
:::
:::::

### Removing Games

To remove game from database use `Remove` method from
[Games](../../api/Playnite.SDK.IGameDatabase.html#Playnite_SDK_IGameDatabase_Games){.xref}
collection.

::::: {#tabgroup_4 .tabGroup}
-   [C#](#tabpanel_4_csharp){role="tab"
    aria-controls="tabpanel_4_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_4_tabpowershell){role="tab"
    aria-controls="tabpanel_4_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_4_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
PlayniteApi.Database.Games.Remove(SomeId);
```
:::

::: {#tabpanel_4_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
$PlayniteApi.Database.Games.Remove($SomeId)
```
:::
:::::

### Bulk updates

Every collection change operation (update, add, remove) generates an
event that is sent to all plugins and other parts of Playnite. Therefore
it\'s highly recommend to bulk as much changes as possible together. You
can use buffer updates which collect all changes and only generate
single event once you commit/end update operation.

::::: {#tabgroup_5 .tabGroup}
-   [C#](#tabpanel_5_csharp){role="tab"
    aria-controls="tabpanel_5_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_5_tabpowershell){role="tab"
    aria-controls="tabpanel_5_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_5_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
using (PlayniteApi.Database.BufferedUpdate())
{
    // Any collection changes here don't generate any events
}
// Single event is sent here for all changes made in previous using block
```
:::

::: {#tabpanel_5_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
$PlayniteApi.Database.BeginBufferUpdate()
try
{
     # Any collection changes here don't generate any events
}
finally
{
    $PlayniteApi.Database.EndBufferUpdate()
    # Single event is sent here for all changes made in previous try block
}
```
:::
:::::

## Handling reference fields

Some fields are only stored as references in `Game` object and can\'t be
directly updated. For example `Series` field is a reference via
`SeriesId` property, you can\'t directly assign new value to `Series`
property (it can be only used to obtain referenced series object).

### Updating references

Every reference field has it\'s own collection accessible in
[Database](../../api/Playnite.SDK.IPlayniteAPI.html#Playnite_SDK_IPlayniteAPI_Database){.xref}
API object. For example all series can be accessed via
[Series](../../api/Playnite.SDK.IGameDatabase.html#Playnite_SDK_IGameDatabase_Series){.xref}
collection.

If you want to change name of the series then you will need to do it by
updating series item from
[Series](../../api/Playnite.SDK.IGameDatabase.html#Playnite_SDK_IGameDatabase_Series){.xref}
collection. The change will be automatically propagated to all games
using that series. All field collections are implemented via
[IItemCollection](../../api/Playnite.SDK.IItemCollection-1.html){.xref}
meaning that the update is done via the same process like updating
general game information via `Update` method on the specific collection.

### Adding references

To assign completely new series to a game:

-   Add new series into
    [Series](../../api/Playnite.SDK.IGameDatabase.html#Playnite_SDK_IGameDatabase_Series){.xref}
    database collection.
-   Assign ID of newly added series to the game via `SeriesId` property.
-   Call Update on `Games` collection to update new `SeriesId` in
    database.

Some fields allow you to assign more items to a game. For example you
can assign multiple tags to a game. In that case you need to assign
[List](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1)
of IDs to `TagIds` property.

## Handling data changes

Collections from
[IDatabaseAPI](../../api/Playnite.SDK.IGameDatabaseAPI.html){.xref}
provide `ItemCollectionChanged` and `ItemUpdated` events which you can
use to react to data changes in the game library.

::::: {#tabgroup_6 .tabGroup}
-   [C#](#tabpanel_6_csharp){role="tab"
    aria-controls="tabpanel_6_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_6_tabpowershell){role="tab"
    aria-controls="tabpanel_6_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_6_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
PlayniteApi.Database.Games.ItemCollectionChanged += (_, args) =>
{
    PlayniteApi.Dialogs.ShowMessage(args.AddedItems.Count + " items added into the library.");
};
```
:::

::: {#tabpanel_6_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
Register-ObjectEvent -InputObject $PlayniteApi.Database.Games -EventName ItemCollectionChanged -Action {
    $PlayniteApi.Dialogs.ShowMessage("$($EventArgs.AddedItems.Count) items added into the library.");
}
```
:::
:::::

If you want to specifically execute code after game library update
(automatic one on startup or manual one from main menu), use
[OnLibraryUpdated](events.html) plugin event. `OnLibraryUpdated` is
called after all library plugins finish with their game library updates.

## Handling Files

All game related image files are stored in game database itself, with
only reference Id being used on the game object itself (with exception
of
[BackgroundImage](../../api/Playnite.SDK.Models.Game.html#Playnite_SDK_Models_Game_BackgroundImage){.xref},
which allows use of WEB URL as well). Following examples show how to
handle game images using database API.

### Exporting Game Cover

Game cover images are referenced in
[CoverImage](../../api/Playnite.SDK.Models.Game.html#Playnite_SDK_Models_Game_CoverImage){.xref}
property. To save a file first get the file record by calling
[GetFullFilePath](../../api/Playnite.SDK.IGameDatabaseAPI.html#Playnite_SDK_IGameDatabaseAPI_GetFullFilePath_System_String_){.xref}
method. `GetFullFilePath` returns full path to a file on the disk drive.

::::: {#tabgroup_7 .tabGroup}
-   [C#](#tabpanel_7_csharp){role="tab"
    aria-controls="tabpanel_7_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_7_tabpowershell){role="tab"
    aria-controls="tabpanel_7_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_7_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
var game = PlayniteApi.Database.Games[SomeId];
var coverPath = PlayniteApi.Database.GetFullFilePath(game.CoverImage);
```
:::

::: {#tabpanel_7_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
$game = $PlayniteApi.Database.Games[$SomeId]
$coverPath = $PlayniteApi.Database.GetFullFilePath($game.CoverImage)
```
:::
:::::

### Changing Cover Image

Changing cover image involves several steps. First remove original image
by calling
[RemoveFile](../../api/Playnite.SDK.IGameDatabaseAPI.html#Playnite_SDK_IGameDatabaseAPI_RemoveFile_System_String_){.xref}
method. Then add new image file to a database using
[AddFile](../../api/Playnite.SDK.IGameDatabaseAPI.html#Playnite_SDK_IGameDatabaseAPI_AddFile_System_String_System_Guid_){.xref}.
And lastly assign Id of new image to a game.

Following example changes cover image of first game in database:

::::: {#tabgroup_8 .tabGroup}
-   [C#](#tabpanel_8_csharp){role="tab"
    aria-controls="tabpanel_8_csharp" tab="csharp" tabindex="0"
    aria-selected="true"}
-   [PowerShell](#tabpanel_8_tabpowershell){role="tab"
    aria-controls="tabpanel_8_tabpowershell" tab="tabpowershell"
    tabindex="-1"}

::: {#tabpanel_8_csharp .section role="tabpanel" tab="csharp"}
``` lang-csharp
var game = PlayniteApi.Database.Games[SomeId];
PlayniteApi.Database.RemoveFile(game.CoverImage);
game.CoverImage = PlayniteApi.Database.AddFile(@"c:\file.png", game.Id);
PlayniteApi.Database.Games.Update(game);
```
:::

::: {#tabpanel_8_tabpowershell .section role="tabpanel" tab="tabpowershell" aria-hidden="true" hidden="hidden"}
``` lang-powershell
$game = $PlayniteApi.Database.Games[SomeId]
$PlayniteApi.Database.RemoveFile($game.CoverImage)
$game.CoverImage = $PlayniteApi.Database.AddFile("c:\file.png", $game.Id)
$PlayniteApi.Database.Games.Update($game)
```
:::
:::::

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/PlayniteDocs/blob/main/docs/tutorials/extensions/library.md/#L1){.edit-link}
:::

::: {#nextArticle .next-article .d-print-none .border-top}
:::
::::::::::::::::::::::::::::::

::: affix
:::
::::::::::::::::::::::::::::::::::::

::: {#search-results .container-xxl .search-results}
:::

:::: container-xxl
::: flex-fill
Made with [docfx](https://dotnet.github.io/docfx)
:::
::::
