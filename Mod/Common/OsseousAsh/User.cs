using System;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptOut
        , MissingMemberHandling = MissingMemberHandling.Ignore
        , ItemNullValueHandling = NullValueHandling.Ignore
        , ItemReferenceLoopHandling = ReferenceLoopHandling.Ignore
        )]
    [JsonConverter(typeof(User.JsonConverter))]
    [Serializable]
    public class User : IDisposable, IVomitable<User>
    {
        #region Debug Registration


        #endregion

        public class JsonConverter : JsonConverter<User>
        {
            #region Debug Registration
            [RegistersMethodsForDebug]
            public static void doDebugRegistry(DebugMethodRegistry Registry)
                => Registry.RegisterEach(
                    Type: typeof(JsonConverter),
                    MethodNameValues: new()
                    {
                    { nameof(ReadJson), false },
                    { nameof(WriteJson), false },
                    });
            #endregion

            [ThreadStatic]
            private static bool _CannotWrite;

            private bool CannotWrite
            {
                get => _CannotWrite;
                set => _CannotWrite = value;
            }

            public override bool CanWrite => !CannotWrite;

            public override User ReadJson(
                JsonReader Reader,
                Type ObjectType,
                User ExistingValue,
                bool HasExistingValue,
                JsonSerializer Serializer
                )
            {
                using var indent = new Indent();
                Debug.LogCaller(
                    Indent: indent[1],
                    ArgPairs: new Debug.ArgPair[]
                    {
                        Debug.Arg(nameof(ObjectType), ObjectType),
                        Debug.Arg(nameof(HasExistingValue), HasExistingValue),
                    });

                if (Reader.TokenType == JsonToken.StartObject)
                {
                    Serializer.Populate(Reader, ExistingValue ??= new());
                    return ExistingValue;
                }
                return null;
            }

            public override void WriteJson(
                JsonWriter Writer,
                User Value,
                JsonSerializer Serializer
                )
            {
                using var indent = new Indent();
                Debug.LogCaller(
                    Indent: indent[1],
                    ArgPairs: new Debug.ArgPair[]
                    {
                        Debug.Arg(nameof(Value), $"{(Value != null ? "not " : null)}null"),
                    });

                using (new Utils.PushValue<bool>(true, () => CannotWrite, v => CannotWrite = v))
                {
                    Serializer.Serialize(Writer, Value);
                    //JObject.FromObject(Value, Serializer).WriteTo(Writer);
                }
            }
        }

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

        public User CopyTo(
            ref User Other,
            bool SanitizePassword = false
            )
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

        public User Vomit(
            bool ForceName = false,
            Indent Indent = null,
            [CallerMemberName] string CallingMethod = "",
            params object[] Parameters
            )
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
