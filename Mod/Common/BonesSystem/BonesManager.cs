using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

using MemoryStream = System.IO.MemoryStream;
using IOException = System.IO.IOException;
using File = Platform.IO.File;

using Cysharp.Text;

using Platform;
using Platform.IO;
using UnityEngine;

using HarmonyLib;
using Genkit;

using Qud.API;
using Qud.UI;

using XRL;
using XRL.Core;
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
using XRL.World.Text;
using XRL.World.WorldBuilders;

using ColorUtility = ConsoleLib.Console.ColorUtility;
using CompressionLevel = System.IO.Compression.CompressionLevel;

using GameObject = XRL.World.GameObject;
using Event = XRL.World.Event;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Events;
using UD_Bones_Folder.Mod.Logging;
using UD_Bones_Folder.Mod.Moderation;
using UD_Bones_Folder.Mod.OsseousAsh;
using UD_Bones_Folder.Mod.Serialization.PseudoTypes;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.UI;

using static UD_Bones_Folder.Mod.Const;

using Debug = UD_Bones_Folder.Mod.Logging.Debug;

namespace UD_Bones_Folder.Mod
{
    [HasRegisterMethodForDebug]
    [HasModSensitiveStaticCache]
    [HasGameBasedStaticCache]
    [HasCallAfterGameLoaded]
    [HasWishCommand]
    [Serializable]
    public class BonesManager
        : IScribedSystem
        , ILoadLunarRegentEventHandler
        , ILoadLunarCourtierEventHandler
        , IPseudoZoneEventHandler
    {
        #region Consts & PsuedoConsts

        public static string BonesFileName => "LunarRegent";

        public static List<string> PartsLunarRegentsShouldNotHave = new List<string>
        {
            nameof(OpeningStory),
        };

        #endregion
        #region Static Caches

        private static bool ShowErrorPopups
        {
            get => XRL.UI.Options.ShowErrorPopups;
            set => XRL.UI.Options.ShowErrorPopups = value;
        }

        private static bool _WishContext;
        public static bool WishContext
        {
            get => _WishContext;
            protected set => _WishContext = value;
        }

        private static BonesManager _System;
        public static BonesManager System
        {
            get
            {
                if (_System == null)
                    BonesManagerSystemInit();
                return _System;
            }
            private set => _System = value;
        }

        [GameBasedStaticCache(CreateInstance = false)]
        private static bool? _HasSaveBones;

        [NonSerialized]
        public EmbarkBuilder EmbarkBuilder;

        [NonSerialized]
        public LocationSet ImmutableLocations;

        #endregion
        #region Instance Caches

        [NonSerialized]
        protected Dictionary<string, ReplacementEntry> MissingBlueprintReplacements = new();

        [NonSerialized]
        protected Dictionary<string, string> TileReplacementsByMissingBlueprint = new();

        [NonSerialized]
        protected Dictionary<string, string> BlueprintReplacementsByMissingBlueprint = new();

        [NonSerialized]
        private List<SaveBonesInfo> EligibleSaveBonesInfoCache;
        [NonSerialized]
        public bool IgnoreCache = false;

        #endregion

        [SerializeField]
        private string GameID;

        public bool Initialized;

        [NonSerialized]
        public Task SaveTask;

        public int ChancePermyriadForBones => Options.GetPermyriadChanceForBones();

        [NonSerialized]
        public Dictionary<string, ZoneBonesAllocation> ZoneBones = new();
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

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        internal static List<BonesSourcesDelegateEntry> BonesSourcesDelegates;

        public BonesManager()
        { }

        private static BonesManager InitializeSystem() => new() { GameID = The.Game?.GameID };

        [CallAfterGameLoaded]
        [GameBasedCacheInit]
        public static void BonesManagerSystemInit()
        {
            if (_System == null)
            {
                System = The.Game?.RequireSystem(InitializeSystem);
                if (_System != null
                    && _System.GameID == null)
                    _System.GameID = The.Game.GameID;
            }
            else
            if (_System.GameID != null
                && _System.GameID != The.Game?.GameID)
            {
                System = null;
                BonesManagerSystemInit();
                return;
            }
            else
            if (The.Game?.GetSystem<BonesManager>() == null)
                The.Game?.AddSystem(System);

            if (_System != null)
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
            UD_Bones_BrokenLunarRegent.ClearCachedSaveBonesInfo();
        }

        [ModSensitiveCacheInit]
        private static void CacheBonesSourceDelegates()
        {
            BonesSourcesDelegates = new();

            var delegatesByPriority = new Dictionary<int, ScopeDisposedList<BonesSourcesDelegateEntry>>();
            foreach (var delegateMethod in ModManager.GetMethodsWithAttribute(typeof(BonesSourcesDelegateAttribute), typeof(HasReturnsBonesSourcesDelegateAttribute)))
            {
                var attribute = delegateMethod.GetCustomAttribute<BonesSourcesDelegateAttribute>();

                var type = attribute.Type;
                var priority = attribute.Priority;

                var modInfo = ModManager.GetMod(delegateMethod?.DeclaringType?.Assembly)
                    ?? Utils.ThisMod;

                if (BonesSourcesDelegateEntry.TryCreate(
                    Type: type,
                    Priority: priority,
                    DelegateMethod: delegateMethod,
                    DelegateEntry: out BonesSourcesDelegateEntry delegateEntry,
                    ModInfo: modInfo))
                {
                    if (!delegatesByPriority.TryGetValue(delegateEntry.Priority, out ScopeDisposedList<BonesSourcesDelegateEntry> delegateList))
                    {
                        delegateList = ScopeDisposedList<BonesSourcesDelegateEntry>.GetFromPool();
                        delegatesByPriority[delegateEntry.Priority] = delegateList;
                    }
                    delegateList.Add(delegateEntry);
                }
            }

            using (var delegateEntryLists = ScopeDisposedList<ScopeDisposedList<BonesSourcesDelegateEntry>>.GetFromPool())
            {
                delegateEntryLists.AddRange(delegatesByPriority.SortBy((x, y) => x.Key.CompareTo(y.Key)).Select(kvp => kvp.Value));

                foreach (var delegateEntryList in delegateEntryLists)
                {
                    foreach (var delegateEntry in delegateEntryList)
                        BonesSourcesDelegates.Add(delegateEntry);
                    delegateEntryList.Dispose();
                }
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
        public static XRL.Version AddedFieldReflectionSerialization => new(0, 0, 4, 2);

        public override void Write(SerializationWriter Writer)
        {
            base.Write(Writer);
            Writer.WriteStringCompositeDictionary(MissingBlueprintReplacements);
            Writer.WriteOptimized(TileReplacementsByMissingBlueprint);
            Writer.WriteOptimized(BlueprintReplacementsByMissingBlueprint);
            Writer.WriteStringCompositeDictionary(ZoneBones);
            Writer.Write(Alerted);
            Writer.Write(Encountered);
            Writer.Write(FailedToLoadBones);
            Writer.WriteComposite(ImmutableLocations);
        }

        public override void Read(SerializationReader Reader)
        {
            var readVersion = Reader.ModVersions[MOD_ID];
            if (readVersion < AddedFieldReflectionSerialization)
            {
                GameID ??= The.Game?.GameID;
                Initialized = true;
            }
            else
            {
                base.Read(Reader);
            }

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(MissingBlueprintReplacements)})");
            MissingBlueprintReplacements = Reader.ReadStringCompositeDictionary<ReplacementEntry>();

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(TileReplacementsByMissingBlueprint)})");
            TileReplacementsByMissingBlueprint = Reader.ReadOptimizedStringDictionary();

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(BlueprintReplacementsByMissingBlueprint)})");
            BlueprintReplacementsByMissingBlueprint = Reader.ReadOptimizedStringDictionary();

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(ZoneBones)})");
            ZoneBones = Reader.ReadStringCompositeDictionary<ZoneBonesAllocation>();

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(Alerted)})");
            Alerted = Reader.ReadDictionary<string, bool>();

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(Encountered)})");
            Encountered = Reader.ReadList<string>();

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(FailedToLoadBones)})");
            FailedToLoadBones = Reader.ReadList<string>();

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)}({nameof(ImmutableLocations)})");
            ImmutableLocations = Reader.ReadComposite<LocationSet>();
            if (readVersion < AddedFieldReflectionSerialization)
            {
                // Utils.Log($"  {nameof(ImmutableLocations)}.{nameof(ImmutableLocations.InvertLocations)}()");
                ImmutableLocations.InvertLocations();
            }

            // Utils.Log($"{nameof(BonesManager)}.{nameof(Read)} - Finished!");
        }

        #endregion

        public static IEnumerable<IBonesSource> YieldBonesSources(Predicate<IBonesSource> Where)
        {
            using (var yielded = BonesSources.GetFromPool())
            {
                foreach (var bonesSourcesDelegate in BonesSourcesDelegates.IteratorSafe())
                    foreach (var bonesSource in bonesSourcesDelegate.GetBonesSources.Invoke())
                        if (Where?.Invoke(bonesSource) is not false
                            && yielded.AddDistinct(bonesSource))
                            yield return bonesSource;
            }
        }

        public static IEnumerable<IBonesSource> YieldBonesSources()
            => YieldBonesSources(null)
            ;

        public static BonesSources GetBonesSourcesFromPool(Predicate<IBonesSource> Where)
        {
            var bonesSources = BonesSources.GetFromPool();
            foreach (var bonesSourcesDelegate in BonesSourcesDelegates.IteratorSafe())
                bonesSources.AddDistinctRange(bonesSourcesDelegate.GetBonesSources.Invoke());

            if (Where != null)
                bonesSources.RemoveAll(bs => !Where.Invoke(bs));

            return bonesSources;
        }

        public static BonesSources GetBonesSourcesFromPool()
            => GetBonesSourcesFromPool(null)
            ;

        public static string GetSaveFileName(string FileName = null)
            => !FileName.IsNullOrEmpty()
            ? $"{FileName}.sav.gz"
            : SaveBonesInfo.SavGz
            ;

        public static string GetInfoFileName(string FileName = null)
            => !FileName.IsNullOrEmpty()
            ? $"{FileName}.json"
            : SaveBonesInfo.Json
            ;

        public static SaveBonesInfo DummyBonesInfo
            => SaveBonesInfo.DummyBonesInfo(
                VisibilityMode: BonesManagement.instance?.VisibilityMode ?? BonesManagement.VisibilityModes.All,
                BonesSource: BonesPile.BonesFolderCache.GetValue(IBonesSource.SourceType.Synced) ?? BonesPile.DummyFolder)
            ;

        private Task ReturnSaveTaskNullWithLogMessage(string Message = null)
        {
            if (!Message.IsNullOrEmpty())
            {
                using var indent = new Indent();
                Debug.Log(Message, Indent: indent[0]);
            }
            return SaveTask = null;
        }

        [RegisterMethodForDebug(true)]
        public static void PrepareZoneObjectsForNextRun(Zone Z, string GameID, GameObject LunarRegent)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Z), Z?.ZoneID ?? "MISSING_ZONE"),
                    Debug.Arg(nameof(GameID), GameID),
                    Debug.Arg(nameof(LunarRegent), LunarRegent?.DebugName ?? "MISSING_LUNAR_REGENT"),
                });

            using var processed = ScopeDisposedList<GameObject>.GetFromPool();
            foreach (var zoneGO in Z.GetObjects())
            {
                if (zoneGO.IsPlayer())
                    continue;

                zoneGO.PerformActionRecursively(delegate (GameObject go, int depth)
                {
                    if (processed.Contains(go))
                        return;

                    processed.Add(go);

                    EarlyPrepareObjectForNextRunEvent.Send(go, Z, GameID, LunarRegent);

                    if (go.GeneIDMatch(The.Player))
                    {
                        go.InjectGeneID(LunarRegent.GeneID);
                        go.SetStringProperty("CloneOfGenes", LunarRegent.GeneID);
                    }

                    bool moderationRestored = false;
                    if (go.TryGetPart(out UD_Bones_Moderated moderated))
                    {
                        moderated.IsModerationPaused = true;
                        if (moderated.NameModerationProcessed != null
                            || moderated.DescModerationProcessed != null)
                        {
                            moderated.RestoreModeratedContent(go);
                            moderationRestored = true;
                        }
                    }

                    if (go.TryGetPart(out Examiner examiner))
                        examiner.EpistemicStatus = Examiner.EPISTEMIC_STATUS_UNINITIALIZED;

                    if (go.TryGetPart(out UD_Bones_BlueprintSpec blueprintSpecPart))
                        blueprintSpecPart.Initialize();
                    else
                        blueprintSpecPart = go.RequirePart<UD_Bones_BlueprintSpec>();

                    go.SetIntProperty(BlueprintSpec.TierProp, go.GetTier());
                    go.SetIntProperty(BlueprintSpec.TechTierProp, go.GetTechTier());
                    go.SetStringProperty(BlueprintSpec.SpeciesProp, go.GetSpecies(), true);
                    go.SetStringProperty(BlueprintSpec.ClassProp, go.GetClass(), true);
                    go.SetStringProperty(BlueprintSpec.RoleProp, go.GetPropertyOrTag("Role"), true);
                    go.SetStringProperty(BlueprintSpec.PaintedWallProp, go.GetPropertyOrTag("PaintedWall"), true);
                    go.SetStringProperty(BlueprintSpec.PaintedFenceProp, go.GetPropertyOrTag("PaintedFence"), true);
                    go.SetStringProperty(BlueprintSpec.ImprovisedWeaponProp, $"{go.GetPart<MeleeWeapon>()?.IsImprovisedWeapon() is true}", true);

                    if (moderationRestored
                        && go.TryGetPart(out moderated))
                    {
                        moderated.IsModerationPaused = false;
                    }

                    if (go.Brain is Brain brain)
                    {
                        if (go.IsPlayerLed())
                        {
                            if (brain.Allegiance?.SourceID == The.Player.BaseID)
                            {
                                var reasonType = brain.Allegiance.Reason.GetType();
                                var courtierPart = go.RequirePart<UD_Bones_LunarCourtier>()
                                    .OverrideBonesIDTyped<UD_Bones_LunarCourtier>(GameID);
                                courtierPart.AllyReasonType = reasonType;
                                courtierPart.Persists = true;
                                // Utils.Log($"{go.DebugName} is PlayerLed: {reasonType.Name ?? "NO_TYPE"}");
                            }
                        }
                    }

                    go.ForfeitTurn();

                    LatePrepareObjectForNextRunEvent.Send(go, Z, GameID, LunarRegent);
                });
            }
        }

        public static async Task<List<ResponseInfo>> UploadBonesAsync(
            SaveBonesJSON BonesJSON,
            byte[] BonesSavGz
            )
        {
            if (Options.EnableOsseousAshUploads)
            {
                if (!BonesSavGz.IsNullOrEmpty())
                {
                    try
                    {
                        return await Task.Run(() => ServerUtils.UploadBones(BonesJSON, new(BonesJSON.ID, BonesSavGz)));
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{nameof(UploadBonesAsync)} failed to upload Bones with BonesID {{{BonesJSON.ID}}}", x);
                    }
                }
                else
                    Utils.Warn($"{nameof(BonesSavGz)} is null or empty for BonesID {{{BonesJSON.ID}}}. Upload aborted.");
            }
            return null;
        }

        public static async Task<bool> TryUploadBonesAsync(
            SaveBonesJSON SaveBonesJSON,
            byte[] BonesSavGz
            )
            => !(await UploadBonesAsync(SaveBonesJSON, BonesSavGz)).IsNullOrEmpty()
            ;

        [RegisterMethodForDebug(true)]
        public static bool SaveBonesFile(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesJSON), BonesJSON.GetNullString()),
                    Debug.Arg(nameof(BonesSavGz), BonesSavGz.GetNullString()),
                });

            BonesJSON.Size = BonesSavGz.Data.LongLength;
            using var bonesSources = GetBonesSourcesFromPool(BonesSources.SourceCanHoard);
            bool anySuccess = false;
            using (var sourceResults = ScopeDisposedList<KeyValuePair<IBonesSource, ActionResult>>.GetFromPool())
            {
                using (var failedSources = ScopeDisposedList<KeyValuePair<IBonesSource, Exception>>.GetFromPool())
                {
                    foreach (var bonesSource in bonesSources.GetHoardableSources())
                    {
                        try
                        {
                            sourceResults.Add(new(bonesSource, bonesSource.SaveBones(BonesJSON, BonesSavGz)));
                            if (bonesSource is BonesFolder folderSource)
                            {
                                try
                                {
                                    MemoryHelper.GCCollect();
                                    CheckBonesSave(folderSource.GetFullSavGzFilePathFor(BonesJSON.ID));
                                }
                                catch (Exception x)
                                {
                                    failedSources.Add(new(bonesSource, x));
                                    continue;
                                }
                            }
                        }
                        catch (Exception x)
                        {
                            if (!bonesSource.IsOnline())
                                failedSources.Add(new(bonesSource, x));
                            else
                                Utils.Error($"{bonesSource.DisplayName} failed to {nameof(bonesSource.SaveBones)}", x);
                        }
                    }

                    if (!failedSources.IsNullOrEmpty())
                        HoardBonesError(BonesJSON.ID, failedSources, true);
                }

                anySuccess = sourceResults.Any(kvp => (bool)kvp.Value);
                bool allFullSuccess = sourceResults.All(kvp => kvp.Value.Type >= ActionResult.ResultType.Pending);

                if (!anySuccess)
                    Utils.Error($"Failed to save Bones with BonesID {{{BonesJSON.ID}}}:");
                else
                {
                    if (!allFullSuccess)
                        Utils.Warn($"Successfully saved Bones with BonesID {{{BonesJSON.ID}}} to some bones sources:");
                    else
                        Utils.Info($"Successfully saved Bones with BonesID {{{BonesJSON.ID}}} to all bones sources:");
                }

                sourceResults.Loggregate(
                    Proc: kvp => $"{kvp.Key.DisplayName.Strip()} => {kvp.Value}",
                    Empty: $"no bones sources",
                    PostProc: s => $"{1.Indent()}: {s}");
            }
            return anySuccess;
        }

        [RegisterMethodForDebug(true)]
        public Task HoardBones(
            IDeathEvent DeathEvent,
            GameObject LunarRegent,
            bool DoStatus = true
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(DeathEvent), DeathEvent != null ? DeathEvent.GetType().ToStringWithGenerics() : "null"),
                    Debug.Arg(nameof(LunarRegent), LunarRegent?.DebugName ?? "MISSING_LUNAR_REGENT"),
                    Debug.Arg(nameof(DoStatus), DoStatus),
                });

            string thisMethod = nameof(HoardBones);

            string abort(string Reason)
                => $"Abort {thisMethod}: {Reason}"
                ;

            Debug.Log($"{nameof(DelayShutdown)}.{nameof(DelayShutdown.AutoScopeForceMainThread)}", Indent: indent[2]);
            using (DelayShutdown.AutoScopeForceMainThread())
            {
                var saveTask = The.Game.SaveTask;

                if (saveTask?.IsCompleted is false)
                    return ReturnSaveTaskNullWithLogMessage(abort($"{nameof(SaveTask)} is not null and not complete"));

                if (The.Game is not XRLGame game)
                    return ReturnSaveTaskNullWithLogMessage(abort($"{Utils.CallChain(nameof(The), nameof(The.Game))} is null"));

                if (!game.Running)
                    return ReturnSaveTaskNullWithLogMessage(abort($"!{Utils.CallChain(nameof(The), nameof(The.Game), nameof(The.Game.Running))}"));

                if (LunarRegent?.CurrentZone is not Zone currentZone)
                    return ReturnSaveTaskNullWithLogMessage(abort($"{nameof(currentZone)} is null"));

                if (SerializationWriter.Get() is not SerializationWriter writer)
                    return ReturnSaveTaskNullWithLogMessage(abort($"failed to get {nameof(SerializationWriter)}"));

                PrepareZoneObjectsForNextRun(currentZone, GameID, LunarRegent);

                string saveFileName = GetSaveFileName();

                Debug.Log(nameof(GetBonesSourcesFromPool), Indent: indent[2]);
                using var bonesSources = GetBonesSourcesFromPool(BonesSources.SourceCanHoard);

                Loading.Status status = default;
                Debug.Log(nameof(status), DoStatus, Indent: indent[2]);
                if (DoStatus)
                    status = Loading.StartTask("Hoarding Bones");
                try
                {
                    EnsureWallTime(game);

                    Debug.Log($"{nameof(game)}.{nameof(Extensions.CreateSaveBonesJSON)}", Indent: indent[2]);
                    var saveBonesJSON = game.CreateSaveBonesJSON(DeathEvent, LunarRegent);

                    Debug.Log($"{nameof(writer)}.{nameof(writer.Start)}", Indent: indent[2]);
                    writer.Start(XRLGame.SaveVersion);
                    Debug.Log($"{nameof(writer)}.{nameof(writer.Write)}({nameof(SERIALIZATION_CHECK)})", SERIALIZATION_CHECK, Indent: indent[2]);
                    writer.Write(SERIALIZATION_CHECK);

                    Debug.Log($"{nameof(writer)}.{nameof(writer.Write)}({nameof(saveBonesJSON.GameVersion)})", saveBonesJSON.GameVersion, Indent: indent[2]);
                    writer.Write(saveBonesJSON.GameVersion);

                    /*var bonesSpec = new BonesSpec(LunarRegent, currentZone);

                    writer.Write(BONES_SPEC_POS);
                    writer.WriteComposite(bonesSpec);*/

                    Debug.Log($"{nameof(writer)}.{nameof(writer.Write)}({nameof(BONES_ZONE_POS)})", BONES_ZONE_POS, Indent: indent[2]);
                    writer.Write(BONES_ZONE_POS);

                    var pseudoZone = PseudoZone.FromZone(currentZone);
                    Debug.Log($"{nameof(writer)}.{nameof(writer.WriteComposite)}({nameof(pseudoZone)})", pseudoZone?.ZoneID ?? "MISSING_PSUEDOZONE", Indent: indent[2]);
                    writer.WriteComposite(pseudoZone);

                    writer.Write(BONES_FINALIZE_POS);

                    pseudoZone?.PrepForFinalizeWrite();
                    Debug.Log($"{nameof(writer)}.{nameof(writer.FinalizeWrite)}", Indent: indent[2]);
                    writer.FinalizeWrite();
                    pseudoZone.UnprepForFinalizeWrite();

                    Debug.Log($"using new {nameof(MemoryStream)}", Indent: indent[2]);
                    using (var memoryStream = new MemoryStream())
                    {
                        Debug.Log($"using new {nameof(GZipStream)}({nameof(CompressionLevel)}: {CompressionLevel.Fastest})", Indent: indent[2]);
                        using (var gZipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
                        {
                            byte[] buffer = writer.Stream.GetBuffer();
                            Debug.Log($"{nameof(gZipStream)}.{nameof(gZipStream.Write)}", Indent: indent[2]);
                            gZipStream.Write(buffer, 0, (int)writer.Stream.Length);
                        }
                        Debug.Log($"using new {nameof(BonesSavGz)}", Indent: indent[2]);
                        using (var bonesSavGz = new BonesSavGz(The.Game.GameID, memoryStream.ToArray()))
                        {
                            SaveBonesFile(saveBonesJSON, bonesSavGz);
                        }
                    }
                    return null;
                }
                catch (Exception x)
                {
                    SerializationWriter.Release(writer);
                    HoardBonesError(The.Game.GameID, bonesSources, x, true);
                    return SaveTask = Task.FromException(x);
                }
                finally
                {
                    SerializationWriter.Release(writer);
                    MemoryHelper.GCCollectMax();
                    _HasSaveBones = null;
                    status.Dispose();
                }
            }
        }

        [RegisterMethodForDebug(true)]
        public static void EnsureWallTime(XRLGame Game)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Game), Game.GetNullString()),
                });

            if (Game == null)
                return;

            if (Game.WallTime == null)
                Game.WallTime = new Stopwatch();
            else
            {
                Game._walltime += Game.WallTime.ElapsedTicks;
                Game.WallTime.Reset();
            }
            Game.WallTime.Start();
        }

        public static void CheckBonesSave(string Path)
        {
            using var stream = File.OpenRead(Path);
            if (stream.Length < 2)
                throw new IOException("Bones file is less than two bytes.");

            Span<byte> buffer = stackalloc byte[2];
            stream.Read(buffer);
            if (buffer[0] != 31
                || buffer[1] != 139)
                throw new IOException("Bones file is missing gzip header.");
        }

        public static async void HoardBonesError(
            string BonesID,
            IEnumerable<IBonesSource> FailedSources,
            Exception X,
            bool RestoreBackup = false
            )
        {
            Utils.Error($"Error trying to {nameof(HoardBones)}", X);

            int sourceCount = FailedSources.Count();
            string sourcesCountString = sourceCount.Things(nameof(BonesFolder));
            string bonesFolders = sourceCount == 1 ? nameof(BonesFolder) : nameof(BonesFolder).Pluralize();
            string listedBonesFolders = FailedSources.Aggregate((string)null, (a, n) => Utils.NewLineDelimitedAggregator(a, n.DisplayName.Strip()));

            using var tB = TextBuilder.Get();
            if (X is UnauthorizedAccessException
                || X is IOException)
            {
                tB.Append("There was a permission error while trying to save bones to ").Append(sourcesCountString).Append(".")
                    .AppendLine().Append(ColorUtility.EscapeFormatting(X.Message));
            }
            else
            {
                tB.Append("There was an error while trying to save bones to ").Append(sourcesCountString).Append(".")
                    .AppendLine().Append("Exception type: ").Append(X.GetType());
            }
            tB.AppendLine()
                .AppendLine();

            if (RestoreBackup)
            {
                string bakFileName = $"{BonesFolder.BonesSavGzName}.bak";
                using var resultStrings = ScopeDisposedList<string>.GetFromPool();
                foreach (var source in FailedSources)
                {
                    if (source is BonesFolder folderSource)
                    {
                        var results = folderSource.RestoreBackUps(BonesID);
                        string resultString = $"{folderSource.DisplayName.Strip()}";
                        resultStrings.Add(results.Aggregate(resultString, (a, n) => Utils.NewLineDelimitedAggregator(a, $"{NBSP.ThisManyTimes(2)}: {n}".Strip())));
                    }
                }

                Utils.Info($"Attempted to restore backups for BonesID {{{BonesID}}}");
                resultStrings.Loggregate(
                    Proc: s => s,
                    Empty: $"no sources",
                    PostProc: s => $"{1.Indent()}: {s}");

                tB.Append(Utils.ModTitle).Append(" has attempted to restore a backup:")
                    .AppendLine().Append(resultStrings)
                    .AppendLine()
                    .AppendLine();
            }
            tB.Append("Caves of Qud will run, but new Bones may not be created for these sources.")
                .AppendLine().Append("It'd be helpful if you could contact ").Append(Utils.AuthorOnPlatforms).Append(", because they'll probably want a copy of the problem bones.");

            await Popup.ShowFailAsync(tB.ToString());
        }

        private static void HoardBonesError(
            string BonesID,
            IEnumerable<KeyValuePair<IBonesSource, Exception>> FailedSources,
            bool RestoreBackup = false
            )
        {
            using var tB = TextBuilder.Get("There was a fatal exception attempting to save some bones. ").Append(Utils.ThisMod.DisplayTitle).Append(" attempted to recover them.")
                .AppendLine().Append("You ought to check out your bones folder(s) for recent changes:");
            
            string bakFileName = $"{BonesFolder.BonesSavGzName}.bak";
            using var resultStrings = ScopeDisposedList<string>.GetFromPool();
            foreach ((var source, var x) in FailedSources)
            {
                Utils.Error($"Error trying to {nameof(HoardBones)} for {source.DisplayName} ", x);
                tB.AppendLine()
                    .AppendLine().Append(source.DisplayName.Strip()).Append(":")
                    .AppendLine();

                if (x is UnauthorizedAccessException
                    || x is IOException)
                {
                    tB.Append("There was a permission error while trying to ").Append(nameof(HoardBones)).Append(".")
                        .AppendLine().Append(NBSP).Append(NBSP).Append(ColorUtility.EscapeFormatting(x.Message))
                        .AppendLine()
                        .AppendLine();
                }
                else
                {
                    tB.Append("There was an error while trying to ").Append(nameof(HoardBones)).Append(".")
                        .AppendLine().Append(NBSP).Append(NBSP).Append("Exception type: ").Append(x.GetType());

                    if (source is BonesFolder folderSource)
                    {
                        if (RestoreBackup)
                        {
                            tB.AppendLine().Append(NBSP).Append(NBSP).Append("An attempt was made to Restore from Backaup:");

                            var results = folderSource.RestoreBackUps(BonesID);
                            foreach (var result in results)
                                tB.AppendLine().Append(1.Indent(NBSP: true)).Append(": ").Append(result.ToString().Strip());

                            resultStrings.Add(results.Aggregate(folderSource.DisplayName.Strip(), (a, n) => Utils.NewLineDelimitedAggregator(a, $"{NBSP.ThisManyTimes(2)}: {n}".Strip())));
                        }
                        tB.AppendLine()
                            .AppendLine();
                    }
                    else
                    {
                        tB.AppendLine().Append(NBSP).Append(NBSP).Append("Restore Backup not possible with this source type (").Append(source.Type).Append(")")
                            .AppendLine()
                            .AppendLine();
                    }
                };
            }
            tB.Append("Caves of Qud will run, but new Bones won't be created for these sources.")
                .AppendLine().Append("It'd be helpful if you could contact ").Append(Utils.AuthorOnPlatforms).Append(", because they'll probably want a copy of the problem bones.");

            Utils.Info($"Attempted to restore backups for BonesID {{{BonesID}}}");
            resultStrings.Loggregate(
                Proc: s => s,
                Empty: $"{1.Indent()}no sources");

            Popup.WaitNewPopupMessage(
                message: tB.ToString(),
                buttons: UIUtils.BummerButton,
                title: "Error reading bones sources.");
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

                // marks significant adjustment to how serializable sets are serialized and broke back-compat.
                var v0039 = new XRL.Version(0, 0, 3, 9);
                if (modVersion >= v0039
                    && saveBonesVersion < v0039)
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
            var saveBonesInfos = new List<SaveBonesInfo>();

            using var bonesSources = GetBonesSourcesFromPool(BonesSources.SourceCanExhume);
            // Utils.Log($"{nameof(BonesManager)}.{nameof(GetSaveBonesInfoAsync)}, Sources:");
            using var failedFileSources = ScopeDisposedList<KeyValuePair<IBonesSource, Exception>>.GetFromPool();
            foreach (var bonesSource in bonesSources)
            {
                try
                {
                    string currentBonesID = null;
                    // Utils.Log($"{1.Indent()}{bonesSource.DisplayName.Strip()}");
                    foreach (var saveBonesInfo in await bonesSource.ListBonesInfosAsync())
                    {
                        try
                        {
                            currentBonesID = saveBonesInfo?.ID;
                            // saveBonesInfo.AddSource(bonesSource);
                            if (!IncludeBlocked
                                && bonesSource.IsBonesBlocked(saveBonesInfo.ID))
                            {
                                // Utils.Log($"{2.Indent()}[blocked] {saveBonesInfo.ID}");
                                continue;
                            }

                            if (!IncludeVersionIncompatible
                                && !IsVersionCompatible(saveBonesInfo))
                            {
                                // Utils.Log($"{2.Indent()}[incpVer] {saveBonesInfo.ID}");
                                continue;
                            }

                            if (Where?.Invoke(saveBonesInfo) is not false)
                            {
                                if (saveBonesInfos.FirstOrDefault(b => b.ID == saveBonesInfo.ID) is SaveBonesInfo existingInfo)
                                {
                                    existingInfo.AddSource(bonesSource);
                                    saveBonesInfo.Dispose();
                                    {
                                        // Utils.Log($"{2.Indent()}[present] {saveBonesInfo.ID}");
                                        continue;
                                    }
                                }
                                saveBonesInfos.Add(saveBonesInfo);
                                {
                                    // Utils.Log($"{2.Indent()}[added!!] {saveBonesInfo.ID}");
                                    continue;
                                }
                            }
                            else
                                saveBonesInfo.Dispose();
                        }
                        catch (Exception x)
                        {
                            Utils.Warn($"Exception adding BonesInfo {{{currentBonesID ?? "MISSING_ID"}}}, {bonesSource?.DisplayName?.Strip() ?? "MISSING_SOURCE"}", x);
                        }
                    }
                }
                catch (Exception x)
                {
                    if (!bonesSource.IsOnline())
                        failedFileSources.Add(new (bonesSource, x));
                    else
                        Utils.Error($"{bonesSource.DisplayName} failed to {nameof(bonesSource.ListBonesInfosAsync)}", x);
                }
            }
            saveBonesInfos.RemoveAll(bi => bonesSources.Any(bs => bs.IsBonesBlocked(bi.ID)));

            if (!failedFileSources.IsNullOrEmpty())
                SeriousExhumeBonesError(failedFileSources);

            if (saveBonesInfos.IsNullOrEmpty()
                && NoBonesPlaceholder != null)
            {
                // Utils.Log($"{2.Indent()}[dummy!] {NoBonesPlaceholder.ID}");
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
        {
            _HasSaveBones = null;
            UD_Bones_BrokenLunarRegent.ClearCachedSaveBonesInfo();
        }

        public static async Task<IEnumerable<SaveBonesInfo>> GetSaveBonesInfoAsync(bool IncludeVersionIncompatible = false)
            => await GetSaveBonesInfoAsync(null, IncludeVersionIncompatible: IncludeVersionIncompatible)
            ;

        public static async Task<IEnumerable<SaveBonesInfo>> GetCrematableSaveBonesInfoAsync(bool IncludeVersionIncompatible = false)
            => await GetSaveBonesInfoAsync(
                Where: bi => bi.BonesSources.Any(bs => bs.IsOnDisk()),
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

        public void ClearEligibleSaveBonesInfoCache()
            => EligibleSaveBonesInfoCache = null
            ;

        public async Task<IEnumerable<SaveBonesInfo>> GetEligibleSaveBonesInfoAsync(
            Predicate<SaveBonesInfo> Where = null,
            bool IgnoreCache = true
            )
        {
            if (IgnoreCache
                || !Options.EnableOsseousAshDownloads
                || !ServerUtils.AllHosts(h => !h.Enabled || h.IsOnCooldown).IsNullOrEmpty()
                || EligibleSaveBonesInfoCache == null)
                EligibleSaveBonesInfoCache = new(await GetSaveBonesInfoAsync(bones => bones.IsLooselyEligible && (Where?.Invoke(bones) is not false)));

            return EligibleSaveBonesInfoCache;
        }

        public IEnumerable<SaveBonesInfo> GetEligibleSaveBonesInfo(
            Predicate<SaveBonesInfo> Where = null,
            bool IgnoreCache = true
            )
            => GetEligibleSaveBonesInfoAsync(Where, IgnoreCache)
                .WaitResult()
                .IteratorSafe()
            ;

        private static void SeriousExhumeBonesError(
            IEnumerable<KeyValuePair<IBonesSource, Exception>> FailedSources
            )
        {
            string listedBonesFolders = FailedSources.Aggregate((string)null, (a, n) => Utils.NewLineDelimitedAggregator(a, n.Key.DisplayName.Strip()));
            using var tB = TextBuilder.Get();
            foreach ((var source, var x) in FailedSources)
            {
                Utils.Error($"Error checking {source.DisplayName} for save files", x);
                if (x is UnauthorizedAccessException
                    || x is IOException)
                {
                    tB.Append("There was a permission error while ").Append(source.DisplayName).Append(" was trying to access bones files.")
                        .AppendLine().Append(NBSP).Append(NBSP).Append(ColorUtility.EscapeFormatting(x.Message))
                        .AppendLine()
                        .AppendLine();
                }
                else
                {
                    tB.Append("There was an error while ").Append(source.DisplayName).Append(" was trying to access ").Append(".")
                        .AppendLine().Append(NBSP).Append(NBSP).Append("Exception type: ").Append(x.GetType())
                        .AppendLine()
                        .AppendLine();
                }
            }
            tB.Append("Caves of Qud will run, but offline Bones won't be available. Please check your directory’s permissions.");

            Popup.WaitNewPopupMessage(
                message: tB.ToString(),
                buttons: UIUtils.BummerButton,
                title: "Error reading bones sources.");
        }

        [RegisterAsyncMethodForDebug(true)]
        public async Task<BonesData> ExhumeLunarRegentAsync(SaveBonesInfo BonesInfo, bool NoStatus = false)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), $"{{{BonesInfo.ID}}}"),
                    Debug.Arg(nameof(NoStatus), NoStatus),
                });

            int fileVersion = -1;
            string versionString = "{unknown}";

            BonesData bonesData = null;

            bool errorPopups = ShowErrorPopups;
            try
            {
                var reader = SerializationReader.Get();

                Loading.Status status = default;
                if (!NoStatus)
                    status = Loading.StartTask($"Exhuming Lunar Regent");

                Utils.Info($"Attempting to exhume Lunar Regent {{{BonesInfo.ID}}} ({nameof(BonesInfo.ModVersion)}: {BonesInfo.GetModVersion()})");

                try
                {
                    var memory = reader.Stream;

                    if (!await TryGetBonesMemoryStream(BonesInfo, memory))
                    {
                        Debug.CheckNah($"{nameof(FatalDeserializationVersionException)}, Failed to retreive BonesMemoryStream", Indent: indent[1]);
                        throw new FatalDeserializationVersionException($"Failed to retreive BonesMemoryStream");
                    }
                    Debug.CheckYeh($"Retreived BonesMemoryStream", Indent: indent[2]);

                    ShowErrorPopups = false;

                    Debug.Log($"{nameof(reader)}.{nameof(SerializationExtensions.StartMetricsOff)}", Indent: indent[2]);
                    reader.StartMetricsOff();

                    ShowErrorPopups = errorPopups;

                    ReadBonesCheckVersion(BonesInfo, reader, out fileVersion, out versionString);
                    TryReadBonesCheckBonesSpec(reader);
                    ReadBonesPseudoZone(BonesInfo, reader, out var loadedPseudoZone, ref errorPopups);
                    ReadBonesFinalize(BonesInfo, reader, ref errorPopups);

                    bonesData = new BonesData
                    {
                        BonesID = BonesInfo.ID,
                        PseudoZone = loadedPseudoZone,
                        Handle = BonesInfo.UserHandle,
                    };

                    if (reader.Errors > 0)
                        SerializationExtensions.PerformOptionallySilently(() => DisplayLoadError(reader, "bones file", reader.Errors));
                }
                catch (Exception x)
                {
                    ShowErrorPopups = false;
                    Utils.Error(x);
                    ShowErrorPopups = errorPopups;
                    bonesData = null;
                }
                finally
                {
                    SerializationReader.Release(reader);
                    status.Dispose();
                    ShowErrorPopups = errorPopups;
                }

            }
            catch (Exception x)
            {
                string message = $"That bones file appears to be corrupt, " +
                    $"you can try to restore the backup in your bones folder ({BonesInfo.BonesBakDisplay}) " +
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
                    if (fileVersion < XRLGame.SaveVersion)
                        message = $"That bones file looks like it's from an older game save format revision ({versionString}). Sorry!\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";
                    else
                    if (fileVersion > XRLGame.SaveVersion)
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
                ShowErrorPopups = errorPopups;
            }

            return bonesData;
        }

        [RegisterAsyncMethodForDebug(true)]
        private async Task<bool> TryGetBonesMemoryStream(
            SaveBonesInfo BonesInfo,
            MemoryStream Memory
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), $"{{{BonesInfo.ID}}}"),
                    Debug.Arg(nameof(Memory), Memory.GetNullString()),
                });

            /* Delete this and the method it references?
            
            BonesSavGz bonesSavGz = null;

            Debug.Log($"Getting savGzStream", Indent: indent[0]);
            if (!await BonesInfo.TryGetBonesSavGzStreamAsync(bsg => bonesSavGz = bsg))
            {
                Debug.CheckNah($"Failed to get BonesSavGzStream", Indent: indent[1]);
                return false;
            }*/

            Debug.Log($"Getting savGzStream", Indent: indent[0]);
            if (await BonesInfo.GetBonesSavGzStreamAsync() is not BonesSavGz bonesSavGz)
            {
                Debug.CheckNah($"Failed to get BonesSavGzStream", Indent: indent[1]);
                return false;
            }

            try
            {
                using (var savGzStream = bonesSavGz.Stream)
                {
                    Debug.Log($"Validating GZipStream", Indent: indent[2]);
                    if (savGzStream.Length < 2)
                    {
                        string message = $"{nameof(savGzStream)}.{nameof(savGzStream.Length)} less than 2";
                        Debug.CheckNah($"{nameof(FatalDeserializationVersionException)}: {message}", Indent: indent[1]);
                        throw new FatalDeserializationVersionException(message);
                    }
                    if (savGzStream.ReadByte() != 31)
                    {
                        string message = $"{nameof(savGzStream)} first byte not 31";
                        Debug.CheckNah($"{nameof(FatalDeserializationVersionException)}: {message}", Indent: indent[1]);
                        throw new FatalDeserializationVersionException(message);
                    }
                    if (savGzStream.ReadByte() != 139)
                    {
                        string message = $"{nameof(savGzStream)} second byte not 139";
                        Debug.CheckNah($"{nameof(FatalDeserializationVersionException)}: {message}", Indent: indent[1]);
                        throw new FatalDeserializationVersionException(message);
                    }
                    Debug.CheckYeh($"GZipStream validated", Indent: indent[2]);

                    savGzStream.Position = 0L;

                    Debug.Log($"Decompressing gZipStream", Indent: indent[2]);
                    using var gZipStream = new GZipStream(savGzStream, CompressionMode.Decompress);
                    await gZipStream.CopyToAsync(Memory);
                }

                Memory.Position = 0L;
            }
            finally
            {
                bonesSavGz.Dispose();
            }
            return true;
        }

        [RegisterMethodForDebug(true)]
        private static void ReadBonesCheckVersion(
            SaveBonesInfo BonesInfo,
            SerializationReader Reader,
            out int FileVersion,
            out string VersionString
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), $"{{{BonesInfo.ID}}}"),
                    Debug.Arg(nameof(Reader), Reader.GetNullString()),
                    Debug.ArgOut(nameof(FileVersion)),
                    Debug.ArgOut(nameof(VersionString)),
                });

            FileVersion = -1;
            VersionString = null;
            if (Reader.ReadInt32() != SERIALIZATION_CHECK)
            {
                Debug.CheckNah($"{nameof(SERIALIZATION_CHECK)} failed", Indent: indent[1]);
                VersionString = "2.0.167.0 or prior";
                throw new FatalDeserializationVersionException($"Bones file ({BonesInfo.ID}) is for incorrect game version.");
            }
            Debug.CheckYeh($"{nameof(SERIALIZATION_CHECK)} passed", Indent: indent[2]);

            Debug.Log($"fileVersion = {nameof(Reader)}.{nameof(Reader.FileVersion)}", Indent: indent[2]);
            FileVersion = Reader.FileVersion;
            Debug.Log($"versionString = {nameof(Reader)}.{nameof(Reader.ReadString)}", Indent: indent[2]);
            VersionString = Reader.ReadString();

            /*if (fileVersion != XRLGame.SaveVersion)
            {
                Debug.CheckNah($"{nameof(DeserializationVersionException)}, incorrect game version ({versionString})", Indent: indent[1]);
                throw new DeserializationVersionException($"Bones file ({BonesInfo.ID}) is for incorrect game version ({versionString})");
            }
            Debug.CheckYeh($"{nameof(XRLGame.SaveVersion)} passed", Indent: indent[2]);*/

            if (FileVersion < MIN_SAVE_VERSION)
            {
                Debug.CheckNah($"{nameof(FatalDeserializationVersionException)}, incorrect game version", Indent: indent[1]);
                throw new FatalDeserializationVersionException($"Bones file ({BonesInfo.ID}) is for incorrect game version");
            }
            Debug.CheckYeh($"{nameof(MIN_SAVE_VERSION)} passed", Indent: indent[2]);

            if (FileVersion > XRLGame.SaveVersion)
            {
                Debug.CheckNah($"{nameof(DeserializationVersionException)}, incorrect game version ({VersionString})", Indent: indent[1]);
                throw new DeserializationVersionException($"Bones file ({BonesInfo.ID}) is for incorrect game version ({VersionString})");
            }
            Debug.CheckYeh($"{nameof(XRLGame.SaveVersion)} passed", Indent: indent[2]);
        }

        [RegisterMethodForDebug(true)]
        private static bool TryReadBonesCheckBonesSpec(SerializationReader Reader)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Reader), Reader.GetNullString()),
                });

            if (Reader.ReadInt32() == BONES_SPEC_POS)
            {
                Reader.UnspoolTo(BONES_ZONE_POS, Prior: true);
                Debug.YehNah($"{nameof(BONES_SPEC_POS)} read, older version file; unspooled to {nameof(BONES_ZONE_POS)} [{BONES_ZONE_POS}]", Indent: indent[2]);
                return true;
            }
            Reader.Stream.Position -= 4L;
            Debug.CheckYeh($"{nameof(BONES_SPEC_POS)} missing, current version file; {nameof(Reader.Stream)}.{nameof(Reader.Stream.Position)} reversed by 4 bytes", Indent: indent[2]);
            return false;
        }

        [RegisterMethodForDebug(true)]
        private static void ReadBonesPseudoZone(
            SaveBonesInfo BonesInfo,
            SerializationReader Reader,
            out PseudoZone LoadedPseudoZone,
            ref bool ErrorPopups
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), $"{{{BonesInfo.ID}}}"),
                    Debug.Arg(nameof(Reader), Reader.GetNullString()),
                    Debug.ArgOut(nameof(LoadedPseudoZone)),
                    Debug.ArgRef(nameof(ErrorPopups), ErrorPopups),
                });

            LoadedPseudoZone = null;
            if (BonesInfo.GetModVersion() < PseudoZone.MinVersion)
            {
                Debug.CheckNah($"{nameof(DeserializationVersionException)}, version is prior to {PseudoZone.MinVersion} (unsupported)", Indent: indent[1]);
                throw new DeserializationVersionException($"Bones file ({BonesInfo.ID}) version is prior to {PseudoZone.MinVersion} (unsupported)");
            }

            try
            {
                ShowErrorPopups = false;
                if (Reader.ReadInt32() != BONES_ZONE_POS)
                {
                    Debug.CheckNah($"{nameof(DeserializationException)}, missing {nameof(PseudoZone)} val-check", Indent: indent[1]);
                    throw new DeserializationException($"Bones file ({BonesInfo.ID}) missing {nameof(PseudoZone)} val-check");
                }
                Debug.CheckYeh($"{nameof(BONES_ZONE_POS)} passed", Indent: indent[2]);

                LoadedPseudoZone = PseudoZone.Load(Reader, BonesInfo);
                Debug.CheckYeh($"{nameof(LoadedPseudoZone)} read", Indent: indent[2]);
            }
            catch (Exception x)
            {
                LoadedPseudoZone = null;
                Reader.Errors++;
                Reader.UnspoolTo(BONES_FINALIZE_POS, Prior: true);
                Debug.YehNah($"{nameof(LoadedPseudoZone)} read failed, unspooled to {nameof(BONES_FINALIZE_POS)} [{BONES_FINALIZE_POS}]", Indent: indent[2]);
                Utils.Error(x);
            }
            finally
            {
                ShowErrorPopups = ErrorPopups;
            }
        }

        [RegisterMethodForDebug(true)]
        private static void ReadBonesFinalize(
            SaveBonesInfo BonesInfo,
            SerializationReader Reader,
            ref bool ErrorPopups
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), $"{{{BonesInfo.ID}}}"),
                    Debug.Arg(nameof(Reader), Reader.GetNullString()),
                    Debug.ArgRef(nameof(ErrorPopups), ErrorPopups),
                });

            try
            {
                if (Reader.ReadInt32() != BONES_FINALIZE_POS)
                {
                    if (!Reader.UnspoolTo(BONES_FINALIZE_POS, Prior: true)
                        || Reader.ReadInt32() != BONES_FINALIZE_POS)
                    {
                        Debug.CheckNah($"{nameof(DeserializationException)}, missing Finalization val-check", Indent: indent[1]);
                        throw new DeserializationException($"Bones file ({BonesInfo.ID}) missing Finalization val-check");
                    }

                    Utils.Warn($"BonesManager has finished reading bones file but has not arrived at Finalization val-check. This bones file may have incompletely deserialized: Finalization val-check found with Unspool.");
                }
                Debug.CheckYeh($"{nameof(BONES_FINALIZE_POS)} passed", Indent: indent[2]);

                Debug.Log($"fileVersion = {nameof(Reader)}.{nameof(SerializationExtensions.FinalizeReadMetricsOff)}", Indent: indent[2]);
                Reader.FinalizeReadMetricsOff();
            }
            catch (Exception x)
            {
                Reader.Errors++;

                ShowErrorPopups = false;
                Utils.Error(x);
            }
            finally
            {
                ShowErrorPopups = ErrorPopups;
            }
        }

        public static void DisplayLoadError(SerializationReader Reader, string Loadable, int Errors = 1)
        {
            bool isSingleError = Errors == 1;
            var tB = TextBuilder.Get()
                .Append("There").Compound(isSingleError ? "was" : "were").Compound(Errors)
                .Compound(isSingleError ? "error" : "errors")
                .Compound("while loading this")
                .Compound(Loadable)
                .Append('.');

            bool isMissingMods = false;
            foreach (string item in Reader.GetSavedMods().Except(ModManager.GetRunningMods()))
            {
                isMissingMods = true;
                tB.Append("\n\nMissing mods: ");
                tB.Append(ModManager.GetModTitle(item));
            }

            if (isMissingMods)
                tB.Append('.');

            Utils.Warn(tB);
        }

        public BonesData ExhumeLunarRegent(SaveBonesInfo SaveBonesInfo, bool NoStatus = false)
            => ExhumeLunarRegentAsync(SaveBonesInfo, NoStatus).WaitResult()
            ;

        public static async Task<SaveBonesInfo> GetBonesInfoByIDAsync(string BonesID, Predicate<SaveBonesInfo> Where = null)
        {
            foreach (var savedBones in await GetSaveBonesInfoAsync(Where))
                if (savedBones.ID == BonesID)
                    return savedBones;

            return null;
        }

        public static async Task<SaveBonesInfo> GetBonesInfoByIDDirectAsync(
            string BonesID,
            bool IncludeVersionIncompatible = false,
            bool IncludeBlocked = false
            )
        {
            if (BonesID.IsNullOrEmpty())
                return null;

            if (!Guid.TryParse(BonesID, out _))
                return null;

            using var bonesSources = GetBonesSourcesFromPool(BonesSources.SourceCanExhume);

            if (!IncludeBlocked
                && bonesSources.Any(bs => bs.IsBonesBlocked(BonesID)))
                return null;

            var bonesInfo = await bonesSources.GetBonesInfoAsync(BonesID);

            if (!IncludeVersionIncompatible
                && !IsVersionCompatible(bonesInfo))
                return null;

            return bonesInfo;
        }

        public static SaveBonesInfo GetSavedBonesByIDDirect(
            string BonesID,
            bool IncludeVersionIncompatible = false,
            bool IncludeBlocked = false
            )
            => GetBonesInfoByIDDirectAsync(
                    BonesID: BonesID,
                    IncludeVersionIncompatible: IncludeVersionIncompatible,
                    IncludeBlocked: IncludeBlocked)
                .WaitResult()
            ;

        public static SaveBonesInfo GetSavedBonesByID(string BonesID, Predicate<SaveBonesInfo> Where = null)
            => GetBonesInfoByIDAsync(BonesID, Where).WaitResult()
            ;

        public static bool TryGetSaveBonesByID(string BonesID, out SaveBonesInfo SaveBonesInfo, Predicate<SaveBonesInfo> Where = null)
            => (SaveBonesInfo = GetSavedBonesByID(BonesID, Where)) != null
            ;

        public bool HasBlueprintReplacement(string Blueprint)
            => BlueprintReplacementsByMissingBlueprint.ContainsKey(Blueprint);

        public bool HasTileReplacement(string Blueprint)
            => TileReplacementsByMissingBlueprint.ContainsKey(Blueprint);

        public ReplacementEntry CacheDefaultReplacementEntryForKey(string Key)
            => MissingBlueprintReplacements[Key] = ReplacementEntry.CreateDefaultFor(Key)
            ;

        public ReplacementEntry RequireReplacementEntryForBlueprintSpec(BlueprintSpec BlueprintSpec)
        {
            if (BlueprintSpec.Blueprint is not string key)
                return null;

            //Utils.Log($"{nameof(RequireReplacementEntryForBlueprintSpec)} for {key}");
            //Utils.Log(JsonConvert.SerializeObject(BlueprintSpec, Formatting.Indented));
            MissingBlueprintReplacements ??= new();
            try
            {
                if (!MissingBlueprintReplacements.TryGetValue(key, out var replacementEntry))
                {
                    //Utils.Log($"{nameof(RequireReplacementEntryForBlueprintSpec)} for {key}");
                    //Utils.Log(JsonConvert.SerializeObject(BlueprintSpec, Formatting.Indented));
                    if (BlueprintSpec.GetOrderedSimilarityRecords() is not IEnumerable<BlueprintSpec.SimilarityRecord> orderedSimilarityRecords)
                    {
                        //Utils.Log($"{1.Indent()}[{CROSS}] {nameof(orderedSimilarityRecords)} is null");
                        return CacheDefaultReplacementEntryForKey(key);
                    }

                    using var similarityRecords = ScopeDisposedList<BlueprintSpec.SimilarityRecord>.GetFromPoolFilledWith(orderedSimilarityRecords);
                    
                    if (similarityRecords.IsNullOrEmpty())
                    {
                        //Utils.Log($"{1.Indent()}[{CROSS}] {nameof(similarityRecords)} is empty");
                        return CacheDefaultReplacementEntryForKey(key);
                    }

                    int maxSimilarity = similarityRecords.FirstOrDefault().Similarity;

                    int halfRecordsCount = similarityRecords.Count / 2;

                    while (similarityRecords.Count > 10
                        && similarityRecords.Count > halfRecordsCount)
                        similarityRecords.RemoveLast();

                    for (int i = similarityRecords.Count - 1; i >= 0; i--)
                    {
                        if (similarityRecords.Count <= 10)
                            break;

                        if (similarityRecords[i].Similarity < (int)(maxSimilarity * 0.75))
                            similarityRecords.RemoveAt(i);
                    }

                    var specsByWeight = new Dictionary<string, int>();
                    //Utils.Log($"{1.Indent()}{key} shortlist ({similarityRecords.Count}):");
                    foreach (var similarityRecord in similarityRecords)
                    {
                        if (similarityRecord.ToString() is not string specWeightKey)
                            continue;

                        if (!specsByWeight.ContainsKey(specWeightKey))
                            specsByWeight[specWeightKey] = 0;

                        specsByWeight[specWeightKey] += similarityRecord.GetWeight();

                        //Utils.Log($"{2.Indent()}{key} is {similarityRecord.DebugString()}");
                    }

                    if (specsByWeight.IsNullOrEmpty())
                    {
                        //Utils.Log($"{1.Indent()}[{CROSS}] {nameof(specsByWeight)} is null or empty");
                        return CacheDefaultReplacementEntryForKey(key);
                    }

                    string rndSeed = $"{nameof(Mod.BlueprintSpec)}::{BlueprintSpec.Blueprint}::{nameof(replacementEntry)}";
                    var rnd = Stat.GetSeededRandomGenerator(rndSeed);

                    GameObjectBlueprint alternativeBlueprint = null;
                    var specsBag = specsByWeight.ToBallBag(rnd);
                    while (!specsBag.IsNullOrEmpty()
                        && alternativeBlueprint == null)
                    {
                        if (specsBag.PickOne() is not string pickedSpec)
                            continue;

                        alternativeBlueprint = GameObjectFactory.Factory.GetBlueprintIfExists(pickedSpec);

                        if (alternativeBlueprint.GetRenderable()?.Tile == null)
                            alternativeBlueprint = null;
                    }

                    if (alternativeBlueprint == null)
                    {
                        //Utils.Log($"{1.Indent()}[{CROSS}] {nameof(alternativeBlueprint)} is null");
                        return CacheDefaultReplacementEntryForKey(key);
                    }

                    string tile = alternativeBlueprint.GetRenderable()?.Tile;
                    if (rnd.Next(0, 7000) % 500 == 0)
                    {
                        GameObjectBlueprint alternativeForTile = null;
                        while (!specsBag.IsNullOrEmpty()
                            && alternativeForTile != null)
                        {
                            if (specsBag.PickOne() is not string pickedSpec)
                                continue;

                            alternativeForTile = GameObjectFactory.Factory.GetBlueprintIfExists(pickedSpec);

                            if (alternativeForTile.GetRenderable()?.Tile == null)
                                alternativeForTile = null;
                        }

                        if (alternativeForTile != null)
                            tile = alternativeForTile.GetRenderable()?.Tile;
                    }
                    replacementEntry = ReplacementEntry.CreateFor(key, alternativeBlueprint.Name, tile);
                    //Utils.Log(JsonConvert.SerializeObject(replacementEntry, Formatting.Indented));
                    MissingBlueprintReplacements[key] = replacementEntry;
                }
                //Utils.Log(JsonConvert.SerializeObject(replacementEntry, Formatting.Indented));
                return replacementEntry;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(RequireReplacementEntryForBlueprintSpec)}({key}), find alternate", x);
                return CacheDefaultReplacementEntryForKey(key);
            }
        }

        public bool TryGetReplacementEntry(BlueprintSpec BlueprintSpec, out ReplacementEntry ReplacementEntry)
        {
            ReplacementEntry = null;
            if (BlueprintSpec.Blueprint is not string key)
                return false;

            return MissingBlueprintReplacements.TryGetValue(key, out ReplacementEntry);
        }

        public bool TryGetReplacementEntry(GameObject GameObject, out ReplacementEntry ReplacementEntry)
        {
            ReplacementEntry = null;

            if (GameObject == null)
                return false;

            if (!GameObject.TryGetPart(out UD_Bones_BlueprintSpec blueprintSpecPart))
                return false;

            if (blueprintSpecPart.BlueprintSpec is not BlueprintSpec blueprintSpec)
                return false;

            if (blueprintSpec.Blueprint is not string key)
                return false;

            return MissingBlueprintReplacements.TryGetValue(key, out ReplacementEntry);
        }

        public static async Task<IEnumerable<SaveBonesInfo>> CremateAllLunarRegentsAsync(
            Predicate<SaveBonesInfo> Where,
            Action BeforeDeletionLoop,
            Action AfterDeletionLoop
            )
        {
            using var bonesSources = await BonesSources.SelectBonesSourcesFromPoolAsync(YieldBonesSources(), DefaultValue: true, FirstDefaultOnly: false, Action: "Cremate All");

            if (bonesSources == null)
            {
                //Debug.CheckNah($"{nameof(bonesSources)} is null", Indent: indent[2]);
                return (await GetSaveBonesInfoAsync()).IteratorSafe();
            }

            if (bonesSources.Count < 1)
            {
                //Debug.CheckNah($"{nameof(bonesSources)} is empty", Indent: indent[2]);
                Popup.NewPopupMessageAsync($"Cremation of all bones cancelled!", PopupMessage.AcceptButton).WaitResult();
                return (await GetSaveBonesInfoAsync()).IteratorSafe();
            }
            string bonesSourcesString = bonesSources.Aggregate((string)null, (a, n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent(2, NBSP: true)}: {n.DisplayName}"));

            using var bonesInfos = bonesSources.GetBonesInfosFromPool(Where);

            var buttons = PopupMessage.AcceptCancelButtonWithoutHotkey;
            if (CapabilityManager.CurrentPlatformClassification() != CapabilityManager.PlatformClassification.PC)
                buttons = PopupMessage.AcceptCancelButton;

            string title = $"Cremate All".Colored("R");
            string confirmText = "CREMATE";

            using var tB = TextBuilder.Get();
            tB.Append("Are you sure you want to cremate ").AppendColored("red", $"all {bonesInfos.Count}").Append("bones from the ").Append(bonesSources.Count.Things(" below source")).Append("?")
                .AppendLine().Append(bonesSourcesString);

            string defaultValue = null;
            if (CapabilityManager.CurrentPlatformClassification() != CapabilityManager.PlatformClassification.Console)
            {
                defaultValue = string.Empty;
                tB.AppendLine()
                    .AppendLine().Append("Type '").Append(confirmText).Append("' to confirm.");
            }

            if ((await UIUtils.AskStringAsync(
                    Message: tB.ToString(),
                    Default: defaultValue,
                    WantsSpecificPrompt: confirmText)
                ) != confirmText)
            /*if ((await Popup.AskStringAsync(
                    Message: $"Are you sure you want to cremate {$"all {bonesInfos.Count}".Colored("red")} bones from the {bonesSources.Count.Things(" below source")}?\n{bonesSourcesString}" + typeToConfirmText,
                    Default: defaultValue,
                    WantsSpecificPrompt: confirmText,
                    MaxLength: confirmText.Length)
                ) != confirmText)*/
                return (await GetSaveBonesInfoAsync()).IteratorSafe();

            BeforeDeletionLoop?.Invoke();

            int countBefore = bonesInfos.Count;
            int paddingAmount = countBefore.ToString().Length;
            int cremateCounter = 0;
            int crematedCounter = 0;

            string paddedCremateCounter()
                => cremateCounter.ToString().PadLeft(paddingAmount, '0');

            string cremateString(string Color = null)
                => $"{paddedCremateCounter().Colored(Color)}/{countBefore}";

            var resultsByInfo = new Dictionary<SaveBonesInfo, ScopeDisposedList<KeyValuePair<IBonesSource, ActionResult>>>();
            foreach (var bonesInfo in bonesInfos)
            {
                resultsByInfo[bonesInfo] = ScopeDisposedList<KeyValuePair<IBonesSource, ActionResult>>.GetFromPool();
                foreach (var source in bonesSources)
                {
                    resultsByInfo[bonesInfo].Add(new(source, await source.DeleteBonesAsync(bonesInfo.ID)));
                }
                if (resultsByInfo[bonesInfo].Any(r => r.Value))
                    cremateCounter++;
            }

            string output = resultsByInfo.Aggregate(
                seed: null,
                func: delegate (string acc, KeyValuePair<SaveBonesInfo, ScopeDisposedList<KeyValuePair<IBonesSource, ActionResult>>> next)
                {
                    if (!acc.IsNullOrEmpty())
                        acc += "\n";

                    var bonesInfo = next.Key;
                    var results = next.Value;
                    acc += $"{bonesInfo.Name.StartReplace().ToString().Strip()} {{{bonesInfo.ID}}}\n";

                    acc += results.Aggregate(
                        seed: null,
                        func: delegate (string acc2, KeyValuePair<IBonesSource, ActionResult> next2)
                        {
                            if (!acc2.IsNullOrEmpty())
                                acc2 += "\n";

                            var source = next2.Key;
                            var result = next2.Value;
                            return acc2 + $"{1.Indent()}: {$"{1.Indent()}: no results"} {(result ? "deleted" : result)}";
                        })
                        ?? $"{1.Indent()}: no results";

                    return acc;
                });

            await Popup.ShowAsync($"{"Cremation Results:".Colored("W")}\n\n{output}");
            Utils.Log("Cremation Results:");
            Utils.Log(output);

            foreach ((var bonesInfo, var results) in resultsByInfo)
            {
                bonesInfo.Dispose();
                results.Dispose();
            }
            foreach (var bonesInfo in bonesInfos)
            {
                cremateCounter++;
                if (bonesInfo == null)
                    continue;

                Loading.SetLoadingStatus($"Cremating {cremateString()} :: {bonesInfo.Name.Strip()}");

                crematedCounter++;
                bonesInfo.CremateFromAllSources();
            }

            AfterDeletionLoop?.Invoke();

            using var newBonesInfos = bonesSources.GetBonesInfosFromPool();

            string crematedString = crematedCounter.ToString();
            if (crematedCounter != countBefore)
                crematedString = crematedString.Colored("red");

            bool isSomethingWrong = newBonesInfos.Any(bi => bi.BonesSources.Any(bs => bs.IsOnDisk()));

            string somethingWrongString = isSomethingWrong ? "\n\n{{K|(something went wrong)}}" : null;
            await Popup.NewPopupMessageAsync($"{crematedString}/{countBefore} Bones Cremated!{somethingWrongString}");
            Loading.SetLoadingStatus(null);
            return newBonesInfos;
        }

        public static async Task<IEnumerable<SaveBonesInfo>> CremateAllLunarRegentsAsync()
            => await CremateAllLunarRegentsAsync(
                Where: null,
                BeforeDeletionLoop: null,
                AfterDeletionLoop: null)
            ;

        public bool IsWorldMapOrNoBones(Zone Z)
            => Z.IsWorldMap()
            || (ZoneBones.TryGetValue(Z.ZoneID, out var allocation)
                && allocation.Type == ZoneBonesAllocation.AllocationTypes.None)
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

        public bool AttemptLoadBones(
            Zone Z,
            SaveBonesInfo PickedBones,
            ZoneBonesAllocation.AllocationTypes Type,
            out bool Blocked,
            string Context = null
            )
        {
            Alerted ??= new();
            BonesData bonesData = null;
            string bonesID = PickedBones.ID;
            Blocked = false;
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
                            callback: result =>
                            {
                                if (result == DialogResult.Yes)
                                    PickedBones.CremateFromAllSources();
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
                            callback: result =>
                            {
                                if (result == DialogResult.Yes)
                                    PickedBones.CremateFromAllSources();
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
                if (!bonesData.Apply(
                    Zone: Z,
                    BonesInfo: PickedBones,
                    Type: Type,
                    LunarRegent: out var lunarRegent,
                    Blocked: out Blocked,
                    Context: Context)
                    || Blocked)
                {
                    if (Blocked)
                        EncounteredBones(bonesID);
                    else
                        Z.SetZoneProperty(nameof(bonesData.BonesID), null);

                    FailedToLoad(bonesID);
                    return false;
                }

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
            bool LogEncountered = true,
            Action<GameObject> ProcPreLoad = null,
            Action<GameObject> ProcPostLoad = null,
            string Context = null
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
                                    PickedBones.CremateFromAllSources();
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
                                    PickedBones.CremateFromAllSources();
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
                    ProcPostLoad: ProcPostLoad,
                    Context: Context)
                    || blocked)
                {
                    if (blocked
                        && LogEncountered)
                        EncounteredBones(bonesID);

                    FailedToLoad(bonesID);
                    return false;
                }
                if (LogEncountered)
                    EncounteredBones(bonesID);
                return true;
            }
            finally
            {
                bonesData?.Dispose();
            }
        }

        public void CheckBones(Zone Z, IZoneEvent FromEvent = null, string Context = null)
        {
            ZoneBones ??= new();
            Encountered ??= new();

            if (IsWorldMapOrNoBones(Z))
                return;

            if (!ZoneBones.TryGetValue(Z.ZoneID, out ZoneBonesAllocation allocation))
                allocation = ZoneBones[Z.ZoneID] = ZoneBonesAllocation.GetForZone(Z);

            if (allocation.IsBlocked())
                return;

            bool blocked = false;
            if (allocation.BonesID is string existingBonesID)
            {
                if (FromEvent.GetType() == typeof(ZoneBuiltEvent)
                    && !existingBonesID.IsNullOrEmpty()
                    && GetSavedBonesByID(existingBonesID) is SaveBonesInfo existingSaveBones)
                {
                    if (!existingSaveBones.AttemptLoad(
                        Z: Z,
                        Type: allocation.Type,
                        Blocked: out blocked,
                        Context: Context)
                        && blocked)
                    {
                        allocation.SetBlocked();
                    }
                    return;
                }
            }

            if (FromEvent.GetType() != typeof(ZoneBuiltEvent)
                && FromEvent.GetType() != typeof(ZoneActivatedEvent))
                return;

            if (Options.DebugEnableNoExhuming
                && !Options.DebugEnablePickingBones)
                return;

            if (The.Player is not GameObject player)
                return;

            var playerSpec = BonesSpec.GetPlayerSpec(Z);

            bool notEncounteredAndWithinSpec(BonesSpec BonesSpec)
                => !Encountered.Contains(BonesSpec?.ID)
                && !FailedToLoadBones.Contains(BonesSpec?.ID)
                && BonesSpec?.IsWithinSpec(playerSpec) is true
                ;

            bool infoNotEncounteredAndWithinSpec(SaveBonesInfo SaveBonesInfo)
                => notEncounteredAndWithinSpec(SaveBonesInfo.BonesSpec)
                ;

            if (GetEligibleSaveBonesInfo(infoNotEncounteredAndWithinSpec, IgnoreCache) is IEnumerable<SaveBonesInfo> bonesInfos
                && !bonesInfos.IsNullOrEmpty())
            {
                var allocationType = allocation.Type;
                bool canRollIt = Options.DebugEnableForcePickingBones
                        && ChancePermyriadForBones < MYRIAD;

                if (allocation.WantsBones()
                    || Options.DebugEnableForcePickingBones)
                {
                    bool bonesLoaded = false;
                    if (!allocation.WantsBones())
                        allocation.AssignType(Z);

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

                        var icon = new BonesRender(GameObjectFactory.Factory.GetBlueprintIfExists("UD The Lunar Face"), HFlip: false, IsMad: true);

                        if (bonesInfos.Any(b => b.IsMad))
                            icon.SetTile(MOON_KING_FEVER_TILE);

                        using var options = new CascadablePickOptionDataSetAsync<SaveBonesInfo>(UIUtils.CascadableResult.CancelSilent, UIUtils.CascadableResult.CancelSilent);
                        do
                        {
                            if (failedBonesList.Count >= bonesInfos.Count())
                            {
                                result = UIUtils.CascadableResult.CancelSilent;
                                break;
                            }

                            options.ClearItems(Dispose: true);

                            // Add None Please:
                            options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                            {
                                Text = "none please",
                                Hotkey = 'n',
                                Icon = SaveBonesInfo.NonePleaseIcon,
                                Callback = e => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                            });

                            // Add Roll It!:
                            if (canRollIt)
                            {
                                options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                                {
                                    Text = "roll it!".Color("yellow"),
                                    Hotkey = 'r',
                                    Icon = SaveBonesInfo.RollItIcon,
                                    Callback = e => Task.Run(() => UIUtils.CascadableResult.BackSilent),
                                });
                            }

                            foreach (var bonesInfo in bonesInfos.OrderBy(b => b, SaveBonesInfo.SaveBonesInfoComparerDescending))
                            {
                                if (!failedBonesList.Contains(bonesInfo)
                                    && !allocation.IsBlocked())
                                {
                                    options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                                    {
                                        Element = bonesInfo,
                                        Text = bonesInfo.GetBonesPickerLine(),
                                        Hotkey = options.GetFirstAvailableHotkey(),
                                        Icon = bonesInfo.FlippedRender,
                                        Callback = e => Task.Run(delegate ()
                                        {
                                            allocation.BonesID = e.ID;
                                            var result = AttemptToLoadBones(
                                                Z: Z,
                                                BonesInfo: e,
                                                Type: allocation.Type,
                                                Blocked: out bool blocked,
                                                BeforeAttempt: info => failedBonesList.Add(info),
                                                Context: Context);

                                            if (result == UIUtils.CascadableResult.Continue
                                                && blocked)
                                                allocation.SetBlocked();

                                            return result;
                                        })
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
                                    FinalSelectedCallback: UIUtils.ShowEscancellepedAsync)
                                .WaitResult();
                        }
                        while (result.IsContinue());
                    }

                    if (result.IsCancel())
                        allocation.Type = allocationType;

                    if (!Options.DebugEnablePickingBones
                        || (result.IsBack()
                            && allocation.WantsBones()))
                    {
                        var bonesBag = new BallBag<SaveBonesInfo>(Stat.GetSeededRandomGenerator($"{nameof(CheckBones)}::{Z.ZoneID}"), bonesInfos.Count());
                        foreach (var bonesInfo in bonesInfos)
                        {
                            if (!failedBonesList.Contains(bonesInfo))
                                if (bonesInfo.EnabledMods?.GetBonesWeight() is int weight)
                                    bonesBag.Add(bonesInfo, weight);
                        }
                        while (!bonesBag.IsNullOrEmpty()
                            && bonesBag.PickOne() is SaveBonesInfo pickedBones)
                        {
                            try
                            {
                                if (AttemptToLoadBones(
                                    Z: Z,
                                    BonesInfo: pickedBones,
                                    Type: allocation.Type,
                                    Blocked: out blocked,
                                    Context: Context
                                    ).IsCancel())
                                {
                                    allocation.BonesID = pickedBones.ID;
                                    result = UIUtils.CascadableResult.CancelSilent;
                                    break;
                                }
                                if (blocked)
                                {
                                    allocation.BonesID = pickedBones.ID;
                                    allocation.SetBlocked();
                                    result = UIUtils.CascadableResult.CancelSilent;
                                    break;
                                }
                                allocation.BonesID = null;
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"Failed to load {nameof(BonesData)} for {nameof(SaveBonesInfo)}.{nameof(pickedBones.ID)} {{{pickedBones.ID}}}", x);
                            }
                        }
                    }

                    if (!bonesLoaded)
                    {
                        allocation.Type = allocationType;
                        if (blocked)
                            allocation.SetBlocked();
                    }

                    if (bonesLoaded
                        || result.IsCancel())
                        return;
                }
            }
        }

        private UIUtils.CascadableResult AttemptToLoadBones(
            Zone Z,
            SaveBonesInfo BonesInfo,
            ZoneBonesAllocation.AllocationTypes Type,
            out bool Blocked,
            Action<SaveBonesInfo> BeforeAttempt = null,
            string Context = null
            )
        {
            BeforeAttempt?.Invoke(BonesInfo);
            if (BonesInfo.AttemptLoad(
                Z: Z,
                Type: Type,
                Blocked: out Blocked,
                Context: Context))
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
            {
                Registrar.Register(AfterPseudoZoneLoadedEvent.ID);
                Registrar.Register(ZoneBuiltEvent.ID);
            }

            Registrar.Register(GetLunarRegentEvent.ID);
            base.Register(Game, Registrar);
        }

        public virtual bool HandleEvent(AfterPseudoZoneLoadedEvent E)
        {
            if (Utils.ModVersion >= PseudoZone.MinVersion)
            {
                if (E.CheckContext(PseudoZone.RECLAIM_CONTEXT))
                {
                    if (E.LunarRegent?.CurrentZone?.GetCell(0, 0) is Cell widgetCell)
                    {
                        widgetCell.RemoveObjects(UD_Bones_LunarRegentAnnouncer.IsAnnouncerWidget);
                        widgetCell.AddObject(ANNOUNCER_WIDGET, Context: $"{nameof(E.BonesID)}::{E.BonesID}");
                    }
                }
            }
            return base.HandleEvent(E);
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

        [WishCommand(Command = "manage bones report")]
        public static bool Report_WishHandler(string Parameters)
        {
            if (Parameters.IsNullOrEmpty())
                return false;

            bool any = false;
            if (Parameters.ContainsNoCase("visited")
                || Parameters.ContainsNoCase("-v"))
                any = ReportVisited_WishHandler()
                    || any
                    ;

            if (Parameters.ContainsNoCase("encountered")
                || Parameters.ContainsNoCase("-e"))
                any = ReportEncountered_WishHandler()
                    || any
                    ;

            if (Parameters.ContainsNoCase("failed")
                || Parameters.ContainsNoCase("-f"))
                any = ReportFailed_WishHandler()
                    || any
                    ;

            if (!any)
            {
                var message = $"Entered parameters, \"{Parameters}\", contains no valid parameters for this wish.\n\n" +
                    $"Try any of: \n=ud_nbps:4=: visited\n=ud_nbps:4=: encountered\n=ud_nbps:4=: failed\n\n" +
                    $"Note: including multiple will report multiple.";

                Popup.Show(
                    Message: message.StartReplace().ToString(),
                    Title: "Invalid Parameters".Colored("yellow"));
            }

            return any;
        }

        //[WishCommand(Command = "manage bones report visited")]
        public static bool ReportVisited_WishHandler()
        {
            try
            {
                string noSystem = $"{1.Indent()}: {"System not instantiated".Colored("red")} (this is probably a bug).";
                string title = $"{nameof(BonesManager)} Visited Zones";
                Utils.Log($"{title}:");
                System?.ZoneBones?.Values.Loggregate(
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}: {s}");
                if (System == null)
                    Utils.Log(noSystem.Strip());

                string aggregate = System?.ZoneBones?.Values?.Aggregate(
                    seed: (string)null,
                    func: (a, n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent()}: {n}"));

                if (aggregate.IsNullOrEmpty())
                    aggregate = $"{1.Indent()}: none";
                if (System == null)
                    aggregate = noSystem;

                string message = $"{title.Colored("yellow")}\n{aggregate}";
                Popup.Show(message);
            }
            catch (Exception x)
            {
                Utils.Error($"Wish: \"{"manage bones report ZoneBones"}\"", x);
                return false;
            }

            return true;
        }

        //[WishCommand(Command = "manage bones report encountered")]
        public static bool ReportEncountered_WishHandler()
        {
            try
            {
                string noSystem = $"{1.Indent()}: {"System not instantiated".Colored("red")} (this is probably a bug).";

                var encounteredInfos = System?.Encountered?.Select(id => GetSavedBonesByID(id));

                string getReportString(SaveBonesInfo BonesInfo, bool Strip = false)
                    => $"{(Strip ? BonesInfo?.GetName()?.Strip() : BonesInfo?.GetName()) ?? "MISSING_INFO"} {{{BonesInfo?.ID ?? "MISSING_ID"}}}"
                    ;

                string title = $"{nameof(BonesManager)} Encountered Bones";
                Utils.Log($"{title}:");
                encounteredInfos?.Loggregate(
                    Proc: b => getReportString(b, Strip: true),
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}: {s}");
                if (System == null)
                    Utils.Log(noSystem.Strip());

                string aggregate = encounteredInfos?.Aggregate(
                    seed: (string)null,
                    func: (a, n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent()}: {getReportString(n)}"));

                if (aggregate.IsNullOrEmpty())
                    aggregate = $"{1.Indent()}: none";
                if (System == null)
                    aggregate = noSystem;

                string message = $"{title.Colored("yellow")}\n{aggregate}";
                Popup.Show(message);
            }
            catch (Exception x)
            {
                Utils.Error($"Wish: \"{"manage bones report encountered"}\"", x);
                return false;
            }

            return true;
        }

        //[WishCommand(Command = "manage bones report failed")]
        public static bool ReportFailed_WishHandler()
        {
            try
            {
                string noSystem = $"{1.Indent()}: {"System not instantiated".Colored("red")} (this is probably a bug).";
                string title = $"{nameof(BonesManager)} Failed Bones";
                Utils.Log($"{title}:");
                System?.FailedToLoadBones.Loggregate(
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}: {s}");
                if (System == null)
                    Utils.Log(noSystem.Strip());

                string aggregate = System?.FailedToLoadBones?.Aggregate(
                    seed: (string)null,
                    func: (a, n) => Utils.NewLineDelimitedAggregator(a, $"{1.Indent()}: {n}"));

                if (aggregate.IsNullOrEmpty())
                    aggregate = $"{1.Indent()}: none";
                if (System == null)
                    aggregate = noSystem;

                string message = $"{title.Colored("yellow")}\n{aggregate}";
                Popup.Show(message);
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
