using System;
using System.Collections.Generic;
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

            if (!Path.IsNullOrEmpty()
                && Platform.IO.Path.GetFileName(Path) is string fileName
                && !fileName.IsNullOrEmpty())
                Path = Path[..^fileName.Length];
            this.Path = Path;

            this.Host = Host?.Clone();
        }

        public FileLocationData(FileLocationData Source)
            : this(Source.Type, Source.Path, Source.Host)
        { }

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
                return NewOnline(host);

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

        public string TaggedDisplayName(string FileName = null)
            => $"[{Type.GetColoredString()}] {SanitiseForDisplay(FileName)}";

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

        public void Write<T>(string FileName, T Object, Formatting formatting)
        {
            if (Type <= LocationType.Synced)
            {
                if (Exists())
                {
                    File.WriteAllText(
                        path: WithFileName(FileName),
                        content: JsonConvert.SerializeObject(Object, formatting));
                }
            }
            else
                Utils.Warn($"Attempted to {nameof(Write)} {FileName} to non-writable location {SanitiseForDisplay()}: " +
                    $"{new InvalidOperationException("write location must be writable")}");
        }

        public override string ToString()
            => this;

        public override bool Equals(object obj)
        {
            if (obj is FileLocationData directoryInfoObj)
                return this == directoryInfoObj;

            return base.Equals(obj);
        }

        public override int GetHashCode()
            => (Path?.GetHashCode() ?? 0)
            ^ Type.GetHashCode()
            ^ (Host?.GetHashCode() ?? 0)
            ;

        public bool Equals(FileLocationData Other)
            => Other != null
            && Type == Other.Type
            && Path == Other.Path
            && Host == Other.Host
            ;

        public void Dispose()
        {
            Type = LocationType.None;
            Path = null;
            Host = null;
        }

        public static bool operator ==(FileLocationData X, FileLocationData Y)
        {
            if (X is null
                || Y is null)
                return (X is null) == (Y is null);

            return X.Equals(Y);
        }

        public static bool operator !=(FileLocationData X, FileLocationData Y)
            => !(X == Y)
            ;

        public static implicit operator string(FileLocationData FileLocationData)
            => FileLocationData.Type switch
            {
                LocationType.Online => FileLocationData.Host,
                LocationType.Mod => FileLocationData.Path,
                _ => FileLocationData.EnsureExists()
                    ?? FileLocationData.Path,
            };
    }
}
