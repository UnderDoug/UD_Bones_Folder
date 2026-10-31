using System;
using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptIn
        , ItemReferenceLoopHandling = ReferenceLoopHandling.Ignore
        )]
    [Serializable]
    public class SuperUser
    {
        /// <summary>
        /// Name of the host, excluding protocol and any port number.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public string Name;

        /// <summary>
        /// Optional port number for host.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int? Port;

        /// <summary>
        /// Determines http or https.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public bool Encrypted;

        /// <summary>
        /// The number of milliseconds to wait for a response.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int TimeoutMS;

        /// <summary>
        /// The maximum number number of times an upload should be attempted before being considered to have failed.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int MaxUploadAttempts;

        /// <summary>
        /// The API version this host expects to connect to.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int Version;

        /// <summary>
        /// Indicates whether or not this host should be utilised.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public bool Enabled;

        /// <summary>
        /// An object representation of the User as it appears on the server.
        /// </summary>
        [DoVomit]
        [JsonConverter(typeof(User.JsonConverter))]
        [JsonProperty]
        public User User;

        public SuperUser()
            : base()
        {
            Name = "test";
            Port = 8000;
            Encrypted = false;
            TimeoutMS = 2200;
            MaxUploadAttempts = 5;
            Version = 1;
            Enabled = true;
        }

        public SuperUser(User User)
            : this()
        {
            this.User = User;
        }
    }
}
