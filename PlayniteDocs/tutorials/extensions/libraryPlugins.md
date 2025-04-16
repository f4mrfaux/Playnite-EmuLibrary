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

# Library Plugins

To implement library plugin:

-   Read the introduction to [extensions](intro.html) and
    [plugins](plugins.html).
-   Create new public class inheriting from
    [LibraryPlugin](../../api/Playnite.SDK.Plugins.LibraryPlugin.html){.xref}
    abstract class.
-   Add implementation for mandatory abstract members.

## Mandatory members

  Member     Description
  ---------- ------------------------------------
  Id         Unique plugin id.
  Name       Library name.
  GetGames   Return games available in library.

`GetGames` returns list of
[Game](../../api/Playnite.SDK.Models.Game.html){.xref} objects and these
properties must be set correctly by the plugin in order for game to be
imported properly:

  Member             Description
  ------------------ -------------------------------------------------------------------------------------------------
  GameId             Unique identifier used to differentiate games of the same plugin.
  PluginId           Source Id of the plugin importing game.
  PlayAction         Game action used to start the game. Only if game is reported as installed via `State` property.
  InstallDirectory   Installation location. Only if game is reported as installed via `State` property.

You can implement additional functionality by overriding virtual methods
from
[LibraryPlugin](../../api/Playnite.SDK.Plugins.LibraryPlugin.html){.xref}
base class.

## Capabilities

If you want to provide extra features for specific library integration,
like ability to close third party client after the game is close, then
implement `Properties` property on your plugin class that represents
[LibraryPluginProperties](../../api/Playnite.SDK.Plugins.LibraryPluginProperties.html){.xref}.

### Supported properties

  Capability                Description
  ------------------------- -----------------------------------------------------------------------------------------------------------------------------------------------------------------
  CanShutdownClient         When supported, library\'s client object has to implement `Shutdown` method.
  HasCustomizedGameImport   Specifies that library is in full control over the game import mechanism. In this case the library should implement `ImportGames` method instead of `GetGames`.

## Example plugin

``` lang-csharp
public class LibraryPlugin : LibraryPlugin
{
    public override Guid Id { get; } = Guid.Parse("D625A3B7-1AA4-41CB-9CD7-74448D28E99B");

    public override string Name { get; } = "Test Library";

    public TestGameLibrary(IPlayniteAPI api) : base (api)
    {
        Properties = new LibraryPluginProperties
        {
            CanShutdownClient = true,
            HasSettings = true
        };
    }

    public override IEnumerable<GameMetadata> GetGames()
    {
        return new List<GameMetadata>()
        {
            new GameMetadata()
            {
                Name = "Some App",
                GameId = "some_app_id",
                GameActions = new List<GameAction>
                {
                    new GameAction
                    {
                        Type = GameActionType.File,
                        Path = "c:\some_path\app.exe",
                        IsPlayAction = true
                    }
                },
                IsInstalled = true,
                Icon = new MetadataFile(@"c:\some_path\app.exe")
            },
            new GameMetadata()
            {
                Name = "Calculator",
                GameId = "calc",
                GameActions = new List<GameAction>
                {
                    new GameAction
                    {
                        Type = GameActionType.File,
                        Path = "calc.exe",
                        IsPlayAction = true
                    }
                },
                IsInstalled = true,
                Icon = new MetadataFile(@"https://playnite.link/applogo.png"),
                BackgroundImage =  new MetadataFile(@"https://playnite.link/applogo.png")
            }
        };
    }
}
```

::: {.contribution .d-print-none}
[Edit this
page](https://github.com/JosefNemec/PlayniteDocs/blob/main/docs/tutorials/extensions/libraryPlugins.md/#L1){.edit-link}
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
