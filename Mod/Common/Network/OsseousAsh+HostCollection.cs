using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Platform.IO;

using Qud.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using static UD_Bones_Folder.Mod.Const;

using Event = XRL.World.Event;

namespace UD_Bones_Folder.Mod
{
    public static partial class OsseousAsh
    {
        [JsonObject(MemberSerialization.OptIn)]
        [Serializable]
        public class HostCollection : HashSet<Host>, IDisposable
        {
            public class HostEqualityComparer : IEqualityComparer<Host>
            {
                public static HostEqualityComparer Default => new();

                public bool Equals(Host x, Host y)
                {
                    if (x == null
                        || y == null)
                        return (x == null) == (y == null);

                    return x.SameAs(y);
                }

                public int GetHashCode(Host obj)
                    => obj?.GetHashCode() ?? 0
                    ;
            }

            [JsonObject(MemberSerialization.OptOut)]
            [Serializable]
            protected class HostCollectionJSON
            {
                public Host[] Hosts;

                public HostCollectionJSON()
                {
                }

                public HostCollectionJSON(HostCollection Source)
                    : this()
                {
                    Source.RemoveWhere(h => h == null);
                    Hosts = Source.ToArray();
                }

                public HostCollection FromJSON(FileLocationData LocationData = null)
                    => new(Hosts, LocationData)
                    ;
            }

            public static HostEqualityComparer DefaultEqualityComparer => HostEqualityComparer.Default;

            public static HostCollection DefaultHosts => new(SyncedFileLocation)
            {
                Host.DefaultHost,
            };

            public FileLocationData LocationData;

            public HostCollection()
                : base(DefaultEqualityComparer)
            { }

            public HostCollection(FileLocationData LocationData)
                : this()
            {
                this.LocationData = LocationData;
            }

            public HostCollection(IEnumerable<Host> Source, FileLocationData LocationData)
                : base(Source, DefaultEqualityComparer)
            {
                this.LocationData = LocationData;
            }

            public static async Task<HostCollection> ReadFromFileAsync(FileLocationData FileLocationData, string FileName)
            {
                if ((await FileLocationData?.ReadFromFileAsync<HostCollectionJSON>(FileName)) is HostCollectionJSON hostCollectionJSON
                    && !hostCollectionJSON.Hosts.IsNullOrEmpty())
                    return hostCollectionJSON.FromJSON(FileLocationData);

                return null;
            }

            public static async Task<HostCollection> ReadAsync()
            {
                if (TryFindBestOsseousAshPath(out FileLocationData fileLocationData, HostsFileName))
                    return await ReadFromFileAsync(fileLocationData, HostsFileName);

                return null;
            }

            public static async Task<HostCollection> ReadOrNewAsync()
            {
                if (TryFindBestOsseousAshPath(out var fileLocationData, HostsFileName))
                {
                    if ((await ReadFromFileAsync(fileLocationData, HostsFileName)) is not HostCollection hosts)
                    {
                        hosts = new(DefaultHosts, fileLocationData);
                        hosts.Write();
                    }
                    return hosts;
                }
                return null;
            }

            public void WriteToFile(FileLocationData FileLocationData, string FileName)
                => FileLocationData.Write(FileName, new HostCollectionJSON(this), Formatting.Indented)
                ;

            public void Write()
            {
                if (LocationData != null
                    || TryFindBestOsseousAshPath(out LocationData, HostsFileName))
                    WriteToFile(LocationData, HostsFileName);
                else
                    Utils.Error(
                        Context: $"Failed to {nameof(Write)} to {HostsFileName}",
                        X: new NullReferenceException($"{nameof(LocationData)} must not be null"));
            }

            public void WriteHosts(IEnumerable<Host> Hosts)
            {
                if (!Hosts.IsNullOrEmpty()
                    && !this.SequenceEqual(Hosts))
                {
                    Clear();
                    this.Union(Hosts);
                    Write();
                }
            }

            public void WriteAddHost(Host Host)
            {
                if (Add(Host))
                    Write();
            }

            public void WriteAddHosts(params Host[] Hosts)
            {
                bool any = false;
                foreach (var host in Hosts ?? Enumerable.Empty<Host>())
                    if (Add(host))
                        any = true;

                if (any)
                    Write();
            }

            public void WriteRemoveHost(Host Host)
            {
                if (Remove(Host))
                    Write();
            }

            public void WriteRemoveHosts(params Host[] Hosts)
            {
                bool any = false;
                foreach (var host in Hosts ?? Enumerable.Empty<Host>())
                    if (Remove(host))
                        any = true;

                if (any)
                    Write();
            }

            public string TaggedDisplayName()
                => $"[{(LocationData?.Type ?? FileLocationData.LocationType.None).GetColoredString()}] {DisplayName()}";

            public string DisplayName()
                => LocationData?.SanitiseForDisplay()
                ?? "{{R|ERR:}} Unknown Location "
                ;

            public override string ToString()
                => this.Aggregate(LocationData.SanitiseForDisplay(), Utils.NewLineDelimitedAggregator);

            public void Dispose()
            {
                LocationData = null;
                Clear();
            }
        }
    }
    public static class OsseousAshHostCollectionExtensions
    {
        public static int TotalCount(this Rack<OsseousAsh.HostCollection> Hosts)
            => Hosts?.Aggregate(0, (a, n) => a + n.Count)
            ?? 0
            ;

        public static Renderable GetAshCloudIcon(this OsseousAsh.HostCollection HostCollection)
            => new Renderable(
                Tile: "Mutations/gas_generation.bmp",
                ColorString: $"&K",
                TileColor: $"&K",
                DetailColor: HostCollection?.LocationData?.GetFileLocationDataTypeColor()?[0] ?? 'y')
            ;
    }
}
