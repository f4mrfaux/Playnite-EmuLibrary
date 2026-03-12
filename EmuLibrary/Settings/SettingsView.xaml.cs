using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EmuLibrary.RomTypes;

namespace EmuLibrary.Settings
{
    public partial class SettingsView : UserControl
    {
        private bool InManualCellCommit = false;

        private Settings PluginSettings => DataContext as Settings;

        public SettingsView()
        {
            InitializeComponent();
            
            FilteredRomTypes = System.Enum.GetValues(typeof(RomType)).Cast<RomType>().ToList();
        }

        public List<RomType> FilteredRomTypes { get; private set; }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            if (PluginSettings == null) return;
            if (((FrameworkElement)sender).DataContext is EmulatorMapping mapping)
            {
                var res = PluginSettings.PlayniteAPI.Dialogs.ShowMessage(string.Format("Delete this mapping?\r\n\r\n{0}", mapping.GetDescriptionLines().Aggregate((a, b) => $"{a}{Environment.NewLine}{b}")), "Confirm delete", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    PluginSettings.Mappings.Remove(mapping);
                }
            }
        }

        private void Click_BrowseSource(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            if (mapping == null) return;
            string path;
            // Use current source path as initial directory if it exists, otherwise use Documents
            var initialDir = GetInitialDirectory(mapping.SourcePath);
            if ((path = GetSelectedFolderPath(initialDir)) != null)
            {
                mapping.SourcePath = path;
            }
        }

        private void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            if (mapping == null) return;
            string path;
            // Use current destination path as initial directory if it exists, otherwise use Documents
            var initialDir = GetInitialDirectory(mapping.DestinationPathResolved);
            if ((path = GetSelectedFolderPath(initialDir)) != null)
            {
                var playnite = PluginSettings.PlayniteAPI;
                if (playnite.Paths.IsPortable)
                {
                    path = path.Replace(playnite.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory);
                }

                mapping.DestinationPath = path;
            }
        }

        /// <summary>
        /// Gets an appropriate initial directory for folder selection dialogs
        /// </summary>
        /// <param name="currentPath">Current path value, if any</param>
        /// <returns>Initial directory to use in SelectFolder dialog</returns>
        private static string GetInitialDirectory(string currentPath)
        {
            // If current path exists and is valid, use it
            if (!string.IsNullOrEmpty(currentPath) && System.IO.Directory.Exists(currentPath))
            {
                return currentPath;
            }

            // Fall back to Documents folder
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private string GetSelectedFolderPath(string initialDirectory = null)
        {
            if (PluginSettings == null) return null;
            // Use the new SDK 6.13+ overload with initial directory if provided
            if (!string.IsNullOrEmpty(initialDirectory))
            {
                return PluginSettings.PlayniteAPI.Dialogs.SelectFolder(initialDirectory);
            }

            return PluginSettings.PlayniteAPI.Dialogs.SelectFolder();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!InManualCellCommit && sender is DataGrid grid)
            {
                InManualCellCommit = true;

                // Use declaration-order index (Columns[n]) not DisplayIndex, which can shift if columns are reordered
                // Column order: Delete=0, Emulator=1, Profile=2, Platform=3, RomType=4, Source=5, Destination=6, Enabled=7
                var col = e.Column;
                if (grid.Columns.Count > 2 && (col == grid.Columns[1] || col == grid.Columns[2])) // Emulator or Profile columns
                {
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                }

                InManualCellCommit = false;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }
}