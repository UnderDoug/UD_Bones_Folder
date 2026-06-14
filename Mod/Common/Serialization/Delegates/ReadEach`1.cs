using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization.Delegates
{
    /// <summary>
    /// Represents the method that reads a single element of a serialized collection from the <see cref="SerializationReader"/>.
    /// </summary>
    /// <typeparam name="T">The type of element to deserialize.</typeparam>
    /// <param name="Reader">The serialization reader.</param>
    /// <returns>The deserialized value.</returns>
    public delegate T ReadEach<T>(SerializationReader Reader);
}
