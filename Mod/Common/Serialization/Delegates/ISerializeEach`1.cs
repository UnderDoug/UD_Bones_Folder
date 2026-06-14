using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization.Delegates
{
    public interface ISerializeEach<T> : IWriteEach<T>, IReadEach<T>
    { }

    public interface IWriteEach<T>
    {
        void WriteEach(SerializationWriter Writer, T Element);

        WriteEach<T> ToWriteEach()
            => WriteEach
            ;
    }

    public interface IReadEach<T>
    {
        T ReadEach(SerializationReader Reader);

        ReadEach<T> ToReadEach()
            => ReadEach
            ;
    }
}
