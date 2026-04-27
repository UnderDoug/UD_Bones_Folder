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

            /// <summary>
            /// Indicates whether or not this host should be utilised.
            /// </summary>
            [JsonProperty]
            public bool Enabled;

            public Host()
            {
                Enabled = true;
            }

            public Host(string Name, int? Port = null, bool Encrypted = false, string AuthToken = null, bool Enabled = true)
                : this()
            {
                this.Name = Name;
                this.Port = Port;
                this.Encrypted = Encrypted;
                this.AuthToken = AuthToken;
                this.AuthToken = AuthToken;
                this.Enabled = Enabled;
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

            public static Host Clone(Host Host)
                => Host != null
                ? new Host
                    {
                        Name = Host.Name,
                        Port = Host.Port,
                        Encrypted = Host.Encrypted,
                        AuthToken = Host.AuthToken,
                        Enabled = Host.Enabled,
                    }
                : throw new ArgumentNullException(nameof(Host))
                ;

            public Host Clone()
                => Clone(this)
                ;

            public static void CopyTo(Host Source, ref Host Destination)
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
                Destination.AuthToken = Source.AuthToken;
                Destination.Enabled = Source.Enabled;
            }

            public void CopyTo(ref Host Destination)
                => CopyTo(this, ref Destination)
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

                    httpReq.Headers.Add(HttpRequestHeader.Authorization, Token.ToString());
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
            
            public override string ToString()
                => $"{GetHostNameWithProtocol(TrailingSlash: true)}"
                ;

            public override bool Equals(object obj)
                => obj is Host hostObj
                ? Equals(hostObj)
                : base.Equals(obj)
                ;

            public override int GetHashCode()
                => (Name?.GetHashCode() ?? 0)
                ^ (Port?.GetHashCode() ?? 0)
                ^ Encrypted.GetHashCode()
                ^ (AuthToken?.GetHashCode() ?? 0)
                ^ Enabled.GetHashCode()
                ;

            public bool Equals(Host Other)
                => Other != null
                && Name == Other.Name
                && Port == Other.Port
                && Encrypted == Other.Encrypted
                && Enabled == Other.Enabled
                ;

            public static Task<bool?> FlipEncryptedAsync(Host Host)
                => Task.Run<bool?>(() => (Host.Encrypted = !Host.Encrypted) || true)
                ;

            public static Task<bool?> FlipEnabledAsync(Host Host)
                => Task.Run<bool?>(() => (Host.Enabled = !Host.Enabled) || true)
                ;

            public void Dispose()
            {
                Name = null;
                Port = null;
                Encrypted = false;
                AuthToken = null;
                Enabled = false;
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
