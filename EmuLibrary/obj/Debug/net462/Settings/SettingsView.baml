   M S B A M L   `   `   `  ����O  Kmscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e0891&clr-namespace:System;assembly=mscorlibSystem  5      E AEmuLibrary, Version=1.0.0.1, Culture=neutral, PublicKeyToken=null9!clr-namespace:EmuLibrary.RomTypesEmuLibrary.RomTypes 5      &    EmuLibrary.Settings.SettingsView   5      G CPlaynite.SDK, Version=6.4.0.0, Culture=neutral, PublicKeyToken=nullW SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      6sys&clr-namespace:System;assembly=mscorlib     5      2elrt!clr-namespace:EmuLibrary.RomTypes     5      $��2,0,2,4q�5      .��5      ]� $��20q�6   .+�5   
   �� $	��Top=�6   .��5      �� -   5          ��Name$  EmulatorSettingsPanel��5      .+�5      �� $��Emulator path mappings��6   $
��Bold"�69   �� 5      $��Right=�6   $��	0,-16,0,0q�68   �� 5      -   5        $R��Lhttps://github.com/psychonic/Playnite-EmuLibrary/blob/master/README.md#setup?�5       .�6�   
Help🔗5      5      �� 5      $��0,5,0,10q�6   �� 5       �� Orientation$ 
Horizontal=�6   $��Right=�66   $��0,0,0,10q�6R   .+�5      �� -   5        $��Add ISO Mapping��5      $
��10,5q�6S   $��SemiBold"�6b   5      � 5      $
��Auto��6   $��0,0,0,10q�6%    � RowDefinitions 5      �� 5      $
��Auto �6(   .+�5      &   System.Windows.Controls.DataGrid  -   5        $��0��5      $��0,0,0,4q�6,      AutoGenerateColumns$ False��6_      SelectionUnit$
 Cell=�6�      CanUserAddRows$
 True��6�   ��6>   �� *
Mappings+ � 	Resources 5         RomTypeValues S� 
ObjectType   System.Enum S� 
MethodName	 S� MethodParameters
 M� TypeName��%{   &	        S� 5      "  5       $ 	GetValues��5!      	 5"   "   M� 5#   &   $
 elrt:RomType��6-   5%      5&         Columns 5'      4  .System.Windows.Controls.DataGridTemplateColumn  5(      ,  &System.Windows.Controls.DataGridColumn   Width5  /System.Windows.Controls.DataGridLengthConverter$ 50 65      CellTemplate 5)   "   �� 5*   &   .R�5+   *   �� -   5        $	��❌��5+   1   
���  ��$��26��6s   $��0q�6~   $��0q�6�   
 � Tag 6=   �� 6�   5,   '      CellEditingTemplate 5.   "   �� 5/   &   .R�50   *   �� $�� ��64   6>   51   '   53        54         Header$ Emulator��65   $	 180 6G    55   "   �� 56   &   .R�57   *   ��  ��Name$ Emulator��64   ��6F   ��  �� Path$ Emulator.Name� �� UpdateSourceTrigger$ PropertyChanged=�58   '    5:   "   �� 5;   &   .R�5<   *   �� $��ComboEmulators��63   $
��Left=�6I    �� DisplayMemberPath$
 Name��5>   !   ��5=   !   �� *AvailableEmulators+ �� SelectedItem 5?   !   �� *
Emulator+ �� Mode$ TwoWay=�5A   +   5B   '   5D        5F      $ Profile��65   $	 180 6F    5G   "   �� 5H   &   .R�5I   *   �� $ Profile��64   ��6F   �� $ EmulatorProfile.Name�$ PropertyChanged=�5J   '    5L   "   �� 5M   &   .R�5N   *   �� $��ComboPlatforms��63   $
��Left=�6I   $
 Name��5P   !   ��5O   !   �� *AvailableProfiles+ 5Q   !   �� *EmulatorProfile+$ TwoWay=�5S   +   5T   '   5V        5X      $ Platform��65   $	 180 6G    5Y   "   �� 5Z   &   .R�5[   *   �� $ Platform��64   ��6F   �� $ Platform.Name�$ PropertyChanged=�5\   '    5^   "   �� 5_   &   .R�5`   *   �� $��ComboPlatforms��63   $
��Left=�6I   $
 Name��5b   !   ��5a   !   �� *AvailablePlatforms+ 5c   !   �� *
Platform+$ TwoWay=�5e   +   5f   '   5h        5j      $ Rom Type��65   $	 180 6G    5k   "   �� 5l   &   .R�5m   *   �� ��64   �� *	RomType+$ PropertyChanged=�5n   '    5p   "   �� 5q   &   .R�5r   *   �� $��ComboRomTypes��63   $
��Left=�6H   ��5s   !   ��  �� Source# [   5t   !   �� *	RomType+$ TwoWay=�5v   +   5w   '   5y        5{      $ Source Path��65   $	 180 6J    5|   "   �� 5}   &   .R�5~   *   �� $
 Path��64   ��6C   �� $ 
SourcePath�$ PropertyChanged=�5   '    5�   "   �� 5�   &   .R�5�   *   �� $��Browse��61   -   5        .��5�   Z   	Browse...6e   5�   '   5�        5�      $ Destination Path��65   $	 180 6O    5�   "   �� 5�   &   .R�5�   *   �� $
 Path��64   ��6C   �� $ DestinationPath�$ PropertyChanged=�5�   '    5�   "   �� 5�   &   .R�5�   *   �� $��Browse��61   -   5        5�   b   5�   '   5�      4  .System.Windows.Controls.DataGridCheckBoxColumn  5�      $ Enabled��65   1  +System.Windows.Controls.DataGridBoundColumn   Binding 6F   �� *	Enabled+0  *System.Windows.Controls.DataGridTextColumn  5�      $ 
