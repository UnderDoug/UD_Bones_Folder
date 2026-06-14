using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization.Delegates
{
    [Serializable]
    public class SerializeEach<T> : ISerializeEach<T>, IComposite
    {
        public string NameWithGenerics => GetType().ToStringWithGenerics();

        public SerializeEach()
        { }

        public virtual void WriteEach(SerializationWriter Writer, T Element)
        {
            Writer.WriteObject(Element);
        }

        public virtual T ReadEach(SerializationReader Reader)
        {
            return (T)Reader.ReadObject();
        }
    }

    public static class SerializeEachExtensions
    {
        [Serializable]
        private class EachWriter<T> : IWriteEach<T>
        {
            private readonly WriteEach<T> WriteEachDelegate;

            public EachWriter(WriteEach<T> WriteEach)
                => WriteEachDelegate = WriteEach
                ;

            public void WriteEach(SerializationWriter Writer, T Element)
                => WriteEachDelegate?.Invoke(Writer, Element)
                ;
        }

        [Serializable]
        private class EachReader<T> : IReadEach<T>
        {
            private readonly ReadEach<T> ReadEachDelegate;

            public EachReader(ReadEach<T> ReadEach)
                => ReadEachDelegate = ReadEach
                ;

            public T ReadEach(SerializationReader Reader)
                => ReadEachDelegate != null
                ? ReadEachDelegate.Invoke(Reader)
                : default
                ;
        }

        public static IWriteEach<T> ToEachWriter<T>(this WriteEach<T> WriteEach)
            => new EachWriter<T>(WriteEach)
            ;

        public static IReadEach<T> ToEachReader<T>(this ReadEach<T> ReadEach)
            => new EachReader<T>(ReadEach)
            ;
    }
}
