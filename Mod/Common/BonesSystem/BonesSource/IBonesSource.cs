using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Core;

using Stream = System.IO.Stream;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    public interface IBonesSource
    {
        /// <summary>
        /// Indicates the general location from and to which the Bones Source will save and load bones.
        /// </summary>
        [Serializable]
        public enum SourceType
        {
            /// <summary><see langword="default"/> type.<br />
            /// Won't typically be included in any lists or caches of Bones Sources.</summary>
            None,

            /// <summary>Stores bones in a "local" directory in the game's save directory per <see cref="XRLCore.LocalPath"/>.<br />
            /// Bones here are exclusive to the machine they're stored on.</summary>
            Local,

            /// <summary>Stores bones in a "sync" directory in the game's save directory per <see cref="XRLCore.SyncedPath"/>.<br />
            /// Bones here are synced via whatever service is managing synchronized saves (Steam, GoG, etc.).</summary>
            Synced,

            /// <summary>Stores bones in a directory named "bones" in other mods' files.<br />
            /// Bones here are included in by other mods.</summary>
            Mod,

            /// <summary>Stores bones somewhere online.<br />
            /// Bones here exist on a remote server and typically require either an internet connection or a means of connecting to a locally hosted server.</summary>
            Online,
        }

        SourceType Type { get; }

        string SourceID { get; }

        string DisplayName { get; }

        bool CanLoad { get; }
        bool CanSave { get; }

        public static string DefaultBlurbDisplayName => $"a {"strange location".Colored(SourceType.None.GetColor())}";
        public static string DefaultBlurbSavedVerb => "materialized mytseriously in";

        string BlurbDisplayName => DefaultBlurbDisplayName;
        string BlurbSavedVerb => DefaultBlurbSavedVerb;

        bool HasInfo(string BonesID);
        bool HasSavGz(string BonesID);
        bool HasBones(string BonesID);
        bool IsBonesBlocked(string BonesID);

        Task<SaveBonesInfo> LoadInfoAsync(string BonesID);
        SaveBonesInfo LoadInfo(string BonesID);
        Task<IEnumerable<SaveBonesInfo>> LoadInfosAsync(IEnumerable<string> BonesIDs = null);
        IEnumerable<SaveBonesInfo> LoadInfos(IEnumerable<string> BonesIDs = null);
        Task<BonesSpec> LoadSpecAsync(string BonesID);
        BonesSpec LoadSpec(string BonesID);
        Task<ModRecordCollection> LoadModConfigAsync(string BonesID);
        ModRecordCollection LoadModConfig(string BonesID);
        Task<BonesStats> LoadStatsAsync(string BonesID);
        BonesStats LoadStats(string BonesID);
        Task<BonesSavGz> LoadSavGzAsync(string BonesID);
        BonesSavGz LoadSavGz(string BonesID);

        Task<ActionResult> SaveInfoAsync(SaveBonesJSON BonesJSON);
        ActionResult SaveInfo(SaveBonesJSON BonesJSON);
        Task<ActionResult> SaveSavGzAsync(BonesSavGz BonesSavGz);
        ActionResult SaveSavGz(BonesSavGz BonesSavGz);
        Task<ActionResult> SaveBonesAsync(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz);
        ActionResult SaveBones(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz);

        ActionResult IncrementStat(string BonesID, string StatName);
        Task<ActionResult> IncrementStatAsync(string BonesID, string StatName);

        Task<ActionResult> BlockBonesAsync(string BonesID);
        ActionResult BlockBones(string BonesID);
        Task<ActionResult> UnblockBonesAsync(string BonesID);
        ActionResult UnblockBones(string BonesID);
        Task<ActionResult> SetBlockedAsync(string BonesID, bool Value);
        ActionResult SetBlocked(string BonesID, bool Value);

        Task<ActionResult> DeleteInfoAsync(string BonesID);
        ActionResult DeleteInfo(string BonesID);
        Task<ActionResult> DeleteSavGzAsync(string BonesID);
        ActionResult DeleteSavGz(string BonesID);
        Task<ActionResult> DeleteBonesAsync(string BonesID);
        ActionResult DeleteBones(string BonesID);

        Task<IEnumerable<string>> ListBonesIDsAsync();
        IEnumerable<string> ListBonesIDs();
        Task<IEnumerable<SaveBonesInfo>> ListBonesInfosAsync();
        IEnumerable<SaveBonesInfo> ListBonesInfos();
        Task<IEnumerable<BonesSpec>> ListBonesSpecsAsync();
        IEnumerable<BonesSpec> ListBonesSpecs();
        Task<IEnumerable<ModRecordCollection>> ListModConfigsAsync();
        IEnumerable<ModRecordCollection> ListModConfigs();

        Task<IEnumerable<string>> GetBonesIDsMatchingSpecAsync(BonesSpec PlayerSpec);
        IEnumerable<string> GetBonesIDsMatchingSpec(BonesSpec PlayerSpec);
        Task<IEnumerable<ModRecordCollection>> GetModRecordsMatchingSpecAsync(BonesSpec PlayerSpec);
        IEnumerable<ModRecordCollection> GetModRecordsMatchingSpec(BonesSpec PlayerSpec);

        public virtual bool SameAs(IBonesSource Other)
            => string.Equals(SourceID, Other?.SourceID)
            ;
    }

    public static class BonesSourceExtensions
    {
        public static string GetColor(this IBonesSource.SourceType Type)
            => Type switch
            {
                IBonesSource.SourceType.Synced => "G",
                IBonesSource.SourceType.Local => "W",
                IBonesSource.SourceType.Mod => "C",
                IBonesSource.SourceType.Online => "Y",
                _ => "R",
            }
            ;

        public static string Tag(this IBonesSource.SourceType Type, bool Braces = true)
            => $"{(Braces ? "[" : null)}{Type.ToString().Colored(Type.GetColor())}{(Braces ? "]" : null)}"
            ;

        public static bool IsPreferredAndWritable(this IBonesSource BonesSource)
            => BonesSource.IsOnDisk()
            && BonesSource is BonesFolder bonesFolder
            && bonesFolder.IsPreferredLocation
            ;

        public static bool IsOnline(this IBonesSource.SourceType SourceType)
            => SourceType is IBonesSource.SourceType type
            && type >= IBonesSource.SourceType.Online
            ;

        public static bool IsOnDisk(this IBonesSource.SourceType SourceType)
            => SourceType.IsTwixtInclusive(IBonesSource.SourceType.Local, IBonesSource.SourceType.Synced) is true
            ;

        public static bool IsModded(this IBonesSource.SourceType SourceType)
            => SourceType is IBonesSource.SourceType type
            && type == IBonesSource.SourceType.Mod
            ;

        public static bool IsOnline(this IBonesSource BonesSource)
            => (BonesSource?.Type)?.IsOnline() is true
            ;

        public static bool IsOnDisk(this IBonesSource BonesSource)
            => (BonesSource?.Type)?.IsOnDisk() is true
            ;

        public static bool IsModded(this IBonesSource BonesSource)
            => (BonesSource?.Type)?.IsModded() is true
            ;
    }
}
