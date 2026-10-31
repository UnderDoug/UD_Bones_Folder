using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptOut
        , MissingMemberHandling = MissingMemberHandling.Ignore
        , ItemNullValueHandling = NullValueHandling.Ignore
        , ItemReferenceLoopHandling = ReferenceLoopHandling.Ignore
        )]
    [Serializable]
    public class User : IDisposable, IVomitable<User>
    {
        #region Debug Registration


        #endregion

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

        public Guid ID;
        public string Handle;

        [DoVomit(nameof(Status))]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(nameof(Status))]
        private StatusTypes? _Status;

        [NoVomit]
        [JsonIgnore]
        public StatusTypes Status
        {
            get => _Status ?? StatusTypes.Active;
            set => _Status = value;
        }

        [DoVomit(nameof(Access))]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(nameof(Access))]
        private AccessLevels? _Access;

        [NoVomit]
        [JsonIgnore]
        public AccessLevels Access
        {
            get => _Access ?? AccessLevels.Up;
            set => _Access = value;
        }

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

        public User Vomit(bool ForceName = false, Indent Indent = null, [CallerMemberName] string CallingMethod = "", params object[] Parameters)
            => ((IVomitable<User>)this)?.Vomit(
                ForceName: ForceName,
                Indent: Indent,
                CallingMethod: CallingMethod,
                Parameters: Parameters)
            ;

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
