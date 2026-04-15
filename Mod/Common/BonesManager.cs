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
using XRL.World.Effects;
using Newtonsoft.Json;
using Random = System.Random;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasGameBasedStaticCache]
    [HasCallAfterGameLoaded]
    [HasWishCommand]
    [Serializable]
    public class BonesManager : IScribedSystem
    {
        #region Consts & PsuedoConsts

        public static string BonesSyncPath => DataManager.SyncedPath("Bones");

        public static string BonesSavePath => DataManager.SavePath("Bones");

        public static DirectoryInfo BonesSaveSyncInfo => DirectoryInfo.NewSync(BonesSyncPath);

        public static DirectoryInfo BonesSavePathInfo => DirectoryInfo.NewLocal(BonesSavePath);

        #endregion
        #region Static Caches

        private static bool _WishContext;
        public static bool WishContext
        {
            get => _WishContext;
            protected set => _WishContext = value;
        }

        [GameBasedStaticCache]
        private static List<string> _SaveGameIDs = null;

        public static IEnumerable<string> SaveGameIDs => _SaveGameIDs ??= SavesAPI.GetSavedGameInfo()?.Result?.Select(info => info.ID)?.ToList();

        public static DirectoryInfo[] BonesPaths => new DirectoryInfo[]
        {
            BonesSaveSyncInfo,
            BonesSavePathInfo,
        };

        [GameBasedStaticCache(CreateInstance = false)]
        public static BonesManager System;

        [ModSensitiveStaticCache(CreateEmptyInstance = false)]
        private static List<string> _RunningMods = null;
        public static IEnumerable<string> RunningMods => _RunningMods ??= ModManager.GetRunningMods().ToList();

        [GameBasedStaticCache(CreateInstance = false)]
        private static bool? _HasSaveBones;

        #endregion
        #region Instance Caches

        protected Dictionary<string, string> TileReplacementsByMissingBlueprint = new();

        protected Dictionary<string, string> BlueprintReplacementsByMissingBlueprint = new();

        #endregion

        [SerializeField]
        private string GameID;

        public bool Initialized;

        [NonSerialized]
        public DirectoryInfo BonesDirectory;

        [NonSerialized]
        public Task SaveTask;

        public int ChancePermyriadForBones => Options.GetPermyriadChanceForBones();

        [NonSerialized]
        public Dictionary<string, bool> Visited = new();
        [NonSerialized]
        public Dictionary<string, bool> Alerted = new();
        [NonSerialized]
        public List<string> Encountered = new();

        public string SeededRandomPrefix => $"{MOD_PREFIX}{GameID}";

        public BonesManager()
        { }

        private static BonesManager InitializeSystem() => new() { GameID = The.Game?.GameID };

        [CallAfterGameLoaded]
        [GameBasedCacheInit]
        public static void BonesManagerSystemInit()
        {
            if (System == null)
            {
                System = The.Game?.RequireSystem(InitializeSystem);
                if (System != null
                    && System.GameID == null)
                    System.GameID = The.Game.GameID;
            }
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
            GameID ??= The.Game.GameID;
            if (Options.EnableOsseousAshDownloads)
            {
                Utils.Log($"(Pretending) to Download Bones");
            }
        }

        #region Serialization

        public override void Write(SerializationWriter Writer)
        {
            Writer.Write(Visited);
            Writer.Write(Alerted);
            Writer.Write(Encountered);
        }

        public override void Read(SerializationReader Reader)
        {
            Visited = Reader.ReadDictionary<string, bool>();
            Alerted = Reader.ReadDictionary<string, bool>();
            Encountered = Reader.ReadList<string>();
        }

        #endregion

        public DirectoryInfo GetBonesDirectory(string FileName = null)
        {
            if (BonesDirectory == default
                || BonesDirectory == DirectoryInfo.Empty)
            {
                BonesDirectory = DirectoryInfo.NewSync(Path.Combine(BonesSyncPath, GameID));
                try
                {
                    BonesDirectory.EnsureExists();
                }
                catch (Exception x)
                {
                    MetricsManager.LogCallingModError(x);
                    BonesDirectory = DirectoryInfo.NewSync(Path.Combine(BonesSavePath, GameID));
                }

                try
                {
                    BonesDirectory.EnsureExists();
                }
                catch (Exception x)
                {
                    MetricsManager.LogCallingModError(x);
                    return DirectoryInfo.Empty;
                }

                BonesDirectory = DirectoryInfo.Empty;
            }
            if (!FileName.IsNullOrEmpty())
                return new(BonesDirectory.Type, Path.Combine(BonesDirectory, FileName));

            return BonesDirectory;
        }

        public DirectoryInfo GetSaveFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.sav.gz")
            ;

        public DirectoryInfo GetInfoFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.json")
            ;

        public static IEnumerable<string> GetBonesPaths(bool NonRemoteOnly = false)
        {
            foreach (var bonesPath in BonesPaths)
                yield return bonesPath;

            if (!NonRemoteOnly
                && Options.EnableOsseousAshDownloads)
                foreach (var bonesPath in OsseousAsh.GetOsseousAshPaths())
                    yield return bonesPath;
        }

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
                    if (zoneGO.Brain is Brain brain
                        && brain.PartyLeader?.IsPlayer() is true)
                        brain.SetPartyLeader(MoonKing, Silent: true);

                    zoneGO.SetIntProperty("Tier", zoneGO.GetTier());
                    zoneGO.SetIntProperty("TechTier", zoneGO.GetTechTier());
                    zoneGO.SetStringProperty("UsesSlots", zoneGO.UsesSlots, true);
                    zoneGO.SetStringProperty("Species", zoneGO.GetSpecies(), true);
                    zoneGO.SetStringProperty("Class", zoneGO.GetClass(), true);
                    zoneGO.SetStringProperty("PaintedWall", zoneGO.GetPropertyOrTag("PaintedWall"), true);
                    zoneGO.SetStringProperty("PaintedFence", zoneGO.GetPropertyOrTag("PaintedFence"), true);
                    zoneGO.SetStringProperty("ImprovisedWeapon", $"{zoneGO.GetPart<MeleeWeapon>()?.IsImprovisedWeapon() is true}", true);
                }

                var bonesFilePath = GetSaveFileFullPath(GameName);
                var bonesInfoPath = GetInfoFileFullPath(GameName);
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

                    var saveBonesJSON = game.CreateSaveBonesJSON(DeathEvent, MoonKing, bonesInfoPath.Type);

                    writer.Start(XRLGame.SaveVersion);
                    writer.Write(SERIALIZATION_CHECK);

                    writer.Write(saveBonesJSON.GameVersion);

                    writer.Write(BONES_SPEC_POS);
                    writer.Write(new BonesSpec(MoonKing, currentZone));

                    writer.Write(BONES_ZONE_POS);

                    writer.WriteBonesZone(currentZone);

                    writer.Write(BONES_FINALIZE_POS);
                    writer.FinalizeWrite();

                    bool restoreBackup = false;
                    try
                    {
                        File.WriteAllText(bonesInfoPath, JsonConvert.SerializeObject(saveBonesJSON, Formatting.Indented));
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

                            if (Options.EnableOsseousAshUploads)
                            {
                               // Do some uploading?
                            }
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
            bool TidyPending = false,
            bool IncludeVersionIncompatible = false
            )
        {
            var saveBonesInfos = new List<SaveBonesInfo>();
            using var cremateSaveBones = ScopeDisposedList<SaveBonesInfo>.GetFromPool();
            string currentPath = null;
            
            foreach (string bonesPath in GetBonesPaths())
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
                            if (!IncludeVersionIncompatible)
                            {
                                if (new XRL.Version(bonesInfo.ModVersion) is XRL.Version bonesVersion
                                    && Utils.ThisMod.Manifest.Version is XRL.Version currentVersion)
                                {
                                    if ((currentVersion.Build >= 3) != (bonesVersion.Build >= 3))
                                    {
                                        Utils.Log($"Skipping incompatible version mismatch, current: {currentVersion}, bones: {bonesVersion}, for BonesID {bonesInfo.ID}");
                                        continue;
                                    }
                                }
                            }
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
                                    SaveBonesInfo.SetPending(bonesInfo, null);
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
            => GetSaveBonesInfoAsync(Where)?.WaitResult()
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<bool> HasSaveBonesAsync()
            => _HasSaveBones ??= !(await GetSaveBonesInfoAsync(null, IncludeVersionIncompatible: true)).IsNullOrEmpty()
            ;

        public static bool HasSaveBones()
            => HasSaveBonesAsync().Result
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetPendingSaveBonesInfoAsync()
            => await GetSaveBonesInfoAsync(bones => bones.IsPending)
            ;

        public static IEnumerable<SaveBonesInfo> GetPendingSaveBonesInfo()
            => GetPendingSaveBonesInfoAsync()?.WaitResult()
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

        public static async Task<IEnumerable<SaveBonesInfo>> GetSaveBonesInfoAsync(bool IncludeVersionIncompatible = false)
            => await GetSaveBonesInfoAsync(null, IncludeVersionIncompatible: IncludeVersionIncompatible)
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetCrematableSaveBonesInfoAsync(bool IncludeVersionIncompatible = false)
            => await GetSaveBonesInfoAsync(
                Where: b => b.DirectoryInfo.Type <= DirectoryInfo.DirectoryType.Synced,
                IncludeVersionIncompatible: IncludeVersionIncompatible)
            ;

        public static IEnumerable<SaveBonesInfo> GetCrematableSaveBonesInfo(bool IncludeVersionIncompatible = false)
            => GetCrematableSaveBonesInfoAsync(IncludeVersionIncompatible)?.WaitResult()
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static IEnumerable<SaveBonesInfo> GetSaveBonesInfo()
            => GetSaveBonesInfoAsync()?.WaitResult()
            ?? Enumerable.Empty<SaveBonesInfo>()
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetAvailableSaveBonesInfoAsync()
            => await GetSaveBonesInfoAsync(
                Where: bones
                    => bones.IsLooselyEligible,
                TidyPending: true)
            ;

        public static IEnumerable<SaveBonesInfo> GetAvailableSaveBonesInfo()
            => GetAvailableSaveBonesInfoAsync()?.WaitResult()
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
            string bonesPath = SaveBonesInfo.FullBonesPathSavGz;

            int versionNumber = -1;
            string versionString = "{unknown}";

            BonesData bonesData = null;
            try
            {
                if (!await File.ExistsAsync(bonesPath))
                {
                    bonesPath = SaveBonesInfo.FullBonesPathSav;
                    if (!await File.ExistsAsync(bonesPath))
                    {
                        Utils.Error($"No saved bones exist. ({SaveBonesInfo.DisplayDirectory})");
                        return bonesData;
                    }
                }

                var reader = SerializationReader.Get();

                var status = Loading.StartTask("Exhuming Moon King");

                try
                {
                    using var stream = File.OpenRead(bonesPath);
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
                        throw new FatalDeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is for incorrect game version.");
                    }

                    versionNumber = reader.FileVersion;
                    versionString = reader.ReadString();
                    try
                    {
                        if (versionNumber != XRLGame.SaveVersion)
                        {
                            string backupPath = bonesPath + $"_upgradebackup_{versionNumber}.gz";
                            if (!File.Exists(backupPath))
                            {
                                File.Copy(bonesPath, backupPath);
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
                        throw new FatalDeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is for incorrect game version.");

                    if (reader.FileVersion > XRLGame.SaveVersion)
                        throw new DeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) " +
                            $"is for incorrect game version ({versionString}).");

                    BonesSpec bonesSpec = null;
                    try
                    {
                        if (reader.ReadInt32() != BONES_SPEC_POS)
                            throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing {nameof(BonesSpec)} val-check.");

                        bonesSpec = reader.ReadComposite<BonesSpec>();
                    }
                    catch (Exception x)
                    {
                        bonesSpec = null;
                        reader.Errors++;
                        reader.UnspoolTo(BONES_ZONE_POS, Prior: true);
                        Utils.Error($"Failed to read {nameof(BonesSpec)}, recovery will be attempted", x);
                    }

                    string bonesID = SaveBonesInfo.ID;
                    Zone loadedZone = null;
                    try
                    {
                        if (reader.ReadInt32() != BONES_ZONE_POS)
                            throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing {nameof(Zone)} val-check.");

                        loadedZone = reader.ReadBonesZone();
                        if (loadedZone != null
                            && loadedZone.Z > 21)
                            loadedZone.Z = Stat.Random(16, 21);
                    }
                    catch (Exception x)
                    {
                        loadedZone = null;
                        reader.Errors++;
                        reader.UnspoolTo(BONES_FINALIZE_POS, Prior: true);
                        Utils.Error(x);
                    }

                    try
                    {
                        if (reader.ReadInt32() != BONES_FINALIZE_POS)
                            throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing Finalization val-check.");

                        reader.FinalizeRead();

                        if (bonesSpec == null)
                        {
                            if (loadedZone.GetFirstObject(go => go.HasLunarRegentBonesID(bonesID)) is not GameObject lunarRegent)
                                throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing Lunar Regent for {nameof(BonesSpec)}.");

                            bonesSpec = new BonesSpec(lunarRegent, loadedZone);
                        }


                        bonesData = new(bonesID, ZoneID, loadedZone);
                    }
                    catch (Exception x)
                    {
                        reader.Errors++;
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
                    $"you can try to restore the backup in your bones folder ({SaveBonesInfo.BonesBakDisplay}) " +
                    $"by removing the 'bak' file extension.";

                if (ModManager.TryGetStackMod(x, out var Mod, out var Frame))
                {
                    if (Frame.GetMethod() is MethodBase method)
                    {
                        string culpritMethod = method.DeclaringType?.FullName + "." + method.Name;
                        Mod.Error(culpritMethod + "::" + x);
                        message = $"That bones file is likely not loading because of a mod error from " +
                            $"{Mod.DisplayTitleStripped} ({culpritMethod}), make sure the correct mods are enabled or contact the mod author.";
                    }
                }
                else
                {
                    if (versionNumber < XRLGame.SaveVersion)
                        message = $"That bones file looks like it's from an older game save format revision ({versionString}). Sorry!\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";
                    else
                    if (versionNumber > XRLGame.SaveVersion)
                        message = $"That bones file looks like it's from a newer game save format revision ({versionString}).\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";

                    MetricsManager.LogException($"{nameof(BonesManager)}.{nameof(ExhumeMoonKing)}::", x, "serialization_error");
                }

                Utils.Error(message);

                throw;
            }
            finally
            {
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

        public BonesData ExhumeMoonKing(string ZoneID, SaveBonesInfo SaveBonesInfo)
        {
            var exhumation = ExhumeMoonKingAsync(ZoneID, SaveBonesInfo);
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
            var orderedBonesInfos = (await GetSaveBonesInfoAsync(IncludeVersionIncompatible: true))
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

        public bool GetZoneIDSeededChanceForEncounter(Zone Z)
            => Stat.SeededRandom($"{SeededRandomPrefix}{Z.ZoneID}", 1, 10000) <= ChancePermyriadForBones
            ;

        public bool IsWorldMapOrVisited(Zone Z)
            => Z.IsWorldMap()
            || Visited.ContainsKey(Z.ZoneID)
            ;

        public bool AttemptLoadBones(Zone Z, SaveBonesInfo PickedBones)
        {
            Alerted ??= new();
            BonesData bonesData = null;
            try
            {
                bonesData = BonesData.GetFromSaveBonesInfo(Z.ZoneID, PickedBones);
            }
            catch (FatalDeserializationVersionException)
            {
                if (!Alerted.TryGetValue(Z.ZoneID, out bool alerted)
                    || !alerted)
                {
                    Popup.ShowYesNo(
                        Message: $"This bones file has encountered a fatal deserialization exception on the basis of the save version and will likely never load.\n\n" +
                        $"If you would like to try and recover it, select {PopupMessage.YesNoButton[1].text}, force the zone to load when asked, and then please contact {Utils.AuthorOnPlatforms} with a copy of the bones file: {DataManager.SanitizePathForDisplay(PickedBones.Directory)}.\n\n" +
                        $"Alternatively, it is recommended that you cremate this bones file, so that it doesn't continue to create issues.\n\n" +
                        $"Would you like to cremate this bones file?",
                        callback: (result) =>
                        {
                            if (result == DialogResult.Yes)
                                PickedBones.Cremate();
                        });
                    Alerted[Z.ZoneID] = true;
                }
                throw;
            }
            catch (DeserializationVersionException)
            {
                if (!Alerted.TryGetValue(Z.ZoneID, out bool alerted)
                    || !alerted)
                {
                    Popup.ShowYesNo(
                        Message: $"This bones file has encountered a deserialization version exception on the basis of the save version and will likely not load this run.\n\n" +
                        $"This bones is for game version {PickedBones.Version}, and will likely load in an appropriately versioned run. If you would like to keep this bones so that it might appear in run on that version of the game, select {PopupMessage.YesNoButton[1].text}, and then force the zone to load when asked.\n\n" +
                        $"Alternatively, if you're unlikely to switch versions of the game, it is recommended that you cremate this bones file, so that it doesn't continue to create issues.\n\n" +
                        $"Would you like to cremate this bones file?",
                        callback: (result) =>
                        {
                            if (result == DialogResult.Yes)
                                PickedBones.Cremate();
                        });
                    Alerted[Z.ZoneID] = true;
                }
                throw;
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(BonesManager), nameof(AttemptLoadBones)), x);
                bonesData = null;
            }

            if (bonesData == null)
                return false;

            if (Z.GetZoneProperty(nameof(BonesData.BonesID), null) is string existingBonesID)
            {
                if (existingBonesID != bonesData.BonesID)
                    Utils.Warn($"Loading {nameof(SaveBonesInfo)} for zone that has already loaded a different bones: " +
                        $"{nameof(existingBonesID)} {existingBonesID}, {nameof(bonesData)}.{nameof(bonesData.BonesID)} {bonesData.BonesID}. " +
                        $"Zone may have errors.");
                else
                    Utils.Warn($"{nameof(SaveBonesInfo)} for zone that has already loaded this bones: " +
                        $"{nameof(existingBonesID)} {existingBonesID}, {nameof(bonesData)}.{nameof(bonesData.BonesID)} {bonesData.BonesID}. " +
                        $"Zone may have errors.");
            }

            if (bonesData.Apply(Z, out GameObject lunarRegent, PickedBones.IsMad))
            {
                Z.GetCell(0, 0).AddObject(ANNOUNCER_WIDGET, Context: $"{nameof(UD_Bones_MoonKingAnnouncer.BonesID)}::{bonesData.BonesID}");
                Encountered.Add(bonesData.BonesID);
                Z.SetZoneProperty(nameof(bonesData.BonesID), bonesData.BonesID);
                try
                {
                    SaveBonesInfo.IncrementEncountered(PickedBones);
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed to increment {nameof(SaveBonesInfo)}.{nameof(SaveBonesInfo.Encountered)}", x);
                }
                return true;
            }
            return false;
        }

        public void CheckBones(Zone Z)
        {
            Visited ??= new();
            Encountered ??= new();

            if (IsWorldMapOrVisited(Z))
                return;

            Visited.Add(Z.ZoneID, value: true);

            if (Options.DebugEnableNoExhuming
                && !Options.DebugEnablePickingBones)
                return;

            if (The.Player is not GameObject player)
                return;

            var playerSpec = new BonesSpec(player, Z);
            if (GetSaveBonesInfo(b => !Encountered.Contains(b.ID) && b.BonesSpec?.IsWithinSpec(playerSpec) is true) is IEnumerable<SaveBonesInfo> bonesInfos
                && !bonesInfos.IsNullOrEmpty())
            {
                bool byChance = GetZoneIDSeededChanceForEncounter(Z);
                if (byChance
                    || Options.DebugEnableForcePickingBones)
                {
                    int selection = 0;
                    if (Options.DebugEnablePickingBones)
                    {
                        using var bonesList = ScopeDisposedList<SaveBonesInfo>.GetFromPoolFilledWith(
                            items: bonesInfos.OrderBy(b => b, SaveBonesInfo.SaveBonesInfoComparerDescending));

                        using var optionsList = ScopeDisposedList<string>.GetFromPool();
                        using var renderList = ScopeDisposedList<BonesRender>.GetFromPool();
                        using var hotkeyList = ScopeDisposedList<char>.GetFromPool();

                        // Add None Please:
                        optionsList.Add("none please");
                        renderList.Add(new(GameObjectFactory.Factory.GetBlueprintIfExists("Lunar Face"), false));
                        renderList[0].TileColor = "&K";
                        renderList[0].DetailColor = 'K';
                        hotkeyList.Add('n');

                        // Add Roll It!:
                        if (Options.DebugEnableForcePickingBones)
                        {
                            optionsList.Add("{{yellow|roll it!}}");
                            renderList.Add(new("Abilities/sw_skill_pointed_circle.png", ColorString: "&y", TileColor: "&y", DetailColor: 'W'));
                            hotkeyList.Add('r');
                        }

                        int offset = optionsList.Count;

                        foreach (var bones in bonesList)
                        {
                            renderList.Add(bones.Render);
                            string bonesOption = bones.GetName();
                            if (bones.GetBonesJSON() is SaveBonesJSON bonesJSON)
                                bonesOption = $"{bonesOption}, {nameof(bonesJSON.Level)}: {bonesJSON.Level}, {nameof(BonesSpec.ZoneTier)}: {bonesJSON.BonesSpec.ZoneTier},\n" +
                                    $"{nameof(BonesSpec.ZoneTerrainType)}: \"{bonesJSON.BonesSpec.ZoneTerrainType}\", {nameof(SaveBonesJSON.ZoneID)}: {bonesJSON.ZoneID}";
                            optionsList.Add(bonesOption);
                            hotkeyList.Add(' ');
                        }

                        var icon = new BonesRender(GameObjectFactory.Factory.GetBlueprintIfExists("Lunar Face"), HFlip: false, IsMad: true);

                        if (bonesInfos.Any(b => b.IsMad))
                            icon.SetTile(MOON_KING_FEVER_TILE);

                        string neutralRegalTitle = UD_Bones_MoonKingFever.REGAL_TITLE.Pluralize();

                        while (true)
                        {
                            var picked = Popup.PickOptionAsync(
                                Title: $"Eligible =LunarShader:{neutralRegalTitle}:*= For This Zone".StartReplace().ToString(),
                                Intro: $"Pick a lunar regent to exhume{(Options.DebugEnableForcePickingBones ? ", or {{yellow|roll it!}}" : ".")}",
                                Options: optionsList,
                                Icons: renderList,
                                IntroIcon: icon,
                                AllowEscape: true);

                            picked.Wait();
                            selection = picked.Result;
                            if (selection >= offset
                                && bonesList.TakeAt(selection - offset) is SaveBonesInfo pickedBones)
                            {
                                try
                                {
                                    if (AttemptLoadBones(Z, pickedBones))
                                        break;
                                }
                                catch (Exception x)
                                {
                                    Utils.Error($"Failed to load {nameof(BonesData)} for {nameof(SaveBonesInfo)}.{nameof(pickedBones.ID)} {pickedBones.ID}", x);
                                }
                                optionsList.RemoveAt(selection);
                                renderList.RemoveAt(selection);
                                hotkeyList.RemoveAt(selection);
                                continue;
                            }
                            Z.SetZoneProperty(nameof(BonesData.BonesID), null);
                            break;
                        }
                    }

                    if (!Options.DebugEnablePickingBones
                        || (selection == 1
                            && byChance))
                    {
                        var bonesBag = new BallBag<SaveBonesInfo>();
                        foreach (var bonesInfo in bonesInfos)
                        {
                            if (bonesInfo.GetBonesWeight() is int weight)
                                bonesBag.Add(bonesInfo, weight);
                        }
                        while (!bonesBag.IsNullOrEmpty()
                            && bonesBag.PickOne() is SaveBonesInfo pickedBones)
                        {
                            try
                            {
                                if (AttemptLoadBones(Z, pickedBones))
                                    break;
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"Failed to load {nameof(BonesData)} for {nameof(SaveBonesInfo)}.{nameof(pickedBones.ID)} {pickedBones.ID}", x);
                            }
                        }
                    }
                }
            }
        }

        public override void Register(XRLGame Game, IEventRegistrar Registrar)
        {
            Registrar.Register(AfterZoneBuiltEvent.ID);
            base.Register(Game, Registrar);
        }

        public override bool HandleEvent(AfterZoneBuiltEvent E)
        {
            CheckBones(E.Zone);
            return base.HandleEvent(E);
        }

        #region Wishes

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
                    if (bonesInfo.BonesSpec.ZoneID is string zoneID)
                    {
                        if (The.Player is GameObject player
                        && The.ZoneManager.GetZone(bonesInfo.BonesSpec.ZoneID) is Zone zone
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
                                    Popup.Show($"Failed to find a single empty cell in zone {zoneID}.");
                                    currentCell.AddObject(player);
                                }
                            }
                            else
                                Popup.Show($"Failed to remove player from their current cell {currentCell.ParentZone.ZoneID}[{currentCell.Location}].");
                        }
                        else
                            Popup.Show($"Failed to locate the zone with the pending bones ({zoneID}), or the player is in an invalid state.");
                    }
                    else
                        Popup.Show($"Weird {nameof(SaveBonesInfo)} with no {nameof(BonesSpec)}.");
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

        [WishCommand(Command = "manage bones report visited")]
        public static bool ReportVisited_WishHandler()
        {
            try
            {
                string title = $"{nameof(BonesManager)} Visited Zones";
                Utils.Log($"{title}:");
                System?.Visited.Loggregate(
                    Proc: kvp => $"{kvp.Key}: {kvp.Value}",
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}: {s}");
                if (System == null)
                    Utils.Log($"{1.Indent()}: System not instantiated (this is probably a bug).");

                Popup.Show(
                    Message: System?.Visited?.Aggregate(
                        seed: (string)null,
                        func: (a,n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent()}: {n.Key}: {n.Value}"))
                        ?? $"{1.Indent()}: {"System not instantiated".Colored("red")} (this is probably a bug).",
                    Title: title.Colored("yellow"));
            }
            catch (Exception x)
            {
                Utils.Error($"Wish: \"{"manage bones report visited"}\"", x);
                return false;
            }

            return true;
        }

        [WishCommand(Command = "manage bones report encountered")]
        public static bool ReportEncountered_WishHandler()
        {
            try
            {
                string title = $"{nameof(BonesManager)} Encountered Bones";
                Utils.Log($"{title}:");
                System?.Encountered.Loggregate(
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}: {s}");
                if (System == null)
                    Utils.Log($"{1.Indent()}: System not instantiated (this is probably a bug).");

                Popup.Show(
                    Message: System?.Encountered?.Aggregate(
                        seed: (string)null,
                        func: (a,n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent()}: {n}"))
                        ?? $"{1.Indent()}: {"System not instantiated".Colored("red")} (this is probably a bug).",
                    Title: title.Colored("yellow"));
            }
            catch (Exception x)
            {
                Utils.Error($"Wish: \"{"manage bones report encountered"}\"", x);
                return false;
            }

            return true;
        }

        #endregion
        #region Temp

        /*public static async Task<Dictionary<string, string>> GetSaveBonesMenuBarConfigAsync()
        {
            var output = new Dictionary<string, string>();
            string currentPath = null;

            foreach (string bonesPath in GetBonesPaths())
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
                                    string key = kvp[0];
                                    using var valueElements = ScopeDisposedList<string>.GetFromPoolFilledWith(kvp);
                                    valueElements.RemoveAt(0);
                                    if (!output.ContainsKey(kvp[0]))
                                        output[key] = valueElements.Aggregate("", (a,n) => Utils.DelimitedAggregator(a, n, ":"));
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
        }*/

        #endregion
    }
}
