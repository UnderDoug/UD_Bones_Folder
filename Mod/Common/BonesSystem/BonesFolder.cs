using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using XRL;
using XRL.Core;

using IOPath = Platform.IO.Path;
using IODirectory = Platform.IO.Directory;
using File = Platform.IO.File;
using FileInfo = Platform.IO.FileInfo;
using Stream = System.IO.Stream;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UD_Bones_Folder.Mod.OsseousAsh;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using XRL.Collections;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    public class BonesFolder : BaseBonesSource
    {
        public static string BonesFileName => "LunarRegent";
        public static string BonesInfoName => $"{BonesFileName}.json";
        public static string BonesSavGzName => $"{BonesFileName}.sav.gz";
        public static string BonesInfoBakName => $"{BonesInfoName}.bak";
        public static string BonesSavGzBakName => $"{BonesSavGzName}.bak";

        private readonly IBonesSource.SourceType _Type;
        public override IBonesSource.SourceType Type => _Type;

        private readonly string _Path;
        public string Path => _Path;

        public override string SourceID => Path;
        public override string DisplayName => $"{Type.Tag()} {SanitizePathForDisplay(SourceID)}";
        public override bool CanLoad => true;
        public override bool CanSave => true;
        public override string BlurbDisplayName => Type != IBonesSource.SourceType.None
            ? $"a {Type.Tag(false)} folder"
            : IBonesSource.DefaultBlurbDisplayName
            ;
        public override string BlurbSavedVerb => Type != IBonesSource.SourceType.None
            ? "saved to"
            : IBonesSource.DefaultBlurbSavedVerb
            ;

        public virtual bool IsPreferredLocation
            => Options.PreferredBonesFolderForSave == IBonesSource.SourceType.None
            || Options.PreferredBonesFolderForSave == Type
            ;

        public BonesFolder(IBonesSource.SourceType Type, string Path)
        {
            _Type = Type;
            _Path = EnsureExists(Path);
        }

        public static string SanitizePathForDisplay(string Path)
            => DataManager.SanitizePathForDisplay(Path)
            ;

        public static string GetInfoFilePathFor(string BonesID)
            => IOPath.Combine(BonesID, BonesInfoName)
            ;

        public static string GetSavGzFilePathFor(string BonesID)
            => IOPath.Combine(BonesID, BonesSavGzName)
            ;

        public bool Exists(string Directory)
            => !Path.IsNullOrEmpty()
            && IODirectory.Exists(IOPath.Combine(Path, Directory))
            ;

        public bool Exists()
            => Exists(null)
            ;

        public static string EnsureExists(string Path)
        {
            try
            {
                if (Path.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(Path), "Cannot ensure empty path exists.");

                if (!Path.StartsWith(BonesPile.SavePath))
                    throw new ArgumentOutOfRangeException(nameof(Path), $"Cannot ensure path outside of {nameof(BonesPile.SavePath)} ({SanitizePathForDisplay(BonesPile.SavePath)}) exists.");

                if (IODirectory.Exists(Path))
                    return Path;

                IODirectory.CreateDirectory(Path);

                return Path;
            }
            catch (Exception x)
            {
                Utils.Error(x);
                return null;
            }
        }

        public string EnsureExists()
            => EnsureExists(Path)
            ;

        public string CombinePath(params string[] Paths)
            => IOPath.Combine(Path, IOPath.Combine(Paths))
            ;

        internal static async Task<bool> FileExistsAsync(string FilePath)
            => await File.ExistsAsync(FilePath)
            ;

        internal static bool FileExists(string FilePath)
            => FileExistsAsync(FilePath).WaitResult()
            ;

        public async Task<bool> FileExistsAsync(string BonesID, string FileName)
            => await FileExistsAsync(CombinePath(BonesID, FileName))
            ;

        public bool FileExists(string BonesID, string FileName)
            => FileExistsAsync(BonesID, FileName)
                .WaitResult()
            ;

        public virtual string GetBonesDirectory(string BonesID)
            => CombinePath(BonesID)
            ;

        public virtual bool HasBonesDirectory(string BonesID)
            => Exists(BonesID)
            ;

        public virtual string EnsureBonesDirectory(string BonesID)
            => EnsureExists(GetBonesDirectory(BonesID))
            ;

        public virtual string GetFullInfoFilePathFor(string BonesID)
            => CombinePath(BonesID, BonesInfoName)
            ;

        public virtual string GetFullSavGzFilePathFor(string BonesID)
            => CombinePath(BonesID, BonesSavGzName)
            ;

        public async Task<IEnumerable<string>> EnumerateFilesAsync(string Directory = null)
            => await IODirectory.EnumerateFilesAsync(CombinePath(Directory))
            ;

        public static async Task<long> GetDirectorySizeAsync(string Directory)
            => await FileExistsAsync(Directory)
            ? (await IODirectory.EnumerateFilesAsync(Directory)).Aggregate(0L, Utils.FileSizeAggregator)
            : 0L
            ;

        public void Delete(string Directory)
        {
            if (Directory.IsNullOrEmpty())
                return;

            IODirectory.Delete(CombinePath(Directory));
        }

        public void DeleteFile(string FilePath)
        {
            if (FilePath.IsNullOrEmpty())
                return;

            File.Delete(CombinePath(FilePath));
        }

        public async Task<long> GetBonesDirectorySizeAsync(string BonesID)
            => await GetDirectorySizeAsync(GetBonesDirectory(BonesID))
            ;

        public override bool HasInfo(string BonesID)
        {
            if (!HasBonesDirectory(BonesID))
                return false;

            return FileExists(BonesID, BonesInfoName);
        }

        public override bool HasSavGz(string BonesID)
        {
            if (!HasBonesDirectory(BonesID))
                return false;

            return FileExists(BonesID, BonesSavGzName);
        }

        public virtual bool HasSavGzBak(string BonesID)
        {
            if (!HasBonesDirectory(BonesID))
                return false;

            return FileExists(BonesID, $"{BonesSavGzName}.bak");
        }

        public override bool IsBonesBlocked(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()} blocking {nameof(SaveBonesInfo)} is not currently supported.", new NotImplementedException());
            return false;
        }

        protected virtual async Task<T> LoadFileAsync<T>(
            string BonesID,
            string FileName,
            Predicate<string> OnlyIfBonesID,
            Func<string, T> ProcFilePath,
            string FailedMessage = null,
            bool DeleteIfEmpty = true
            )
        {
            try
            {
                if (IOPath.GetFileNameWithoutExtension(Path).EqualsNoCase("mods")
                    || IOPath.GetFileNameWithoutExtension(Path).EqualsNoCase("textures"))
                    return default;

                if (OnlyIfBonesID?.Invoke(BonesID) is not false)
                    return ProcFilePath.Invoke(CombinePath(BonesID, FileName));
                else
                if (!FailedMessage.IsNullOrEmpty())
                    Utils.Warn(FailedMessage);

                if (DeleteIfEmpty
                    && (await EnumerateFilesAsync(BonesID)).IsNullOrEmpty())
                {
                    try
                    {
                        IODirectory.Delete(GetBonesDirectory(BonesID));
                    }
                    catch (Exception x)
                    {
                        Utils.Warn(x);
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
            return default;
        }

        protected virtual async Task<SaveBonesJSON> LoadJSONAsync(string BonesID, bool DeleteIfEmpty = true)
            => await LoadFileAsync(
                BonesID: BonesID,
                FileName: BonesInfoName,
                OnlyIfBonesID: HasInfo,
                ProcFilePath: filePath => File.ReadAllJsonAsync<SaveBonesJSON>(filePath).WaitResult(),
                FailedMessage: $"Weird bones directory with no .json file present: {SanitizePathForDisplay(GetBonesDirectory(BonesID))}")
            ;

        public override SaveBonesInfo LoadInfo(string BonesID)
            => LoadInfoAsync(BonesID)
                .WaitResult()
            ;

        public override async Task<SaveBonesInfo> LoadInfoAsync(string BonesID)
        {
            try
            {
                if (await LoadJSONAsync(BonesID) is SaveBonesJSON bonesJSON)
                {
                    return SaveBonesJSON.CreateInfoFrom(
                        SaveBonesJSON: bonesJSON,
                        BonesSource: this);
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

        public override async Task<IEnumerable<SaveBonesInfo>> LoadInfosAsync(IEnumerable<string> BonesIDs = null)
            => (await ListBonesIDsAsync())
                .Select(LoadInfo)
            ;

        public override async Task<ModRecordCollection> LoadModConfigAsync(string BonesID)
        {
            try
            {
                using var bonesJSON = await LoadJSONAsync(BonesID);

                if (bonesJSON == null)
                    return null;

                return bonesJSON.EnabledMods;
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

        public override async Task<BonesSpec> LoadSpecAsync(string BonesID)
        {
            try
            {
                using var bonesJSON = await LoadJSONAsync(BonesID);
                return bonesJSON?.BonesSpec;
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

        public override async Task<BonesStats> LoadStatsAsync(string BonesID)
        {
            try
            {
                using var bonesJSON = await LoadJSONAsync(BonesID);
                return bonesJSON?.Stats;
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

        protected virtual async Task<BonesSavGz> GetSaveGzAsync(string BonesID, bool DeleteDirectoryIfEmpty = true)
            => await LoadFileAsync(
                BonesID: BonesID,
                FileName: BonesSavGzName,
                OnlyIfBonesID: HasInfo,
                ProcFilePath: filePath => new BonesSavGz(BonesID, File.OpenRead(filePath)),
                FailedMessage: $"Weird bones directory with no .json file present: {SanitizePathForDisplay(GetBonesDirectory(BonesID))}")
            ;

        public override async Task<BonesSavGz> LoadSavGzAsync(string BonesID)
            => await GetSaveGzAsync(BonesID)
            ;

        public static async Task SafeWriteSaveBonesJSONAsync(
            string FileName,
            string Path,
            SaveBonesJSON BonesJSON,
            bool RequireExisting = true
            )
        {
            if (FileName.IsNullOrEmpty())
            {
                Utils.Warn($"Attempted to safe write SaveBonesJSON to null {nameof(FileName)}");
                return;
            }

            if (Path.IsNullOrEmpty())
            {
                Utils.Warn($"Attempted to safe write SaveBonesJSON to null {nameof(Path)}");
                return;
            }
            string filePath = IOPath.Combine(Path, FileName);
            bool fileExists = await FileExistsAsync(filePath);
            if (!RequireExisting
                || fileExists)
            {
                if (!RequireExisting)
                    EnsureExists(Path);

                if (fileExists)
                    await File.CopyAsync(filePath, $"{filePath}.bak", overwrite: true);

                // Utils.Log($"{nameof(SafeWriteSaveBonesJSONAsync)} {LocationData?.SanitiseForDisplay(Json)}");
                await BonesJSON.HotSwapCharIconAsync(
                    EitherSideOf: File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(BonesJSON, Formatting.Indented)));
            }
            else
            {
                Utils.Warn($"Attempted to safe write SaveBonesJSON to without existing file to overwrite.");
            }
        }

        public async Task SafeWriteSaveBonesJSON(
            string BonesID,
            SaveBonesJSON BonesJSON,
            bool RequireExisting = true
            )
            => await SafeWriteSaveBonesJSONAsync(
                FileName: BonesInfoName,
                Path: CombinePath(BonesID),
                BonesJSON: BonesJSON,
                RequireExisting: RequireExisting)
            ;

        public override async Task<ActionResult> IncrementStatAsync(string BonesID, string StatName)
        {
            var result = ActionResult.NewFailed();
            try
            {
                using var bonesJSON = await LoadJSONAsync(BonesID, false);
                if (bonesJSON == null)
                    return result.SetMessage($"{nameof(bonesJSON)} is null");

                if (bonesJSON.Stats is not BonesStats bonesStats)
                    return result.SetMessage($"{nameof(bonesJSON)}.{nameof(bonesJSON.Stats)} is null");

                var userID = Configuration.Instance?.ID ?? Guid.Empty;
                bool success = false;
                if (!success
                    && bonesStats.IncrementMethodByByName.TryGetValue(StatName, out var incrementMethod))
                    success = incrementMethod.Invoke(userID);

                if (!success
                    && bonesStats.StatSetsByName.TryGetValue(StatName, out var statSet))
                    success = statSet.IncrementStat(userID);

                if (success)
                {
                    await SafeWriteSaveBonesJSON(BonesID, bonesJSON, true);
                    return result
                        .SetMessage($"{StatName} incremented")
                        .SetSuccess();
                }
                else
                    Utils.Warn($"Failed to {nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"}");

            }
            catch (ThreadInterruptedException x)
            {
                throw x;
            }
            catch (Exception x)
            {
                Utils.Warn(x);
                result = result.SetCaught(x);
            }
            return result;
        }

        public override async Task<ActionResult> SaveInfoAsync(SaveBonesJSON BonesJSON)
        {
            var result = ActionResult.NewFailed();
            try
            {
                if (BonesJSON == null)
                    return result.SetMessage($"{nameof(BonesJSON)} is null");

                string bonesID = BonesJSON.ID;
                using var bonesJSON = BonesJSON.Clone();
                if (bonesJSON == null)
                    return result.SetMessage($"{nameof(bonesJSON)} is null");

                await SafeWriteSaveBonesJSON(bonesID, bonesJSON, false);
                return result
                    .SetMessage($"{nameof(SaveBonesJSON)} written")
                    .SetSuccess();
            }
            catch (ThreadInterruptedException x)
            {
                throw x;
            }
            catch (Exception x)
            {
                Utils.Warn(x);
                result = result.SetCaught(x);
            }
            return result;
        }

        public override async Task<ActionResult> SaveSavGzAsync(BonesSavGz BonesSavGz)
        {
            var result = ActionResult.NewFailed();
            try
            {
                if (BonesSavGz == null)
                    return result.SetMessage($"{nameof(BonesSavGz)} is null");

                if (!HasInfo(BonesSavGz.BonesID))
                    return result.SetMessage($"Missing {nameof(SaveBonesInfo)} for {{{BonesSavGz.BonesID}}}");

                string path = EnsureBonesDirectory(BonesSavGz.BonesID);
                string fileName = BonesSavGzName;
                string filePath = IOPath.Combine(path, fileName);
                bool madeBak = false;
                if (await FileExistsAsync(filePath))
                {
                    madeBak = true;
                    File.Copy(filePath, $"{filePath}.bak");
                }

                await File.WriteAllBytesAsync(filePath, BonesSavGz.Data);
                return result
                    .SetMessage($"{nameof(BonesSavGz)} written{(madeBak ? $" (backup created)" : null)}")
                    .SetSuccess();
            }
            catch (ThreadInterruptedException x)
            {
                throw x;
            }
            catch (Exception x)
            {
                Utils.Warn(x);
                result = result.SetCaught(x);
            }
            return result;
        }

        public virtual async Task<IEnumerable<ActionResult>> RestoreBackUpsAsync(string BonesID)
        {
            var results = new List<ActionResult>();
            foreach (var bakFileName in (await EnumerateFilesAsync(BonesID)).Where(fp => fp.EndsWith(".bak")))
            {
                var result = ActionResult.NewFailed();
                string fileName = bakFileName[..^4];
                if (FileExists(BonesID, bakFileName))
                {
                    try
                    {
                        await File.CopyAsync(CombinePath(BonesID, bakFileName), CombinePath(BonesID, fileName), overwrite: true);
                        result
                            .SetMessage($"restored {fileName} from backup")
                            .SetSuccess();
                    }
                    catch (Exception x)
                    {
                        Utils.Warn($"Failed to restore backup bones file \n{bakFileName}\"", x);
                        result
                            .AppendCaught(x)
                            .SetFailed();
                    }
                    results.Add(result);
                }
            }
            return results;
        }

        public virtual IEnumerable<ActionResult> RestoreBackUps(string BonesID)
            => RestoreBackUpsAsync(BonesID)
                .WaitResult()
            ;

        public override async Task<ActionResult> BlockBonesAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()}.{nameof(BlockBones)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => ActionResult.NewFailed().SetMessage("Unsupported"));
        }

        public override async Task<ActionResult> UnblockBonesAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()}.{nameof(UnblockBones)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => ActionResult.NewFailed().SetMessage("Unsupported"));
        }

        public override async Task<ActionResult> DeleteInfoAsync(string BonesID)
        {
            string infoFilePath = GetInfoFilePathFor(BonesID);
            int attempts = 0;
            var result = ActionResult.NewFailed();
            while (await FileExistsAsync(CombinePath(infoFilePath)))
            {
                try
                {
                    DeleteFile(infoFilePath);
                    return result
                        .SetMessage($"deleted /{infoFilePath}")
                        .SetSuccess();
                }
                catch (Exception x)
                {
                    if (attempts++ < 20)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    Utils.Error($"Error deleting file {SanitizePathForDisplay(CombinePath(infoFilePath))}", x);
                    result = result.SetCaught(x);
                    break;
                }
            }
            return result;
        }

        public override async Task<ActionResult> DeleteSavGzAsync(string BonesID)
        {
            string savGzFilePath = GetSavGzFilePathFor(BonesID);
            int attempts = 0;
            var result = ActionResult.NewFailed();
            while (await FileExistsAsync(savGzFilePath))
            {
                try
                {
                    DeleteFile(savGzFilePath);
                    return result
                        .SetMessage($"deleted /{savGzFilePath}")
                        .SetSuccess();
                }
                catch (Exception x)
                {
                    if (attempts++ < 20)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    Utils.Error($"Error deleting file {SanitizePathForDisplay(CombinePath(savGzFilePath))}", x);
                    result = result.SetCaught(x);
                    break;
                }
            }
            return result;
        }

        public override async Task<ActionResult> DeleteBonesAsync(string BonesID)
        {
            if (!HasBonesDirectory(BonesID))
                return ActionResult.NewSuccess()
                    .SetMessage($"No directory");

            var result = (await DeleteInfoAsync(BonesID))
                .MergeWith(await DeleteSavGzAsync(BonesID));

            if (HasBonesDirectory(BonesID))
            {
                string bonesDir = GetBonesDirectory(BonesID);
                int attempts = 0;
                while (Exists(bonesDir))
                {
                    try
                    {
                        Delete(BonesID);
                        break;
                    }
                    catch (Exception x)
                    {
                        if (attempts++ < 20)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        Utils.Error($"Error deleting directory {SanitizePathForDisplay(bonesDir)}", x);
                        result.AppendCaught(x)
                            .SetCompletedWithWarnings();
                        break;
                    }
                }
            }
            if (!HasBonesDirectory(BonesID))
            {
                if (result.Type <= ActionResult.ResultType.Failed)
                    result.SetSuccess();
                else
                    result.SetCompletedWithWarnings();
            }
            return result;
        }

        public override async Task<IEnumerable<string>> ListBonesIDsAsync()
            => (await IODirectory.EnumerateDirectoriesAsync(Path))
                .Select(IOPath.GetFileNameWithoutExtension)
                .Where(HasBones)
            ;

        public override async Task<IEnumerable<SaveBonesInfo>> ListBonesInfosAsync()
            => (await ListBonesIDsAsync())
                .Select(LoadInfo)
            ;

        public override async Task<IEnumerable<BonesSpec>> ListBonesSpecsAsync()
            => (await ListBonesIDsAsync())
                .Select(LoadSpec)
            ;

        public override async Task<IEnumerable<ModRecordCollection>> ListModConfigsAsync()
            => (await ListBonesIDsAsync())
                .Select(LoadModConfig)
            ;

        public override async Task<IEnumerable<string>> GetBonesIDsMatchingSpecAsync(BonesSpec PlayerSpec)
            => (await ListBonesSpecsAsync())
                .Where(bs => bs.IsWithinSpec(PlayerSpec))
                .Select(bs => bs.BonesID)
            ;

        public override async Task<IEnumerable<ModRecordCollection>> GetModRecordsMatchingSpecAsync(BonesSpec PlayerSpec)
            => (await ListBonesSpecsAsync())
                .Where(bs => bs.IsWithinSpec(PlayerSpec))
                .Select(bs => LoadModConfig(bs.BonesID))
            ;
    }
}
