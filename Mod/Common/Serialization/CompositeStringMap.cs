using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using XRL.Collections;
using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization
{
    [Serializable]
    public class CompositeStringMap<T>
        : StringMap<T>
        where T
        : IComposite
        , new()
    {
        public CompositeStringMap()
            : base()
        { }

        public CompositeStringMap(int Capacity = 0)
            : base(Capacity)
        { }

        public CompositeStringMap([DisallowNull] IDictionary<string, T> Dictionary)
            : base(Dictionary.Count)
        { }

        public CompositeStringMap([DisallowNull] StringMap<T> Source)
            : base(Source)
        { }

        public override void Write(SerializationWriter Writer)
        {
            Writer.WriteComposite(this as IDictionary<string, T>);
        }

        public override void Read(SerializationReader Reader)
        {
            var dictionary = Reader.ReadCompositeValues<string, T>();
            if (dictionary != null)
            {
                Resize(dictionary.Count);
                foreach ((string key, T value) in dictionary)
                    InsertInternal(key, value);
            }
        }
    }
}
