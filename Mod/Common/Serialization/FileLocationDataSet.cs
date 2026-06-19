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

        public FileLocationDataSet()
            : base(FileLocationData.DefaultEqualityComparer, FileLocationData.DefaultCoalescer)
        { }


    }

    public static class FileLocationDataSetExtensions
    {
    }
}
