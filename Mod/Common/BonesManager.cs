using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Cysharp.Text;

using Platform;
using Platform.IO;
using UnityEngine;

using Qud.API;
using Qud.UI;

using XRL;
using XRL.Core;
using XRL.Collections;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.AI;
using XRL.World.Effects;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using GameObject = XRL.World.GameObject;
using ColorUtility = ConsoleLib.Console.ColorUtility;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Event = XRL.World.Event;
using UD_Bones_Folder.Mod.UI;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasGameBasedStaticCache]
    [HasWishCommand]
    [Serializable]
    public class BonesManager : IScribedSystem
    {
        public static string BonesSyncPath => DataManager.SyncedPath("Bones");

        public static string BonesSavePath => DataManager.SavePath("Bones");

        [GameBasedStaticCache]
        private static List<string> _SaveGameIDs = null;

        public static IEnumerable<string> SaveGameIDs => _SaveGameIDs ??= SavesAPI.GetSavedGameInfo()?.Result?.Select(info => info.ID)?.ToList();


        protected static string[] BonesPaths = new string[]
        {
            BonesSyncPath,
            BonesSavePath,
        };

        [GameBasedStaticCache(CreateInstance = false)]
        public static BonesManager System;

        private static List<string> _RunningMods;
        public static IEnumerable<string> RunningMods => _RunningMods ??= ModManager.GetRunningMods().ToList();

        public bool Initialized;

        [NonSerialized]
        public string BonesDirectory;

        [NonSerialized]
        public Task SaveTask;

        public BonesManager()
        { }

        private static BonesManager InitializeSystem() => new();

        [GameBasedCacheInit]
        public static void BonesManagerSystemInit()
        {
            if (System == null)
                System = The.Game?.RequireSystem(InitializeSystem);
            else
                The.Game?.AddSystem(System);

            if (System == null)
                System = The.Game?.RequireSystem(InitializeSystem);
            else
                The.Game?.AddSystem(System);

            if (System != null)
                Loading.LoadTask($"Preparing Bones", System.PrepareBonesPile);
            else
            if (The.Game != null)
                Utils.Error($"Failed to load {nameof(BonesManager)}.");
        }

        public void PrepareBonesPile()
        {
            // TidyBonesPile();
        }

        // [ModSensitiveCacheInit]
        public static void TidyBonesPile()
        {
            using var saveGameInfoIDs = ScopeDisposedList<string>.GetFromPool();

            foreach (var saveInfo in SavesAPI.GetSavedGameInfo().Result)
                saveGameInfoIDs.Add(saveInfo.ID);

            var bonesInfos = GetPendingSaveBonesInfoAsync().Result;

            foreach (var bonesInfo in bonesInfos)
            {
                if (!saveGameInfoIDs.Contains(bonesInfo.Pending))
                {
                    Utils.Info($"Save file {bonesInfo.Pending} missing without encountering bones; bones no longer pending.");
                    SaveBonesInfo.SetPending(bonesInfo, $"{false}").Wait();
                }
            }
        }

        public string GetBonesDirectory(string FileName = null)
        {
            if (BonesDirectory == null)
            {
                string path = Path.Combine(BonesSyncPath, The.Game.GameID);
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception x)
                {
                    MetricsManager.LogCallingModError(x);
                    return null;
                }
                BonesDirectory = path;
            }
            if (!FileName.IsNullOrEmpty())
                return Path.Combine(BonesDirectory, FileName);

            return BonesDirectory;
        }

        public string GetSaveFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.sav.gz")
            ;

        public string GetInfoFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.json")
            ;

        public Task HoardBones(
            string GameName,
            IDeathEvent DeathEvent,
            GameObject MoonKing
            )
        {
            using (DelayShutdown.AutoScopeForceMainThread())
            {
                var task = SaveTask;

                if (task != null
                    && !task.IsCompleted)
                    return null;

                string message = "Hoarding Bones";

                Loading.Status status = Loading.StartTask(message);

                if (The.Game is XRLGame game
                    && DeathEvent?.Dying?.CurrentZone is Zone currentZone
                    && SerializationWriter.Get() is SerializationWriter writer)
                {
                    // Clear object ID's so they get a new one once re-serialzed (hopefully this works...)
                    foreach (var zoneGO in currentZone.GetObjects())
                    {
                        zoneGO._BaseID = 0;
                        zoneGO.RemoveStringProperty("id");
                    }

                    string bonesFilePath = GetSaveFileFullPath(GameName);
                    string bonesInfoPath = GetInfoFileFullPath(GameName);
                    try
                    {
                        if (game.WallTime != null)
                        {
                            game._walltime += game.WallTime.ElapsedTicks;
                            game.WallTime.Reset();
                            game.WallTime.Start();
                        }
                        else
                        {
                            game.WallTime = new Stopwatch();
                            game.WallTime.Start();
                        }

                        var saveBonesJSON = game.CreateSaveBonesJSON(DeathEvent, MoonKing);

                        writer.Start(CURR_SAVE_VERSION);
                        writer.Write(SERIALIZATION_CHECK);

                        writer.Write(saveBonesJSON.GameVersion);

                        writer.WriteBonesZone(currentZone);

                        writer.FinalizeWrite();

                        bool restoreBackup = false;
                        try
                        {
                            File.WriteAllText(bonesInfoPath, JsonUtility.ToJson(saveBonesJSON, prettyPrint: true));
                            if (File.Exists(bonesFilePath))
                            {
                                File.Copy(bonesFilePath, bonesFilePath + ".bak", overwrite: true);
                                restoreBackup = true;
                            }
                            using (var memoryStream = new System.IO.MemoryStream())
                            {
                                using (var gZipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
                                {
                                    byte[] buffer = writer.Stream.GetBuffer();
                                    gZipStream.Write(buffer, 0, (int)writer.Stream.Length);
                                }
                                File.WriteAllBytes(bonesFilePath, memoryStream.ToArray());
                            }
                            MemoryHelper.GCCollect();
                            game.CheckSave(bonesFilePath);
                        }
                        catch (Exception x)
                        {
                            BonesHoardError(bonesFilePath, x, restoreBackup);
                        }
                        finally
                        {
                            SerializationWriter.Release(writer);
                        }
                        task = null;
                    }
                    catch (Exception x)
                    {
                        SerializationWriter.Release(writer);
                        BonesHoardError(bonesFilePath, x);
                        task = SaveTask = Task.FromException(x);
                    }
                    finally
                    {
                        MemoryHelper.GCCollectMax();
                        status.Dispose();
                    }
                }
                return task;
            }
        }

        public void BonesHoardError(
            string Path,
            Exception Exception,
            bool RestoreBackup = false
            )
        {
            Utils.Error(nameof(HoardBones), Exception);
            if (RestoreBackup
                && File.Exists(Path + ".bak"))
            {
                try
                {
                    File.Copy(Path + ".bak", Path, overwrite: true);
                }
                catch (Exception x)
                {
                    Utils.Warn($"Failed to restore backup bones file. {x}");
                }
            }

            Popup.ShowFailAsync($"There was a fatal exception attempting to save some bones. " +
                $"{Utils.ThisMod.DisplayTitle} attempted to recover them.\n" +
                $"You ought to check out your bones folder for recent changes ({Utils.BothBonesLocations})\n\n" +
                $"It'd be helpful if you could contact {Utils.AuthorOnPlatforms}, " +
                $"because they'll probably want a copy of the problem bones.");
        }

        public static async Task<IEnumerable<SaveBonesInfo>> GetSavedBonesInfoAsync(Predicate<SaveBonesInfo> Where, bool TidyPending = false)
        {
            var saveBonesInfos = new List<SaveBonesInfo>();
            string currentPath = null;
            
            foreach (string bonesPath in BonesPaths)
            {
                try
                {
                    currentPath = bonesPath;
                    if (!Directory.Exists(bonesPath))
                        continue;

                    var enumerationResult = await Folder.EnumerateDirectoriesAsync(bonesPath);
                    enumerationResult.LogErrorIfFailed();

                    string[] directories = enumerationResult.directories;

                    for (int i = 0; i < directories.Length; i++)
                    {
                        if (directories[i] is string bonesFolder
                            && await SaveBonesInfo.GetSaveBonesInfo(bonesFolder) is SaveBonesInfo bonesInfo)
                        {
                            if (TidyPending
                                && !bonesInfo.Pending.EqualsNoCase($"{false}")
                                && SaveGameIDs?.Contains(bonesInfo.Pending) is false)
                                SaveBonesInfo.SetPending(bonesInfo, null).Wait();

                            if (Where?.Invoke(bonesInfo) is not false)
                                saveBonesInfos.Add(bonesInfo);
                        }
                    }
                }
                catch (Exception x)
                {
                    SeriousBonesError(x, currentPath);
                    saveBonesInfos.Clear();
                }
            }

            return saveBonesInfos;
        }

        public static IEnumerable<SaveBonesInfo> GetSavedBonesInfo(Predicate<SaveBonesInfo> Where)
            => GetSavedBonesInfoAsync(Where)?.Result
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<bool> HasSavedBonesAsync()
            => !(await GetSavedBonesInfoAsync(null)).IsNullOrEmpty()
            ;

        public static bool HasSavedBones()
            => HasSavedBonesAsync().Result
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetPendingSaveBonesInfoAsync()
            => await GetSavedBonesInfoAsync(bones => bones.IsPending)
            ;

        public static IEnumerable<SaveBonesInfo> GetPendingSaveBonesInfo()
            => GetPendingSaveBonesInfoAsync()?.Result
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetSavedBonesInfoAsync()
            => await GetSavedBonesInfoAsync(null)
            ;

        public static IEnumerable<SaveBonesInfo> GetSavedBonesInfo()
            => GetSavedBonesInfoAsync()?.Result
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetAvailableSavedBonesInfoAsync()
            => await GetSavedBonesInfoAsync(
                Where: bones
                    => !bones.IsPending
                    && bones.ID != The.Game.GameID,
                TidyPending: true)
            ;

        public static IEnumerable<SaveBonesInfo> GetAvailableSavedBonesInfo()
            => GetAvailableSavedBonesInfoAsync()?.Result
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        private static void SeriousBonesError(Exception Ex, string Path)
        {
            MetricsManager.LogError("Error checking for save files", Ex);
            using var sB = ZString.CreateStringBuilder();
            if (Ex is UnauthorizedAccessException
                || Ex is System.IO.IOException)
            {
                sB.AppendLine("There was a permission error while trying to access your bones directory.");
                sB.AppendLine();
                sB.AppendLine(ColorUtility.EscapeFormatting(Ex.Message));
            }
            else
            {
                sB.AppendLine("There was an error while trying to access your bones directory.");
                sB.AppendLine();
                sB.Append("Directory: ");
                sB.AppendLine(ColorUtility.EscapeFormatting(Path));
            }
            sB.AppendLine();
            sB.AppendLine("Caves of Qud will run, but Bones won't be available. Please check your directory’s permissions.");

            Popup.WaitNewPopupMessage(
                message: sB.ToString(),
                buttons: new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = "Bummer!",
                        hotkey = "Accept,Cancel",
                        command = "Cancel"
                    }
                },
                title: "Error reading bones location.");
        }

        public async Task<BonesData> ExhumeMoonKingAsync(
            string ZoneID,
            SaveBonesInfo SaveBonesInfo
            )
        {
            string savGz = $"{UD_Bones_BonesSaver.BonesName}.sav.gz";
            string savGzBak = $"{savGz}.bak";

            string fullBonesPath = Path.Combine(SaveBonesInfo.Directory, savGz);
            string fullBonesBak = Path.Combine(SaveBonesInfo.Directory, savGzBak);
            string bonesBakDisplay = DataManager.SanitizePathForDisplay(fullBonesBak);
            string directoryDisplay = DataManager.SanitizePathForDisplay(SaveBonesInfo.Directory);

            int versionNumber = -1;
            string versionString = "{unknown}";

            BonesData bonesData = null;
            try
            {
                if (!await File.ExistsAsync(fullBonesPath))
                {
                    fullBonesPath = Path.Combine(SaveBonesInfo.Directory, $"{UD_Bones_BonesSaver.BonesName}.sav");
                    if (!await File.ExistsAsync(fullBonesPath))
                    {
                        Utils.Error($"No saved bones exists. ({directoryDisplay})");
                        return bonesData;
                    }
                }

                var reader = SerializationReader.Get();

                var status = Loading.StartTask("Exhuming Moon King");

                try
                {
                    using var stream = File.OpenRead(fullBonesPath);
                    var memory = reader.Stream;

                    if (stream.Length >= 2
                        && stream.ReadByte() == 31)
                        stream.ReadByte();

                    stream.Position = 0L;

                    using var gZipStream = new GZipStream(stream, CompressionMode.Decompress);
                    await gZipStream.CopyToAsync(memory);

                    memory.Position = 0L;

                    reader.Start();
                    if (reader.ReadInt32() != SERIALIZATION_CHECK)
                    {
                        versionString = "2.0.167.0 or prior";
                        throw new Exception("Bones file is the incorrect version.");
                    }

                    versionNumber = reader.FileVersion;
                    versionString = reader.ReadString();
                    try
                    {
                        if (versionNumber != CURR_SAVE_VERSION)
                        {
                            string backupPath = fullBonesPath + $"_upgradebackup_{versionNumber}.gz";
                            if (!File.Exists(backupPath))
                            {
                                File.Copy(fullBonesPath, backupPath);
                                string cacheDBPath = Path.Combine(SaveBonesInfo.Directory, "Cache.db");
                                string cacheDBBackupPath = cacheDBPath + $"_upgradebackup_{versionNumber}.gz";
                                if (File.Exists(cacheDBPath)
                                    && !File.Exists(cacheDBBackupPath))
                                    File.Copy(cacheDBPath, cacheDBBackupPath);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"bones upgrade backup: {x}");
                    }

                    if (reader.FileVersion < MIN_SAVE_VERSION
                        || reader.FileVersion > CURR_SAVE_VERSION)
                        throw new Exception($"Bones file is the incorrect version ({versionString}).");

                    string bonesID = SaveBonesInfo.ID;
                    Zone loadedZone = null;
                    try
                    {
                        loadedZone = reader.ReadBonesZone();
                        if (loadedZone != null
                            && loadedZone.Z > 21)
                            loadedZone.Z = Stat.Random(16, 21);

                        reader.FinalizeRead();

                        bonesData = new(bonesID, ZoneID, loadedZone);
                    }
                    catch (Exception x)
                    {
                        Utils.Error(x);
                        bonesData = null;
                    }

                    if (reader.Errors > 0)
                        DisplayLoadError(reader, "bones file", reader.Errors);
                }
                catch (Exception x)
                {
                    Utils.Error(x);
                    bonesData = null;
                }
                finally
                {
                    SerializationReader.Release(reader);
                    status.Dispose();
                }

            }
            catch (Exception x)
            {
                string message = $"That bones file appears to be corrupt, " +
                    $"you can try to restore the backup in your bones folder ({bonesBakDisplay}) " +
                    $"by removing the 'bak' file extension.";

                if (ModManager.TryGetStackMod(x, out var Mod, out var Frame))
                {
                    if (Frame.GetMethod() is MethodBase method)
                    {
                        string culpritMethod = method.DeclaringType?.FullName + "." + method.Name;
                        Mod.Error(culpritMethod + "::" + x);
                        message = $"That bones file is likely not loading because of a mod error from {Mod.DisplayTitleStripped} ({culpritMethod}), " +
                            $"make sure the correct mods are enabled or contact the mod author.";
                    }
                }
                else
                {
                    if (versionNumber < CURR_SAVE_VERSION)
                        message = $"That bones file looks like it's from an older save format revision ({versionString}). Sorry!\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";
                    else
                    if (versionNumber > CURR_SAVE_VERSION)
                        message = $"That bones file looks like it's from a newer save format revision ({versionString}).\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";

                    MetricsManager.LogException($"{nameof(BonesManager)}.{nameof(ExhumeMoonKing)}::", x, "serialization_error");
                }

                Utils.Error(message);

                throw;
            }
            finally
            {
                // DataManager.CloseCacheConnection();
            }

            return bonesData;
        }

        public static void DisplayLoadError(SerializationReader Reader, string Loadable, int Errors = 1)
        {
            bool isSingleError = Errors == 1;
            var sB = Event.NewStringBuilder()
                .Append("There").Compound(isSingleError ? "was" : "were").Compound(Errors)
                .Compound(isSingleError ? "error" : "errors")
                .Compound("while loading this")
                .Compound(Loadable)
                .Append('.');

            bool isMissingMods = false;
            foreach (string item in Reader.GetSavedMods().Except(ModManager.GetRunningMods()))
            {
                    isMissingMods = true;
                    sB.Append("\n\nMissing mods: ");

                sB.Append(ModManager.GetModTitle(item));
            }

            if (isMissingMods)
                sB.Append('.');

            Utils.Warn(Event.FinalizeString(sB));
        }

        public BonesData ExhumeMoonKing(string ZoneID, SaveBonesInfo DeathEvent)
        {
            var exhumation = ExhumeMoonKingAsync(ZoneID, DeathEvent);
            exhumation.Wait();
            return exhumation.Result;
        }

        public async Task<SaveBonesInfo> GetSavedBonesByIDAsync(string BonesID)
        {
            foreach (var savedBones in await GetSavedBonesInfoAsync())
            {
                if (savedBones.ID == BonesID)
                    return savedBones;
            }
            return null;
        }

        public SaveBonesInfo GetSavedBonesByID(string BonesID)
            => GetSavedBonesByIDAsync(BonesID)?.Result
            ;

        public static void DeleteBonesInfoDirectory(string Directory)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    Platform.IO.Directory.Delete(Directory);
                    break;
                }
                catch (Exception x)
                {
                    if (attempts++ < 20)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    Utils.Error("Error deleting saved bones", x);
                    break;
                }
            }
            // DataManager.CloseCacheConnection();
        }

        public bool TryGetSaveBonesByID(string BonesID, out SaveBonesInfo SavedBonesInfo)
            => (SavedBonesInfo = GetSavedBonesByID(BonesID)) != null
            ;

        public void CremateMoonKing(string BonesID)
        {
            if (TryGetSaveBonesByID(BonesID, out var savedBones))
                savedBones.Cremate();
        }

        [WishCommand(Command = "cremate bones")]
        public static bool ClearBones_WishHandler()
        {
            bool success = true;
            try
            {
                BonesManagement.instance.HandleDeleteAll();
            }
            catch (Exception x)
            {
                Utils.Error($"Failed to cremate all bones", x);
                success = false;
            }
            return success;
        }
    }
}
