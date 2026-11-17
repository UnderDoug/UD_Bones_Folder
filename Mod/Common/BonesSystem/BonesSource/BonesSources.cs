using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using XRL.Collections;
using XRL.World.Text;

using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Logging;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.UI;

using static UD_Bones_Folder.Mod.UI.UIUtils;
using Qud.UI;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    [HasRegisterMethodForDebug]
    public class BonesSources
        : PooledList<IBonesSource>
        , IVomitable<BonesSources>
    {
        public class BonesSourceEqualityComparer : CompositeEqualityComparer<IBonesSource>
        {
            public override bool Equals(IBonesSource x, IBonesSource y)
            {
                if (x?.SourceID is null
                    || y?.SourceID is null)
                    return (x?.SourceID is null) == (y?.SourceID is null);

                return x.SameAs(y);
            }

            public override int GetHashCode(IBonesSource obj)
                => obj?.SourceID?.GetHashCode()
                ?? 0
                ;
        }

        public static BonesSourceEqualityComparer DefaultEQC = new();

        public static Comparer<IBonesSource> TypeComparer = Comparer<IBonesSource>.Create(CompareTypePriority);

        protected static ConcurrentBag<BonesSources> UnusedPool = new();

        protected bool Pooled;

        [NoVomit]
        public bool OptIn => true;

        [DoVomit]
        private IBonesSource[] Sources => this.ToArray();

        public static BonesSources GetFromPool()
        {
            if (UnusedPool.TryTake(out var result))
            {
                result.Reuse();
                return result;
            }

            return new BonesSources();
        }

        public static BonesSources GetFromPoolFilledWith(IEnumerable<IBonesSource> items)
        {
            BonesSources fromPool = GetFromPool();
            fromPool.AddDistinctRange(items);
            return fromPool;
        }

        public static BonesSources GetFromPoolFilledWith(IReadOnlyCollection<IBonesSource> items)
        {
            BonesSources fromPool = GetFromPool();
            fromPool.AddDistinctRange(items);
            return fromPool;
        }

        public static BonesSources GetFromPoolFilledWith(IReadOnlyList<IBonesSource> items)
        {
            BonesSources fromPool = GetFromPool();
            fromPool.AddDistinctRange(items);
            return fromPool;
        }

        protected void Reuse()
        {
            if (!Pooled)
                throw new Exception("Attempt to reuse ScopeDisposedList not in pool");

            Pooled = false;
        }

        public override void Dispose()
        {
            if (!Pooled)
            {
                base.Dispose();
                Pooled = true;
                UnusedPool.Add(this);
            }
        }

        public virtual bool AddDistinct(IBonesSource Item)
        {
            if (this.Any(e => DefaultEQC.Equals(e, Item)))
                return false;

            Add(Item);
            Sort(TypeComparer); 
            return true;
        }

        public virtual int AddDistinctRange(IEnumerable<IBonesSource> Items)
            => Items.IteratorSafe()
                .Aggregate(0, (a, n) => a + (AddDistinct(n) ? 1 : 0))
            ;

        public virtual BonesSources CloneFromPool()
            => GetFromPoolFilledWith(this)
            ;

        public virtual BonesSources Intersect(BonesSources Other)
        {
            if (!Other.IsNullOrEmpty())
                this.RemoveAll(bs => !Other.Any(obs => DefaultEQC.Equals(obs, bs)));

            return this;
        }

        public ScopeDisposedList<IBonesSource.SourceType> GetDistinctSourcesFromPoolFor(string BonesID)
        {
            var distinctResults = ScopeDisposedList<IBonesSource.SourceType>.GetFromPool();
            foreach (var source in this)
            {
                if (!source.HasBones(BonesID))
                    continue;

                var result = source.Type;
                if (distinctResults.Contains(result))
                    continue;

                distinctResults.Add(result);
            }
            return distinctResults;
        }

        public ScopeDisposedList<string> GetBonesIDsFromPool()
        {
            var results = ScopeDisposedList<string>.GetFromPool();
            foreach (var source in this)
            {
                foreach (var bonesID in source.ListBonesIDs())
                {
                    if (results.Contains(bonesID))
                        continue;

                    results.Add(bonesID);
                }
            }
            return results;
        }

        protected static bool CheckTypePriority(IBonesSource.SourceType Type, IBonesSource.SourceType SourceType)
            => Type < SourceType
            && Type != IBonesSource.SourceType.None
            && SourceType != IBonesSource.SourceType.None
            ;

        protected static int CompareTypePriority(IBonesSource X, IBonesSource Y)
        {
            if (Utils.EitherNull(X, Y, out int nullComp))
                return nullComp;

            var xNotNone = X.Type > IBonesSource.SourceType.None;
            var yNotNone = X.Type > IBonesSource.SourceType.None;
            if (!xNotNone
                || !yNotNone)
                return xNotNone.CompareTo(yNotNone);
            
            return X.Type.CompareTo(Y.Type);
        }

        private ScopeDisposedList<T> GetBonesFileElementFromPool<T>(Predicate<T> Where = null)
            where T : IBonesFileElement
        {
            var resultTypes = new Dictionary<string, IBonesSource.SourceType>();
            var resultIDs = new Dictionary<string, T>();
            foreach (var source in this)
            {
                if (source == null)
                    continue;

                var tType = typeof(T);
                var bonesElements = Enumerable.Empty<T>();
                if (tType == typeof(SaveBonesInfo))
                    bonesElements = source.ListBonesInfos() as IEnumerable<T>;
                else
                if (tType == typeof(BonesSpec))
                    bonesElements = source.ListBonesSpecs() as IEnumerable<T>;
                else
                if (tType == typeof(ModRecordSet))
                    bonesElements = source.ListModConfigs() as IEnumerable<T>;
                else
                    throw new NotSupportedException($"{tType.Name} is not a supported type");

                foreach (var bonesElement in bonesElements)
                {
                    if (bonesElement == null)
                        continue;

                    if (Where?.Invoke(bonesElement) is false)
                        continue;

                    string bonesID = bonesElement.BonesID;
                    if (resultTypes.TryGetValue(bonesID, out var type))
                        if (CheckTypePriority(type, source.Type))
                            continue;

                    resultTypes[bonesID] = source.Type;
                    resultIDs[bonesID] = bonesElement;
                }
            }
            var result = ScopeDisposedList<T>.GetFromPoolFilledWith(resultIDs.Values);
            resultTypes.Clear();
            resultIDs.Clear();
            return result;
        }

        public ScopeDisposedList<SaveBonesInfo> GetBonesInfosFromPool(Predicate<SaveBonesInfo> Where = null)
            => GetBonesFileElementFromPool(Where)
            ;

        public ScopeDisposedList<BonesSpec> GetBonesSpecsFromPool()
            => GetBonesFileElementFromPool<BonesSpec>()
            ;

        public ScopeDisposedList<ModRecordSet> GetModRecordsFromPool()
            => GetBonesFileElementFromPool<ModRecordSet>()
            ;

        public ScopeDisposedList<string> GetBonesIDsMatchingSpecFromPool(BonesSpec PlayerSpec)
        {
            var resultTypes = new Dictionary<string, IBonesSource.SourceType>();
            foreach (var source in this)
            {
                if (source == null)
                    continue;

                foreach (var bonesID in source.GetBonesIDsMatchingSpec(PlayerSpec))
                {
                    if (bonesID.IsNullOrEmpty())
                        continue;

                    if (resultTypes.TryGetValue(bonesID, out var type))
                        if (CheckTypePriority(type, source.Type))
                            continue;

                    resultTypes[bonesID] = source.Type;
                }
            }
            var result = ScopeDisposedList<string>.GetFromPoolFilledWith(resultTypes.Keys);
            resultTypes.Clear();
            return result;
        }

        public ScopeDisposedList<ModRecordSet> GetModConfigsMatchingSpecFromPool(BonesSpec PlayerSpec)
        {
            var resultTypes = new Dictionary<string, IBonesSource.SourceType>();
            var resultIDs = new Dictionary<string, ModRecordSet>();
            foreach (var source in this)
            {
                if (source == null)
                    continue;

                foreach (var modRecord in source.GetModRecordsMatchingSpec(PlayerSpec))
                {
                    if (modRecord.IsNullOrEmpty())
                        continue;

                    string bonesID = modRecord.ID;
                    if (resultTypes.TryGetValue(bonesID, out var type))
                        if (CheckTypePriority(type, source.Type))
                            continue;

                    resultTypes[bonesID] = source.Type;
                    resultIDs[bonesID] = modRecord;
                }
            }
            var result = ScopeDisposedList<ModRecordSet>.GetFromPoolFilledWith(resultIDs.Values);
            resultTypes.Clear();
            resultIDs.Clear();
            return result;
        }

        public string GetRandomWeightedBonesIDMatchingSpec(BonesSpec PlayerSpec, Random Rnd = null)
        {
            if (PlayerSpec == null)
                return null;

            using var modConfigsMatchingSpec = GetModConfigsMatchingSpecFromPool(PlayerSpec);
            var bonesBag = new BallBag<string>(Rnd, modConfigsMatchingSpec.Count);
            foreach (var modConfig in modConfigsMatchingSpec)
                if (modConfig.GetBonesWeight() is int bonesWeight)
                    bonesBag.Add(modConfig.ID, bonesWeight);

            return bonesBag.PickOne();
        }

        public IBonesSource GetBestSource(string BonesID)
        {
            IBonesSource bestSource = null;
            foreach (var source in this.Where(bs => bs?.HasBones(BonesID) is true))
            {
                bestSource ??= source;
                if (DefaultEQC.Equals(bestSource, source))
                    continue;

                if (bestSource is BonesFolder bestFolderSource
                    && bestFolderSource.IsPreferredLocation)
                    continue;

                if (CheckTypePriority(bestSource.Type, source.Type))
                    continue;

                bestSource = source;
            }
            return bestSource ?? this.FirstOrDefault();
        }

        public bool AnyOnDisk()
            => this.Any(bs => bs.IsOnDisk())
            ;

        public bool AnyModded()
            => this.Any(bs => bs.IsModded())
            ;

        public bool AnyOnline()
            => this.Any(bs => bs.IsOnline())
            ;

        public bool AllOnDisk()
            => this.All(bs => bs.IsOnDisk())
            ;

        public bool AllModded()
            => this.All(bs => bs.IsModded())
            ;

        public bool AllOnline()
            => this.All(bs => bs.IsOnline())
            ;

        public static bool SourceCanHoard(IBonesSource BonesSource)
        {
            if (BonesSource is not BonesFolder bonesFolder)
                return BonesSource.CanSave;

            return bonesFolder.IsPreferredLocation;
        }

        public static bool SourceCanExhume(IBonesSource BonesSource)
            => BonesSource.CanLoad
            ;

        public static bool BonesSourceIsOnDiskAndPreferred(IBonesSource BonesSource)
        {
            if (BonesSource is not BonesFolder bonesFolder)
                return false;

            return bonesFolder.IsPreferredLocation;
        }

        public IEnumerable<IBonesSource> GetHoardableSources()
        {
            foreach (var source in this.Where(SourceCanHoard))
                yield return source;
        }

        public IEnumerable<IBonesSource> GetOnDiskPreferredSources()
        {
            foreach (var source in this.Where(BonesSourceIsOnDiskAndPreferred))
                yield return source;
        }

        public bool TryGetBestSource(string BonesID, out IBonesSource BestSource)
            => (BestSource = GetBestSource(BonesID)) != null
            ;

        public async Task<SaveBonesInfo> GetBonesInfoAsync(string BonesID)
        {
            if (!TryGetBestSource(BonesID, out var bestSource))
                return null;

            return await bestSource.LoadInfoAsync(BonesID);
        }

        public SaveBonesInfo GetBonesInfo(string BonesID)
        {
            if (!TryGetBestSource(BonesID, out var bestSource))
                return null;

            return bestSource.LoadInfo(BonesID);
        }

        public async Task<BonesSavGz> GetBonesSavGzAsync(string BonesID)
        {
            if (!TryGetBestSource(BonesID, out var bestSource))
                return null;

            return await bestSource.LoadSavGzAsync(BonesID);
        }

        public BonesSavGz GetBonesSavGz(string BonesID)
            => GetBonesSavGzAsync(BonesID)
                .WaitResult()
            ;

        public SaveBonesInfo GetRandomWeightedBonesInfoMatchingSpec(BonesSpec PlayerSpec, Random Rnd = null)
            => GetRandomWeightedBonesIDMatchingSpec(PlayerSpec, Rnd) is string bonesID
            ? GetBonesInfo(bonesID)
            : null
            ;

        public BonesSavGz GetRandomWeightedBonesSavGzMatchingSpec(BonesSpec PlayerSpec, Random Rnd = null)
            => GetRandomWeightedBonesIDMatchingSpec(PlayerSpec, Rnd) is string bonesID
            ? GetBonesSavGz(bonesID)
            : null
            ;

        public IEnumerable<KeyValuePair<IBonesSource, ActionResult>> SaveBones(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz)
        {
            if (BonesSavGz.TryEnsureSavable())
            {
                foreach (var source in this)
                    yield return new(source, source.SaveBones(BonesJSON, BonesSavGz));
            }
            else
                throw new InvalidOperationException($"{nameof(BonesSavGz)} failed to convert to writable format");
        }

        public string GetSourceTypesColorString(bool IncludeAdditionalPips = true)
        {
            string output = $"{this[0].Type}".Colored(this[0].Type.GetColor() ?? "R");

            if (IncludeAdditionalPips)
                if (Count > 1)
                    for (int i = 1; i < Count; i++)
                        output += Const.BULLET.Colored(this[i].Type.GetColor());

            return output;
        }

        [RegisterAsyncMethodForDebug(true)]
        public static async Task<BonesSources> SelectBonesSourcesFromPoolAsync(
            IEnumerable<IBonesSource> BonesSources,
            bool DefaultValue,
            bool FirstDefaultOnly,
            string Action
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesSources), BonesSources.GetNullString()),
                    Debug.Arg(nameof(DefaultValue), DefaultValue),
                    Debug.Arg(nameof(FirstDefaultOnly), FirstDefaultOnly),
                    Debug.Arg(nameof(Action), Action),
                });

            if (BonesSources.Count() == 1)
                return GetFromPoolFilledWith(BonesSources);

            CascadableResult result;
            using var tB = TextBuilder.Get();
            Dictionary<int, Func<Task<CascadableResult>>> buttonCallbacks = null;
            using var options = new CascadablePickOptionDataSetAsync<SelectionPair<IBonesSource>>(CascadableResult.Back, CascadableResult.Cancel);
            int defaultSelection = 0;
            using var selections = ScopeDisposedList<SelectionPair<IBonesSource>>.GetFromPool();
            var pickerIcon = BonesModeModuleWindow.RandomBonesRenderable;
            bool flipDefault = false;
            foreach (var bonesSource in BonesSources)
            {
                selections.Add(new SelectionPair<IBonesSource>
                {
                    Element = bonesSource,
                    Selected = !flipDefault
                        ? (DefaultValue ? 1 : 0)
                        : (DefaultValue ? 0 : 1)
                        ,
                    GetLabel = bs => bs.DisplayName,
                });
                flipDefault = FirstDefaultOnly;
            }
            do
            {
                tB.Clear();
                options.ClearItems(Dispose: true);

                tB.Append($"Pick which Bones Sources you would like to perform this action: {Action}")
                    .AppendLineEnd();

                foreach (var selection in selections)
                {
                    char hotkey = options.GetFirstAvailableHotkey(selection.DesiredHotkeys);
                    if (hotkey == ' ')
                        hotkey = options.GetHotkeys().GetNextHotKey();

                    options.Add(new CascadablePickOptionDataAsync<SelectionPair<IBonesSource>>
                    {
                        Element = selection,
                        Text = $"{selection}",
                        Hotkey = hotkey,
                        Icon = selection.Element.Icon,
                        Callback = (e) => Task.Run(() => e.SelectContinue()),
                    });
                }

                List<QudMenuItem> buttons = null;
                buttonCallbacks?.Clear();
                buttonCallbacks = null;

                if (selections.Any(sp => sp.IsSelected()))
                {
                    buttons = ConfirmButton;
                    buttonCallbacks = new()
                    {
                        { -4, () => Task.Run(() => CascadableResult.BackSilent) }
                    };
                }

                result = await PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: $"Pick Bones Sources".Colored("yellow"),
                    Intro: tB.ToString(),
                    IntroIcon: pickerIcon,
                    AdditionalButtons: buttons,
                    DefaultSelected: defaultSelection,
                    LastSelectedCallback: c => defaultSelection = Math.Clamp(c, 0, options.Count - 1),
                    ButtonCallbacks: buttonCallbacks,
                    FinalSelectedCallback: (o, r) => Task.Run(() => r));
            }
            while (result.IsContinue());

            Debug.YehNah($"{nameof(result)}: {result}", Good: result.IsSilent(), Indent: indent[2]);

            if (result.IsCancel())
                return null;

            if (!result.IsSilent())
                return GetFromPool();

            return GetFromPoolFilledWith(selections.Where(sp => sp.IsSelected()).Select(sp => sp.Element));
        }

        public virtual BonesSources Vomit(
            bool ForceName = false,
            Indent Indent = null,
            [CallerMemberName] string CallingMethod = "",
            params object[] Parameters
            )
            => (BonesSources)IVomitable.Vomit(
                Object: this,
                ForceName: ForceName,
                Indent: Indent,
                CallingMethod: CallingMethod,
                Parameters: Parameters)
            ;
    }
}
