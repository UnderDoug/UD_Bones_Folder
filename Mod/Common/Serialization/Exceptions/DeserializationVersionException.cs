using System;

namespace UD_Bones_Folder.Mod
{
    public class DeserializationVersionException : DeserializationException
    {
        public DeserializationVersionException(string Message)
            : base(Message) { }

        public DeserializationVersionException(string Message, Exception InnerException)
            : base(Message, InnerException) { }
    }
}
