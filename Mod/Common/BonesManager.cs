using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
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
using XRL.UI.Framework;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using ColorUtility = ConsoleLib.Console.ColorUtility;
using CompressionLevel = System.IO.Compression.CompressionLevel;

using GameObject = XRL.World.GameObject;
using Event = XRL.World.Event;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasGameBasedStaticCache]
    [HasCallAfterGameLoaded]
    [HasWishCommand]
    [Serializable]
    public class BonesManager : IScribedSystem
    {
        public class DeserializationException : Exception
        {
            public DeserializationException(string Message)
                : base(Message) { }

            public DeserializationException(string Message, Exception InnerException)
                : base(Message, InnerException) { }
        }

        public class DeserializationVersionException : DeserializationException
        {
            public DeserializationVersionException(string Message)
                : base(Message) { }

            public DeserializationVersionException(string Message, Exception InnerException)
                : base(Message, InnerException) { }
        }

        public class FatalDeserializationVersionException : DeserializationVersionException
        {
            public FatalDeserializationVersionException(string Message)
                : base(Message) { }

            public FatalDeserializationVersionException(string Message, Exception InnerException)
                : base(Message, InnerException) { }
        }

        private static bool _WishContext;
        public static bool WishContext
        {
            get => _WishContext;
            protected set => _WishContext = value;
        }

        protected Dictionary<string, string> TileReplacementsByMissingBlueprint = new();

        protected Dictionary<string, string> BlueprintReplacementsByMissingBlueprint = new();

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

        [GameBasedStaticCache(CreateInstance = false)]
        private static bool? _HasSaveBones;

        private string GameID;

        public bool Initialized;

        [NonSerialized]
        public string BonesDirectory;

        [NonSerialized]
        public Task SaveTask;

        public BonesManager()
        { }

        private static BonesManager InitializeSystem() => new() { GameID = The.Game?.GameID };

        [CallAfterGameLoaded]
        [GameBasedCacheInit]
        public static void BonesManagerSystemInit()
        {
            if (System == null)
                System = The.Game?.RequireSystem(InitializeSystem);
            else
            if (System.GameID != null
                && System.GameID != The.Game?.GameID)
            {
                System = null;
                BonesManagerSystemInit();
                return;
            }
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
            // Consider adding code here.
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

        private Task ReturnSaveTaskNullWithLogMessage(string Message = null)
        {
            Message.Log();
            return SaveTask = null;
        }

        public Task HoardBones(
            string GameName,
            IDeathEvent DeathEvent,
            GameObject MoonKing
            )
        {
            using (DelayShutdown.AutoScopeForceMainThread())
            {
                var saveTask = The.Game.SaveTask;

                if (saveTask?.IsCompleted is false)
                    return ReturnSaveTaskNullWithLogMessage($"Abort {nameof(HoardBones)}: {nameof(SaveTask)} is not null and not complete");

                if (The.Game is not XRLGame game)
                    return ReturnSaveTaskNullWithLogMessage($"Abort {nameof(HoardBones)}: {Utils.CallChain(nameof(The), nameof(The.Game))} is null");

                if (!game.Running)
                    return ReturnSaveTaskNullWithLogMessage($"Abort {nameof(HoardBones)}: !{Utils.CallChain(nameof(The), nameof(The.Game), nameof(The.Game.Running))}");

                if (DeathEvent?.Dying?.CurrentZone is not Zone currentZone)
                    return ReturnSaveTaskNullWithLogMessage($"Abort {nameof(HoardBones)}: {nameof(currentZone)} is null");

                if (SerializationWriter.Get() is not SerializationWriter writer)
                    return ReturnSaveTaskNullWithLogMessage($"Abort {nameof(HoardBones)}: failed to get {nameof(SerializationWriter)}");

                string message = "Hoarding Bones";

                using var status = Loading.StartTask(message);

                foreach (var zoneGO in currentZone.GetObjects())
                {
                    // Clear object ID's so they get a new one once re-serialzed
                    //zoneGO._BaseID = 0;
                    //zoneGO.RemoveStringProperty("id");

                    zoneGO.SetIntProperty("Tier", zoneGO.GetTier());
                    zoneGO.SetIntProperty("TechTier", zoneGO.GetTechTier());
                    zoneGO.SetStringProperty("UsesSlots", zoneGO.UsesSlots, true);
                    zoneGO.SetStringProperty("Species", zoneGO.GetSpecies(), true);
                    zoneGO.SetStringProperty("Class", zoneGO.GetClass(), true);
                    zoneGO.SetStringProperty("PaintedWall", zoneGO.GetPropertyOrTag("PaintedWall"), true);
                    zoneGO.SetStringProperty("PaintedFence", zoneGO.GetPropertyOrTag("PaintedFence"), true);
                    zoneGO.SetStringProperty("ImprovisedWeapon", $"{(zoneGO.GetPart<MeleeWeapon>()?.IsImprovisedWeapon() is true)}", true);
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

                    writer.Start(XRLGame.SaveVersion);
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
                    return null;
                }
                catch (Exception x)
                {
                    SerializationWriter.Release(writer);
                    BonesHoardError(bonesFilePath, x);
                    return SaveTask = Task.FromException(x);
                }
                finally
                {
                    MemoryHelper.GCCollectMax();
                    _HasSaveBones = null;
                }
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

        public static async Task<IEnumerable<SaveBonesInfo>> GetSaveBonesInfoAsync(
            Predicate<SaveBonesInfo> Where,
            bool TidyPending = false
            )
        {
            var saveBonesInfos = new List<SaveBonesInfo>();
            using var cremateSaveBones = ScopeDisposedList<SaveBonesInfo>.GetFromPool();
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
                            if (TidyPending)
                            {
                                if (!bonesInfo.Pending.EqualsNoCase($"{false}"))
                                {
                                    if (!Options.DebugEnableNoCremation
                                        && SaveGameIDs?.Contains(bonesInfo.Pending) is false
                                        && bonesInfo.Encountered > 0)
                                    {
                                        cremateSaveBones.Add(bonesInfo);
                                        continue;
                                    }
                                    SaveBonesInfo.SetPending(bonesInfo, null).Wait();
                                }
                            }

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

            if (!cremateSaveBones.IsNullOrEmpty())
                foreach (var saveBones in cremateSaveBones)
                    saveBones.Cremate();

            return saveBonesInfos.OrderBy(b => b, SaveBonesInfo.SaveBonesInfoComparerDescending).AsEnumerable();
        }

        public static IEnumerable<SaveBonesInfo> GetSaveBonesInfo(Predicate<SaveBonesInfo> Where)
            => GetSaveBonesInfoAsync(Where)?.Result
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<bool> HasSaveBonesAsync()
            => _HasSaveBones ??= !(await GetSaveBonesInfoAsync(null)).IsNullOrEmpty()
            ;

        public static bool HasSaveBones()
            => HasSaveBonesAsync().Result
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetPendingSaveBonesInfoAsync()
            => await GetSaveBonesInfoAsync(bones => bones.IsPending)
            ;

        public static IEnumerable<SaveBonesInfo> GetPendingSaveBonesInfo()
            => GetPendingSaveBonesInfoAsync()?.Result
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<SaveBonesInfo> GetThisRunPendingSaveBonesInfoAsync()
            => (await GetPendingSaveBonesInfoAsync()).FirstOrDefault(b => b.Pending == The.Game.GameID)
            ;

        public static SaveBonesInfo GetThisRunPendingSaveBonesInfo()
            => GetThisRunPendingSaveBonesInfoAsync()?.Result
            ;

        public static bool TryGetThisRunPendingSaveBonesInfo(out SaveBonesInfo SaveBonesInfo)
            => (SaveBonesInfo = GetThisRunPendingSaveBonesInfo()) != null
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetSaveBonesInfoAsync()
            => await GetSaveBonesInfoAsync(null)
            ;

        public static IEnumerable<SaveBonesInfo> GetSaveBonesInfo()
            => GetSaveBonesInfoAsync()?.Result
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetAvailableSaveBonesInfoAsync()
            => await GetSaveBonesInfoAsync(
                Where: bones
                    => bones.IsLooselyEligible,
                TidyPending: true)
            ;

        public static IEnumerable<SaveBonesInfo> GetAvailableSaveBonesInfo()
            => GetAvailableSaveBonesInfoAsync()?.Result
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
                        throw new FatalDeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is the incorrect version.");
                    }

                    versionNumber = reader.FileVersion;
                    versionString = reader.ReadString();
                    try
                    {
                        if (versionNumber != XRLGame.SaveVersion)
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

                    if (reader.FileVersion < MIN_SAVE_VERSION)
                        throw new FatalDeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is the incorrect version.");

                    if (reader.FileVersion > XRLGame.SaveVersion)
                        throw new DeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is the incorrect version ({versionString}).");

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
                    if (versionNumber < XRLGame.SaveVersion)
                        message = $"That bones file looks like it's from an older save format revision ({versionString}). Sorry!\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";
                    else
                    if (versionNumber > XRLGame.SaveVersion)
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
                _HasSaveBones = null;
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
            foreach (var savedBones in await GetSaveBonesInfoAsync())
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
                    _HasSaveBones = null;
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

        public bool TryGetSaveBonesByID(string BonesID, out SaveBonesInfo SaveBonesInfo)
            => (SaveBonesInfo = GetSavedBonesByID(BonesID)) != null
            ;

        public bool HasBlueprintReplacement(string Blueprint)
            => BlueprintReplacementsByMissingBlueprint.ContainsKey(Blueprint);

        public bool HasTileReplacement(string Blueprint)
            => TileReplacementsByMissingBlueprint.ContainsKey(Blueprint);

        public void RequireAlternativeTileAndBlueprintForGameObject(
            Utils.BlueprintSpec BlueprintSpec,
            out string Blueprint,
            out string Tile
            )
        {
            Blueprint = "Object";
            Tile = "Creatures/sw_mimic.bmp";

            // Utils.Log($"{nameof(RequireAlternativeTileAndBlueprintForGameObject)}: {BlueprintSpec?.DebugName}");
            if (BlueprintSpec.Blueprint is string key)
            {
                if (!TileReplacementsByMissingBlueprint.ContainsKey(key))
                {
                    try
                    {
                        var altBlueprints = Utils.GetAlternativeBlueprintsBySpec(BlueprintSpec);
                        /*altBlueprints.Loggregate(
                            Proc: s => s,
                            Empty: "empty",
                            PostProc: e => $"{1.Indent()}: {e}");*/

                        string altBlueprint = "Object";
                        var altTile = "Creatures/sw_mimic.bmp";

                        if (!altBlueprints.IsNullOrEmpty())
                        {
                            altBlueprint = altBlueprints.GetRandomElementCosmetic();
                            var altModel = GameObjectFactory.Factory.GetBlueprintIfExists(altBlueprint);
                            altTile = altModel?.GetRenderable()?.Tile;
                        }
                        
                        BlueprintReplacementsByMissingBlueprint[key] = altBlueprint;
                        TileReplacementsByMissingBlueprint[key] = altTile;

                        //Utils.Log($"{1.Indent()}{nameof(BlueprintSpec)}: {BlueprintSpec.Blueprint}");
                        //BlueprintSpec.GetDebugLines(2).Loggregate(Empty: $"{2.Indent()}: empty");
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{nameof(RequireAlternativeTileAndBlueprintForGameObject)}({key}), find alternate", x);
                        Blueprint = "Object";
                        Tile = "Creatures/sw_mimic.bmp";
                    }
                }
                BlueprintReplacementsByMissingBlueprint.TryGetValue(key, out Blueprint);
                TileReplacementsByMissingBlueprint.TryGetValue(key, out Tile);
                //Utils.Log($"{1.Indent()}{BlueprintSpec.Blueprint} -> {nameof(Blueprint)}: {Blueprint}, {nameof(Tile)}: {Tile}");
            }
        }

        public void CremateMoonKing(string BonesID)
        {
            if (TryGetSaveBonesByID(BonesID, out var savedBones))
                savedBones.Cremate();
        }

        public static async Task<IEnumerable<SaveBonesInfo>> CremateAllMoonKings(
            Action BeforeDeletionLoop,
            Action AfterDeletionLoop
            )
        {
            var orderedBonesInfos = (await GetSaveBonesInfoAsync())
                ?? Enumerable.Empty<SaveBonesInfo>();

            if (orderedBonesInfos.IsNullOrEmpty())
            {
                await Popup.NewPopupMessageAsync(
                    message: $"There aren't any bones to cremate!",
                    title: "{{yellow|No Bones!}}");
                return orderedBonesInfos;
            }

            var buttons = PopupMessage.AcceptCancelButtonWithoutHotkey;
            if (CapabilityManager.CurrentPlatformClassification() != CapabilityManager.PlatformClassification.PC)
                buttons = PopupMessage.AcceptCancelButton;

            string title = $"Cremate All".Colored("R");
            string confirmText = "CREMATE";
            string typeToConfirmText = "\n\nType '" + confirmText + "' to confirm.";
            string defaultValue = string.Empty;
            if (CapabilityManager.CurrentPlatformClassification() == CapabilityManager.PlatformClassification.Console)
            {
                typeToConfirmText = string.Empty;
                defaultValue = null;
            }
            if ((await Popup.AskStringAsync(
                    Message: "Are you sure you want to cremate {{red|all}} bones?" + typeToConfirmText,
                    Default: defaultValue,
                    WantsSpecificPrompt: confirmText,
                    MaxLength: confirmText.Length)
                ) == confirmText)
            {
                BeforeDeletionLoop?.Invoke();

                int countBefore = orderedBonesInfos.Count();
                int paddingAmount = countBefore.ToString().Length;
                int cremateCounter = 0;
                int crematedCounter = 0;

                string paddedCremateCounter()
                    => cremateCounter.ToString().PadLeft(paddingAmount, '0');

                string cremateString(string Color = null)
                    => $"{paddedCremateCounter().Colored(Color)}/{countBefore}";
                foreach (var bonesInfo in orderedBonesInfos)
                {
                    cremateCounter++;
                    if (bonesInfo == null)
                        continue;

                    Loading.SetLoadingStatus($"Cremating {cremateString()} :: {bonesInfo.Name.Strip()}");

                    crematedCounter++;
                    bonesInfo.Cremate();
                }

                AfterDeletionLoop?.Invoke();

                orderedBonesInfos = (await GetSaveBonesInfoAsync())
                    ?? Enumerable.Empty<SaveBonesInfo>();

                string crematedString = crematedCounter.ToString();
                if (crematedCounter != countBefore)
                    crematedString = crematedString.Colored("red");

                string somethingWrongString = !orderedBonesInfos.IsNullOrEmpty() ? "\n\n{{K|(something went wrong)}}" : null;
                await Popup.NewPopupMessageAsync($"{crematedString}/{countBefore} Bones Cremated!{somethingWrongString}");
                Loading.SetLoadingStatus(null);
            }
            return orderedBonesInfos;
        }

        public static async Task<IEnumerable<SaveBonesInfo>> CremateAllMoonKings()
            => await CremateAllMoonKings(null, null)
            ;

        [WishCommand(Command = "cremate bones")]
        public static bool ClearBones_WishHandler()
        {
            bool success = true;
            Task<IEnumerable<SaveBonesInfo>> task = null;
            try
            {
                task = NavigationController.instance.SuspendContextWhile(CremateAllMoonKings);
                task?.Wait();
                success = task?.IsCompletedSuccessfully is not false;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed to cremate all bones", x);
                success = false;
            }
            return success
                || task?.Result?.IsNullOrEmpty() is false;
        }

        [WishCommand(Command = "go2bones")]
        public static bool Go2Bones_WishHandler()
        {
            bool success = false;
            try
            {
                if (GetPendingSaveBonesInfo().FirstOrDefault(b => b.Pending == The.Game.GameID) is not SaveBonesInfo bonesInfo)
                    Popup.Show("There are no bones pending for this save.");
                else
                {
                    if (The.Player is GameObject player
                        && The.ZoneManager.GetZone(bonesInfo.ZoneID) is Zone zone
                        && player.CurrentCell is Cell currentCell)
                    {
                        if (player.Physics.CurrentCell.RemoveObject(player))
                        {
                            if (zone.GetEmptyCells().GetRandomElement()?.AddObject(player) != null)
                            {
                                The.ZoneManager.SetActiveZone(zone);
                                The.ZoneManager.ProcessGoToPartyLeader();
                                success = true;
                            }
                            else
                            {
                                Popup.Show($"Failed to find a single empty cell in zone {bonesInfo.ZoneID}.");
                                currentCell.AddObject(player);
                            }
                        }
                        else
                            Popup.Show($"Failed to remove player from their current cell {currentCell.ParentZone.ZoneID}[{currentCell.Location}].");
                    }
                    else
                        Popup.Show($"Failed to locate the zone with the pending bones ({bonesInfo.ZoneID}), or the player is in an invalid state.");
                }
            }
            catch (Exception x)
            {
                Utils.Error($"Failed to find bones", x);
                Popup.Show($"Process failed, check Player.log for errors.");
                success = false;
            }
            return success;
        }

        public static async Task<Dictionary<string, string>> GetSaveBonesMenuBarConfigAsync()
        {
            var output = new Dictionary<string, string>();
            string currentPath = null;

            foreach (string bonesPath in BonesPaths)
            {
                try
                {
                    currentPath = bonesPath;
                    if (!Directory.Exists(bonesPath))
                        continue;

                    if (await GetSaveBonesMenuBarConfigRawAsync(bonesPath) is string bonesConfig)
                    {
                        if (bonesConfig.Contains('\n')
                            && bonesConfig.Split('\n') is string[] lines)
                        {
                            foreach (var line in lines)
                            {
                                if (line.Contains(':')
                                    && line.Split(':') is string[] kvp
                                    && kvp.Length > 1
                                    && !kvp[0].IsNullOrEmpty()
                                    && !kvp[1].IsNullOrEmpty())
                                {
                                    if (!output.ContainsKey(kvp[0]))
                                        output[kvp[0]] = kvp[1];
                                }
                            }
                        }
                    }

                }
                catch (Exception x)
                {
                    SeriousBonesError(x, currentPath);
                    output.Clear();
                }
            }

            return output;
        }

        public static async Task<string> GetSaveBonesMenuBarConfigRawAsync(string Directory)
        {
            try
            {
                if (Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("mods")
                    || Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("textures"))
                    return null;

                if (Path.Combine(Directory, "menubar.config") is string path
                    && File.Exists(path))
                {
                    try
                    {
                        return await File.ReadAllTextAsync(path);
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Loading menubar config {path}", x);
                    }
                }
            }
            catch (ThreadInterruptedException x)
            {
                throw x;
            }
            catch (Exception x)
            {
                Utils.Warn(x);
            }
            return null;
        }
    }
}
