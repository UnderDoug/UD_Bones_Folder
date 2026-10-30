using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Serialization;

using XRL.Collections;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    public class BonesSources : PooledList<IBonesSource>
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

        protected static ConcurrentBag<BonesSources> UnusedPool = new();

        protected bool Pooled;

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
            fromPool.AddRange(items);
            return fromPool;
        }

        public static BonesSources GetFromPoolFilledWith(IReadOnlyCollection<IBonesSource> items)
        {
            BonesSources fromPool = GetFromPool();
            fromPool.AddRange(items);
            return fromPool;
        }

        public static BonesSources GetFromPoolFilledWith(IReadOnlyList<IBonesSource> items)
        {
            BonesSources fromPool = GetFromPool();
            fromPool.AddRange(items);
            return fromPool;
        }

        protected void Reuse()
        {
            if (!Pooled)
            {
                throw new Exception("Attempt to reuse ScopeDisposedList not in pool");
            }

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
            return true;
        }

        public virtual bool AddDistinctRange(IEnumerable<IBonesSource> Items)
        {
            bool any = false;
            foreach (var item in Items.IteratorSafe())
                any = AddDistinct(item)
                    || any;

            return any;
        }

        public virtual BonesSources CloneFromPool()
            => GetFromPoolFilledWith(this)
            ;

        public virtual BonesSources Intersect(BonesSources Other)
        {
            if (!Other.IsNullOrEmpty())
                this.RemoveAll(bs => !Other.Any(tbs => DefaultEQC.Equals(tbs, bs)));

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
                if (tType == typeof(ModRecordCollection))
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

        public ScopeDisposedList<ModRecordCollection> GetModRecordsFromPool()
            => GetBonesFileElementFromPool<ModRecordCollection>()
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

        public ScopeDisposedList<ModRecordCollection> GetModConfigsMatchingSpecFromPool(BonesSpec PlayerSpec)
        {
            var resultTypes = new Dictionary<string, IBonesSource.SourceType>();
            var resultIDs = new Dictionary<string, ModRecordCollection>();
            foreach (var source in this)
            {
                if (source == null)
                    continue;

                foreach (var modRecord in source.GetModRecordsMatchingSpec(PlayerSpec))
                {
                    if (modRecord.IsNullOrEmpty())
                        continue;

                    string bonesID = modRecord.BonesID;
                    if (resultTypes.TryGetValue(bonesID, out var type))
                        if (CheckTypePriority(type, source.Type))
                            continue;

                    resultTypes[bonesID] = source.Type;
                    resultIDs[bonesID] = modRecord;
                }
            }
            var result = ScopeDisposedList<ModRecordCollection>.GetFromPoolFilledWith(resultIDs.Values);
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
                    bonesBag.Add(modConfig.BonesID, bonesWeight);

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
            return bestSource;
        }

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
    }
}
