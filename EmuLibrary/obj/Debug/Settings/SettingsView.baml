   M S B A M L   `   `   `  ����O  Kmscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e0891&clr-namespace:System;assembly=mscorlibSystem  5      E AEmuLibrary, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null9!clr-namespace:EmuLibrary.RomTypesEmuLibrary.RomTypes 5      &    EmuLibrary.Settings.SettingsView   5      G CPlaynite.SDK, Version=6.4.0.0, Culture=neutral, PublicKeyToken=nullW SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      6sys&clr-namespace:System;assembly=mscorlib     5      2elrt!clr-namespace:EmuLibrary.RomTypes     5      $��2,0,2,4q�5        � 	Resources  5       �� 
TargetType �� Property �� Value*  $System.Windows.Controls.DataGridCell=  7System.Windows.Controls.Primitives.DataGridColumnHeader �� 	Resources��%�  '��       '�� P     '  �     '  �     '�� l    '�� 1    '�� �    '�� )    �� 5   
   " ��6   .�5      ��  � �� �	#FF3333336+   5      �� 5   
   " ��6   .�5      ��  � �� �	#FF3333336+   5      �� 5   
   "  6   .�5      ��  � �� �	#FF3333336+   5      �� 5   
   "  6   .�5      ��  � �� �	#FF3333336+   �� 5       � �� �	#FFD8D8D86+   5      �� 5   
   " ��6   .�5       ��  � �� �	#FF3333336+    5!      ���� 5"      '��       " ��6   .�5#      ��  � �� �	#FF33333363   5$      5%      5&      �� 5'   
   " ��6   .�5(      ��  � �� �	#FF3333336+   �� 5)       � �� �	#FFE0E0E06+   5*      �� 5+   
   " ��6   .�5,      ��  � �� �	#FF3333336+   �� 5-       � �� �	#FFFFFFFF6+   5.      �� 5/   
   " ��6   .�50      ��  � �� �	#FF3333336+   �� 51       � �� �	#FFFFFFFF6+   52      53      .��54      ]� $��20q�6   .+�55   
   �� $	��Top=�6   .��56      �� -   5         ��Name$ EmulatorSettingsPanel��56      .+�57      �� $��Emulator path mappings��6   $
��Bold"�69   �� 58      $��Right=�6   $��	0,-16,0,0q�68   �� 59      -   5        $R��Lhttps://github.com/psychonic/Playnite-EmuLibrary/blob/master/README.md#setup?�59       .�6�   
Help🔗5;      5<      �� 5=      $��0,5,0,10q�6   � 5>      $
��Auto��6   $��0,0,0,10q�6%    � RowDefinitions 5?      �� 5@      $
��Auto �6(   .+�5B      &   System.Windows.Controls.DataGrid  -   5        $��0��5B      $��0,0,0,4q�6,      AutoGenerateColumns$ False��6_      SelectionUnit$
 Cell=�6�   	   CanUserAddRows$
	 True��6�   ��6>   �� *
Mappings+  5C         RomTypeValues
 S� 
ObjectType   System.Enum S� 
MethodName S� MethodParameters M� TypeName��%{   &	        S� 5D      "
  5E      $ 	GetValues��5F       5G   "   M� 5H   &   $ elrt:RomType��6-   5J      5K         Columns 5L      4  .System.Windows.Controls.DataGridTemplateColumn  5M      ,  &System.Windows.Controls.DataGridColumn   Width5  /System.Windows.Controls.DataGridLengthConverter$ 50 65      CellTemplate 5N   "   �� 5O   &   .R�5P   *   �� -   5        $	��❌��5P   1   
���  ��$��26��6s   $��0q�6~   $��0q�6�   
 � Tag 6=   �� 6�   5Q   '      CellEditingTemplate 5S   "   �� 5T   &   .R�5U   *   �� $�� ��64   6>   5V   '   5X        5Y         Header$ Emulator��65   $	 180 6G    5Z   "   �� 5[   &   .R�5\   *   ��  ��Name$ Emulator��64   ��6F   ��  �� Path$ Emulator.Name� �� UpdateSourceTrigger$ PropertyChanged=�5]   '    5_   "   �� 5`   &   .R�5a   *   �� $��ComboEmulators��63   $
��Left=�6I    �� DisplayMemberPath$
 Name��5c   !   ��5b   !   �� *AvailableEmulators+ �� SelectedItem 5d   !   �� *
