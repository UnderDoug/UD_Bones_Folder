using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Serialization;

using Stream = System.IO.Stream;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    public abstract class BaseBonesSource : IBonesSource
    {
        public abstract IBonesSource.SourceType Type { get; }
        public abstract string SourceID { get; }
        public abstract string DisplayName { get; }
        public abstract bool CanLoad { get; }
        public abstract bool CanSave { get; }
        public abstract string BlurbDisplayName { get; }
        public abstract string BlurbSavedVerb { get; }

        public abstract bool HasInfo(string BonesID);
        public abstract bool HasSavGz(string BonesID);

        public virtual bool HasBones(string BonesID)
            => HasInfo(BonesID)
            && HasSavGz(BonesID)
            ;

        public abstract bool IsBonesBlocked(string BonesID);

        #region Load

        public abstract Task<SaveBonesInfo> LoadInfoAsync(string BonesID);
        public virtual SaveBonesInfo LoadInfo(string BonesID)
            => LoadInfoAsync(BonesID)
                .WaitResult()
            ;

        public abstract Task<IEnumerable<SaveBonesInfo>> LoadInfosAsync(IEnumerable<string> BonesIDs = null);
        public virtual IEnumerable<SaveBonesInfo> LoadInfos(IEnumerable<string> BonesIDs = null)
            => LoadInfosAsync(BonesIDs)
                .WaitResult()
            ;

        public abstract Task<BonesSpec> LoadSpecAsync(string BonesID);
        public virtual BonesSpec LoadSpec(string BonesID)
            => LoadSpecAsync(BonesID)
                .WaitResult()
            ;

        public abstract Task<ModRecordCollection> LoadModConfigAsync(string BonesID);
        public virtual ModRecordCollection LoadModConfig(string BonesID)
            => LoadModConfigAsync(BonesID)
                .WaitResult()
            ;

        public abstract Task<BonesStats> LoadStatsAsync(string BonesID);
        public virtual BonesStats LoadStats(string BonesID)
            => LoadStatsAsync(BonesID)
                .WaitResult()
            ;

        public abstract Task<BonesSavGz> LoadSavGzAsync(string BonesID);
        public virtual BonesSavGz LoadSavGz(string BonesID)
            => LoadSavGzAsync(BonesID)
                .WaitResult()
            ;

        #endregion
        #region Save/Update

        public abstract Task<ActionResult> IncrementStatAsync(string BonesID, string StatName);
        public virtual ActionResult IncrementStat(string BonesID, string StatName)
            => IncrementStatAsync(BonesID, StatName)
                .WaitResult()
            ;

        public abstract Task<ActionResult> SaveInfoAsync(SaveBonesJSON BonesJSON);
        public virtual ActionResult SaveInfo(SaveBonesJSON BonesJSON)
            => SaveInfoAsync(BonesJSON)
                .WaitResult()
            ;

        public abstract Task<ActionResult> SaveSavGzAsync(BonesSavGz BonesSavGz);
        public virtual ActionResult SaveSavGz(BonesSavGz BonesSavGz)
            => SaveSavGzAsync(BonesSavGz)
                .WaitResult()
            ;

        public virtual async Task<ActionResult> SaveBonesAsync(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz)
        {
            var infoResult = await SaveInfoAsync(BonesJSON);
            if (!infoResult)
                return infoResult;

            return infoResult.MergeWith(await SaveSavGzAsync(BonesSavGz));
        }

        public virtual ActionResult SaveBones(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz)
            => SaveBonesAsync(BonesJSON, BonesSavGz)
                .WaitResult()
            ;

        public abstract Task<ActionResult> BlockBonesAsync(string BonesID);
        public virtual ActionResult BlockBones(string BonesID)
            => BlockBonesAsync(BonesID)
                .WaitResult()
            ;

        public abstract Task<ActionResult> UnblockBonesAsync(string BonesID);
        public virtual ActionResult UnblockBones(string BonesID)
            => UnblockBonesAsync(BonesID)
                .WaitResult()
            ;

        public virtual async Task<ActionResult> SetBlockedAsync(string BonesID, bool Value) => Value
            ? await BlockBonesAsync(BonesID)
            : await UnblockBonesAsync(BonesID)
            ;

        public virtual ActionResult SetBlocked(string BonesID, bool Value)
            => SetBlockedAsync(BonesID, Value)
                .WaitResult()
            ;
            

        #endregion
        #region Delete

        public abstract Task<ActionResult> DeleteInfoAsync(string BonesID);
        public virtual ActionResult DeleteInfo(string BonesID)
            => DeleteInfoAsync(BonesID)
                .WaitResult()
            ;

        public abstract Task<ActionResult> DeleteSavGzAsync(string BonesID);
        public virtual ActionResult DeleteSavGz(string BonesID)
            => DeleteSavGzAsync(BonesID)
                .WaitResult()
            ;

        public virtual async Task<ActionResult> DeleteBonesAsync(string BonesID)
        {
            var infoResult = await DeleteInfoAsync(BonesID);
            var savGzResult = await DeleteSavGzAsync(BonesID);

            savGzResult.Type = ActionResult.LeastSuccesful(savGzResult.Type, infoResult.Type);
            if (!infoResult.Message.IsNullOrEmpty())
                savGzResult.Message = $"{infoResult.Message}; {savGzResult.Message}";

            return savGzResult;
        }

        public virtual ActionResult DeleteBones(string BonesID)
            => DeleteBonesAsync(BonesID)
                .WaitResult()
            ;

        #endregion
        #region List

        public abstract Task<IEnumerable<string>> ListBonesIDsAsync();
        public virtual IEnumerable<string> ListBonesIDs()
            => ListBonesIDsAsync()
                .WaitResult()
            ;

        public abstract Task<IEnumerable<SaveBonesInfo>> ListBonesInfosAsync();
        public virtual IEnumerable<SaveBonesInfo> ListBonesInfos()
            => ListBonesInfosAsync()
                .WaitResult()
            ;

        public abstract Task<IEnumerable<BonesSpec>> ListBonesSpecsAsync();
        public virtual IEnumerable<BonesSpec> ListBonesSpecs()
            => ListBonesSpecsAsync()
                .WaitResult()
            ;

        public abstract Task<IEnumerable<ModRecordCollection>> ListModConfigsAsync();
        public virtual IEnumerable<ModRecordCollection> ListModConfigs()
            => ListModConfigsAsync()
                .WaitResult()
            ;

        #endregion
        #region Matching Spec

        public abstract Task<IEnumerable<string>> GetBonesIDsMatchingSpecAsync(BonesSpec PlayerSpec);
        public virtual IEnumerable<string> GetBonesIDsMatchingSpec(BonesSpec PlayerSpec)
            => GetBonesIDsMatchingSpecAsync(PlayerSpec)
                .WaitResult()
            ;

        public abstract Task<IEnumerable<ModRecordCollection>> GetModRecordsMatchingSpecAsync(BonesSpec PlayerSpec);
        public virtual IEnumerable<ModRecordCollection> GetModRecordsMatchingSpec(BonesSpec PlayerSpec)
            => GetModRecordsMatchingSpecAsync(PlayerSpec)
                .WaitResult()
            ;

        #endregion

    }
}
