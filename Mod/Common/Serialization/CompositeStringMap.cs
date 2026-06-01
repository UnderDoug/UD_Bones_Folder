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
            Writer.WriteOptimized(Amount);
            for (int i = 0; i < Length; i++)
            {
                if (Slots[i].Key != null)
                {
                    Writer.WriteOptimized(Slots[i].Key);
                    Writer.WriteComposite(Slots[i].Value);
                }
            }
        }

        public override void Read(SerializationReader Reader)
        {
            int amount = Reader.ReadOptimizedInt32();
            Resize(amount);
            for (int i = 0; i < amount; i++)
                InsertInternal(Reader.ReadOptimizedString(), Reader.ReadComposite<T>());
        }
    }
}
