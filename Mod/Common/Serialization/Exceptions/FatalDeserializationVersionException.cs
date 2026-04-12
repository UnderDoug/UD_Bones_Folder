using System;

namespace UD_Bones_Folder.Mod
{
    public class FatalDeserializationVersionException : DeserializationVersionException
    {
        public FatalDeserializationVersionException(string Message)
            : base(Message) { }

        public FatalDeserializationVersionException(string Message, Exception InnerException)
            : base(Message, InnerException) { }
    }
}
