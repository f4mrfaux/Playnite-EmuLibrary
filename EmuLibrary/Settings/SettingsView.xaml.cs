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

        public SettingsView()
        {
            InitializeComponent();
            
            // Create filtered RomType list (exclude ISOInstaller - now handled by PCInstaller)
            var allRomTypes = System.Enum.GetValues(typeof(RomType)).Cast<RomType>();
            FilteredRomTypes = allRomTypes.Where(rt => rt != RomType.ISOInstaller).ToList();
        }
        
        /// <summary>
        /// RomType values filtered for UI display (excludes ISOInstaller which is now handled by PCInstaller)
        /// ISOInstaller is kept in backend for backward compatibility but hidden from users
        /// </summary>
        public List<RomType> FilteredRomTypes { get; private set; }

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
    }
}