using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

using Cysharp.Text;

using Platform;
using Platform.IO;
using UnityEngine;

using Qud.API;
using Qud.UI;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.Collections;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.UI.Framework;
using XRL.Wish;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.WorldBuilders;

using UD_Bones_Folder.Mod.UI;
using UD_Bones_Folder.Mod.Events;
using static UD_Bones_Folder.Mod.Const;

using ColorUtility = ConsoleLib.Console.ColorUtility;
using CompressionLevel = System.IO.Compression.CompressionLevel;

using GameObject = XRL.World.GameObject;
using Event = XRL.World.Event;
using XRL.Core;
using UD_Bones_Folder.Mod.Serialization.PseudoTypes;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasGameBasedStaticCache]
    [HasCallAfterGameLoaded]
    [HasWishCommand]
    [Serializable]
    public class BonesManager
        : IScribedSystem
        , ILoadLunarRegentEventHandler
        , ILoadLunarCourtierEventHandler
    {
        #region Consts & PsuedoConsts

        public static string BonesFileName => "LunarRegent";

        public static string BonesSyncPath => DataManager.SyncedPath("Bones");

        public static string BonesSavePath => DataManager.SavePath("Bones");

        public static FileLocationData BonesSaveSyncInfo => FileLocationData.NewSynced(BonesSyncPath);

        public static FileLocationData BonesSavePathInfo => FileLocationData.NewLocal(BonesSavePath);

        public static string NoBones => nameof(NoBones);

        public static List<string> PartsLunarRegentsShouldNotHave = new List<string>
        {
            nameof(OpeningStory),
        };

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

        public static FileLocationData[] BonesPaths => new FileLocationData[]
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

        [NonSerialized]
        public EmbarkBuilder EmbarkBuilder;

        #endregion
        #region Instance Caches

        protected Dictionary<string, string> TileReplacementsByMissingBlueprint = new();

        protected Dictionary<string, string> BlueprintReplacementsByMissingBlueprint = new();

        #endregion

        [SerializeField]
        private string GameID;

        public bool Initialized;

        [NonSerialized]
        public FileLocationData BonesDirectory;

        [NonSerialized]
        public Task SaveTask;

        public int ChancePermyriadForBones => Options.GetPermyriadChanceForBones();

        [NonSerialized]
        public StringMap<string> ZoneBones = new();
        [NonSerialized]
        public Dictionary<string, bool> Alerted = new();
        [NonSerialized]
        public List<string> Encountered = new();
        [NonSerialized]
        public List<string> FailedToLoadBones = new();

        public string SeededRandomPrefix => $"{MOD_PREFIX}{GameID}";

        [SerializeField]
        private GlobalLocation _StartingLocation;
        public GlobalLocation StartingLocation
        {
            get
            {
                if (_StartingLocation.IsNullOrEmpty()
                    && (EmbarkBuilder ??= EmbarkBuilder.gameObject.GetComponent<EmbarkBuilder>()) is EmbarkBuilder builder)
                    _StartingLocation = builder.info.fireBootEvent(
                        id: QudGameBootModule.BOOTEVENT_BOOTSTARTINGLOCATION,
                        game: The.Game,
                        element: new GlobalLocation());

                return _StartingLocation;
            }
        }

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
                // Utils.Log($"(Pretending) to Download Bones");
            }
        }

        [ModSensitiveCacheInit]
        public static void AddAchievement()
        {
            if (AchievementManager.State?.Stats != null)
                if (!AchievementManager.State.Stats.ContainsKey("STAT_WEAR_FACE_7"))
                    StatInfo.Create("STAT_WEAR_FACE_7", 1);
        }

        #region Serialization

        public override void Write(SerializationWriter Writer)
        {
            Writer.WriteComposite(ZoneBones);
            Writer.Write(Alerted);
            Writer.Write(Encountered);
            Writer.Write(FailedToLoadBones);
        }

        public override void Read(SerializationReader Reader)
        {
            ZoneBones = Reader.ReadComposite<StringMap<string>>();
            Alerted = Reader.ReadDictionary<string, bool>();
            Encountered = Reader.ReadList<string>();
            FailedToLoadBones = Reader.ReadList<string>();
        }

        #endregion

        public FileLocationData GetBonesLocationData()
        {
            if (BonesDirectory is null)
            {
                BonesDirectory = FileLocationData.NewSynced(Path.Combine(BonesSyncPath, GameID));
                try
                {
                    BonesDirectory.EnsureExists();
                }
                catch (Exception x)
                {
                    MetricsManager.LogCallingModError(x);
                    BonesDirectory = FileLocationData.NewSynced(Path.Combine(BonesSavePath, GameID));
                }

                try
                {
                    BonesDirectory.EnsureExists();
                }
                catch (Exception x)
                {
                    MetricsManager.LogCallingModError(x);
                    return null;
                }
            }
            return BonesDirectory;
        }

        public static string GetSaveFileName(string FileName = null)
            => $"{FileName ?? BonesFileName}.sav.gz"
            ;

        public static string GetInfoFileName(string FileName = null)
            => $"{FileName ?? BonesFileName}.json"
            ;

        public static IEnumerable<FileLocationData> GetBonesFileLocationData(bool NonRemoteOnly = false)
        {
            foreach (var bonesPath in BonesPaths)
                yield return bonesPath;

            if (!NonRemoteOnly
                && Options.EnableOsseousAshDownloads)
                foreach (var bonesPath in OsseousAsh.GetOsseousAshFileLocationData())
                    yield return bonesPath;
        }

        public static SaveBonesInfo DummyBonesInfo
            => SaveBonesInfo.DummyBonesInfo(
                VisibilityMode: BonesManagement.instance?.VisibilityMode ?? BonesManagement.VisibilityModes.All,
                LocationData: BonesSaveSyncInfo)
            ;

        private Task ReturnSaveTaskNullWithLogMessage(string Message = null)
        {
            Message.Log();
            return SaveTask = null;
        }

        public static void PrepareZoneObjectsForNextRun(Zone Z, GameObject LunarRegent, string GameID)
        {
            foreach (var zoneGO in Z.GetObjects())
            {
                zoneGO.PerformActionRecursively(delegate (GameObject go)
                {
                    if (go.TryGetPart(out Examiner examiner))
                        examiner.EpistemicStatus = Examiner.EPISTEMIC_STATUS_UNINITIALIZED;

                    go.SetIntProperty("Tier", go.GetTier());
                    go.SetIntProperty("TechTier", go.GetTechTier());
                    go.SetStringProperty("UsesSlots", go.UsesSlots, true);
                    go.SetStringProperty("Species", go.GetSpecies(), true);
                    go.SetStringProperty("Class", go.GetClass(), true);
                    go.SetStringProperty("PaintedWall", go.GetPropertyOrTag("PaintedWall"), true);
                    go.SetStringProperty("PaintedFence", go.GetPropertyOrTag("PaintedFence"), true);
                    go.SetStringProperty("ImprovisedWeapon", $"{go.GetPart<MeleeWeapon>()?.IsImprovisedWeapon() is true}", true);
                });

                if (zoneGO.Brain is Brain brain)
                {
                    if (zoneGO.IsPlayerLed())
                    {
                        if (brain.Allegiance?.SourceID == The.Player.BaseID)
                        {
                            var reasonType = brain.Allegiance.Reason.GetType();
                            var courtierPart = zoneGO.RequirePart<UD_Bones_LunarCourtier>()
                                .OverrideBonesIDTyped<UD_Bones_LunarCourtier>(GameID);
                            courtierPart.AllyReasonType = reasonType;
                            courtierPart.Persists = true;
                            // Utils.Log($"{zoneGO.DebugName} is PlayerLed: {reasonType.Name ?? "NO_TYPE"}");
                        }
                    }
                }
            }
        }

        public Task HoardBones(
            IDeathEvent DeathEvent,
            GameObject LunarRegent
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

                if (LunarRegent?.CurrentZone is not Zone currentZone)
                    return ReturnSaveTaskNullWithLogMessage($"Abort {nameof(HoardBones)}: {nameof(currentZone)} is null");

                if (SerializationWriter.Get() is not SerializationWriter writer)
                    return ReturnSaveTaskNullWithLogMessage($"Abort {nameof(HoardBones)}: failed to get {nameof(SerializationWriter)}");

                string message = "Hoarding Bones";

                using var status = Loading.StartTask(message);

                PrepareZoneObjectsForNextRun(currentZone, LunarRegent, GameID);

                var fileLocationData = GetBonesLocationData();
                var bonesFilePath = fileLocationData.WithFileName(GetSaveFileName());
                var bonesInfoPath = fileLocationData.WithFileName(GetInfoFileName());
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

                    var saveBonesJSON = game.CreateSaveBonesJSON(DeathEvent, LunarRegent);

                    writer.Start(XRLGame.SaveVersion);
                    writer.Write(SERIALIZATION_CHECK);

                    writer.Write(saveBonesJSON.GameVersion);

                    var bonesSpec = new BonesSpec(LunarRegent, currentZone);

                    writer.Write(BONES_SPEC_POS);
                    writer.WriteComposite(bonesSpec);

                    writer.Write(BONES_ZONE_POS);

                    PseudoZone pseudoZone = null;
                    if (Utils.ModVersion < PseudoZone.MinVersion)
                        writer.WriteBonesZone(currentZone);
                    else
                    {
                        pseudoZone = PseudoZone.FromZone(currentZone);
                        writer.WriteComposite(pseudoZone);
                    }

                    writer.Write(BONES_FINALIZE_POS);

                    pseudoZone?.PrepForFinalizeWrite();
                    writer.FinalizeWrite();
                    pseudoZone.UnprepForFinalizeWrite();

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
                            var writeBuffer = memoryStream.ToArray();
                            File.WriteAllBytes(bonesFilePath, writeBuffer);

                            if (Options.EnableOsseousAshUploads)
                            {
                                if (!writeBuffer.IsNullOrEmpty())
                                {
                                    try
                                    {
                                        OsseousAsh.TryUploadBones(GameID, saveBonesJSON, writeBuffer).Wait();
                                    }
                                    catch (Exception x)
                                    {
                                        Utils.Error($"{nameof(HoardBones)} failed to upload Bones with BonesID {GameID}", x);
                                    }
                                }
                                else
                                    Utils.Warn($"{nameof(writeBuffer)} is null or empty for BonesID {GameID}. Upload aborted.");

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

        public static void BonesHoardError(
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

        public static bool IsVersionCompatible(SaveBonesInfo SaveBonesInfo)
        {
            if (SaveBonesInfo.GetModVersion() is XRL.Version saveBonesVersion
                && Utils.ModVersion is XRL.Version modVersion)
            {
                bool isMisMatch = false;

                if (modVersion.Build != saveBonesVersion.Build)
                    isMisMatch = true;

                if (modVersion.Revision < saveBonesVersion.Revision)
                    isMisMatch = true;

                if (!Options.EnableBonesFromEarlierModVersions
                    && modVersion != saveBonesVersion)
                    isMisMatch = true;

                if (isMisMatch)
                {
                    // Utils.Log($"Version mismatch, current: {currentVersion}, bones: {bonesVersion}, for BonesID {SaveBonesInfo.ID}");
                    return false;
                }
            }
            return true;
        }

        public static async Task<IEnumerable<SaveBonesInfo>> GetSaveBonesInfoAsync(
            Predicate<SaveBonesInfo> Where,
            bool IncludeVersionIncompatible = false,
            bool IncludeBlocked = false,
            SaveBonesInfo NoBonesPlaceholder = null
            )
        {
            //Utils.Log($"GetSaveBonesInfoAsync - {nameof(GameManager)}.{nameof(GameManager.AwakeComplete)}: {GameManager.AwakeComplete}");
            var saveBonesInfos = new List<SaveBonesInfo>();

            FileLocationData currentLocationData = null;
            
            foreach (var bonesFileLocationData in GetBonesFileLocationData(NonRemoteOnly: true))
            {
                try
                {
                    currentLocationData = bonesFileLocationData;
                    if (!bonesFileLocationData.Exists())
                        continue;

                    foreach (var bonesFolder in await bonesFileLocationData.EnumerateDirectoriesAsync())
                    {
                        if (await SaveBonesInfo.GetPhysicalSaveBonesInfoAsync(bonesFolder) is SaveBonesInfo savedBonesInfo)
                        {
                            try
                            {
                                if (!IncludeVersionIncompatible
                                    && !IsVersionCompatible(savedBonesInfo))
                                    continue;

                                if (Where?.Invoke(savedBonesInfo) is not false)
                                {
                                    if (saveBonesInfos.FirstOrDefault(b => b.ID == savedBonesInfo.ID) is SaveBonesInfo existingInfo
                                        && existingInfo.FileLocationData != null)
                                    {
                                        existingInfo.FileLocationData = bonesFolder;
                                        continue;
                                    }
                                    saveBonesInfos.Add(savedBonesInfo);
                                }
                            }
                            catch (Exception x)
                            {
                                Utils.Warn($"Exception adding saved BonesInfo {savedBonesInfo.ID}, {currentLocationData}: {x}");
                            }
                        }
                    }
                }
                catch (Exception x)
                {
                    SeriousBonesError(x, currentLocationData);
                    saveBonesInfos.Clear();
                }
            }

            if (Options.EnableOsseousAshDownloads)
            {
                try
                {
                    foreach (var onlineBonesInfo in OsseousAsh.GetBonesInfos().IteratorSafe())
                    {
                        currentLocationData = onlineBonesInfo.FileLocationData;
                        try
                        {
                            if (!IncludeBlocked
                                && onlineBonesInfo.IsBlocked)
                                continue;

                            if (!IncludeVersionIncompatible
                                && !IsVersionCompatible(onlineBonesInfo))
                                continue;

                            if (Where?.Invoke(onlineBonesInfo) is not false)
                            {
                                if (saveBonesInfos.FirstOrDefault(b => b.ID == onlineBonesInfo.ID) is SaveBonesInfo existingInfo
                                    && existingInfo.FileLocationData != null)
                                {
                                    existingInfo.FileLocationData = onlineBonesInfo.FileLocationData;
                                    continue;
                                }
                                saveBonesInfos.Add(onlineBonesInfo);
                            }
                        }
                        catch (Exception x)
                        {
                            Utils.Warn($"Exception adding online BonesInfo {onlineBonesInfo.ID}, {onlineBonesInfo}: {x}");
                        }
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed to retrieve any online BonesInfos", x);
                }
            }

            if (saveBonesInfos.IsNullOrEmpty()
                && NoBonesPlaceholder != null)
            {
                saveBonesInfos.Add(NoBonesPlaceholder);
            }

            return saveBonesInfos
                .OrderBy(b => b, SaveBonesInfo.SaveBonesInfoComparerDescending)
                .AsEnumerable();
        }

        public static IEnumerable<SaveBonesInfo> GetSaveBonesInfo(Predicate<SaveBonesInfo> Where)
            => GetSaveBonesInfoAsync(Where)
                .WaitResult()
                .IteratorSafe()
            ;

        public static async Task<bool> HasSaveBonesAsync()
            => _HasSaveBones ??= !(await GetSaveBonesInfoAsync(
                    Where: null,
                    IncludeVersionIncompatible: true,
                    NoBonesPlaceholder: DummyBonesInfo)
                ).IsNullOrEmpty()
            ;

        public static bool HasSaveBones()
            => HasSaveBonesAsync().WaitResult()
            ;

        public static void ClearHasSaveBones()
            => _HasSaveBones = null
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetSaveBonesInfoAsync(bool IncludeVersionIncompatible = false)
            => await GetSaveBonesInfoAsync(null, IncludeVersionIncompatible: IncludeVersionIncompatible)
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetCrematableSaveBonesInfoAsync(bool IncludeVersionIncompatible = false)
            => await GetSaveBonesInfoAsync(
                Where: b => b.FileLocationData.Type <= FileLocationData.LocationType.Synced,
                IncludeVersionIncompatible: IncludeVersionIncompatible)
            ;

        public static IEnumerable<SaveBonesInfo> GetCrematableSaveBonesInfo(bool IncludeVersionIncompatible = false)
            => GetCrematableSaveBonesInfoAsync(IncludeVersionIncompatible)
                .WaitResult()
                .IteratorSafe()
            ;

        public static IEnumerable<SaveBonesInfo> GetSaveBonesInfo(
            Predicate<SaveBonesInfo> Where,
            bool IncludeVersionIncompatible = false,
            bool IncludeBlocked = false,
            SaveBonesInfo NoBonesPlaceholder = null
            )
            => GetSaveBonesInfoAsync(Where, IncludeVersionIncompatible, IncludeBlocked, NoBonesPlaceholder)
                .WaitResult()
                .IteratorSafe()
            ;

        public static IEnumerable<SaveBonesInfo> GetSaveBonesInfo()
            => GetSaveBonesInfoAsync()
                .WaitResult()
                .IteratorSafe()
            ;

        public async Task<IEnumerable<SaveBonesInfo>> GetEligibleSaveBonesInfoAsync(Predicate<SaveBonesInfo> Where = null)
            => await GetSaveBonesInfoAsync(bones => bones.IsLooselyEligible && (Where?.Invoke(bones) is not false))
            ;

        public IEnumerable<SaveBonesInfo> GetEligibleSaveBonesInfo(Predicate<SaveBonesInfo> Where = null)
            => GetEligibleSaveBonesInfoAsync(Where)
                .WaitResult()
                .IteratorSafe()
            ;

        private static void SeriousBonesError(Exception X, FileLocationData FileLocationData)
        {
            MetricsManager.LogError("Error checking for save files", X);
            using var sB = ZString.CreateStringBuilder();
            if (X is UnauthorizedAccessException
                || X is System.IO.IOException)
            {
                sB.AppendLine("There was a permission error while trying to access your bones directory.");
                sB.AppendLine();
                sB.AppendLine(ColorUtility.EscapeFormatting(X.Message));
            }
            else
            {
                sB.AppendLine("There was an error while trying to access your bones directory.");
                sB.AppendLine();
                sB.Append("Directory: ");
                sB.AppendLine(ColorUtility.EscapeFormatting(FileLocationData.SanitiseForDisplay()));
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

        public async Task<BonesData> ExhumeLunarRegentAsync(SaveBonesInfo SaveBonesInfo, bool NoStatus = false)
        {
            int versionNumber = -1;
            string versionString = "{unknown}";

            BonesData bonesData = null;

            bool errorPopups = XRL.UI.Options.ShowErrorPopups;
            try
            {
                var reader = SerializationReader.Get();

                Loading.Status status = default;
                if (!NoStatus)
                    status = Loading.StartTask($"Exhuming Lunar Regent");

                Utils.Info($"Attempting to exhume {{{SaveBonesInfo.ID}}} ({nameof(SaveBonesInfo.ModVersion)}: {SaveBonesInfo.GetModVersion()})");

                try
                {
                    using var savGzStream = await SaveBonesInfo.GetSavGzStreamAsync();

                    var memory = reader.Stream;

                    if (savGzStream.Length >= 2
                        && savGzStream.ReadByte() == 31)
                        savGzStream.ReadByte();

                    savGzStream.Position = 0L;

                    using (var gZipStream = new GZipStream(savGzStream, CompressionMode.Decompress))
                    {
                        await gZipStream.CopyToAsync(memory);
                    }

                    memory.Position = 0L;

                    XRL.UI.Options.ShowErrorPopups = false;

                    reader.StartMetricsOff();

                    XRL.UI.Options.ShowErrorPopups = errorPopups;

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
                            if (SaveBonesInfo.IsOnline)
                                throw new InvalidOperationException("Bones file is online and can't be upgraded (yet)");

                            if ((await SaveBonesInfo.GetBonesFilePathAsync()) is string bonesPath)
                            {
                                // need to put code here that maybe tries to upgrade the file locally?
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
                    PseudoZone loadedPseudoZone = null;
                    if (SaveBonesInfo.GetModVersion() < PseudoZone.MinVersion)
                    {
                        try
                        {
                            XRL.UI.Options.ShowErrorPopups = false;
                            if (reader.ReadInt32() != BONES_ZONE_POS)
                                throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing {nameof(Zone)} val-check.");

                            loadedZone = reader.ReadBonesZone();
                        }
                        catch (Exception x)
                        {
                            loadedZone = null;
                            reader.Errors++;
                            reader.UnspoolTo(BONES_FINALIZE_POS, Prior: true);
                            Utils.Error(x);
                        }
                        XRL.UI.Options.ShowErrorPopups = errorPopups;
                    }
                    else
                    {
                        try
                        {
                            XRL.UI.Options.ShowErrorPopups = false;
                            if (reader.ReadInt32() != BONES_ZONE_POS)
                                throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing {nameof(Zone)} val-check.");

                            loadedPseudoZone = PseudoZone.Load(reader, SaveBonesInfo);
                        }
                        catch (Exception x)
                        {
                            loadedPseudoZone = null;
                            reader.Errors++;
                            reader.UnspoolTo(BONES_FINALIZE_POS, Prior: true);
                            Utils.Error(x);
                        }
                        finally
                        {
                            XRL.UI.Options.ShowErrorPopups = errorPopups;
                        }
                    }

                    try
                    {
                        if (reader.ReadInt32() != BONES_FINALIZE_POS)
                            throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing Finalization val-check.");

                        reader.FinalizeReadMetricsOff();
                    }
                    catch (Exception x)
                    {
                        reader.Errors++;

                        XRL.UI.Options.ShowErrorPopups = false;
                        Utils.Error(x);
                    }
                    finally
                    {
                        XRL.UI.Options.ShowErrorPopups = errorPopups;
                    }

                    try
                    {
                        if (SaveBonesInfo.GetModVersion() < PseudoZone.MinVersion)
                        {

                            if (bonesSpec == null)
                            {
                                if (loadedZone.GetFirstObject(go => go.IsLunarRegent(bonesID)) is not GameObject lunarRegent)
                                    throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing Lunar Regent for {nameof(BonesSpec)}.");

                                bonesSpec = new BonesSpec(lunarRegent, loadedZone);
                            }

                            bonesData = new(
                                BonesID: bonesID,
                                BonesZone: loadedZone,
                                OsseousAshID: SaveBonesInfo.OsseousAshID,
                                OsseousAshHandle: SaveBonesInfo.OsseousAshHandle);
                        }
                        else
                        {
                            if (bonesSpec == null)
                            {
                                if (loadedPseudoZone?.LunarRegent is not GameObject lunarRegent)
                                    throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing Lunar Regent for {nameof(BonesSpec)}.");

                                bonesSpec = new BonesSpec(lunarRegent, loadedPseudoZone);
                            }

                            bonesData = new BonesData
                            {
                                BonesID = bonesID,
                                PseudoZone = loadedPseudoZone,
                                OsseousAshHandle = SaveBonesInfo.OsseousAshHandle,
                            };
                        }
                    }
                    catch (Exception x)
                    {
                        reader.Errors++;

                        XRL.UI.Options.ShowErrorPopups = false;
                        Utils.Error(x);

                        bonesData = null;
                    }
                    finally
                    {
                        XRL.UI.Options.ShowErrorPopups = errorPopups;
                    }

                    if (bonesSpec != null)
                        SaveBonesInfo.BonesSpec = bonesSpec;

                    if (reader.Errors > 0)
                        SerializationExtensions.OptionallyPerformSilently(() => DisplayLoadError(reader, "bones file", reader.Errors));
                }
                catch (Exception x)
                {
                    XRL.UI.Options.ShowErrorPopups = false;
                    Utils.Error(x);
                    XRL.UI.Options.ShowErrorPopups = errorPopups;
                    bonesData = null;
                }
                finally
                {
                    SerializationReader.Release(reader);
                    status.Dispose();
                    XRL.UI.Options.ShowErrorPopups = errorPopups;
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

                    Utils.Error($"{nameof(BonesManager)}.{nameof(ExhumeLunarRegentAsync)}::serialization_error", x);
                }
                Utils.Error(message);

                throw;
            }
            finally
            {
                _HasSaveBones = null;
                XRL.UI.Options.ShowErrorPopups = errorPopups;
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

        public BonesData ExhumeLunarRegent(SaveBonesInfo SaveBonesInfo, bool NoStatus = false)
            => ExhumeLunarRegentAsync(SaveBonesInfo, NoStatus).WaitResult()
            ;

        public async Task<SaveBonesInfo> GetSavedBonesByIDAsync(string BonesID, Predicate<SaveBonesInfo> Where = null)
        {
            foreach (var savedBones in await GetSaveBonesInfoAsync(Where))
                if (savedBones.ID == BonesID)
                    return savedBones;

            return null;
        }

        public SaveBonesInfo GetSavedBonesByID(string BonesID, Predicate<SaveBonesInfo> Where = null)
            => GetSavedBonesByIDAsync(BonesID, Where).WaitResult()
            ;

        public bool TryGetSaveBonesByID(string BonesID, out SaveBonesInfo SaveBonesInfo, Predicate<SaveBonesInfo> Where = null)
            => (SaveBonesInfo = GetSavedBonesByID(BonesID, Where)) != null
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

        public void CremateLunarRegent(string BonesID)
        {
            if (TryGetSaveBonesByID(BonesID, out var savedBones))
                savedBones.Cremate();
        }

        public static async Task<IEnumerable<SaveBonesInfo>> CremateAllLunarRegentsAsync(
            Action BeforeDeletionLoop,
            Action AfterDeletionLoop
            )
        {
            var crematableBonesInfos = (await GetSaveBonesInfoAsync(
                    Where: b => b.IsCrematable,
                    IncludeVersionIncompatible: true))
                .IteratorSafe();

            if (crematableBonesInfos.IsNullOrEmpty())
            {
                await Popup.NewPopupMessageAsync(
                    message: $"There aren't any local bones to cremate!",
                    title: "{{yellow|No Bones!}}");

                return (await GetSaveBonesInfoAsync())
                    .IteratorSafe();
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

                int countBefore = crematableBonesInfos.Count();
                int paddingAmount = countBefore.ToString().Length;
                int cremateCounter = 0;
                int crematedCounter = 0;

                string paddedCremateCounter()
                    => cremateCounter.ToString().PadLeft(paddingAmount, '0');

                string cremateString(string Color = null)
                    => $"{paddedCremateCounter().Colored(Color)}/{countBefore}";
                foreach (var bonesInfo in crematableBonesInfos)
                {
                    cremateCounter++;
                    if (bonesInfo == null)
                        continue;

                    Loading.SetLoadingStatus($"Cremating {cremateString()} :: {bonesInfo.Name.Strip()}");

                    crematedCounter++;
                    bonesInfo.Cremate();
                }

                AfterDeletionLoop?.Invoke();

                var bonesInfos = (await GetSaveBonesInfoAsync()).IteratorSafe();

                string crematedString = crematedCounter.ToString();
                if (crematedCounter != countBefore)
                    crematedString = crematedString.Colored("red");

                bool isSomethingWrong = bonesInfos.Any(b => b.FileLocationData.Type < FileLocationData.LocationType.Mod);

                string somethingWrongString = isSomethingWrong ? "\n\n{{K|(something went wrong)}}" : null;
                await Popup.NewPopupMessageAsync($"{crematedString}/{countBefore} Bones Cremated!{somethingWrongString}");
                Loading.SetLoadingStatus(null);
                return bonesInfos;
            }
            return (await GetSaveBonesInfoAsync()).IteratorSafe();
        }

        public static async Task<IEnumerable<SaveBonesInfo>> CremateAllLunarRegentsAsync()
            => await CremateAllLunarRegentsAsync(null, null)
            ;

        public bool GetZoneIDSeededChanceForEncounter(Zone Z)
            => Stat.SeededRandom($"{SeededRandomPrefix}:{Z.ZoneID}", 1, 10000) <= ChancePermyriadForBones
            ;

        public bool IsWorldMapOrNoBones(Zone Z)
            => Z.IsWorldMap()
            || (ZoneBones.ContainsKey(Z.ZoneID)
                && ZoneBones[Z.ZoneID] == NoBones)
            ;

        public bool HasAllocatedBones(Zone Z)
            => !Z.IsWorldMap()
            && ZoneBones.ContainsKey(Z.ZoneID)
            && ZoneBones[Z.ZoneID] != NoBones
            ;

        public void EncounteredBones(string BonesID)
        {
            Encountered ??= new();
            if (!Encountered.Contains(BonesID))
                Encountered.Add(BonesID);
        }

        public void FailedToLoad(string BonesID)
        {
            FailedToLoadBones ??= new();
            if (!FailedToLoadBones.Contains(BonesID))
                FailedToLoadBones.Add(BonesID);
        }

        public bool AttemptLoadBones(Zone Z, SaveBonesInfo PickedBones)
        {
            Alerted ??= new();
            BonesData bonesData = null;
            string bonesID = PickedBones.ID;
            try
            {
                bonesData = BonesData.GetFromSaveBonesInfo(PickedBones);
            }
            catch (FatalDeserializationVersionException)
            {
                if (!Alerted.TryGetValue(Z.ZoneID, out bool alerted)
                    || !alerted)
                {
                    string message = $"This bones file has encountered a fatal deserialization exception on the basis of the save version and will likely never load.\n\n" +
                        $"If you would like to try and recover it, select {PopupMessage.YesNoButton[1].text}, force the zone to load when asked, " +
                        $"and then please contact {Utils.AuthorOnPlatforms} with a copy of the bones file: {DataManager.SanitizePathForDisplay(PickedBones.Directory)}.\n\n";
                    if (PickedBones.IsCrematable)
                    {
                        message += $"Alternatively, it is recommended that you cremate this bones file, so that it doesn't continue to create issues.\n\n" +
                            $"Would you like to cremate this bones file?";
                        Popup.ShowYesNo(
                            Message: message,
                            callback: (result) =>
                            {
                                if (result == DialogResult.Yes)
                                    PickedBones.Cremate();
                            });
                    }
                    else
                        Popup.Show(message);

                    Alerted[Z.ZoneID] = true;
                }
                throw;
            }
            catch (DeserializationVersionException)
            {
                if (!Alerted.TryGetValue(Z.ZoneID, out bool alerted)
                    || !alerted)
                {
                    string message = $"This bones file has encountered a deserialization version exception on the basis of the save version and will likely not load this run.\n\n" +
                        $"This bones is for game version {PickedBones.Version}, and will likely load in an appropriately versioned run. " +
                        $"If you would like to keep this bones so that it might appear in run on that version of the game, select {PopupMessage.YesNoButton[1].text}, " +
                        $"and then force the zone to load when asked.\n\n";
                    if (PickedBones.IsCrematable)
                    {
                        message += $"Alternatively, if you're unlikely to switch versions of the game, it is recommended that you cremate this bones file, " +
                            $"so that it doesn't continue to create issues.\n\n" +
                            $"Would you like to cremate this bones file?";
                        Popup.ShowYesNo(
                            Message: message,
                            callback: (result) =>
                            {
                                if (result == DialogResult.Yes)
                                    PickedBones.Cremate();
                            });
                    }
                    else
                        Popup.Show(message);

                    Alerted[Z.ZoneID] = true;
                }
                throw;
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(BonesManager), nameof(AttemptLoadBones)), x);
                bonesData?.Dispose();
                bonesData = null;
                FailedToLoad(bonesID);
            }

            if (bonesData == null)
                return false;

            try
            {
                if (!bonesData.Apply(Z, PickedBones, out GameObject lunarRegent, out bool blocked)
                    || blocked)
                {
                    if (blocked)
                        EncounteredBones(bonesID);
                    else
                        Z.SetZoneProperty(nameof(bonesData.BonesID), null);

                    FailedToLoad(bonesID);
                    return false;
                }

                Z.GetCell(0, 0).AddObject(ANNOUNCER_WIDGET, Context: $"{nameof(UD_Bones_MoonKingAnnouncer.BonesID)}::{bonesID}");
                EncounteredBones(bonesID);

                try
                {
                    PickedBones.IncrementEncountered();
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed to increment {nameof(SaveBonesInfo)}.{nameof(SaveBonesInfo.Stats)}.{nameof(SaveBonesInfo.Stats.Encountered)}", x);
                }
                return true;
            }
            finally
            {
                bonesData?.Dispose();
            }
        }

        public bool AttemptLoadBones(
            string ObjectID,
            SaveBonesInfo PickedBones,
            out GameObject LunarRegent,
            out LunarPartyIDs CachedCourtiers,
            Action<GameObject> ProcPreLoad = null,
            Action<GameObject> ProcPostLoad = null
            )
        {
            LunarRegent = null;
            CachedCourtiers = null;
            BonesData bonesData = null;
            string bonesID = PickedBones.ID;
            try
            {
                bonesData = BonesData.GetFromSaveBonesInfo(PickedBones, NoStatus: true);
            }
            catch (FatalDeserializationVersionException)
            {
                if (!Alerted.TryGetValue(ObjectID, out bool alerted)
                    || !alerted)
                {
                    string message = $"This bones file has encountered a fatal deserialization exception on the basis of the save version and will likely never load.\n\n" +
                        $"If you would like to try and recover it, select {PopupMessage.YesNoButton[1].text}, force the zone to load when asked, " +
                        $"and then please contact {Utils.AuthorOnPlatforms} with a copy of the bones file: {DataManager.SanitizePathForDisplay(PickedBones.Directory)}.\n\n";
                    if (PickedBones.IsCrematable)
                    {
                        message += $"Alternatively, it is recommended that you cremate this bones file, so that it doesn't continue to create issues.\n\n" +
                            $"Would you like to cremate this bones file?";
                        Popup.ShowYesNo(
                            Message: message,
                            callback: (result) =>
                            {
                                if (result == DialogResult.Yes)
                                    PickedBones.Cremate();
                            });
                    }
                    else
                        Popup.Show(message);

                    Alerted[ObjectID] = true;
                }
                throw;
            }
            catch (DeserializationVersionException)
            {
                if (!Alerted.TryGetValue(ObjectID, out bool alerted)
                    || !alerted)
                {
                    string message = $"This bones file has encountered a deserialization version exception on the basis of the save version and will likely not load this run.\n\n" +
                        $"This bones is for game version {PickedBones.Version}, and will likely load in an appropriately versioned run. " +
                        $"If you would like to keep this bones so that it might appear in run on that version of the game, select {PopupMessage.YesNoButton[1].text}, " +
                        $"and then force the zone to load when asked.\n\n";
                    if (PickedBones.IsCrematable)
                    {
                        message += $"Alternatively, if you're unlikely to switch versions of the game, it is recommended that you cremate this bones file, " +
                            $"so that it doesn't continue to create issues.\n\n" +
                            $"Would you like to cremate this bones file?";
                        Popup.ShowYesNo(
                            Message: message,
                            callback: (result) =>
                            {
                                if (result == DialogResult.Yes)
                                    PickedBones.Cremate();
                            });
                    }
                    else
                        Popup.Show(message);

                    Alerted[ObjectID] = true;
                }
                throw;
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(BonesManager), nameof(AttemptLoadBones)), x);
                bonesData?.Dispose();
                bonesData = null;
                FailedToLoad(bonesID);
            }

            if (bonesData == null)
                return false;

            try
            {
                if (!bonesData.TryExtractLunarRegent(
                    BonesInfo: PickedBones,
                    LunarRegent: out LunarRegent,
                    CachedLunarCourtiers: out CachedCourtiers,
                    Blocked: out bool blocked,
                    ProcPreLoad: ProcPreLoad,
                    ProcPostLoad: ProcPostLoad)
                    || blocked)
                {
                    if (blocked)
                        EncounteredBones(bonesID);

                    FailedToLoad(bonesID);
                    return false;
                }
                EncounteredBones(bonesID);
                return true;
            }
            finally
            {
                bonesData?.Dispose();
            }
        }

        public void CheckBones(Zone Z, IZoneEvent FromEvent = null)
        {
            ZoneBones ??= new();
            Encountered ??= new();

            if (IsWorldMapOrNoBones(Z))
                return;

            string existingBonesID = null;
            if (!HasAllocatedBones(Z))
                ZoneBones.Add(Z.ZoneID, NoBones);
            else
                existingBonesID = ZoneBones[Z.ZoneID];

            if (FromEvent.GetType() == typeof(ZoneBuiltEvent)
                && !existingBonesID.IsNullOrEmpty()
                && GetSavedBonesByID(existingBonesID) is SaveBonesInfo existingSaveBones)
            {
                existingSaveBones.AttemptLoad(Z);
                return;
            }

            if (FromEvent.GetType() != typeof(ZoneBuiltEvent)
                && FromEvent.GetType() != typeof(ZoneActivatedEvent))
                return;

            if (Options.DebugEnableNoExhuming
                && !Options.DebugEnablePickingBones)
                return;

            if (The.Player is not GameObject player)
                return;

            if (Z.ZoneID == JoppaWorldBuilder.ID_JOPPA)
                return;

            if (StartingLocation?.ZoneID == Z.ZoneID)
                return;

            var playerSpec = new BonesSpec(player, Z);

            bool notEncounteredAndWithinSpec(SaveBonesInfo SaveBonesInfo)
                => !Encountered.Contains(SaveBonesInfo.ID)
                && !FailedToLoadBones.Contains(SaveBonesInfo.ID)
                && SaveBonesInfo.BonesSpec?.IsWithinSpec(playerSpec) is true
                ;

            if (GetEligibleSaveBonesInfo(notEncounteredAndWithinSpec) is IEnumerable<SaveBonesInfo> bonesInfos
                && !bonesInfos.IsNullOrEmpty())
            {
                bool byChance = GetZoneIDSeededChanceForEncounter(Z);
                bool canRollIt = Options.DebugEnableForcePickingBones
                        && ChancePermyriadForBones < 10000;
                if (byChance
                    || Options.DebugEnableForcePickingBones)
                {
                    bool bonesLoaded = false;

                    // int selection = 0;
                    var result = UIUtils.CascadableResult.BackSilent;
                    using var failedBonesList = ScopeDisposedList<SaveBonesInfo>.GetFromPool();
                    if (Options.DebugEnablePickingBones)
                    {
                        string neutralRegalTitle = UD_Bones_MoonKingFever.REGAL_TITLE.Pluralize();
                        string title = $"Eligible =LunarShader:{neutralRegalTitle}:*= For This Zone".StartReplace().ToString();

                        string rollIt = "roll it!".Color("yellow");
                        string message = "Pick a lunar regent to exhume.";
                        if (canRollIt)
                            message = $"{message[..^1]}, or {rollIt}";

                        var icon = new BonesRender(GameObjectFactory.Factory.GetBlueprintIfExists("Lunar Face"), HFlip: false, IsMad: true);

                        if (bonesInfos.Any(b => b.IsMad))
                            icon.SetTile(MOON_KING_FEVER_TILE);

                        var options = new PickOptionDataSetAsync<SaveBonesInfo, UIUtils.CascadableResult>();
                        do
                        {
                            if (failedBonesList.Count >= bonesInfos.Count())
                            {
                                result = UIUtils.CascadableResult.CancelSilent;
                                break;
                            }

                            options.Clear();

                            // Add None Please:
                            options.Add(new PickOptionData<SaveBonesInfo, Task<UIUtils.CascadableResult>>
                            {
                                Text = "none please",
                                Hotkey = 'n',
                                Icon = new BonesRender(
                                        Blueprint: GameObjectFactory.Factory.GetBlueprintIfExists("Lunar Face"),
                                        HFlip: false)
                                    .SetTileColor("&K")
                                    .SetDetailColor('K'),
                                Callback = e => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                            });

                            // Add Roll It!:
                            if (canRollIt)
                            {
                                options.Add(new PickOptionData<SaveBonesInfo, Task<UIUtils.CascadableResult>>
                                {
                                    Text = "roll it!".Color("yellow"),
                                    Hotkey = 'r',
                                    Icon = new BonesRender(
                                            Tile: "Abilities/sw_skill_pointed_circle.png",
                                            ColorString: "&y",
                                            TileColor: "&y",
                                            DetailColor: 'W'),
                                    Callback = e => Task.Run(() => UIUtils.CascadableResult.BackSilent),
                                });
                            }

                            foreach (var bonesInfo in bonesInfos.OrderBy(b => b, SaveBonesInfo.SaveBonesInfoComparerDescending))
                            {
                                if (!failedBonesList.Contains(bonesInfo))
                                {
                                    options.Add(new PickOptionData<SaveBonesInfo, Task<UIUtils.CascadableResult>>
                                    {
                                        Element = bonesInfo,
                                        Text = bonesInfo.GetBonesPickerLine(),
                                        Hotkey = options.GetFirstAvailableHotkey(),
                                        Icon = new BonesRender(bonesInfo.Render, HFlip: false, IsMad: bonesInfo.Render.IsMad),
                                        Callback = e => Task.Run(() => AttemptToLoadBones(Z, e, info => failedBonesList.Add(info)))
                                    });
                                }
                            }

                            result = UIUtils.PerformPickOptionAsync(
                                    OptionDataSet: options,
                                    Title: title,
                                    Intro: message,
                                    IntroIcon: icon,
                                    NoBackButton: true,
                                    DefaultSelected: 0,
                                    OnBackCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                                    OnEscapeCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                                    FinalSelectedCallback: UIUtils.ShowEscancellepedAsync)
                                .WaitResult();
                        }
                        while (result.IsContinue());
                    }

                    if (!Options.DebugEnablePickingBones
                        || (result.IsBack()
                            && byChance))
                    {
                        var bonesBag = new BallBag<SaveBonesInfo>();
                        foreach (var bonesInfo in bonesInfos)
                        {
                            if (!failedBonesList.Contains(bonesInfo))
                                if (bonesInfo.GetBonesWeight() is int weight)
                                    bonesBag.Add(bonesInfo, weight);
                        }
                        while (!bonesBag.IsNullOrEmpty()
                            && bonesBag.PickOne() is SaveBonesInfo pickedBones)
                        {
                            try
                            {
                                if (AttemptToLoadBones(Z, pickedBones).IsCancel())
                                {
                                    result = UIUtils.CascadableResult.CancelSilent;
                                    break;
                                }
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"Failed to load {nameof(BonesData)} for {nameof(SaveBonesInfo)}.{nameof(pickedBones.ID)} {pickedBones.ID}", x);
                            }
                        }
                    }

                    if (bonesLoaded
                        || result.IsCancel())
                        return;
                }
            }
        }

        private UIUtils.CascadableResult AttemptToLoadBones(Zone Z, SaveBonesInfo BonesInfo, Action<SaveBonesInfo> BeforeAttempt = null)
        {
            BeforeAttempt?.Invoke(BonesInfo);
            if (BonesInfo.AttemptLoad(Z))
            {
                Utils.Info($"Loaded {Grammar.MakePossessive(BonesInfo.GetName().Strip())} bones {{{BonesInfo.ID}}}");
                return UIUtils.CascadableResult.CancelSilent;
            }

            Utils.Warn($"Failed to load {Grammar.MakePossessive(BonesInfo.GetName().Strip())} bones {{{BonesInfo.ID}}}");
            return UIUtils.CascadableResult.Continue;
        }

        public override void Register(XRLGame Game, IEventRegistrar Registrar)
        {
            if (Utils.ModVersion < PseudoZone.MinVersion)
                Registrar.Register(ZoneActivatedEvent.ID);
            else
                Registrar.Register(ZoneBuiltEvent.ID);

            Registrar.Register(GetLunarRegentEvent.ID);
            base.Register(Game, Registrar);
        }

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            if (Utils.ModVersion < PseudoZone.MinVersion)
                CheckBones(E.Zone, E);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ZoneBuiltEvent E)
        {
            if (Utils.ModVersion >= PseudoZone.MinVersion)
                CheckBones(E.Zone, E);
            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(GetLunarRegentEvent E)
        {
            if (E.LunarObject is GameObject lunarRegent)
            {
                foreach (var part in PartsLunarRegentsShouldNotHave.IteratorSafe())
                    lunarRegent.RemovePart(part);

                var playerParts = lunarRegent.GetPartsDescendedFrom<IPlayerPart>();
                foreach (var part in playerParts.IteratorSafe())
                    lunarRegent.RemovePart(part);
            }
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
                task = NavigationController.instance.SuspendContextWhile(CremateAllLunarRegentsAsync);
                task?.Wait();
                success = task?.IsCompletedSuccessfully is not false;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed to cremate all bones", x);
                success = false;
            }
            return success
                || task?.Result?.Any(b => b.IsCrematable) is not true;
        }

        [WishCommand(Command = "go2bones")]
        public static bool Go2Bones_WishHandler()
        {
            Popup.Show("This wish used to take you to the single bones file that was pending for this run, but that's not how bones are loaded anymore.\n\n" +
                "At some point, if there are enough uploaded bones, it may be possible to use this wish to travel to a \"bones world\" that is an overworld map with all the online bones in their respective locations.\n\n" +
                "This is likely a long way off, however.");
            return true;
        }

        [WishCommand(Command = "manage bones report visited")]
        public static bool ReportVisited_WishHandler()
        {
            try
            {
                string title = $"{nameof(BonesManager)} Visited Zones";
                Utils.Log($"{title}:");
                System?.ZoneBones.Loggregate(
                    Proc: kvp => $"{kvp.Key}: {kvp.Value}",
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}: {s}");
                if (System == null)
                    Utils.Log($"{1.Indent()}: System not instantiated (this is probably a bug).");

                Popup.Show(
                    Message: System?.ZoneBones?.Aggregate(
                        seed: (string)null,
                        func: (a,n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent()}: {n.Key}: {n.Value}"))
                        ?? $"{1.Indent()}: {"System not instantiated".Colored("red")} (this is probably a bug).",
                    Title: title.Colored("yellow"));
            }
            catch (Exception x)
            {
                Utils.Error($"Wish: \"{"manage bones report ZoneBones"}\"", x);
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

        [WishCommand(Command = "manage bones report failed")]
        public static bool ReportFailed_WishHandler()
        {
            try
            {
                string title = $"{nameof(BonesManager)} Encountered Bones";
                Utils.Log($"{title}:");
                System?.FailedToLoadBones.Loggregate(
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}: {s}");
                if (System == null)
                    Utils.Log($"{1.Indent()}: System not instantiated (this is probably a bug).");

                Popup.Show(
                    Message: System?.FailedToLoadBones?.Aggregate(
                        seed: (string)null,
                        func: (a,n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent()}: {n}"))
                        ?? $"{1.Indent()}: {"System not instantiated".Colored("red")} (this is probably a bug).",
                    Title: title.Colored("yellow"));
            }
            catch (Exception x)
            {
                Utils.Error($"Wish: \"{"manage bones report failed"}\"", x);
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