Emulator+ �� Mode$ TwoWay=�5f   +   5g   '   5i        5k      $ Profile��65   $	 180 6F    5l   "   �� 5m   &   .R�5n   *   �� $ Profile��64   ��6F   �� $ EmulatorProfile.Name�$ PropertyChanged=�5o   '    5q   "   �� 5r   &   .R�5s   *   �� $��ComboPlatforms��63   $
��Left=�6I   $
 Name��5u   !   ��5t   !   �� *AvailableProfiles+ 5v   !   �� *EmulatorProfile+$ TwoWay=�5x   +   5y   '   5{        5}      $ Platform��65   $	 180 6G    5~   "   �� 5   &   .R�5�   *   �� $ Platform��64   ��6F   �� $ Platform.Name�$ PropertyChanged=�5�   '    5�   "   �� 5�   &   .R�5�   *   �� $��ComboPlatforms��63   $
��Left=�6I   $
 Name��5�   !   ��5�   !   �� *AvailablePlatforms+ 5�   !   �� *
Platform+$ TwoWay=�5�   +   5�   '   5�        5�      $ Rom Type��65   $	 180 6G    5�   "   �� 5�   &   .R�5�   *   �� ��64   �� *	RomType+$ PropertyChanged=�5�   '    5�   "   �� 5�   &   .R�5�   *   �� $��ComboRomTypes��63   $
��Left=�6H   ��5�   !   ��  �� Source# [   5�   !   �� *	RomType+$ TwoWay=�5�   +   5�   '   5�        5�      $ Source Path��65   $	 180 6J    5�   "   �� 5�   &   .R�5�   *   �� $
 Path��64   ��6C   �� $ 
SourcePath�$ PropertyChanged=�5�   '    5�   "   �� 5�   &   .R�5�   *   �� $��Browse��61   -   5        .��5�   Z   	Browse...6e   5�   '   5�        5�      $ Destination Path��65   $	 180 6O    5�   "   �� 5�   &   .R�5�   *   �� $
 Path��64   ��6C   �� $ DestinationPath�$ PropertyChanged=�5�   '    5�   "   �� 5�   &   .R�5�   *   �� $��Browse��61   -   5        5�   b   5�   '   5�      4  .System.Windows.Controls.DataGridCheckBoxColumn  5�      $ Enabled��65   1	  +System.Windows.Controls.DataGridBoundColumn 	  Binding 6F   �� *	Enabled+0
  *System.Windows.Controls.DataGridTextColumn
  5�      $ 
Mapping ID��61      
IsReadOnly$
 True��6E    6W   �� *	MappingId+5�      5�      �� 5�      $��Library scanning��6   $
