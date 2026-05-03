using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Platform.IO;

using Qud.UI;

using UD_Bones_Folder.Mod.UI;

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
        public class Host : IComposite, IEquatable<Host>, IDisposable
        {
            public static Host DefaultHost => new Host
            {
                Name = "osseousash.cloud",
                Port = null,
                Encrypted = true,
                TimeoutMS = 2200,
                Enabled = false,
            };

            private static long SeverStatusCheckInterval => 300000;

            private static int CurrentHostID = 0;

            public static string status => nameof(status);
            public static string canUp => nameof(canUp);
            public static string Bones => nameof(Bones);
            public static string ID => CombineRoute(Bones, nameof(ID));
            public static string IDs => CombineRoute(Bones, nameof(IDs));
            public static string Info => CombineRoute(Bones, nameof(Info));
            public static string Infos => CombineRoute(Bones, nameof(Infos));
            public static string Spec => CombineRoute(Bones, nameof(Spec));
            public static string Specs => CombineRoute(Bones, nameof(Specs));
            public static string SavGz => CombineRoute(Bones, nameof(SavGz));
            public static string Stats => CombineRoute(Bones, nameof(Stats));
            public static string Report => nameof(Report);

            [JsonIgnore]
            private int? _HostID;
            [JsonIgnore]
            private int HostID => _HostID ??= CurrentHostID++;

            /// <summary>
            /// Name of the host, excluding protocol and any port number.
            /// </summary>
            [JsonProperty]
            public string Name;

            /// <summary>
            /// Optional port number for host.
            /// </summary>
            [JsonProperty]
            public int? Port;

            /// <summary>
            /// Determines http or https.
            /// </summary>
            [JsonProperty]
            public bool Encrypted;

            /// <summary>
            /// The number of milliseconds to wait for a response.
            /// </summary>
            [JsonProperty]
            public int TimeoutMS = 1500;

            /// <summary>
            /// Current unused but will allow for effectively (optionally) password protecting a sever which will requiring this be assigned and sent along with the request.
            /// </summary>
            [JsonProperty]
            public string AuthToken;

            /// <summary>
            /// Indicates whether or not this host should be utilised.
            /// </summary>
            [JsonProperty]
            public bool Enabled;

            [JsonIgnore]
            private bool IsBuilt;

            [JsonIgnore]
            private string BuiltString => !IsBuilt
                ? " Unbuilt"
                : null
                ;

            [JsonIgnore]
            private bool? WrittenEnabled;

            [JsonIgnore]
            public bool IsRunning => GetServerStatus();

            [JsonIgnore]
            public bool CanUp => GetCanUp();

            [JsonIgnore]
            public int ConnectionLevel
            {
                get
                {
                    if (Enabled)
                    {
                        if (IsRunning)
                        {
                            if (CanUp)
                                return 3;
                            return 2;
                        }
                        return 1;
                    }
                    return 0;
                }
            }

            [JsonIgnore]
            public string ConnectionColor => GetConnectionColor(ConnectionLevel);

            [JsonIgnore]
            public string ServerStatusString
            {
                get
                {
                    int connectionLevel = ConnectionLevel;
                    return GetConnectionString(connectionLevel).Colored(GetConnectionColor(connectionLevel));
                }
            }

            [JsonIgnore]
            private Timer StatusCheckTimer;
            [JsonIgnore]
            private TimerCallback StatusCheckCallback => delegate (object state)
            {
                if (!IsBuilt)
                    return;

                if (!WrittenEnabled.HasValue)
                {
                    Utils.Info($"{DateTime.Now.Timestamp()} - [{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: " +
                        $"{GetHostNameWithProtocol()} lacks a {nameof(WrittenEnabled)} value - Clearing timer");
                    ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
                    return;
                }

                if (!Enabled
                    && WrittenEnabled.GetValueOrDefault())
                {
                    Enabled = true;
                    try
                    {
                        if (IsRunning)
                        {
                            WrittenEnabled = null;
                            Utils.Info($"{DateTime.Now.Timestamp()} - [{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: " +
                                $"Connection re-established to {GetHostNameWithProtocol()} - Re-enabled");
                            ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
                        }
                        else
                            Enabled = false;
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{DateTime.Now.Timestamp()} - [{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)} Checking Status", x);
                        Enabled = false;  
                    }
                }
                else
                {
                    Utils.Info($"{DateTime.Now.Timestamp()} - [{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: " +
                        $"{GetHostNameWithProtocol()} is already enabled, or {nameof(WrittenEnabled)} is false - Clearing timer");
                    ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
                }
            };

            public Host()
            {
                Enabled = true;
            }

            public Host(string Name, int? Port = null, bool Encrypted = false, string AuthToken = null, bool Enabled = true, int TimeoutMS = 2000)
                : this()
            {
                this.Name = Name;
                this.Port = Port;
                this.Encrypted = Encrypted;
                this.AuthToken = AuthToken;
                this.AuthToken = AuthToken;
                this.Enabled = Enabled;
                this.TimeoutMS = TimeoutMS;
            }

            public Host(string HostName, string AuthToken = null, int TimeoutMS = 2000)
                : this()
            {
                Parse(HostName, out Name, out Port, out Encrypted);
                if (!AuthToken.IsNullOrEmpty())
                    this.AuthToken = AuthToken;

                this.TimeoutMS = TimeoutMS;
            }

            private static void ClearStatusCheckTimer(ref Timer StatusCheckTimer, int Indent = 0)
            {
                if (StatusCheckTimer is not null)
                {
                    if (Indent == 0)
                        Utils.Info($"Cleared {nameof(StatusCheckTimer)}");
                    else
                        Utils.Log($"{Indent.Indent()}Cleared {nameof(StatusCheckTimer)}");
                }

                StatusCheckTimer?.Dispose();
                StatusCheckTimer = null;
            }

            private static void SetupStatusCheckTimer(ref Timer StatusCheckTimer, Host Host, int Indent = 0)
            {
                if (!Host.IsBuilt)
                {
                    ClearStatusCheckTimer(ref StatusCheckTimer, Indent);
                    return;
                }

                if (StatusCheckTimer is null)
                {
                    Host.WrittenEnabled = Host.Enabled;
                    Host.Enabled = false;
                    StatusCheckTimer = new Timer(Host.StatusCheckCallback, null, SeverStatusCheckInterval, SeverStatusCheckInterval);

                    string successMessage = $"Timer for {Host.GetHostNameWithProtocol()} set up successfully.";
                    if (Indent == 0)
                        Utils.Info($"{DateTime.Now.Timestamp()} - [{nameof(HostID)}: {Host.HostID}] {nameof(SetupStatusCheckTimer)}: {successMessage}");
                    else
                        Utils.Log($"{Indent.Indent()}{successMessage}");
                }
                else
                {
                    string failureMessage = $"Timer for {Host.GetHostNameWithProtocol()} already exists.";
                    if (Indent == 0)
                        Utils.Info($"{DateTime.Now.Timestamp()} - [{nameof(HostID)}: {Host.HostID}] {nameof(SetupStatusCheckTimer)}: {failureMessage}");
                    /*else
                        Utils.Log($"{Indent.Indent()}{failureMessage}");*/
                }
            }

            private bool HandleTimeoutWebException(WebException X, string URI, int? Timeout)
            {
                if (X.Status == WebExceptionStatus.Timeout)
                {
                    Utils.Info($"{DateTime.Now.Timestamp()} - [{nameof(HostID)}: {HostID}{BuiltString}] Timed out getting status from {URI} ({GetTimeoutString(Timeout)}).");
                    if (IsBuilt)
                        SetupStatusCheckTimer(ref StatusCheckTimer, this, Indent: 1);

                    return true;
                }
                return false;
            }

            public static void Parse(string HostName, out string Name, out int? Port, out bool Encrypted)
            {
                if (HostName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(HostName));

                Port = null;
                Encrypted = false;

                Name = HostName;
                if (Name.StartsWith("http"))
                {
                    Name = Name[4..];
                    Encrypted = Name[0] == 's';
                    if (Encrypted)
                        Name = Name[1..];
                }
                if (Name.StartsWith(":"))
                    Name = Name[1..];

                while (Name.StartsWith('/'))
                    Name = Name[1..];

                if (Name.TrySplitOut(":", out Name, out string port))
                {
                    if (port.IndexOf("/") is int slashIndex
                        && slashIndex >= 0)
                        port = port[..slashIndex];

                    if (!port.IsNullOrEmpty())
                    {
                        if (int.TryParse(port, out int portInt))
                            Port = portInt;
                        else
                            Utils.Warn($"Failed to parse {nameof(port)} {port} to {typeof(int).Name}");
                    }
                }
                if (Name.EndsWith("/"))
                    Name = Name[..^1];
            }

            public static bool TryParse(string HostName, out Host Host)
            {
                Host = null;
                try
                {
                    Parse(HostName, out string name, out int? port, out bool encrypted);
                    Host = new Host
                    {
                        Name = name,
                        Port = port,
                        Encrypted = encrypted,
                    };
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public void HotSwapEnabledWhile(Action Action)
            {
                bool enabled = Enabled;
                if (WrittenEnabled.HasValue)
                    Enabled = WrittenEnabled.GetValueOrDefault();

                Action?.Invoke();

                Enabled = enabled;
            }

            public async Task HotSwapEnabledWhileAsync(Task Task)
            {
                bool enabled = Enabled;
                if (WrittenEnabled.HasValue)
                    Enabled = WrittenEnabled.GetValueOrDefault();

                await Task;

                Enabled = enabled;
            }

            public void HotSwapEnabled(ref Dictionary<Host, bool?> Enableds)
            {
                Enableds ??= new();
                if (!Enableds.TryGetValue(this, out bool? enabled))
                    enabled = (Enableds[this] = null);

                if (enabled.HasValue)
                {
                    Enabled = enabled.GetValueOrDefault();
                    Enableds[this] = null;
                    return;
                }

                Enableds[this] = Enabled;
                if (WrittenEnabled.HasValue)
                    Enabled = WrittenEnabled.GetValueOrDefault();
            }

            public string GetEnabledCheckbox()
                => WrittenEnabled.HasValue
                ? WrittenEnabled.GetValueOrDefault().GetCheckboxText(nameof(Enabled))
                : Enabled.GetCheckboxText(nameof(Enabled))
                ;

            public bool GetEnabledValueForMenu()
                => WrittenEnabled
                ?? Enabled
                ;

            public static Host Clone(Host Host, bool? SetBuiltTo = null)
            {
                if (Host == null)
                    throw new ArgumentNullException(nameof(Host));

                var newHost = new Host();

                Host.CopyTo(ref newHost, SetBuiltTo);
                return newHost;
            }

            public Host Clone(bool? SetBuiltTo = null)
                => Clone(this, SetBuiltTo)
                ;

            public static void CopyTo(
                Host Source,
                ref Host Destination,
                bool? SetBuiltTo = null,
                bool DisposeSource = false
                )
            {
                if (Source == null)
                    return;

                if (Destination == null)
                {
                    Destination = Source.Clone();
                    return;
                }

                Destination.Name = Source.Name;
                Destination.Port = Source.Port;
                Destination.Encrypted = Source.Encrypted;
                Destination.TimeoutMS = Source.TimeoutMS;
                Destination.AuthToken = Source.AuthToken;
                Destination.Enabled = Source.Enabled;
                Destination.IsBuilt = Source.IsBuilt;

                if (SetBuiltTo.HasValue)
                {
                    if (SetBuiltTo.GetValueOrDefault())
                        Destination.Build(Ping: false);
                    else
                        Destination.Unbuild();
                }

                if (Destination.WrittenEnabled.HasValue)
                    Destination.WrittenEnabled = Source.Enabled;

                if (Source.StatusCheckTimer is not null)
                {
                    Destination.Enabled = Source.WrittenEnabled.GetValueOrDefault();
                    ClearStatusCheckTimer(ref Destination.StatusCheckTimer);
                    SetupStatusCheckTimer(ref Destination.StatusCheckTimer, Destination);
                }

                if (DisposeSource)
                    Source.Dispose();
            }

            public void CopyTo(
                ref Host Destination,
                bool? SetBuiltTo = null,
                bool ThenDispose = false
                )
                => CopyTo(this, ref Destination, SetBuiltTo, ThenDispose)
                ;

            public void CopyTo(
                Host Destination,
                bool? SetBuiltTo = null,
                bool ThenDispose = false
                )
                => CopyTo(this, ref Destination, SetBuiltTo, ThenDispose)
                ;

            public void Unbuild()
            {
                IsBuilt = false;
                ClearStatusCheckTimer(ref StatusCheckTimer);
            }

            public void Build(bool Ping = false)
            {
                IsBuilt = true;
                if (Ping)
                    _ = IsRunning;
            }

            public int? GetTimeout()
                => TimeoutMS >= 0
                ? TimeoutMS
                : null
                ;

            public static string GetTimeoutString(int? Timeout)
                => Timeout is int timeout
                ? $"{timeout} ms"
                : $"\u00ec ms"
                ;

            public string GetTimeoutString()
                => GetTimeoutString(GetTimeout())
                ;

            public static string GetConnectionColor(int ConnectionLevel)
                => ConnectionLevel switch
                {
                    3 => "green",
                    2 => "yellow",
                    1 => "red",
                    0 => "black",
                    _ => "M",
                }
                ;

            public static string GetConnectionString(int ConnectionLevel)
                => ConnectionLevel switch
                {
                    3 => "Running",
                    2 => "Download Only",
                    1 => "Not Responding",
                    0 => "Disabled",
                    _ => "Error",
                }
                ;

            public static string GetConnectionSymbol(int ConnectionLevel)
                => "\u000f".Colored(GetConnectionColor(ConnectionLevel)) // \u000f ☼ | \u0017 ↨
                ;

            public string GetEnableDisableUIText()
            {
                string notColor = "K";
                string color = "W";

                string enabledText = "Enabled";
                string disabledText = "Disabled";

                if (Enabled)
                {
                    enabledText = enabledText.Colored(color);
                    disabledText = disabledText.Colored(notColor);
                }
                else
                {
                    enabledText = enabledText.Colored(notColor);
                    disabledText = disabledText.Colored(color);
                }
                return $"Toggle: {enabledText}/{disabledText}";
            }

            public static string GetProtocol(
                string HostName = null,
                bool Encrypted = false,
                bool TrailingSlash = true
                )
            {
                string protocol = Encrypted
                    ? "https://"
                    : "http://"
                    ;

                string trailingSlash = TrailingSlash
                    ? "/"
                    : null
                    ;

                return $"{protocol}{TrimSlashes(HostName)}{trailingSlash}";
            }

            public string GetHostNameWithProtocol(bool Encrypted, bool TrailingSlash = true)
                => GetProtocol(NameWithPort(Name, Port), Encrypted, TrailingSlash)
                ;

            public string GetHostNameWithProtocol(bool TrailingSlash = true)
                => GetHostNameWithProtocol(Encrypted, TrailingSlash)
                ;

            public string FullDisplayName(bool IncludeAuth = false)
                => $"{Enabled.GetCheckbox()} "
                + $"{GetConnectionSymbol(ConnectionLevel)} " // \u000f ☼ | \u0017 ↨
                + GetHostNameWithProtocol()
                + (AuthToken.IsNullOrEmpty() || !IncludeAuth ? null : " (Auth)")
                ;

            public static string TrimSlashes(string Path)
            {
                if (Path?.StartsWith('/') is true)
                    Path = Path[1..];
                if (Path?.EndsWith('/') is true)
                    Path = Path[..^1];

                return Path;
            }

            public static string NameWithPort(string HostName, int? Port = null)
            {
                string nameWithPort = HostName;

                if (nameWithPort.EndsWith("/"))
                    nameWithPort = nameWithPort[..^1];

                if (Port.HasValue)
                    nameWithPort += $":{Port}";

                return nameWithPort;
            }

            public static string SlashTrimmedSlashDelimitedAggregator(string Accumulator, string Next)
                => Utils.DelimitedAggregator(Accumulator, TrimSlashes(Next), "/")
                ;

            public static string CombineRoute(params string[] Routes)
                => Routes?.Aggregate("", SlashTrimmedSlashDelimitedAggregator)
                ;

            private string CombineHostRoute(params string[] Route)
                => CombineRoute(GetHostNameWithProtocol(), CombineRoute(Route))
                ;

            public string statusGetRoute(string BonesID = null)
                => CombineHostRoute(status, BonesID)
                ;

            public string canUpGetRoute(Guid OsseousAshID)
                => CombineHostRoute(canUp, OsseousAshID.ToString())
                ;

            public string BonesIDGetRoute(string BonesID = null)
                => CombineHostRoute(ID, BonesID)
                ;

            public string BonesInfoGetRoute(string BonesID = null)
                => CombineHostRoute(Info, BonesID)
                ;

            public string BonesSavGzGetRoute(string BonesID = null)
                => CombineHostRoute(SavGz, BonesID)
                ;

            public string BonesInfosGetRoute()
                => CombineHostRoute(Infos)
                ;

            public string BonesPostRoute()
                => CombineHostRoute(Bones)
                ;

            public string BonesSavGzPutRoute(string BonesID = null)
                => CombineHostRoute(SavGz, BonesID)
                ;

            public string BonesReportPostRoute()
                => CombineHostRoute(Report)
                ;

            public string BonesStatsPutRoute(string BonesID, Guid OsseousAshID)
                => CombineHostRoute(Stats, BonesID, OsseousAshID.ToString())
                ;

            public string DisplayName()
                => NameWithPort(Name, Port);

            private static HttpWebRequest CreateWebRequest(
                string URI,
                string ContentType,
                WebMethods Method,
                int? Timeout = null,
                Action<System.IO.StreamWriter> Proc = null
                )
            {
                var httpReq = (HttpWebRequest)WebRequest.Create(URI);
                httpReq.ContentType = ContentTypes.GetValue(ContentType) ?? ContentType;
                httpReq.Method = Methods.GetValue(Method);

                if (Timeout.HasValue)
                    httpReq.Timeout = Timeout.GetValueOrDefault();

                if (Proc != null)
                {
                    using var streamWriter = new System.IO.StreamWriter(httpReq.GetRequestStream());
                    Proc(streamWriter);
                }

                return httpReq;
            }

            private static HttpWebRequest CreatePostJSON(
                string URI,
                int? Timeout = null,
                Action<System.IO.StreamWriter> Proc = null
                )
            => CreateWebRequest(
                URI: URI,
                ContentType: "json",
                Method: WebMethods.POST,
                Timeout: Timeout,
                Proc: Proc)
            ;

            private static HttpWebRequest CreatePutJSON(
                string URI,
                int? Timeout = null,
                Action<System.IO.StreamWriter> Proc = null
                )
            => CreateWebRequest(
                URI: URI,
                ContentType: "json",
                Method: WebMethods.PUT,
                Timeout: Timeout,
                Proc: Proc)
            ;

            private static HttpWebRequest CreatePutGz(
                string URI,
                int? Timeout = null,
                Action<System.IO.StreamWriter> Proc = null
                )
            => CreateWebRequest(
                URI: URI,
                ContentType: "gz",
                Method: WebMethods.PUT,
                Timeout: Timeout,
                Proc: Proc)
            ;

            private static HttpWebRequest CreateGetJSON(
                string URI,
                int? Timeout = null,
                Action<System.IO.StreamWriter> Proc = null
                )
            => CreateWebRequest(
                URI: URI,
                ContentType: "json",
                Method: WebMethods.GET,
                Timeout: Timeout,
                Proc: Proc)
            ;

            private static HttpWebRequest CreateGetGz(
                string URI,
                int? Timeout = null,
                Action<System.IO.StreamWriter> Proc = null
                )
            => CreateWebRequest(
                URI: URI,
                ContentType: "gz",
                Method: WebMethods.GET,
                Timeout: Timeout,
                Proc: Proc)
            ;

            #region Get Server Status

            public bool GetServerStatus()
            {
                if (!Enabled)
                    return false;

                string uRI = statusGetRoute();
                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                if (timeout.HasValue)
                    timeout = (int)(timeout.GetValueOrDefault() * 0.5);

                try
                {
                    httpReq = CreateGetJSON(uRI, timeout);
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating Server Status HttpWebRequest for {uRI}", x);
                    return false;
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        if (httpRes.StatusCode == HttpStatusCode.OK
                            && JObject.Parse(result) is JObject jObject
                            && jObject["status"].ToObject<string>() is string statusString
                            && statusString == "Running")
                        {
                            return true;
                        }
                        else
                            Utils.Warn($"{nameof(GetBonesInfos)} got no status from {ToString()}" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return false;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving GET response for {uRI}", x);
                }
                return false;
            }

            public bool GetCanUp()
            {
                if (!Enabled)
                    return false;

                if (!IsRunning)
                    return false;

                if (Config?.ID is not Guid osseousAshID
                    || osseousAshID == Guid.Empty)
                    return false;

                string uRI = canUpGetRoute(osseousAshID);
                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreateGetJSON(uRI, timeout);
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating GET canUp HttpWebRequest for {uRI}", x);
                    return false;
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        if (httpRes.StatusCode == HttpStatusCode.OK
                            && JsonConvert.DeserializeObject<bool>(result) is bool parsedResult
                            && parsedResult)
                        {
                            return true;
                        }
                        else
                            Utils.Warn($"{nameof(GetBonesInfos)} got no status from {ToString()}" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return false;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving GET response for {uRI}", x);
                }
                return false;
            }

            #endregion
            #region Upload Saves

            public async Task<Guid> PostBonesInfo(
                string BonesID,
                SaveBonesJSON SaveBonesJSON,
                byte[] SavGz
                )
            {
                if (!Enabled
                    || BonesID.IsNullOrEmpty()
                    || SaveBonesJSON == null
                    || SavGz.IsNullOrEmpty())
                    return Guid.Empty;

                SaveBonesJSON.FileLocationType = FileLocationData.LocationType.Online;

                string uRI = BonesPostRoute();

                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreatePostJSON(
                        URI: uRI,
                        Timeout: timeout,
                        Proc: async delegate (System.IO.StreamWriter streamWriter)
                        {
                            using var record = new OsseousAsh.Record(
                                BonesID: BonesID,
                                SaveBonesJSON: SaveBonesJSON,
                                SavGz: SavGz)
                            ;
                            await streamWriter.WriteAsync(JsonConvert.SerializeObject(record));
                        });
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating POST HttpWebRequest for {uRI}", x);
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = await streamReader.ReadToEndAsync();
                        if (httpRes.StatusCode == HttpStatusCode.Created)
                        {
                            if (JObject.Parse(result) is JObject jObject
                                && jObject["success"].ToObject<Guid>() is Guid parsedToken)
                            {
                                Utils.Info($"Successfully created new BonesInfo on server at \"{ToString()}\"" +
                                    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})" +
                                    $" - {nameof(parsedToken)}: {parsedToken}");

                                return parsedToken;
                            }
                        }
                        else
                        {
                            Utils.Warn($"{nameof(TryUploadBonesAsync)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode}) " +
                                $"instead of expected {HttpStatusCode.Created} ({(int)HttpStatusCode.Created})");
                        }
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return Guid.Empty;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving POST response for {uRI}", x);
                    return Guid.Empty;
                }
                return Guid.Empty;
            }

            public async Task<bool> PutBonesSavGz(
                string BonesID,
                Guid Token,
                byte[] SavGz
                )
            {
                if (!Enabled
                    || !IsRunning
                    || BonesID.IsNullOrEmpty()
                    || Token.IsEmptyOrDefault()
                    || SavGz.IsNullOrEmpty())
                    return false;

                string uRI = BonesSavGzPutRoute(BonesID);

                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreatePutGz(
                        URI: uRI,
                        Timeout: timeout,
                        Proc: async delegate (System.IO.StreamWriter streamWriter)
                        {
                            using (var savGzStream = new System.IO.MemoryStream(SavGz))
                            {
                                await savGzStream.CopyToAsync(streamWriter.BaseStream);
                            }
                        });

                    string authHeader = null;
                    if (httpReq.Headers.Get(HttpRequestHeader.Authorization.ToString()) is string existingAuthHeader)
                        authHeader = existingAuthHeader + ";";

                    httpReq.Headers.Add(HttpRequestHeader.Authorization, $"basic {Token}");
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating PUT HttpWebRequest for {uRI}", x);
                    return false;
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = await streamReader.ReadToEndAsync();
                        if (httpRes.StatusCode == HttpStatusCode.Created)
                        {
                            if (JObject.Parse(result) is JObject jObject
                                && jObject["success"].ToObject<bool>())
                            {
                                Utils.Info($"Successfully added \".sav.gz\" blob to Bones on server at \"{ToString()}\"" +
                                    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})\n{jObject}");
                                return true;
                            }
                        }
                        else
                        {
                            Utils.Warn($"{nameof(TryUploadBonesAsync)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode}) " +
                                $"instead of expected {HttpStatusCode.Created} ({(int)HttpStatusCode.Created})");
                        }
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return false;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving PUT response for {uRI}", x);
                    return false;
                }

                return false;
            }

            public async Task<bool> TryUploadBonesAsync(
                string BonesID,
                SaveBonesJSON SaveBonesJSON,
                byte[] SavGz
                )
            {
                if (!Enabled)
                    return false;

                try
                {
                    Guid token = Guid.Empty;
                    try
                    {
                        token = await PostBonesInfo(BonesID, SaveBonesJSON, SavGz);
                    }
                    catch
                    {
                        token = Guid.Empty;
                    }

                    if (token.IsEmptyOrDefault())
                        return false;

                    try
                    {
                        return await PutBonesSavGz(BonesID, token, SavGz);
                    }
                    catch
                    {
                        return false;
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"{nameof(TryUploadBonesAsync)} failed to upload Bones with BonesID {BonesID}", x);
                    return false;
                }
            }

            public bool TryUploadBones(
                string BonesID,
                SaveBonesJSON SaveBonesJSON,
                byte[] SavGz
                )
                => TryUploadBonesAsync(BonesID, SaveBonesJSON, SavGz).WaitResult();

            #endregion
            #region Post Bones Report

            public async Task<bool> PostBonesReport(Report Report)
            {
                if (!Enabled
                    || !IsRunning
                    || Report == null
                    || Report.BonesID.IsNullOrEmpty())
                    return false;

                string uRI = BonesReportPostRoute();

                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreatePostJSON(
                        URI: uRI,
                        Timeout: timeout,
                        Proc: async delegate (System.IO.StreamWriter streamWriter)
                        {
                            await streamWriter.WriteAsync(JsonConvert.SerializeObject(Report));
                        });
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating POST HttpWebRequest for {uRI}", x);
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = await streamReader.ReadToEndAsync();
                        if (httpRes.StatusCode == HttpStatusCode.Created)
                        {
                            if (JObject.Parse(result) is JObject jObject
                                && jObject["success"].ToObject<bool>() is bool success)
                            {
                                Utils.Info($"Successfully reported {Report.BonesID} on server at \"{ToString()}\"" +
                                    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");

                                return success;
                            }
                        }
                        else
                        {
                            Utils.Warn($"{nameof(PostBonesReport)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode}) " +
                                $"instead of expected {HttpStatusCode.Created} ({(int)HttpStatusCode.Created})");
                        }
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return false;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving POST response for {uRI}", x);
                    return false;
                }
                return false;
            }

            #endregion
            #region Get Bones Info(s)

            private SaveBonesJSON[] GetSaveBonesJSONs()
            {
                if (!Enabled
                    || !IsRunning)
                    return null;

                string uRI = BonesInfosGetRoute();
                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreateGetJSON(uRI, timeout);
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating GET HttpWebRequest for {uRI}", x);
                    return null;
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        if (httpRes.StatusCode == HttpStatusCode.OK
                            && JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<bool>())
                        {
                            try
                            {
                                return JsonConvert.DeserializeObject<SaveBonesJSON[]>(jObject["data"].ToString());
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"{nameof(GetBonesInfos)} failed to deserialize {nameof(jObject)}[\"data\"] to {typeof(SaveBonesJSON[]).Name}", x);
                                return null;
                            }
                        }
                        else
                        if (httpRes.StatusCode == HttpStatusCode.NoContent)
                        {
                            //Utils.Log($"{nameof(GetBonesInfos)} got no SaveBonesInfo from {ToString()}" +
                            //    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                        }
                        else
                        {
                            Utils.Warn($"{nameof(GetBonesInfos)} got no SaveBonesInfo from {ToString()}" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                        }
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return null;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving GET response for {uRI}", x);
                }
                return null;
            }

            public IEnumerable<SaveBonesInfo> GetSaveBonesInfos()
            {
                if (GetSaveBonesJSONs() is not SaveBonesJSON[] saveBonesJSONs)
                    yield break;

                using var saveBonesInfos = ScopeDisposedList<SaveBonesInfo>.GetFromPool();
                foreach (var saveBonesJSON in saveBonesJSONs)
                {
                    if (saveBonesInfos.Any(bonesInfo => bonesInfo.ID == saveBonesJSON.ID))
                        continue;

                    var saveBonesInfo = SaveBonesJSON.InfoFromJson(
                        SaveBonesJSON: saveBonesJSON,
                        SaveLocation: GetHostNameWithProtocol(TrailingSlash: true),
                        FileName: CombineRoute(SavGz, saveBonesJSON.ID),
                        SaveSize: 0);

                    yield return saveBonesInfo;

                    saveBonesInfos.Add(saveBonesInfo);
                }

                //Utils.Log($"{nameof(GetBonesInfos)} got {saveBonesInfos.Count} Bones Info from {ToString()}");
            }

            private SaveBonesJSON GetSaveBonesJSON(string BonesID)
            {
                if (!Enabled
                    || !IsRunning)
                    return null;

                string uRI = BonesInfoGetRoute(BonesID);
                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreateGetJSON(uRI, timeout);
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating GET HttpWebRequest for {uRI} ({timeout} ms).", x);
                    return null;
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        if (httpRes.StatusCode == HttpStatusCode.OK
                            && JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<bool>())
                        {
                            try
                            {
                                return JsonConvert.DeserializeObject<SaveBonesJSON>(jObject["BonesInfo"].ToString());
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"{nameof(GetBonesInfos)} failed to deserialize {nameof(jObject)}[\"BonesInfo\"] to {typeof(SaveBonesJSON).Name}", x);
                                return null;
                            }
                        }
                        else
                        if (httpRes.StatusCode == HttpStatusCode.NoContent)
                        {
                            //Utils.Log($"{nameof(GetBonesInfo)} got no SaveBonesInfo from {ToString()}" +
                            //    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                        }
                        else
                        {
                            Utils.Warn($"{nameof(GetBonesInfos)} got no SaveBonesInfo from {ToString()}" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                        }
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return null;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving GET response for {uRI}", x);
                }
                return null;
            }

            public SaveBonesInfo GetSaveBonesInfo(string BonesID)
                => GetSaveBonesJSON(BonesID)?.InfoFromJson(
                    SaveLocation: ToString(),
                    FileName: CombineRoute(SavGz, BonesID),
                    SaveSize: 0)
                ;

            #endregion
            #region Get Bones SavGz

            public byte[] GetBonesSavGz(string BonesID)
            {
                if (!Enabled
                    || !IsRunning)
                    return null;

                if (BonesID.IsNullOrEmpty())
                    return null;

                string uRI = BonesSavGzGetRoute(BonesID);
                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreateGetGz(uRI, timeout);
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating PUT HttpWebRequest for {uRI}", x);
                    return null;
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        try
                        {
                            if (streamReader.ReadAllBytes() is byte[] rawBuffer)
                            {
                                //Utils.Log($"{nameof(GetBonesSavGz)} got a SaveBonesSavGz from {ToString()} ({Buffer.ByteLength(rawBuffer).Things(typeof(byte).Name)})" +
                                //    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                                return rawBuffer;
                            }
                        }
                        catch (Exception x)
                        {
                            Utils.Error($"{nameof(GetBonesSavGz)} failed to get a Bones SavGz from {ToString()}" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})", x);
                            return null;
                        }
                    }
                    Utils.Warn($"{nameof(GetBonesSavGz)} failed to get a Bones SavGz from {ToString()}" +
                        $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return null;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"{nameof(GetBonesSavGz)} failed to get a Bones SavGz from {ToString()}", x);
                }
                return null;
            }

            #endregion
            #region Put Bones Stats

            public async Task<bool> PutBonesStats(
                string BonesID,
                SaveBonesJSON SaveBonesJSON
                )
            {
                if (!Enabled
                    || !IsRunning
                    || BonesID.IsNullOrEmpty()
                    || SaveBonesJSON == null)
                    return false;

                string uRI = BonesStatsPutRoute(BonesID, Config.ID);

                HttpWebRequest httpReq = null;
                int? timeout = GetTimeout();
                try
                {
                    httpReq = CreatePutJSON(
                        URI: uRI,
                        Timeout: timeout,
                        Proc: async delegate (System.IO.StreamWriter streamWriter)
                        {
                            await streamWriter.WriteAsync(JsonConvert.SerializeObject(SaveBonesJSON));
                        });
                }
                catch (Exception x)
                {
                    Utils.Error($"Creating PUT HttpWebRequest for {uRI}", x);
                    return false;
                }

                try
                {
                    var httpRes = (HttpWebResponse)httpReq.GetResponse();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = await streamReader.ReadToEndAsync();
                        if (httpRes.StatusCode == HttpStatusCode.OK)
                        {
                            if (JObject.Parse(result) is JObject jObject
                                && jObject["success"].ToObject<bool>())
                            {
                                Utils.Info($"Successfully updated stats to BonesRecord on server at \"{ToString()}\"" +
                                    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})\n{jObject}");
                                return true;
                            }
                        }
                        else
                        {
                            Utils.Warn($"{nameof(TryUploadBonesAsync)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode}) " +
                                $"instead of expected {HttpStatusCode.OK} ({(int)HttpStatusCode.OK})");
                        }
                    }
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, timeout))
                        return false;
                    throw x;
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed receiving PUT response for {uRI}", x);
                    return false;
                }

                return false;
            }

            #endregion

            public bool SameAs(Host Other, bool IgnoreDisabled = false)
                => Other is not null
                && Name == Other.Name
                && Port == Other.Port
                && Encrypted == Other.Encrypted
                && AuthToken == Other.AuthToken
                && (IgnoreDisabled
                    || GetEnabledValueForMenu() == Other.GetEnabledValueForMenu())
                ;

            public override string ToString()
                => $"{GetHostNameWithProtocol(TrailingSlash: true)}"
                ;

            public override bool Equals(object obj)
                => obj is Host hostObj
                ? Equals(hostObj)
                : base.Equals(obj)
                ;

            public override int GetHashCode()
                => Name.GetHashCode()
                ^ (Port?.GetHashCode() ?? 0)
                ^ Encrypted.GetHashCode()
                ^ (AuthToken?.GetHashCode() ?? 0)
                ^ Enabled.GetHashCode()
                ;

            public bool Equals(Host other)
                => SameAs(other)
                ;

            public static Task<bool> FlipEncryptedAsync(Host Host)
                => Task.Run(() => (Host.Encrypted = !Host.Encrypted) || true) // always true, but flip it first.
                ;

            public static Task<bool> FlipEnabledAsync(Host Host)
                => Task.Run(delegate ()
                {
                    if (Host.WrittenEnabled.HasValue)
                        return (Host.WrittenEnabled = !Host.WrittenEnabled.GetValueOrDefault()).GetValueOrDefault()
                            || true; // always true, but flip it first.

                    return (Host.Enabled = !Host.Enabled) || true; // always true, but flip it first.
                })
                ;

            public void Dispose()
            {
                Name = null;
                Port = null;
                Encrypted = false;
                AuthToken = null;
                Enabled = false;
                WrittenEnabled = false;
                ClearStatusCheckTimer(ref StatusCheckTimer);
            }

            public static implicit operator string(Host Host)
                => Host.ToString()
                ;
        }
    }
}
