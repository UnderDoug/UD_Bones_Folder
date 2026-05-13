using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Platform.IO;

using UnityEngine;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using GameObject = XRL.World.GameObject;

namespace UD_Bones_Folder.Mod
{
    public static partial class OsseousAsh
    {
        [JsonObject(MemberSerialization.OptOut)]
        [Serializable]
        public class User : IDisposable
        {
            public enum StatusTypes
            {
                None,
                Pending,
                Active,
                Disabled,
            }

            public enum AccessLevels
            {
                None,
                Down,
                Up,
                Manage,
            }

            public Guid ID;
            public string Handle;

            [JsonConverter(typeof(StringEnumConverter))]
            public StatusTypes Status;

            [JsonConverter(typeof(StringEnumConverter))]
            public AccessLevels Access;

            public User()
            {
            }

            public User(
                Guid ID,
                string Handle,
                StatusTypes Status,
                AccessLevels Access
            ) : this()
            {
                this.ID = ID;
                this.Handle = Handle;
                this.Status = Status;
                this.Access = Access;
            }

            public User(
                Configuration Config
            ) : this(Config.ID, Config.Handle, StatusTypes.Pending, AccessLevels.None)
            { }

            public void Dispose()
            {
                this.ID = default;
                this.Handle = null;
                this.Status = StatusTypes.Pending;
                this.Access = AccessLevels.None;
            }
        }
    }
}
