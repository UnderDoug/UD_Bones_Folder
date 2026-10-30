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
using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
    public class HostSet : CompositeSet<Host>
    {
        #region Debug Registration
        [UD_DebugRegistry]
        public static void doDebugRegistry(DebugMethodRegistry Registry)
            => Registry.RegisterEach(
                Type: typeof(HostSet),
                MethodNameValues: new()
                {
                    { nameof(ReadFromFileAsync), true },
                });
        #endregion

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

            public void Deconstruct(out Host Host, out FileLocationData FileLocationData)
            {
                Host = this.Host;
                FileLocationData = this.FileLocationData;
            }

            public bool IsValid()
                => Host != null
                && FileLocationData != null
                ;

            public override string ToString()
                => $"{Host?.GetHostNameWithProtocol() ?? "NO_HOST"} in {FileLocationData?.SanitizeForDisplay() ?? "NO_LOCATION"}"
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

            public override string ToString()
                => $"{HostSet?.LocationData?.ToString() ?? "MISSING_HOSTSET"} -> {Host?.ToString() ?? "MISSING_HOST"}"
                ;

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

        [JsonProperty]
        private Host[] Hosts
        {
            get => ToArray();
            set
            {
                Clear();
                EnsureCapacity(value?.Length ?? 0);
                AddRange(value);
            }
        }

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
            if ((await FileLocationData?.ReadFromFileAsync<HostSet>(FileName)) is HostSet hostSet)
            {
                if (!hostSet.Hosts.IsNullOrEmpty())
                {
                    using var indent = new Indent(1);
                    Debug.LogCaller(
                        Indent: indent,
                        ArgPairs: new Debug.ArgPair[]
                        {
                            Debug.Arg(nameof(FileLocationData), FileLocationData),
                            Debug.Arg(nameof(FileName), FileName),
                            Debug.Arg(nameof(HostSet)),
                        });

                    hostSet.LocationData = FileLocationData;
                    return hostSet;
                }
            }
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
            using var indent = new Indent(1);
            Debug.LogCaller(
                Indent: indent,
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(FileLocationData), FileLocationData),
                    Debug.Arg(nameof(FileName), FileName),
                });

            var enableds = new Dictionary<Host, bool?>();
            foreach (var host in this)
                host.HotSwapEnabled(ref enableds);

            try
            {
                Debug.LogJson(this, Indent: indent[1]);
                FileLocationData.Write(FileName, this, Formatting.Indented);
                return;
            }
            catch (Exception x)
            {
                Utils.Warn($"{nameof(HostSet)} failed to {nameof(WriteToFile)}", x);
            }
            finally
            {
                foreach (var host in this)
                    host.HotSwapEnabled(ref enableds);
            }
        }

        public void Write()
        {
            using var indent = new Indent(1);
            Debug.LogCaller(
                Indent: indent,
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(HostSet), LocationData),
                });

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
                    Host.Dispose(DisposePending: true);
            }
        }

        public void WriteRemoveHostsDispose(params Host[] Hosts)
        {
            bool any = false;
            foreach (var host in Hosts.IteratorSafe())
            {
                if (Remove(host))
                {
                    host.Dispose(DisposePending: true);
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
            => $"{LocationData.Tag()} {DisplayName()}";

        public string DisplayName()
            => LocationData?.SanitizeForDisplay()
            ?? "{{R|ERR:}} Unknown Location "
            ;

        public override string ToString()
            => this.Aggregate(LocationData.SanitizeForDisplay(), Utils.NewLineDelimitedAggregator)
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

        public void DisposeClear(Predicate<Host> Where, bool DisposePendingSavGz = false)
        {
            foreach (var host in this)
                if (Where?.Invoke(host) is not false)
                    host.Dispose(DisposePending: DisposePendingSavGz);

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

