using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Cysharp.Text;

using Platform;
using Platform.IO;
using UnityEngine;

using Qud.API;
using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.AI;
using XRL.World.Effects;
using XRL.World.Parts;

using GameObject = XRL.World.GameObject;
using ColorUtility = ConsoleLib.Console.ColorUtility;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using System.Threading;

namespace Bones.Mod
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

        public List<string> RunningMods;

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
            RunningMods = ModManager.GetRunningMods().ToList();

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

        public Task HoardBones(string GameName, IDeathEvent DeathEvent)
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

                        var saveBonesJSON = new SaveBonesJSON(DeathEvent, The.Game.SaveGameInfo());

                        writer.Start(400);
                        writer.Write(123457);

                        writer.Write(The.Game.GetType().Assembly.GetName().Version.ToString());

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

            Popup.ShowFailAsync($"There was a fatal exception attempting to save some bones. {Utils.ThisMod.DisplayTitle} attempted to recover them.\n" +
                $"You ought to check out your bones folder for recent changes ({Utils.BothBonesLocations})\n\n" +
                $"It'd be helpful if you could contact {Utils.ThisMod.Manifest.Author}, either via GitHub or on the steam workshop, because they'll probably want a copy of the problem bones.");
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
            => await GetSavedBonesInfoAsync(bones => !bones.IsPending, TidyPending: true)
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
            string savGz = $"{BonesSaver.BonesName}.sav.gz";
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
                    fullBonesPath = Path.Combine(SaveBonesInfo.Directory, $"{BonesSaver.BonesName}.sav");
                    if (!await File.ExistsAsync(fullBonesPath))
                    {
                        Utils.Error($"No saved game exists. ({directoryDisplay})");
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
                    if (reader.ReadInt32() != 123457)
                    {
                        versionString = "2.0.167.0 or prior";
                        throw new Exception("Bones file is the incorrect version.");
                    }

                    versionNumber = reader.FileVersion;
                    versionString = reader.ReadString();
                    try
                    {
                        if (versionNumber != 400)
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

                    if (reader.FileVersion < 395
                        || reader.FileVersion > 400)
                        throw new Exception($"Bones file is the incorrect version ({versionString}).");

                    string bonesID = SaveBonesInfo.ID;
                    Zone loadedZone = null;
                    try
                    {
                        loadedZone = reader.ReadBonesZone(/*ZoneID*/);
                        if (loadedZone != null
                            && loadedZone.Z > 21)
                        {
                            loadedZone.Z = Stat.Random(16, 21);
                        }
                        reader.FinalizeRead();
                        /*
                        if (loadedZone != null)
                        {
                            if (The.ActionManager is ActionManager actionManager
                                && actionManager.ActionQueue is RingDeque<GameObject> actionQueue)
                            {
                                foreach (var loadedObject in loadedZone.GetObjects())
                                {
                                    if (actionQueue.Contains(loadedObject))
                                    {
                                        loadedObject.ApplyActiveRegistrar();
                                        if (loadedObject.Abilities != null)
                                            actionManager.AbilityObjects.Add(loadedObject);
                                    }
                                }
                            }
                        }
                        */
                        bonesData = new(bonesID, ZoneID, loadedZone);
                    }
                    catch (Exception x)
                    {
                        Utils.Error(x);
                        bonesData = null;
                    }
                    if (reader.Errors > 0)
                    {
                        Popup.DisplayLoadError(reader, "bones file", reader.Errors);
                    }
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
                        message = "That bones file is likely not loading because of a mod error from " + Mod.DisplayTitleStripped + " (" + culpritMethod + "), make sure the correct mods are enabled or contact the mod author.";
                    }
                }
                else
                {
                    if (versionNumber < 400)
                        message = $"That bones file looks like it's from an older save format revision ({versionString}). Sorry!\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";
                    else
                    if (versionNumber > 400)
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

        public static GameObject CreateMoonKing(
            GameObject Player,
            Cell TargetCell
            )
        {
            if (!Player.CanBeReplicated(Player, BonesSaver.BonesName, Temporary: false))
                return null;

            string factions = "Entropic-100,Mean-100,Playerhater-99";

            var moonKing = Player.DeepCopy();

            if (Player.IsPlayer())
                moonKing.SetStringProperty("PlayerCopy", "true");

            moonKing.SetStringProperty("EvilTwin", "true");
            moonKing.SetStringProperty(BonesSaver.BonesName, "true");
            moonKing.SetIntProperty("Entropic", 1);

            var brain = moonKing.Brain;
            brain.PartyLeader = null;
            brain.Hibernating = false;
            brain.Staying = false;
            brain.Passive = false;
            brain.Factions = factions;
            brain.Allegiance.Hostile = true;
            brain.Allegiance.Calm = false;

            brain.PerformEquip();

            if (Player != null)
            {
                brain.AddOpinion<OpinionMollify>(Player);
                Player.AddOpinion<OpinionMollify>(moonKing);
            }

            string regalTitle = LunarRegent.GetRegalTitle(Player);

            moonKing.RequirePart<Titles>().Primary = regalTitle;

            if (TargetCell == null)
            {
                moonKing.Obliterate();
                return null;
            }

            if (moonKing.Render is Render render)
                render.Visible = false;

            var lunarRegentPart = moonKing.RequirePart<LunarRegent>();

            lunarRegentPart.RegalTitle = regalTitle;
            lunarRegentPart.Onset();

            TargetCell.AddObject(moonKing);

            moonKing.MakeActive();
            
            return moonKing;
        }

        public static void ApplyHostility(GameObject Actor, Brain Brain, int Depth)
        {
            if (Actor != null && Depth < 100)
            {
                Brain.AddOpinion<OpinionInscrutable>(Actor);
                ApplyHostility(Actor.PartyLeader, Brain, Depth + 1);
                if (Actor.TryGetEffect<Dominated>(out var Effect))
                {
                    ApplyHostility(Effect.Dominator, Brain, Depth + 1);
                }
            }
        }
    }
}
