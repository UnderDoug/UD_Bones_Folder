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
        public class Host : IComposite, IEquatable<Host>, IDisposable
        {
            public static Host DefaultHost => new Host
            {
                Name = "osseousash.cloud",
                Port = null,
                Encrypted = true,
            };

            public static string canUp => nameof(canUp);
            public static string Bones => nameof(Bones);
            public static string ID => CombineRoute(Bones, nameof(ID));
            public static string IDs => CombineRoute(Bones, nameof(IDs));
            public static string Info => CombineRoute(Bones, nameof(Info));
            public static string Infos => CombineRoute(Bones, nameof(Infos));
            public static string Spec => CombineRoute(Bones, nameof(Spec));
            public static string Specs => CombineRoute(Bones, nameof(Specs));
            public static string SavGz => CombineRoute(Bones, nameof(SavGz));

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
            /// Current unused but will allow for effectively (optionally) password protecting a sever which will requiring this be assigned and sent along with the request.
            /// </summary>
            [JsonProperty]
            public string AuthToken;

            public Host()
            { }

            public Host(string Name, int? Port = null, bool Encrypted = false, string AuthToken = null)
                : this()
            {
                this.Name = Name;
                this.Port = Port;
                this.Encrypted = Encrypted;
                this.AuthToken = AuthToken;
            }

            public Host(string HostName, string AuthToken = null)
                : this()
            {
                Parse(HostName, out Name, out Port, out Encrypted);
                if (!AuthToken.IsNullOrEmpty())
                    this.AuthToken = AuthToken;
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
                    Encrypted = HostName[0] == 's';
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
                        port = port[..^slashIndex];

                    if (int.TryParse(port, out int portInt))
                        Port = portInt;
                    else
                        Utils.Warn($"Failed to parse {nameof(port)} {port} to {typeof(int).Name}");
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

            public static Host Clone(Host Host)
                => Host != null
                ? new Host
                    {
                        Name = Host.Name,
                        Port = Host.Port,
                        Encrypted = Host.Encrypted,
                        AuthToken = Host.AuthToken,
                    }
                : throw new ArgumentNullException(nameof(Host))
                ;

            public Host Clone()
                => Clone(this)
                ;

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

            public string canUpGetRoute(string BonesID = null)
                => CombineHostRoute(canUp, BonesID)
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

            #region Upload Saves

            public async Task<Guid> PostBonesInfo(
                string BonesID,
                SaveBonesJSON SaveBonesJSON,
                byte[] SavGz
                )
            {
                if (BonesID.IsNullOrEmpty()
                    || SaveBonesJSON == null
                    || SavGz.IsNullOrEmpty())
                    return Guid.Empty;

                SaveBonesJSON.DirectoryType = FileLocationData.LocationType.Online;

                string uRI = BonesPostRoute();

                HttpWebRequest httpReq = null;
                try
                {
                    httpReq = CreatePostJSON(
                        URI: uRI,
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
                    Utils.Error($"Failed to create POST request for {uRI}", x);
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
                if (BonesID.IsNullOrEmpty()
                    || Token.IsEmptyOrDefault()
                    || SavGz.IsNullOrEmpty())
                    return false;

                string uRI = BonesSavGzPutRoute(BonesID);

                HttpWebRequest httpReq = null;
                try
                {
                    httpReq = CreatePutGz(
                        URI: uRI,
                        Proc: async delegate (System.IO.StreamWriter streamWriter)
                        {
                            using (var savGzStream = new System.IO.MemoryStream(SavGz))
                            {
                                await savGzStream.CopyToAsync(streamWriter.BaseStream);
                            }
                        });
                }
                catch
                {
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
                                var bonesIDString = jObject[nameof(BonesID)].ToObject<string>();
                                var savGzString = jObject[nameof(BonesID)].ToObject<string>();
                                Utils.Info($"Successfully added \".sav.gz\" blob to BonesRecord on server at \"{ToString()}\"" +
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
            #region Get Bones Infos

            private SaveBonesJSON[] GetSaveBonesJSONs()
            {
                string uRI = BonesInfosGetRoute();
                HttpWebRequest httpReq = null;
                int timeout = TimeSpan.FromSeconds(4).Milliseconds;
                try
                {
                    httpReq = CreateGetJSON(uRI);
                }
                catch (TaskCanceledException)
                {
                    Utils.Info($"Timed out getting Bones Info from {uRI} ({timeout} ms).");
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
                        SaveLocation: ToString(),
                        FileName: CombineRoute(SavGz, saveBonesJSON.ID),
                        SaveSize: 0);

                    yield return saveBonesInfo;

                    saveBonesInfos.Add(saveBonesInfo);
                }

                //Utils.Log($"{nameof(GetBonesInfos)} got {saveBonesInfos.Count} Bones Info from {ToString()}");
            }

            #endregion
            #region Get Bones SavGz

            public byte[] GetBonesSavGz(string BonesID)
            {
                if (BonesID.IsNullOrEmpty())
                    return null;

                string uRI = BonesSavGzGetRoute(BonesID);
                HttpWebRequest httpReq = null;
                int timeout = TimeSpan.FromSeconds(4).Milliseconds;
                try
                {
                    httpReq = CreateGetGz(uRI);
                }
                catch (TaskCanceledException)
                {
                    Utils.Info($"Timed out getting Bones SavGz from {uRI} ({timeout} ms).");
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
                catch (Exception x)
                {
                    Utils.Error($"{nameof(GetBonesSavGz)} failed to get a Bones SavGz from {ToString()}", x);
                }
                return null;
            }

            #endregion
            #region Option Handling

            private static void RemoveHostOption(
                int Index,
                ref Rack<string> Options,
                ref Rack<IRenderable> Renders,
                ref Rack<char> Hotkeys
                )
            {
                Options.RemoveAt(Index);
                Renders.RemoveAt(Index);
                Hotkeys.RemoveAt(Index);
            }

            private static void AddHostOption(
                Host Option,
                ref Rack<string> Options,
                ref Rack<IRenderable> Renders,
                ref Rack<char> Hotkeys
                )
            {
                Options.Add(Option.DisplayName());
                Renders.Add(new Renderable(Tile: "Mutations/gas_generation.bmp", ColorString: "&y", TileColor: "&y", DetailColor: 'K'));
                Hotkeys.Add(Hotkeys.GetNextHotKey());
            }

            public static async Task ManageHostsOptionButton()
            {
                using var hosts = ScopeDisposedList<Host>.GetFromPool();
                if (!Hosts.IsNullOrEmpty())
                    hosts.AddRange(Hosts);
                var options = new Rack<string>
                {
                    "new host",
                };
                int offset = options.Count;
                var renders = new Rack<IRenderable>
                {
                    new Renderable(Tile: "UI/sw_newchar.bmp", ColorString: "&W", TileColor: "&W", DetailColor: 'y'),
                };
                var hotkeys = new Rack<char>
                {
                    'n',
                };
                foreach (var host in hosts)
                    AddHostOption(host, ref options, ref renders, ref hotkeys);

                int choice;
                do
                {
                    choice = await Popup.PickOptionAsync(
                        Title: "{{yellow|Manage {{black|Osseous Ash}} Hosts}}",
                        Intro: $"Use the options below to manage the hosts to/from which you'd like to upload/download bones files.",
                        Options: options,
                        Hotkeys: hotkeys,
                        Icons: renders,
                        IntroIcon: new Renderable(
                            Tile: "Mutations/gas_generation.bmp",
                            ColorString: "&y",
                            TileColor: "&y",
                            DetailColor: 'K'),
                        DefaultSelected: 1,
                        AllowEscape: true);

                    if (choice == 0)
                    {
                        while (true)
                        {
                            string entered = await AskFullHostName(null);

                            string authToken = null;
                            if (entered != null)
                                authToken = await AskHostAuthToken(null);

                            var newHost = !entered.IsNullOrEmpty()
                                ? new Host(entered, authToken)
                                : null
                                ;

                            bool? confirmed = await ConfirmParsedHost(newHost);

                            if (!confirmed.HasValue)
                                break;

                            if (confirmed.GetValueOrDefault())
                            {
                                Config.WriteAddHost(newHost);
                                break;
                            }
                        }
                    }
                    else
                    if (choice > 0)
                    {

                    }
                }
                while (choice >= 0);
                

                if ((await Popup.NewPopupMessageAsync(
                        message: $"Opting in to \"{OSSEOUS_ASH_DOWNLOADS}\" will include bones files from the {OSSEOUS_ASH} when looking for bones files to load.",
                        buttons: PopupMessage.YesNoButton,
                        DefaultSelected: 1,
                        title: $"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_DOWNLOADS}?",
                        afterRender: new Renderable(
                            Tile: "Abilities/tile_supressive_fire.png",
                            ColorString: "&y",
                            TileColor: "&y",
                            DetailColor: 'R'))
                        ).command == "Yes")
                {
                    any = true;
                    XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshDownloads)}",
                        Value: Options.EnableOsseousAshDownloads = true);
                }

                if ((await Popup.NewPopupMessageAsync(
                    message: $"Opting in to \"{OSSEOUS_ASH_UPLOADS}\" will upload any bones files saved while the option is enabled to the {OSSEOUS_ASH}, " +
                        "and, if you choose one, will associate a handle with the uploaded bones.",
                    buttons: PopupMessage.YesNoButton,
                    DefaultSelected: 1,
                    title: $"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_UPLOADS}?",
                    afterRender: new FlippableRender(
                        Source: new Renderable(
                            Tile: "Abilities/tile_supressive_fire.png",
                            ColorString: "&y",
                            TileColor: "&y",
                            DetailColor: 'W'),
                        HFlip: false,
                        VFlip: true))
                    ).command == "Yes")
                {
                    any = true;

                    XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                        Value: Options.EnableOsseousAshUploads = true);
                    await Options.ManageOsseousAshHandle();
                }

                if (any)
                    return;

                await Popup.ShowAsync("You haven't been opted in.\n\n" +
                    "If you'd like to change your mind at any time, there are options available in the options menu.");
            }

            private static async Task<string> AskFullHostName(string PrependMessage)
            {
                var sB = Event.NewStringBuilder();
                sB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                    .AppendLine().AppendLine();
                if (!PrependMessage.IsNullOrEmpty())
                    sB.Append(PrependMessage)
                        .AppendLine().AppendLine();

                sB.Append($"Enter the full host name of the host you'd like to add, including the protocol and, if applicable, the port number.")
                    .AppendLine().AppendLine();
                sB.Append("Examples:")
                    .AppendLine().Append("https://osseousash.cloud/")
                    .AppendLine().Append("http://localhost:8000/")
                    .AppendLine().AppendLine();
                sB.Append("You'll be asked to confirm the parsed result.");

                return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: "", ReturnNullForEscape: true);
            }

            private static async Task<string> AskHostAuthToken(string PrependMessage)
            {
                var sB = Event.NewStringBuilder();
                sB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                    .AppendLine().AppendLine();
                if (!PrependMessage.IsNullOrEmpty())
                    sB.Append(PrependMessage)
                        .AppendLine().AppendLine();

                sB.Append($"If the new host requires authentication of some kind, enter the required key below.")
                    .AppendLine().AppendLine();
                sB.Append("You can change/update this later if necessary.");

                return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: null, ReturnNullForEscape: true);
            }

            private static async Task<bool?> ConfirmParsedHost(Host NewHost)
            {
                if (NewHost == null)
                {
                    await ShowCancelledAddHost();
                    return true;
                }

                var confirmResult = await Popup.ShowYesNoCancelAsync(
                        Message: Event.FinalizeString(
                            SB: Event.NewStringBuilder("The host you've entered was parsed in the following way:")
                                .AppendLine().AppendPair(nameof(NewHost.Name), NewHost.Name)
                                .AppendLine().AppendPair(nameof(NewHost.Port), (NewHost.Port)?.ToString() ?? "")
                                .AppendLine().AppendPair(nameof(NewHost.Encrypted), NewHost.Encrypted)
                                .AppendLine().AppendPair(nameof(NewHost.AuthToken), NewHost.AuthToken)
                                .AppendLine().AppendLine()
                                .AppendLine().Append(NewHost.GetHostNameWithProtocol())
                                .AppendLine().AppendLine()
                                .Append("Is this correct?"))
                        );

                switch (confirmResult)
                {
                    case DialogResult.Yes:
                        return true;
                    case DialogResult.No:
                        return false;
                    case DialogResult.Cancel:
                    default:
                        await ShowCancelledAddHost();
                        return null;
                }
            }

            private static async Task ShowCancelledAddHost()
            {
                await Popup.ShowAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("Addition of new host cancelled."))
                    );
            }

            #endregion

            public override string ToString()
            {
                string completeHost = GetHostNameWithProtocol(TrailingSlash: false);

                if (completeHost.EndsWith("/"))
                    completeHost = completeHost[..^1];

                if (Port.HasValue)
                    completeHost += $":{Port}";

                return $"{completeHost}/";
            }

            public override bool Equals(object obj)
                => obj is Host hostObj
                ? Equals(hostObj)
                : base.Equals(obj)
                ;

            public override int GetHashCode()
                => (Name?.GetHashCode() ?? 0)
                ^ (Port?.GetHashCode() ?? 0)
                ^ Encrypted.GetHashCode();

            public bool Equals(Host Other)
                => Other != null
                && Name == Other.Name
                && Port == Other.Port
                && Encrypted == Other.Encrypted
                ;

            public void Dispose()
            {
                Name = null;
                Port = null;
                Encrypted = false;
            }

            public static bool operator ==(Host X, Host Y)
            {
                if (X is null
                    || Y is null)
                    return (X is null) == (Y is null);

                return X.Equals(Y);
            }

            public static bool operator !=(Host X, Host Y)
                => !(X == Y)
                ;

            public static implicit operator string(Host Host)
                => Host.ToString()
                ;
        }
    }
}
