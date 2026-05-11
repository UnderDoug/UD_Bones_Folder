using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Platform.IO;

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
        [Serializable]
        public enum LocationType
        {
            None,
            Local,
            Synced,
            Mod,
            Online,
        }

        private static readonly char[] PathSeparatorChars = new char[3]
        {
            '/',
            '\\',
            System.IO.Path.DirectorySeparatorChar,
        };

        public static string MissingLocaitonShortDisplayName => $"a {"strange location".Colored(LocationType.None.TypeColor())}";

        public LocationType Type;
        public string Path;
        public OsseousAsh.Host Host;

        public FileLocationData()
        { }

        public FileLocationData(
            LocationType Type,
            string Path,
            OsseousAsh.Host Host
            )
            : this()
        {
            this.Type = Type;

            this.Path = PathWithoutFileName(Path);

            this.Host = Host;
        }

        public FileLocationData(FileLocationData Source)
            : this(Source.Type, Source.Path, Source.Host)
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
                LocationType.Mod => "C",
                LocationType.Online => "Y",
                _ => "R",
            };

        public string GetFileLocationDataTypeColor()
            => GetFileLocationDataTypeColor(Type)
            ;

        public static FileLocationData Clone(FileLocationData DirectoryInfo)
            => new(DirectoryInfo)
            ;

        public FileLocationData Clone()
            => Clone(this)
            ;

        public static FileLocationData NewLocal(string Path)
            => new FileLocationData
            {
                Type = LocationType.Local,
                Path = Path,
            };

        public static FileLocationData NewSynced(string Path)
            => new FileLocationData
            {
                Type = LocationType.Synced,
                Path = Path,
            };

        public static FileLocationData NewMod(string Path)
            => new FileLocationData
            {
                Type = LocationType.Mod,
                Path = Path,
            };

        public static FileLocationData NewOnline(OsseousAsh.Host Host)
            => new FileLocationData
            {
                Type = LocationType.Online,
                Host = Host,
            };

        public static FileLocationData NewOnline(string HostName, string AuthToken = null)
            => new FileLocationData
            {
                Type = LocationType.Online,
                Host = new OsseousAsh.Host(HostName, AuthToken),
            };

        public static FileLocationData NewAssumed(string Path)
        {
            if (Path.ContainsAny("\\Mods\\", "/Mods/", "\\workshop\\content\\333640\\", "/workshop/content/333640/"))
                return NewMod(Path);

            if (Path.Contains(XRLCore.SyncedPath))
                return NewSynced(Path);

            if (Path.Contains(XRLCore.SavePath))
                return NewLocal(Path);

            if (OsseousAsh.Host.TryParse(Path, out OsseousAsh.Host host))
            {
                if (OsseousAsh.Hosts.FirstHostMatching(h => h.SameAs(host, IgnoreDisabled: true)) is OsseousAsh.Host existingHost)
                    return NewOnline(existingHost, existingHost.AuthToken);
                return NewOnline(host);
            }

            return new FileLocationData
            {
                Type = LocationType.None,
                Path = Path,
            };
        }

        public string SanitiseForDisplay(string FileName = null)
        {
            string output = Path;
            if (!FileName.IsNullOrEmpty())
                output = WithFileName(FileName);

            return DataManager.SanitizePathForDisplay(output);
        }

        public string ShortDisplayName()
            => Type switch
            {
                LocationType.Synced or
                LocationType.Local => $"a {Tag(false)} folder",
                LocationType.Mod => $"a {Tag(false)}", // eventually display the actual mod name.
                LocationType.Online => $"{Host.DisplayName().Colored(LocationType.Online.TypeColor())}",
                _ => MissingLocaitonShortDisplayName,
            }
            ;

        public string DisplayName()
            => Type <= LocationType.Mod
            ? SanitiseForDisplay()
            : Host?.GetHostNameWithProtocol()
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
            if (Type == LocationType.Online
                && !Options.EnableOsseousAshDownloads)
            {
                // consider adding code here to check "canUp" or "status"
                return null;
            }

            if (Type == LocationType.Mod
                || Type == LocationType.None)
                return null;

            if (Exists())
                return Path;

            try
            {
                Directory.CreateDirectory(Path);
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
                    if (JsonConvert.DeserializeObject<T>(await File.ReadAllTextAsync(WithFileName(FileName))) is T json)
                        return json;
                }
                catch (Exception x)
                {
                    Utils.Error($"Reading File {SanitiseForDisplay(FileName)}", x);
                }
            }
            return default;
        }

        public async Task WriteAsync<T>(string FileName, T Object, Formatting Formatting, bool Ensure = false)
        {
            if (Type <= LocationType.Synced)
            {
                if ((Ensure
                        && TryEnsureExists())
                    || Exists())
                {
                    // Utils.Log($"{nameof(WriteAsync)} {SanitiseForDisplay(FileName)}, {nameof(Formatting)}: {Formatting}");
                    await File.WriteAllTextAsync(
                        path: WithFileName(FileName),
                        content: JsonConvert.SerializeObject(Object, Formatting));
                }
            }
            else
                Utils.Warn($"Attempted to {nameof(Write)} {FileName} to non-writable location {SanitiseForDisplay()}: " +
                    $"{new InvalidOperationException("write location must be writable")}");
        }

        public void Write<T>(string FileName, T Object, Formatting Formatting, bool Ensure = false)
            => WriteAsync(FileName, Object, Formatting, Ensure).Wait()
            ;

        public override string ToString()
            => this;

        public virtual bool SameAs(FileLocationData Other)
            => Other is not null
            && Type == Other.Type
            && Path == Other.Path
            && (Host?.SameAs(Other.Host) is not false)
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
            ^ (Host?.GetHashCode() ?? 0)
            ;

        public void Dispose()
        {
            Type = LocationType.None;
            Path = null;
            Host = null;
        }

        public static implicit operator string(FileLocationData FileLocationData)
            => FileLocationData.Type switch
            {
                LocationType.Online => FileLocationData.Host,
                LocationType.Mod => FileLocationData.Path,
                _ => FileLocationData.EnsureExists()
                    ?? FileLocationData.Path,
            };
    }

    public static class FileLocationDataExtensions
    {
        public static string TypeColor(this FileLocationData.LocationType Type)
            => FileLocationData.GetFileLocationDataTypeColor(Type)
            ;

        public static string GetColoredString(this FileLocationData.LocationType Type)
            => Type.ToString().Colored(Type.TypeColor())
            ;

        public static async Task PerformBasedOnTypeAsync(
            this FileLocationData LocationData,
            Func<Task> OnlineCallback = null,
            Func<Task> ModCallback = null,
            Func<Task> FileCallback = null,
            Func<Task> DefaultCallback = null
            )
        {
            switch (LocationData?.Type)
            {
                case FileLocationData.LocationType.Online:
                    await OnlineCallback?.Invoke();
                    return;
                case FileLocationData.LocationType.Mod:
                    await ModCallback?.Invoke();
                    return;
                case FileLocationData.LocationType.Synced:
                case FileLocationData.LocationType.Local:
                    await FileCallback?.Invoke();
                    return;
                case FileLocationData.LocationType.None:
                default:
                    await DefaultCallback?.Invoke();
                    return;
            }
        }

        public static async Task PerformBasedOnTypeAsync<T>(
            this FileLocationData LocationData,
            T Value,
            Func<T, Task> OnlineCallback = null,
            Func<T, Task> ModCallback = null,
            Func<T, Task> FileCallback = null,
            Func<T, Task> DefaultCallback = null
            )
        {
            switch (LocationData?.Type)
            {
                case FileLocationData.LocationType.Online:
                    await OnlineCallback?.Invoke(Value);
                    return;
                case FileLocationData.LocationType.Mod:
                    await ModCallback?.Invoke(Value);
                    return;
                case FileLocationData.LocationType.Synced:
                case FileLocationData.LocationType.Local:
                    await FileCallback?.Invoke(Value);
                    return;
                case FileLocationData.LocationType.None:
                default:
                    await DefaultCallback?.Invoke(Value);
                    return;
            }
        }

        public static async Task<T> ReturnBasedOnTypeAsync<T>(
            this FileLocationData LocationData,
            Func<Task<T>> OnlineCallback = null,
            Func<Task<T>> ModCallback = null,
            Func<Task<T>> FileCallback = null,
            Func<Task<T>> DefaultCallback = null
            )
            => LocationData?.Type switch
            {
                FileLocationData.LocationType.Online => await OnlineCallback?.Invoke(),

                FileLocationData.LocationType.Mod => await ModCallback?.Invoke(),

                FileLocationData.LocationType.Synced or
                FileLocationData.LocationType.Local => await FileCallback?.Invoke(),

                _ => await DefaultCallback?.Invoke(),
            }
            ;

        public static async Task<T> ReturnBasedOnTypeAsync<T>(
            this FileLocationData LocationData,
            T Value,
            Func<T, Task<T>> OnlineCallback = null,
            Func<T, Task<T>> ModCallback = null,
            Func<T, Task<T>> FileCallback = null,
            Func<T, Task<T>> DefaultCallback = null
            )
            => LocationData?.Type switch
            {
                FileLocationData.LocationType.Online => await OnlineCallback?.Invoke(Value),

                FileLocationData.LocationType.Mod => await ModCallback?.Invoke(Value),

                FileLocationData.LocationType.Synced or
                FileLocationData.LocationType.Local => await FileCallback?.Invoke(Value),

                _ => await DefaultCallback?.Invoke(Value),
            }
            ;
    }
}
