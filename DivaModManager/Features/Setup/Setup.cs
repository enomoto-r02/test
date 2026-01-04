using DivaModManager.Common.Config;
using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace DivaModManager.Features.Setup
{
    public static class Setup
    {
        // called by SetupGame, 
        // MM+とDMLファイルの存在を確認する。DMLは起動時に毎回バージョンチェックする
        public static bool Generic(string exe, string defaultPath)
        {
            // Get install path from registry
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1761390");
                    if (!string.IsNullOrEmpty(key.GetValue("InstallLocation") as string))
                        defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}DivaMegaMix.exe";
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Couldn't find install path in registry ({e.Message})", LoggerType.Error);
            }
            // レジストリ記載先(優先)
            // "C:\Program Files (x86)\Steam\steamapps\common\Hatsune Miku Project DIVA Mega Mix Plus\DivaMegaMix.exe"(2番目)
            // どちらにもない場合は"DivaMegaMix.exe"のパスを参照させるダイアログ(3番目)
            if (!File.Exists(defaultPath))
            {
                MessageBox.Show("Hatsune Miku Project DIVA Mega Mix Plus could not be found.\nPlease select DivaMegaMix.exe.", "Information");
                OpenFileDialog dialog = new()
                {
                    DefaultExt = ".exe",
                    Filter = $"Executable Files ({exe})|{exe}",
                    Title = $"Select {exe} from your Steam Install folder",
                    Multiselect = false,
                    InitialDirectory = Global.assemblyLocation
                };
                dialog.ShowDialog();
                if (!string.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals(exe, StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!string.IsNullOrEmpty(dialog.FileName))
                {
                    Logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    // ファイル選択しなかったら終了
                    return false;
            }

            Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher = defaultPath;
            Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder = $"{Path.GetDirectoryName(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].Launcher)}{Global.s}mods";
            Directory.CreateDirectory(Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModsFolder);

            if (!Directory.Exists(Global.downloadBaseLocation))
                Directory.CreateDirectory(Global.downloadBaseLocation);
            if (!File.Exists(Global.temporaryWarningFilePath))
                File.Create(Global.temporaryWarningFilePath);

            // Check for DML update
            if (!Global.ConfigJson.CurrentConfig.FirstOpen)
            {
                Global.ConfigJson.Configs[Global.ConfigJson.CurrentGame].ModLoaderVersion = null;
                ConfigJson.UpdateConfig();
            }
            return true;
        }
    }
}
