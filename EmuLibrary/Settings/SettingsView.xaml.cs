using EmuLibrary.Util.AssetImporter;
using EmuLibrary.RomTypes;
using System;
using System.Diagnostics;
using System.IO;
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
                Settings.Instance.EmuLibrary.Logger.Info($"Updated mapping source path: ID={mapping.MappingId}, Type={mapping.RomType}, Path={path}");
                
                // For ISO installers, show a help message
                if (mapping.RomType == RomType.ISOInstaller)
                {
                    Settings.Instance.PlayniteAPI.Notifications.Add(
                        $"EmuLibrary-ISOInstaller-PathSet-{mapping.MappingId}",
                        $"ISO Installer source path set to: {path}. Make sure this folder contains your ISO files.",
                        Playnite.SDK.NotificationType.Info);
                }
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

                // When editing emulator, profile, or ROM type, we need to commit the edit
                // to ensure the UI is updated correctly, especially for platform selection
                if (e.Column.Header?.ToString() == "Emulator" || 
                    e.Column.Header?.ToString() == "Profile" || 
                    e.Column.Header?.ToString() == "Rom Type")
                {
                    // Commit the current edit
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                    
                    // When ROM type changes, we need to refresh the UI to show the correct platform options
                    if (e.Column.Header?.ToString() == "Rom Type" && e.Row.Item is EmulatorMapping mapping)
                    {
                        var logger = Settings.Instance.EmuLibrary.Logger;
                        logger.Info($"ROM type changed to {mapping.RomType}");
                        
                        // Force the platform to null if not one of the PC installer types
                        // This ensures the dropdown is populated correctly
                        if (mapping.RomType != RomType.PCInstaller && 
                            mapping.RomType != RomType.ISOInstaller)
                        {
                            mapping.Platform = null;
                        }
                        
                        // Force refresh the DataGrid
                        grid.Items.Refresh();
                    }
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
        
        // Helper button to quickly create an ISO installer mapping
        private void Click_AddIsoMapping(object sender, RoutedEventArgs e)
        {
            var logger = Settings.Instance.EmuLibrary.Logger;
            logger.Info("Creating a new ISO Installer mapping");
            
            // Select source folder
            string sourcePath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                logger.Warn("No source path selected, cancelling ISO mapping creation");
                return;
            }
            
            // Get default destination path (user's Documents folder)
            string destPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            destPath = Path.Combine(destPath, "InstalledGames");
            
            // Create the mapping
            var mapping = new EmulatorMapping()
            {
                MappingId = Guid.NewGuid(),
                RomType = RomType.ISOInstaller,
                SourcePath = sourcePath,
                DestinationPath = destPath,
                Enabled = true
            };
            
            // Find PC platform
            try
            {
                var pcPlatform = Settings.Instance.PlayniteAPI.Database.Platforms
                    .FirstOrDefault(p => p.Name == "PC");
                
                if (pcPlatform != null)
                {
                    mapping.PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString();
                    logger.Info($"Set platform to PC (ID: {mapping.PlatformId})");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error finding PC platform: {ex.Message}");
            }
            
            // Add to mappings
            Settings.Instance.Mappings.Add(mapping);
            logger.Info($"Added new ISO mapping: ID={mapping.MappingId}, Source={sourcePath}, Destination={destPath}");
            
            // Show notification
            Settings.Instance.PlayniteAPI.Notifications.Add(
                "EmuLibrary-ISOMapping-Created",
                $"Created new ISO Installer mapping for {sourcePath}. After closing settings, Playnite will scan for ISO files.",
                Playnite.SDK.NotificationType.Info);
                
            // Display a detailed help message
            Settings.Instance.PlayniteAPI.Dialogs.ShowMessage(
                "ISO Installer mapping has been created successfully.\n\n" +
                "Important tips for ISO scanning:\n" +
                "1. Make sure your ISO files are actual game installers (not disk images of installed games)\n" +
                "2. Supported formats: ISO, BIN, CUE, IMG, MDF, MDS\n" +
                "3. The scanner looks inside all subdirectories automatically\n" +
                "4. After finding games, they will appear in Playnite's Games list\n" +
                "5. Click 'Install ISO Game' from the right-click menu to install them\n\n" +
                "If games aren't appearing, check Playnite logs for more details.",
                "ISO Mapping Created");
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
        
        // The SteamGridDbApiKey button and handler have been removed
        // Now using Playnite's built-in metadata providers
    }
}