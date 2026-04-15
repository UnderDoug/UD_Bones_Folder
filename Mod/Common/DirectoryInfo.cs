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
    public struct DirectoryInfo : IComposite
    {
        [Serializable]
        public enum DirectoryType
        {
            None,
            Local,
            Synced,
            Mod,
            Online,
        }

        public static DirectoryInfo Empty = default;

        public DirectoryType Type;
        public string Path;

        public DirectoryInfo(DirectoryType Type, string Path)
        {
            this.Type = Type;
            this.Path = Path;
        }

        public static DirectoryInfo NewLocal(string Path)
            => new DirectoryInfo
            {
                Type = DirectoryType.Local,
                Path = Path,
            };

        public static DirectoryInfo NewSync(string Path)
            => new DirectoryInfo
            {
                Type = DirectoryType.Synced,
                Path = Path,
            };

        public static DirectoryInfo NewMod(string Path)
            => new DirectoryInfo
            {
                Type = DirectoryType.Mod,
                Path = Path,
            };

        public static DirectoryInfo NewOnline(string Path)
            => new DirectoryInfo
            {
                Type = DirectoryType.Online,
                Path = Path,
            };

        public static DirectoryInfo NewAssumed(string Path)
        {
            using var maybeOnlinePaths = ScopeDisposedList<string>.GetFromPool();
            maybeOnlinePaths.Add("Online");
            maybeOnlinePaths.Add("OsseousAsh");
            maybeOnlinePaths.Add("\\Cloud");
            maybeOnlinePaths.Add("/Cloud");

            if (Path.ContainsAny("\\Mods\\", "/Mods/", "\\workshop\\content\\333640\\", "/workshop/content/333640/"))
            {
                if (Path.ContainsAny(maybeOnlinePaths))
                    return NewOnline(Path);
                return NewMod(Path);
            }
            if (Path.Contains(XRLCore.SyncedPath))
            {
                if (Path.ContainsAny(maybeOnlinePaths))
                    return NewOnline(Path);
                return NewSync(Path);
            }
            if (Path.Contains(XRLCore.SavePath))
            {
                if (Path.ContainsAny(maybeOnlinePaths))
                    return NewOnline(Path);
                return NewLocal(Path);
            }
            return new DirectoryInfo
            {
                Type = DirectoryType.None,
                Path = Path,
            };
        }

        public async readonly Task<bool> ExistsAsync()
            => !Path.IsNullOrEmpty()
            && await File.ExistsAsync(Path)
            ;

        public readonly bool Exists()
            => ExistsAsync()?.WaitResult() is true
            ;

        public async readonly Task<string> EnsureExistsAsync()
        {
            if (Type == DirectoryType.Online
                && !Options.EnableOsseousAshDownloads)
                return null;

            if (Type == DirectoryType.Mod
                || Type == DirectoryType.None)
                return null;

            if (await ExistsAsync())
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

        public readonly string EnsureExists()
            => EnsureExistsAsync()?.WaitResult()
            ;

        public override readonly string ToString()
            => this;

        public override readonly bool Equals(object obj)
        {
            if (obj is DirectoryInfo directoryInfoObj)
                return this == directoryInfoObj;

            return base.Equals(obj);
        }

        public override readonly int GetHashCode()
            => (Path?.GetHashCode() ?? 0)
            ^ Type.GetHashCode()
            ;

        public static bool operator ==(DirectoryInfo X, DirectoryInfo Y)
            => X.Path == Y.Path
            && X.Type == Y.Type
            ;

        public static bool operator !=(DirectoryInfo X, DirectoryInfo Y)
            => X.Path != Y.Path
            || X.Type != Y.Type
            ;

        public static implicit operator string(DirectoryInfo DirectoryInfo)
        {
            if (DirectoryInfo.Type >= DirectoryType.Mod)
                return DirectoryInfo.Path;
            return DirectoryInfo.EnsureExists()
                ?? DirectoryInfo.Path;
        }
    }
}
