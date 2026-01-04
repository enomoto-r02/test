using DivaModManager.Features.Debug;
using DivaModManager.Features.DML;
using DivaModManager.Features.Extract;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DivaModManager.Common.Helpers
{
    public class DMMFileSystemWatcher : FileSystemWatcher
    {
        private Timer Timer;
        private const int DelayTimeMilliSecondMods = 3000;  // リフレッシュを呼び出すまでの秒数
        public Action RefreshAction = default;

        private string ExtractMainStartFilePath { get; set; } = string.Empty;
        private string ExtractMainEndFilePath { get; set; } = string.Empty;

        public DMMFileSystemWatcher() : base()
        {
        }

        public DMMFileSystemWatcher(string path) : base(path)
        {
        }

        // フォルダの初期化・監視開始/停止メソッド
        public static DMMFileSystemWatcher InitializeWatcher(
            MainWindow window, string WatchPath, string FilterStr = null, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            DMMFileSystemWatcher dmmWatcher = new();
            dmmWatcher = new(WatchPath);
            ;
            if (!string.IsNullOrEmpty(WatchPath) && Directory.Exists(WatchPath))
            {
                var fileSystemWatcher = new FileSystemWatcher(WatchPath)
                {
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
                    IncludeSubdirectories = false
                };
                dmmWatcher.Created += dmmWatcher.ModDirectory_FileSystemChanged;
                dmmWatcher.Deleted += dmmWatcher.ModDirectory_FileSystemChanged;
                dmmWatcher.Renamed += dmmWatcher.ModDirectory_FileSystemChanged;
                if (WatchPath == Global.ConfigJson.GetGameLocation())
                {
                    if (FilterStr != null)
                    {
                        dmmWatcher.Filter = FilterStr;
                        if (FilterStr == DMLUpdater.MODULE_NAME_DLL)
                        {
                            // Mods側はファイルを生成だけでエラーになったのでいったんコメント化
                            dmmWatcher.Changed += dmmWatcher.ModDirectory_FileSystemChanged;
                        }
                    }
                }
                dmmWatcher.Timer = new Timer(dmmWatcher.FileMoveEnd_Callback, null, Timeout.Infinite, Timeout.Infinite);
                Logger.WriteLine($"{MeInfo} Initialized watcher for: \"{WatchPath}\"", LoggerType.Developer, param: ParamInfo);
            }
            else
            {
                Logger.WriteLine($"Mods folder not set or does not exist. Watcher not initialized. for: \"{WatchPath}\"", LoggerType.Warning);
            }
            Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);

            return dmmWatcher;
        }

        public static async Task WatchPathChangeAsync(
            DMMFileSystemWatcher Watcher, string ModsPath, WatcherChangeTypes ChangeType, [CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            if (Watcher.Path == Global.ConfigJson.CurrentConfig.ModsFolder
                && FileHelper.PathStartsWith(ModsPath, Global.ConfigJson.CurrentConfig.ModsFolder))
            {
                // 移動中のModフォルダ内の.extractファイルを監視するよう修正
                if (string.IsNullOrEmpty(Watcher.ExtractMainStartFilePath))
                {
                    Watcher.EnableRaisingEvents = false;
                    Watcher.Deleted -= Watcher.ModDirectory_FileSystemChanged;
                    Watcher.Renamed -= Watcher.ModDirectory_FileSystemChanged;

                    Watcher.Path = ModsPath;
                    Watcher.EnableRaisingEvents = true;
                    Watcher.ExtractMainStartFilePath = System.IO.Path.Combine(ModsPath, ExtractInfo.ExtractMainMoveStartFileName);
                    Watcher.ExtractMainEndFilePath = System.IO.Path.Combine(ModsPath, ExtractInfo.ExtractMainMoveEndFileName);

                    Logger.WriteLine(string.Join(" ", MeInfo, $"Watcher.Path Change. Path:\"{Watcher.Path}\""), LoggerType.Debug, param: ParamInfo);
                }

                var isExtractMainStart = await FileHelper.FileExistsAsync(Watcher.ExtractMainStartFilePath);
                var isExtractMainEnd = await FileHelper.FileExistsAsync(Watcher.ExtractMainEndFilePath);
                ParamInfo = string.Join(", ", ParamInfo, $"ExtractMainStartFilePath:\"{Watcher.ExtractMainStartFilePath}\"({isExtractMainStart}), ExtractMainEndFilePath:\"{Watcher.ExtractMainEndFilePath}\"({isExtractMainEnd})");

                if (isExtractMainStart && isExtractMainEnd)
                {
                    Watcher.EnableRaisingEvents = false;
                    Watcher.Timer.Change(DelayTimeMilliSecondMods, Timeout.Infinite);
                }
            }
            else
            {
                Watcher.EnableRaisingEvents = false;
                Watcher.Timer.Change(DelayTimeMilliSecondMods, Timeout.Infinite);
            }

            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        public void StartWatching([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Path:\"{Path}\"";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Developer, param: ParamInfo);

            try
            {
                EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error starting FileSystemWatcher: {ex.Message}", LoggerType.Error, param: ParamInfo);
            }
            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Developer, param: ParamInfo);
        }

        public void StopWatching([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}, Path:\"{Path}\"";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Developer, param: ParamInfo);

            if (this != null)
            {
                try
                {
                    EnableRaisingEvents = false;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Error stopping FileSystemWatcher: {ex.Message}", LoggerType.Error, param: ParamInfo);
                }
            }
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Developer, param: ParamInfo);
        }

        private void ModDirectory_FileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 特定のファイル（例：一時ファイル）を除外したい場合はここでフィルタリング
            // if (e.Name.EndsWith(".tmp")) return;

            Task.Run(async () =>
            {
                await WorkManager.RunAsync(async () =>
                {
                    string MeInfo = Logger.GetMeInfo(new StackFrame());
                    string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}, e.ChangeType:{e.ChangeType.ToString()}, e.FullPath:\"{e.FullPath}\"";
                    //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

                    // Modsフォルダ内へファイル転送を開始した場合は、ExtractMainEndファイルが生成されるまで監視
                    var MoveDirectoryPath = new DirectoryInfo(e.FullPath).FullName;
                    await WatchPathChangeAsync(this, MoveDirectoryPath, e.ChangeType);

                    //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
                });
            });
        }

        // --- タイマーのコールバックメソッド ---
        private void FileMoveEnd_Callback(object state)
        {

            Task.Run(async () =>
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    string MeInfo = Logger.GetMeInfo(new StackFrame());
                    string ParamInfo = $"id:{Thread.CurrentThread.ManagedThreadId}, Path:{Path}";
                    //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

                    if (Path.Contains(Global.ConfigJson.CurrentConfig.ModsFolder))
                    {
                        var retStart = FileHelper.DeleteFile(ExtractMainStartFilePath);
                        var retEnd = FileHelper.DeleteFile(ExtractMainEndFilePath);
                        ParamInfo += $" ExtractMainFile Delete:{ExtractMainStartFilePath}({retStart}), {ExtractMainEndFilePath}({retEnd})";
                        Logger.WriteLine(string.Join(" ", MeInfo), LoggerType.Debug, param: ParamInfo);

                        // Modsフォルダを対象に戻す
                        Path = Global.ConfigJson.CurrentConfig.ModsFolder;
                        Deleted += ModDirectory_FileSystemChanged;
                        Renamed += ModDirectory_FileSystemChanged;
                    }

                    try
                    {
                        if (App.Current.Dispatcher.CheckAccess())
                        {
                            if (RefreshAction != default)
                            {
                                Logger.WriteLine(string.Join(" ", MeInfo, $"Call Refresh!"), LoggerType.Debug, param: ParamInfo);
                                RefreshAction?.Invoke();
                            }
                            else
                            {
                                Logger.WriteLine(string.Join(" ", MeInfo, $"Call Refresh(CheckAccess) Failed..."), LoggerType.Debug, param: ParamInfo);
                            }
                        }
                        else
                        {
                            if (RefreshAction != default)
                            {
                                Logger.WriteLine(string.Join(" ", MeInfo, $"Call Refresh!"), LoggerType.Debug, param: ParamInfo);
                                RefreshAction?.Invoke();
                            }
                            else
                            {
                                Logger.WriteLine(string.Join(" ", MeInfo, $"Call Refresh(Not CheckAccess) Failed..."), LoggerType.Debug, param: ParamInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"Error during DebounceTimerCallback:\nex.Message:{ex.Message}\nex.StackTrace:{ex.StackTrace}", LoggerType.Error);
                    }
                });
                ClearTimerCallbackAfter();
                //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
            });
        }

        private void ClearTimerCallbackAfter([CallerMemberName] string caller = "")
        {
            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            ExtractMainStartFilePath = string.Empty;
            ExtractMainEndFilePath = string.Empty;

            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }

        // --- FileSystemWatcher と Timer の破棄処理を DisposeWatcherAndTimer に集約 ---
        public static void DisposeWatcherAndTimer(DMMFileSystemWatcher DMMWatcher, [CallerMemberName] string caller = "")
        {
            if (DMMWatcher == null)
                return;

            string MeInfo = Logger.GetMeInfo(new StackFrame());
            string ParamInfo = $"caller:{caller}, id:{Thread.CurrentThread.ManagedThreadId}";
            //Logger.WriteLine(string.Join(" ", MeInfo, $"Start."), LoggerType.Debug, param: ParamInfo);

            DMMWatcher.StopWatching();

            DMMWatcher.Created -= DMMWatcher.ModDirectory_FileSystemChanged;
            DMMWatcher.Deleted -= DMMWatcher.ModDirectory_FileSystemChanged;
            DMMWatcher.Renamed -= DMMWatcher.ModDirectory_FileSystemChanged;
            DMMWatcher.Changed -= DMMWatcher.ModDirectory_FileSystemChanged;
            DMMWatcher.RefreshAction = default;

            DMMWatcher.Timer?.Dispose();
            DMMWatcher.Timer = null;
            DMMWatcher.Dispose();
            DMMWatcher = null;
            //Logger.WriteLine(string.Join(" ", MeInfo, $"End."), LoggerType.Debug, param: ParamInfo);
        }
    }
}
