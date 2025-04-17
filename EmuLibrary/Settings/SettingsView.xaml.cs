using EmuLibrary.Util.AssetImporter;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EmuLibrary.Settings
{
    public partial class SettingsView : UserControl
    {
        private bool InManualCellCommit = false;

        public SettingsView()
        {
            InitializeComponent();
        }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is EmulatorMapping mapping)
            {
                var res = Settings.Instance.PlayniteAPI.Dialogs.ShowMessage(string.Format("Delete this mapping?\r\n\r\n{0}", mapping.GetDescriptionLines().Aggregate((a, b) => $"{a}{Environment.NewLine}{b}")), "Confirm delete", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    Settings.Instance.Mappings.Remove(mapping);
                }
            }
        }

        private void Click_BrowseSource(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) != null)
            {
                mapping.SourcePath = path;
            }
        }

        private void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) != null)
            {
                var playnite = Settings.Instance.PlayniteAPI;
                if (playnite.Paths.IsPortable)
                {
                    path = path.Replace(playnite.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory);
                }

                mapping.DestinationPath = path;
            }
        }

        private static string GetSelectedFolderPath()
        {
            return Settings.Instance.PlayniteAPI.Dialogs.SelectFolder();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!InManualCellCommit && sender is DataGrid grid)
            {
                InManualCellCommit = true;

                // HACK!!!!
                // Alternate approach 1: try to find new value here and store that somewhere as the currently selected emu
                // Alternate approach 2: the "right" way(?) https://stackoverflow.com/a/34332709
                if (e.Column.Header?.ToString() == "Emulator" || e.Column.Header?.ToString() == "Profile")
                {
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                }

                InManualCellCommit = false;
            }
        }

        private void DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
        
        private void Click_ClearAssetCache(object sender, RoutedEventArgs e)
        {
            var result = Settings.Instance.PlayniteAPI.Dialogs.ShowMessage(
                "Are you sure you want to clear the asset cache? This will delete all cached asset files.",
                "Clear Asset Cache",
                MessageBoxButton.YesNo);
                
            if (result == MessageBoxResult.Yes)
            {
                // Get or create AssetImporter instance
                var assetImporter = AssetImporter.Instance ?? 
                    new AssetImporter(Settings.Instance.EmuLibrary.Logger, Settings.Instance.PlayniteAPI);
                
                assetImporter.ClearCache();
                
                Settings.Instance.PlayniteAPI.Dialogs.ShowMessage(
                    "Asset cache has been cleared.",
                    "Cache Cleared",
                    MessageBoxButton.OK);
            }
        }
        
        private void Click_ViewCacheInfo(object sender, RoutedEventArgs e)
        {
            // Get or create AssetImporter instance
            var assetImporter = AssetImporter.Instance ?? 
                new AssetImporter(Settings.Instance.EmuLibrary.Logger, Settings.Instance.PlayniteAPI);
            
            var cacheInfo = assetImporter.GetCacheInfo();
            
            // Format the cache size in a readable form (MB/GB)
            string sizeText;
            if (cacheInfo.TotalSize < 1024 * 1024)
            {
                sizeText = $"{cacheInfo.TotalSize / 1024.0:F2} KB";
            }
            else if (cacheInfo.TotalSize < 1024 * 1024 * 1024)
            {
                sizeText = $"{cacheInfo.TotalSize / (1024.0 * 1024):F2} MB";
            }
            else
            {
                sizeText = $"{cacheInfo.TotalSize / (1024.0 * 1024 * 1024):F2} GB";
            }
            
            Settings.Instance.PlayniteAPI.Dialogs.ShowMessage(
                $"Asset Cache Information:\n\n" +
                $"Total Size: {sizeText}\n" +
                $"Items Cached: {cacheInfo.ItemCount}",
                "Cache Information",
                MessageBoxButton.OK);
        }
    }
}