using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Platform.IO;

using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Collections;
using XRL.Core;
using XRL.World;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class FileLocationDataSet : CompositeSet<FileLocationData>, IDisposable
    {
        public bool IncludesOnline => this.Any(f => f.Type.IsOnline());

        public bool IsOnlyOnline => this.All(f => f.Type.IsOnline());

        public bool IsCrematable => this.Any(f => f.Type.IsFile() && f.Exists());

        public FileLocationDataSet()
            : base(FileLocationData.DefaultEqualityComparer, FileLocationData.DefaultCoalescer)
        { }

        public bool AnyHostsMissing(IEnumerable<OsseousAsh.Host> Hosts)
            => Hosts?.Any(h => !this.Any(f => h.SameAs(f.Host))) is true
            ;
    }

    public static class FileLocationDataSetExtensions
    {
    }
}
