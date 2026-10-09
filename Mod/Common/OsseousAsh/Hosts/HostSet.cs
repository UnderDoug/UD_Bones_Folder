using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Platform.IO;

using ConsoleLib.Console;

using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using UD_Bones_Folder.Mod.Serialization;

using static UD_Bones_Folder.Mod.Const;

using Event = XRL.World.Event;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
    public class HostSet : CompositeSet<Host>
    {
        [Serializable]
        public class HostEqualityComparer : CompositeEqualityComparer<Host>
        {
            public static HostEqualityComparer DefaultComparer => new();

            public HostEqualityComparer()
                : base()
            { }

            public override bool Equals(Host x, Host y)
            {
                if (x == null
                    || y == null)
                    return (x == null) == (y == null);

                return x.SameAs(y);
            }

            public override int GetHashCode(Host obj)
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

            public HostCollectionJSON(HostSet Source)
                : this()
            {
                Source.RemoveWhere(h => h == null);
                Hosts = Source.ToArray();
            }

            public HostSet ToHostSet(FileLocationData LocationData)
                => new(Hosts, LocationData)
                ;
        }

        public class HostLocationPair : IDisposable
        {
            public Host Host;
            public FileLocationData FileLocationData;

            public HostLocationPair()
                : base()
            { }

            public HostLocationPair(Host Host, FileLocationData FileLocationData)
                : this()
            {
                this.Host = Host;
                this.FileLocationData = FileLocationData;
            }

            public bool IsValid()
                => Host != null
                && FileLocationData != null
                ;

            public override string ToString()
                => $"{Host?.GetHostNameWithProtocol() ?? "NO_HOST"} in {FileLocationData?.SanitiseForDisplay() ?? "NO_LOCATION"}"
                ;

            public void Dispose()
            {
                Host = null;
                FileLocationData = null;
            }
        }

        public class HostSetHostPair : IDisposable
        {
            public HostSet HostSet;
            public Host Host;

            public HostSetHostPair()
                : base()
            { }

            public HostSetHostPair(HostSet HostSet, Host Host)
                : this()
            {
                this.HostSet = HostSet;
                this.Host = Host;
            }

            public bool IsValid()
                => HostSet != null
                && Host != null
                ;

            public void Dispose()
            {
                HostSet = null;
                Host = null;
            }
        }

        public static string HostsFileName => $"{nameof(ServerUtils.Hosts)}.json";

        public static HostEqualityComparer DefaultEqualityComparer => HostEqualityComparer.DefaultComparer;

        public static List<QudMenuItem> _OptInOptOut = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{y|Opt In}}",
                command = "accept",
                hotkey = "Accept",
            },
            new QudMenuItem
            {
                text = "{{y|Opt Out}}",
                command = "decline",
                hotkey = "N,V Negative"
            },
        };

        public static List<QudMenuItem> OptInOptOut
        {
            get
            {
                if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Gamepad)
                    return _OptInOptOut;

                return new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputFormatted("Accept", XRL.UI.Options.ModernUI) + " {{y|Opt In}}",
                        command = "accept",
                        hotkey = "Accept",
                    },
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputFormatted("V Negative", XRL.UI.Options.ModernUI) + " {{y|Opt Out}}",
                        command = "decline",
                        hotkey = "N,V Negative"
                    },
                };
            }
        }

        public static HostSet DefaultHosts => new(Configuration.SyncedFileLocation)
        {
            Host.DefaultHost,
        };

        public FileLocationData LocationData;

        public HostSet()
            : base(DefaultEqualityComparer)
        { }

        public HostSet(FileLocationData LocationData)
            : this()
        {
            this.LocationData = LocationData;
        }

        public HostSet(IEnumerable<Host> Source, FileLocationData LocationData)
            : base(Source, DefaultEqualityComparer)
        {
            this.LocationData = LocationData;
        }

        public static async Task<HostSet> ReadFromFileAsync(FileLocationData FileLocationData, string FileName)
        {
            if ((await FileLocationData?.ReadFromFileAsync<HostCollectionJSON>(FileName)) is HostCollectionJSON hostCollectionJSON
                && !hostCollectionJSON.Hosts.IsNullOrEmpty())
                return hostCollectionJSON.ToHostSet(FileLocationData);

            return null;
        }

        public static async Task<HostSet> ReadAsync()
        {
            if (Configuration.TryFindBestOsseousAshPath(out FileLocationData fileLocationData, HostsFileName))
                return await ReadFromFileAsync(fileLocationData, HostsFileName);

            return null;
        }

        public static async Task<HostSet> ReadOrNewAsync()
        {
            if (Configuration.TryFindBestOsseousAshPath(out var fileLocationData, HostsFileName))
            {
                if ((await ReadFromFileAsync(fileLocationData, HostsFileName)) is not HostSet hosts)
                {
                    hosts = new(DefaultHosts, fileLocationData);
                    hosts.Write();
                }
                return hosts;
            }
            return null;
        }

        public void WriteToFile(FileLocationData FileLocationData, string FileName)
        {
            var enableds = new Dictionary<Host, bool?>();
            foreach (var host in this)
                host.HotSwapEnabled(ref enableds);

            FileLocationData.Write(FileName, new HostCollectionJSON(this), Formatting.Indented);

            foreach (var host in this)
                host.HotSwapEnabled(ref enableds);
        }

        public void Write()
        {
            // Utils.Log($"{nameof(Write)}({nameof(HostCollection)}: {LocationData}");
            if (LocationData is not null
                || Configuration.TryFindBestOsseousAshPath(out LocationData, HostsFileName))
                WriteToFile(LocationData, HostsFileName);
            else
                Utils.Error($"{nameof(Write)} to {HostsFileName}", new NullReferenceException($"{nameof(LocationData)} must not be null"));
        }

        public void WriteHosts(IEnumerable<Host> Hosts)
        {
            if (!Hosts.IsNullOrEmpty()
                && !this.SequenceEqual(Hosts))
            {
                DisposeClear();
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
            foreach (var host in Hosts.IteratorSafe())
                if (Add(host))
                    any = true;

            if (any)
                Write();
        }

        public void WriteRemoveHost(Host Host, bool Dispose = true)
        {
            if (Remove(Host))
            {
                Write();
                if (Dispose)
                    Host.Dispose(DisposePendingSavGz: true);
            }
        }

        public void WriteRemoveHostsDispose(params Host[] Hosts)
        {
            bool any = false;
            foreach (var host in Hosts.IteratorSafe())
            {
                if (Remove(host))
                {
                    host.Dispose(DisposePendingSavGz: true);
                    any = true;
                }
            }

            if (any)
                Write();
        }

        public void WriteRemoveHosts(params Host[] Hosts)
        {
            bool any = false;
            foreach (var host in Hosts.IteratorSafe())
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
            => this.Aggregate(LocationData.SanitiseForDisplay(), Utils.NewLineDelimitedAggregator)
            ;

        public void BuildHosts(bool Ping = false)
        {
            foreach (var host in this)
                host.Build(Ping: Ping);
        }

        public IEnumerable<Host.ConnectionLevels> PingHosts()
        {
            foreach (var host in this)
                yield return host.ConnectionLevel;
        }

        public static IEnumerable<HostLocationPair> AllHostsWithLocation(Predicate<HostLocationPair> Where)
        {
            foreach (var hostSet in ServerUtils.Hosts.IteratorSafe())
            {
                foreach (var host in hostSet.IteratorSafe())
                {
                    var element = new HostLocationPair(host, hostSet?.LocationData);
                    if (Where?.Invoke(element) is false)
                    {
                        element.Dispose();
                        continue;
                    }

                    yield return element;
                }
            }
        }

        public static IEnumerable<HostLocationPair> AllHostsWithLocation()
            => AllHostsWithLocation(null)
            ;

        public void DisposeClear(Predicate<Host> Where, bool DisposePendingSavGz = false)
        {
            foreach (var host in this)
                if (Where?.Invoke(host) is not false)
                    host.Dispose(DisposePendingSavGz: DisposePendingSavGz);

            Clear();
        }

        public void DisposeClear(bool DisposePendingSavGz = false)
            => DisposeClear(null, DisposePendingSavGz)
            ;

        public override void Dispose()
        {
            LocationData = null;
            DisposeClear();
            base.Dispose();
        }
        }
    public static class OsseousAshHostCollectionExtensions
    {
        public static int TotalCount(this Rack<HostSet> Hosts)
            => Hosts?.Aggregate(0, (a, n) => a + n.Count)
            ?? 0
            ;

        public static void BuildHosts(this Rack<HostSet> Hosts, bool Ping = false)
        {
            foreach (var hostCollection in Hosts.IteratorSafe())
                hostCollection.BuildHosts(Ping);
        }

        public static void PingHosts(this Rack<HostSet> Hosts)
            => Hosts?.Aggregate((IEnumerable<Host.ConnectionLevels>)null, (a, n) => n.PingHosts())
            ;

        public static Renderable GetAshCloudIcon(this HostSet HostCollection)
            => new Renderable(
                Tile: "Mutations/gas_generation.bmp",
                ColorString: $"&K",
                TileColor: $"&K",
                DetailColor: HostCollection?.LocationData?.GetFileLocationDataTypeColor()?[0] ?? 'y')
            ;

        public static IEnumerable<Report> CacheReports(this Rack<HostSet> Hosts)
        {
            if (Hosts.IsNullOrEmpty())
                yield break;

            foreach (var hostCollection in Hosts)
            {
                foreach (var host in hostCollection)
                {
                    if (!host.IsRunning)
                        continue;

                    var reportsCache = host.ReportsCache.IteratorSafe();
                    foreach (var report in reportsCache)
                        yield return report;
                }
            }
        }

        public static IEnumerable<Report> GetReports(
            this Rack<HostSet> Hosts,
            Predicate<Report> Filter
            )
        {
            if (Hosts.IsNullOrEmpty())
                yield break;

            //Utils.Log($"Getting Reports");
            var reportsCache = ServerUtils.ReportsCache.IteratorSafe();
            foreach (var report in reportsCache)
                if (Filter?.Invoke(report) is not false)
                    yield return report;
        }

        public static IEnumerable<Report> GetReports(this Rack<HostSet> Hosts)
            => Hosts.GetReports(null)
            ;

        private static bool ReportMatchesSpec(
            Report Report,
            Guid OAID = default, 
            string BonesID = null,
            bool? Blocked = null
            )
        {
            if (Report == null)
                return false;

            if (!OAID.IsEmptyOrDefault()
                && Report.UserID != OAID)
                return false;

            if (!BonesID.IsNullOrEmpty()
                && Report.BonesID != BonesID)
                return false;

            if (Blocked.HasValue
                && Blocked.GetValueOrDefault() != Report.Blocked)
                return false;

            return true;
        }

        public static IEnumerable<Report> GetReportsMatchingSpec(
            this Rack<HostSet> Hosts,
            Guid OAID = default,
            string BonesID = null,
            bool? Blocked = null
            )
            => Hosts.GetReports(r => ReportMatchesSpec(r, OAID, BonesID, Blocked))
            ;
    }
}

