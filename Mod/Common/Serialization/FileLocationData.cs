using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Platform.IO;
using Path = Platform.IO.Path;

using UD_Bones_Folder.Mod.Coalescence;
using UD_Bones_Folder.Mod.Logging;
using UD_Bones_Folder.Mod.OsseousAsh;
using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Collections;
using XRL.Core;
using XRL.World;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class FileLocationData : IComposite, IEquatable<FileLocationData>, IDisposable
    {
        #region Debug Registration
        [RegistersMethodsForDebug]
        public static void doDebugRegistry(DebugMethodRegistry Registry)
            => Registry.RegisterEach(
                Type: typeof(FileLocationData),
                MethodNameValues: new()
                {
                    { nameof(ReadFromFileAsync), true },
                    { nameof(WriteAsync), true },
                });
        #endregion

        [Serializable]
        public enum LocationType
        {
            None,
            Local,
            Synced,
        }

        private static readonly char[] PathSeparatorChars = new char[3]
        {
            '/',
            '\\',
            System.IO.Path.DirectorySeparatorChar,
        };

        public static string LocalTypeString => LocationType.Local.GetColoredString();
        public static string SyncedTypeString => LocationType.Synced.GetColoredString();

        public LocationType Type;
        public string Path;

        public FileLocationData()
        { }

        public FileLocationData(
            LocationType Type,
            string Path
            )
            : this()
        {
            this.Type = Type;
            this.Path = PathWithoutFileName(Path);
        }

        public FileLocationData(FileLocationData Source)
            : this(Source.Type, Source.Path)
        { }

        private static int FindExtension(string path)
        {
            if (!path.IsNullOrEmpty())
            {
                int extSep = path.LastIndexOf('.');
                int lastPathSep = path.LastIndexOfAny(PathSeparatorChars);
                if (extSep > lastPathSep)
                    return extSep;
            }
            return -1;
        }

        private static string PathWithoutFileName(string Path)
        {
            if (!Path.IsNullOrEmpty()
                && FindExtension(Path) >= 0
                && Platform.IO.Path.GetFileName(Path) is string fileName
                && !fileName.IsNullOrEmpty())
                Path = Path[..^fileName.Length];

            return Path;
        }

        public static string GetFileLocationDataTypeColor(LocationType Type)
            => Type switch
            {
                LocationType.Synced => "G",
                LocationType.Local => "W",
                _ => "R",
            }
            ;

        public string GetFileLocationDataTypeColor()
            => GetFileLocationDataTypeColor(Type)
            ;

        public static FileLocationData Clone(FileLocationData FileLocationData)
            => new(FileLocationData)
            ;

        public FileLocationData Clone()
            => Clone(this)
            ;

        public static FileLocationData NewLocal(string Path)
            => new FileLocationData
            {
                Type = LocationType.Local,
                Path = Path,
            }
            ;

        public static FileLocationData NewSynced(string Path)
            => new FileLocationData
            {
                Type = LocationType.Synced,
                Path = Path,
            }
            ;

        public string SanitizeForDisplay(string FileName = null)
        {
            string output = Path;
            if (!FileName.IsNullOrEmpty())
                output = WithFileName(FileName);

            return DataManager.SanitizePathForDisplay(output);
        }

        public string DisplayName()
            => SanitizeForDisplay()
            ;

        public string Tag(bool Braces = true)
            => $"{(Braces ? "[" : null)}{Type.GetColoredString()}{(Braces ? "]" : null)}"
            ;

        public string TaggedDisplayName()
            => $"{Tag()} {DisplayName()}"
            ;

        public bool Exists()
            => !Path.IsNullOrEmpty()
            && Directory.Exists(Path)
            ;

        public string EnsureExists()
        {
            if (Type <= LocationType.None)
                return null;

            try
            {
                if (!Exists())
                    Path = Directory.CreateDirectory(Path).FullName;
            }
            catch (Exception x)
            {
                Utils.Error(x);
                return null;
            }

            return Path;
        }

        public bool TryEnsureExists()
            => !EnsureExists().IsNullOrEmpty()
            ;

        public string WithFileName(string FileName)
            => Platform.IO.Path.Combine(this, FileName)
            ;

        public async Task<bool> FileExistsAsync(string FileName)
            => !FileName.IsNullOrEmpty()
            && (await File.ExistsAsync(WithFileName(FileName)))
            ;

        public bool FileExists(string FileName)
            => FileExistsAsync(FileName).WaitResult()
            ;

        public async Task<T> ReadFromFileAsync<T>(string FileName)
        {
            if (await FileExistsAsync(FileName))
            {
                try
                {
                    using var indent = new Indent();
                    Debug.LogCaller(
                        Indent: indent[1],
                        ArgPairs: new Debug.ArgPair[]
                        {
                            Debug.Arg(typeof(T).ToStringWithGenerics()),
                            Debug.Arg(nameof(FileName), FileName),
                        });

                    if (await File.ReadAllTextAsync(WithFileName(FileName)) is not string fileContents)
                    {
                        Utils.Warn($"Failed to {nameof(ReadFromFileAsync)} {SanitizeForDisplay(FileName)}");
                        return default;
                    }
                    if (JsonConvert.DeserializeObject<T>(fileContents) is not T deserializeObject)
                    {
                        Utils.Warn($"Failed to {nameof(JsonConvert.DeserializeObject)} in {SanitizeForDisplay(FileName)}");
                        return default;
                    }
                    return deserializeObject;
                }
                catch (Exception x)
                {
                    Utils.Error($"Reading File {SanitizeForDisplay(FileName)}", x);
                }
            }
            return default;
        }

        public async Task WriteAsync<T>(string FileName, T Object, Formatting Formatting, bool Ensure = false)
        {
            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(FileName), FileName),
                    Debug.Arg(nameof(Object), Object?.GetType()?.ToStringWithGenerics() ?? "MISSING"),
                    Debug.Arg(nameof(Formatting), Formatting),
                    Debug.Arg(nameof(Ensure), Ensure),
                });

            try
            {
                if (Type <= LocationType.None)
                    throw new InvalidOperationException("Write location must be writable");

                if ((Ensure
                        && TryEnsureExists())
                    || Exists())
                {
                    Debug.Log($"{nameof(File)}.{nameof(File.WriteAllTextAsync)}", Indent: indent[2]);
                    Debug.LogJson(Object, Indent: indent[2]);
                    await File.WriteAllTextAsync(
                        path: WithFileName(FileName),
                        content: JsonConvert.SerializeObject(Object, Formatting));
                }
            }
            catch (Exception x)
            {
                string message = x is InvalidOperationException
                    ? $"Attempted to {nameof(Write)} {FileName} to non-writable location {SanitizeForDisplay()}"
                    : $"Encountered issue trying to {nameof(WriteAsync)} to {SanitizeForDisplay()}"
                    ;

                Utils.Warn(message, x);
            }

            Debug.CheckYeh($"{nameof(WriteAsync)} {FileName} Finished", Indent: indent[1]);
        }

        public async void Write<T>(string FileName, T Object, Formatting Formatting, bool Ensure = false)
            => await WriteAsync(FileName, Object, Formatting, Ensure)
            ;

        public override string ToString()
            => (string)this
            ;

        public virtual bool SameAs(FileLocationData Other)
            => Other is not null
            && Type == Other.Type
            && Path == Other.Path
            ;

        public bool Equals(FileLocationData Other)
            => SameAs(Other)
            ;

        public override bool Equals(object obj)
            => obj is FileLocationData fileLocationDataObj
            ? Equals(fileLocationDataObj)
            : Equals(this, obj)
            ;

        public override int GetHashCode()
            => (Path?.GetHashCode() ?? 0)
            ^ Type.GetHashCode()
            ;

        public void Dispose()
        {
            Type = LocationType.None;
            Path = null;
        }

        public static implicit operator string(FileLocationData FileLocationData)
            => FileLocationData.EnsureExists()
            ?? FileLocationData.Path
            ;
    }

    public static class FileLocationDataExtensions
    {
        public static string TypeColor(this FileLocationData.LocationType Type)
            => FileLocationData.GetFileLocationDataTypeColor(Type)
            ;

        public static string GetColoredString(this FileLocationData.LocationType Type)
            => Type.ToString().Colored(Type.TypeColor())
            ;
    }
}