��Bold"�63   $��0,10,0,0q�6E   �� 5�      $��0,5,0,10q�6   �� 5�      $��0,0,0,10q�6T    d� 	IsChecked 6   �� *ScanGamesInFullScreen+$ TwoWay=�.��5�      �� $-��'Enable game scanning in fullscreen mode��6    $
��Wrap=�6O   5�      �� 5�      $��0,0,0,10q�6j    6   �� *-+AutoRemoveUninstalledGamesMissingFromSource+$ TwoWay=�.��5�      �� $R��LAutomatically remove non-installed games from library if missing from source��6    $
��Wrap=�6t   5�      �� 5�      $��Installation��6   $
��Bold"�6/   $��0,10,0,0q�6A   �� 5�      $��0,5,0,10q�6   �� 5�      $��0,0,0,10q�6V    6   �� *NotifyOnInstallComplete+$ TwoWay=�.��5�      �� $6��0Enable notification upon game install completion��6    $
��Wrap=�6X   5�      �� 5�      $��0,0,0,10q�6`    � ToolTip$� ~Game installs will be performed natively by Windows. The Copy
File dialog box will be displayed to show installation progress.��6r    6   �� *#!UseWindowsCopyDialogInDesktopMode+$ TwoWay=�.��5�      �� $.��(Show Windows copy dialog in desktop mode��6    $
��Wrap=�6P   5�      �� 5�      $��0,0,0,10q�6c   $� �Game installs will be performed natively by Windows. The Copy
File dialog box will be displayed to show installation progress.
Please note that enabling this option may cause issues with focus
in fullscreen mode.��6u    6   �� *&$UseWindowsCopyDialogInFullscreenMode+$ TwoWay=�.��5�      �� $1��+Show Windows copy dialog in fullscreen mode��6    $
��Wrap=�6S   5�      �� 5�      $��PC Installer Settings��6   $
��Bold"�68   $��0,10,0,0q�6J   �� 5�      $��Right=�6   $��	0,-16,0,0q�68   �� 5�      -   5        $N��Hhttps://github.com/psychonic/Playnite-EmuLibrary/wiki/PC-Installer-Guide?�5�       .�6�   
Help🔗5�      5�      �� 5�      $��0,5,0,10q�6   �� 5�      ���	#FFF5F9FF���	#FFADD8E6$��1q�6H    �� Padding$ 10q�6\   $��0,0,0,10q�6i   ._�5�      �� .+�5�      �� $
��Wrap=�6$   $��SemiBold"�68   ���	#FF333333📌 PC Installer Guide5�      �� 5�      $
��Wrap=�6$   $��0,5,0,0q�68   ���	#FF333333��This plugin allows you to scan network shares for game installers and install them on demand. Follow these steps to get started:5�      �� 5�      $
��Wrap=�6$   $��10,5,0,0q�68   ���	#FF333333?=1. Add a new mapping above with "PcInstaller" as the Rom Type�� 5�   [   DB2. Set the source path to your network folder with game installers�� 5�   `   ?=3. Set the destination path to where you want games installed�� 5�   [   644. Click Save and restart Playnite to see your games5�      5�      5�      �� 5�      $��0,0,0,10q�6U    6   �� *AutoDetectPcInstallers+$ TwoWay=�.��5�      �� $>��8Auto-detect installer executables (scan file properties)��6    $
��Wrap=�6`   5�      �� 5�      $$��Default installation location:��6   $��0,0,0,5q�6B   ]� 5�      $
��True��6   $��0,0,0,10q�61   .+�5�      �� -   5        $��Right=�5�      $��	Browse...��64   $��5,0,0,0q�6s   �� 5�      ��6   �� *DefaultInstallLocation+5�      �� 5�      $��0,0,0,10q�6c    6   �� *&$CreateGameSpecificInstallDirectories+$ TwoWay=�.��5�      �� $3��-Create game-specific installation directories��6    $
��Wrap=�6U   $ yWhen enabled, games will be installed in a subfolder named after the game.
For example: [Install Location]/The Witcher 3/��5�      5�      �� 5�      $��0,0,0,10q�6Z    6   �� *PromptForInstallerSelection+$ TwoWay=�.��5�      �� -	   5        $&  PromptForInstallerSelectionLabel��5�       $D��>Prompt for installer selection when multiple options are found��5�      $
��Wrap=�5�      $� zWhen enabled, you will be asked to choose which installer to run
when multiple executable options are found in an archive.��5      5     �� 5     $��0,0,0,10q�6W    6   �� *PromptForInstallLocation+$ TwoWay=�.��5     �� -
   5        $# PromptForInstallLocationLabel��5      $9��3Prompt for installation location after installation��5     $
��Wrap=�5     $k eWhen enabled, you will be asked to specify where the game was installed
if automatic detection fails.��5     5     �� 5
     ���	#FFFFF5F0���	#FFFFD0B0$��1q�6H   $ 10q�6\   $��0,0,0,10q�6i   ._�5     �� .+�5     �� $
��Wrap=�6$   $��SemiBold"�68   ���	#FF333333⚠️ Archive Support5     �� 5     $
��Wrap=�6$   $��0,5,0,0q�68   ���	#FF333333^\For ISO and RAR archive support, you need to place 7z.exe and UnRAR.exe in the Tools folder.�� 5     NLThis enables handling of ISOs, multi-part RAR archives, and nested archives.5     5     5     �� 5     $��Metadata Settings��6   $
��Bold"�64   $��0,10,0,0q�6F   �� 5     $��0,5,0,10q�6   �� 5     $��0,0,0,10q�6U    6   �� *EnableMetadataDownload+$ TwoWay=�.��5     �� $;��5Enable automatic metadata download for detected games��6    $
��Wrap=�6]   5     �� 5     $��0,0,0,10q�6^    6   �� *!UseSourceFolderNamesForMetadata+$ TwoWay=�.��5     �� $L��FUse folder names for metadata matching (better matches with Steam/GOG)��6    $
��Wrap=�6n   $� �Uses parent folder names instead of installer filenames for better metadata matching. This improves results when folder names contain clean game titles.��5     5     5      5!     5"     5#     