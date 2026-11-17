using System;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
    public class HostJSON : IDisposable, IVomitable<HostJSON>
    {
        [DoVomit]
        [JsonProperty]
        public string Name;

        [DoVomit]
        [JsonProperty]
        public int? Port;

        [DoVomit]
        [JsonProperty]
        public bool Encrypted;

        [DoVomit]
        [JsonProperty]
        public int TimeoutMS;

        [DoVomit]
        [JsonProperty]
        public int MaxUploadAttempts;

        [DoVomit]
        [JsonProperty]
        public int Version;

        [DoVomit]
        [JsonProperty]
        public bool Enabled;

        [DoVomit]
        [JsonProperty]
        public string JwtToken;

        [DoVomit]
        [JsonConverter(typeof(User.JsonConverter))]
        [JsonProperty]
        public User User;

        public HostJSON()
            : base()
        { }

        public HostJSON(User User)
            : this()
        {
            this.User = User;
        }

        public HostJSON(Host Source)
            : this()
        {
            Name = Source.Name;
            Port = Source.Port;
            Encrypted = Source.Encrypted;
            TimeoutMS = Source.TimeoutMS;
            MaxUploadAttempts = Source.MaxUploadAttempts;
            Version = Source.Version;
            Enabled = Source.GetEnabledValueForMenu();
            JwtToken = Source.JwtToken;
            User = Source.User;
        }

        public static void CopyTo(
            HostJSON Source,
            ref Host Destination
            )
        {
            if (Source == null)
                return;

            if (Destination == null)
            {
                Destination = Host.GetFrom(Source);
                return;
            }

            Destination.Name = Source.Name;
            Destination.Port = Source.Port;
            Destination.Encrypted = Source.Encrypted;
            Destination.TimeoutMS = Source.TimeoutMS;
            Destination.Enabled = Source.Enabled;
            Destination.JwtToken = Source.JwtToken;
            Destination.User = Source.User;
        }

        public void CopyTo(ref Host Destination)
            => CopyTo(this, ref Destination)
            ;

        public static Host ToHost(HostJSON HostJSON)
            => Host.GetFrom(HostJSON)
            ;

        public Host ToHost()
            => ToHost(this)
            ;

        public static HostJSON GetFrom(Host Host)
            => new(Host)
            ;

        public void Dispose()
        {
            Name = default;
            Port = default;
            Encrypted = default;
            TimeoutMS = default;
            MaxUploadAttempts = default;
            Version = default;
            Enabled = default;
            JwtToken = default;
            User = default;
        }

        public virtual HostJSON Vomit(
            bool ForceName = false,
            Indent Indent = null,
            [CallerMemberName] string CallingMethod = "",
            params object[] Parameters
            )
            => ((IVomitable<HostJSON>)this)?.Vomit(
                ForceName: ForceName,
                Indent: Indent,
                CallingMethod: CallingMethod,
                Parameters: Parameters)
            ;
    }
}
