using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    /*[JsonObject(MemberSerialization.OptOut
        , MissingMemberHandling = MissingMemberHandling.Ignore
        , ItemNullValueHandling = NullValueHandling.Ignore
        )]*/
    [Serializable]
    public class User : IDisposable
    {
        [Serializable]
        public enum StatusTypes
        {
            Pending,
            Active,
            Disabled,
        }

        [Serializable]
        public enum AccessLevels
        {
            None,
            Down,
            Up,
            Manage,
        }

        [JsonProperty]
        public Guid ID;
        [JsonProperty]
        public string Handle;

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(nameof(Status))]
        private StatusTypes? _Status;

        [JsonIgnore]
        public StatusTypes Status
        {
            get => _Status ?? StatusTypes.Active;
            set => _Status = value;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(nameof(Access))]
        private AccessLevels? _Access;

        [JsonIgnore]
        public AccessLevels Access
        {
            get => _Access ?? AccessLevels.Up;
            set => _Access = value;
        }

        [JsonProperty]
        public string Password;

        public User()
            : base()
        { }

        public User(
            Guid ID,
            string Handle,
            StatusTypes? Status = null,
            AccessLevels? Access = null,
            string Password = null
        ) : this()
        {
            this.ID = ID;
            this.Handle = Handle;
            _Status = Status;// ?? StatusTypes.Active;
            _Access = Access;// ?? AccessLevels.Up;
            this.Password = Password;
        }

        public User(
            Configuration Config,
            string Password = null
        ) : this(Config.ID, Config.Handle, null, null, Password)
        { }

        public User CopyTo(ref User Other, bool SanitizePassword = false)
        {
            Other ??= new();

            if (ID != Guid.Empty)
                Other.ID =  ID;

            Other.Handle = Handle;
            Other._Status = _Status;
            Other._Access = _Access;
            if (!SanitizePassword)
                Other.Password = Password;
            else
            {
                if (Password == null)
                    Other.Password = "";
                else
                    Other.Password = null;
            }
            return Other;
        }

        public User Clone()
        {
            var newUser = new User();
            return CopyTo(ref newUser);
        }

        public void Dispose()
        {
            ID = default;
            Handle = null;
            _Status = default;// null;
            _Access = default;// null;
            Password = null;
        }
    }
}