Mapping ID��61      
IsReadOnly$
 True��6E    6W   �� *	MappingId+5�      5�      �� 5�      $��Library scanning��6   $
��Bold"�63   $��0,10,0,0q�6E   �� 5�      $��0,5,0,10q�6   �� 5�      $��0,0,0,10q�6T    d� 	IsChecked 6   �� *ScanGamesInFullScreen+$ TwoWay=�.��5�      �� $-��'Enable game scanning in fullscreen mode��6    $
��Wrap=�6O   5�      �� 5�      $��0,0,0,10q�6j    6   �� *-+AutoRemoveUninstalledGamesMissingFromSource+$ TwoWay=�.��5�      �� $R��LAutomatically remove non-installed games from library if missing from source��6    $
��Wrap=�6t   5�      �� 5�      $��Installation��6   $
��Bold"�6/   $��0,10,0,0q�6A   �� 5�      $��0,5,0,10q�6   �� 5�      $��0,0,0,10q�6V    6   �� *NotifyOnInstallComplete+$ TwoWay=�.��5�      �� $6��0Enable notification upon game install completion��6    $
��Wrap=�6X   5�      �� 5�      $��0,0,0,10q�6`    � ToolTip$� ~Game installs will be performed natively by Windows. The Copy
File dialog box will be displayed to show installation progress.��6r    6   �� *#!UseWindowsCopyDialogInDesktopMode+$ TwoWay=�.��5�      �� $.��(Show Windows copy dialog in desktop mode��6    $
��Wrap=�6P   5�      �� 5�      $��0,0,0,10q�6c   $� �Game installs will be performed natively by Windows. The Copy
File dialog box will be displayed to show installation progress.
Please note that enabling this option may cause issues with focus
in fullscreen mode.��6u    6   �� *&$UseWindowsCopyDialogInFullscreenMode+$ TwoWay=�.��5�      �� $1��+Show Windows copy dialog in fullscreen mode��6    $
��Wrap=�6S   5�      �� 5�      $��Asset Import and Caching��6   $
��Bold"�6;   $��0,10,0,0q�6M   �� 5�      $��0,5,0,10q�6   �� 5�      $��0,0,0,10q�6Q   $v pWhen enabled, imported assets will be cached to improve performance
for subsequent operations on the same files.��6c    6   �� *EnableAssetCaching+$ TwoWay=�.��5�      �� $��Enable asset caching��6    $
��Wrap=�6<   5�      �� 5�      $��0,0,0,10q�6S   $| vWhen enabled, verifies that imported assets are not corrupted
by comparing file sizes and checksums (for small files).��6e    6   �� *VerifyImportedAssets+$ TwoWay=�.��5�      �� $��Verify imported assets��6    $
��Wrap=�6>   5�      � 5�      $��0,0,0,10q�6    � ColumnDefinitions 5�      �� 5�      $��* �6+   �� 5�      $	��100 �6+   .+�5�      �� $��0��6    $'��!File size warning threshold (MB):��60   $��Center=�6Y   $M GShows a confirmation dialog when importing files larger than this size.��5�       �� 5�      $��1��6   $��Center=�5�      ��5�   .   �� *!LargeFileSizeWarningThresholdMB+$ TwoWay=�5�      5�      � 5�      $��0,0,0,10q�6    5�      �� 5�      $��* �6+   �� 5�      $	��100 �6+   .+�5�      �� $��0��6    $��Network retry attempts:��60   $��Center=�6O   $? 9Number of times to retry network operations if they fail.��5�       �� 5�      $��1��6   $��Center=�5�      ��5�   .   �� *NetworkRetryAttempts+$ TwoWay=�5�      5�      ]� 5�      $��False��6   $��	0,10,0,10q�62   $��Right=�6E   .+�5�      �� -   5        $��Right=�5�      $��Clear Asset Cache��64   $
��10,5q�5�      $��5,0,0,0q�6,   �� 5�      -	   5        $��Right=�5�      $��View Cache Info��64   $
��10,5q�5�      5�      �� 5�      $��API Integration��6   $
��Bold"�62   $��0,10,0,0q�6D   �� 5�      $��0,5,0,10q�6   �� 5�      $��0,0,0,10q�6X   $L FUse SteamGridDB for improved game title matching when scanning folders��5�       5�      �� *EnableSteamGridDbMatching+$ TwoWay=�.��5�      �� $'��!Enable SteamGridDB title matching��6    $
��Wrap=�6I   5�      � 5�      $��0,0,0,10q�6    5�      �� 5�      $
��Auto �6+   �� 5�      $��* �6+   �� 5�      $
��Auto �6+   .+�5�      �� $��0��6    $��SteamGridDB API Key:��60   $��Center=�6L   $��0,0,10,0q�6g   $h bAPI key for SteamGridDB services. Get yours at https://www.steamgriddb.com/profile/preferences/api��5�       �� 5�      $��1��6   $��Center=�5�      ��5�   .   �� *SteamGridDbApiKey+$ TwoWay=�5�      �� 5�      -
   5        $��2��5�      $��Get API Key��6-   $��10,0,0,0q�6C   $	��5,3q�6U   5�      5�      5�      5�      5�      5�      