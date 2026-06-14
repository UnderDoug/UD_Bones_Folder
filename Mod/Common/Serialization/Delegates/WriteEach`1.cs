using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization.Delegates
{
    /// <summary>
    /// Represents the method that writes a single element of a serializeable collection to the <see cref="SerializationWriter"/>.
    /// </summary>
    /// <typeparam name="T">The type of element to serialize.</typeparam>
    /// <param name="Writer">The serialization writer.</param>
    /// <param name="Element">The element to be serialized</param>
    public delegate void WriteEach<T>(SerializationWriter Writer, T Element);
}
