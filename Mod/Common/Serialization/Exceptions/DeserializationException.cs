using System;

namespace UD_Bones_Folder.Mod
{
    public class DeserializationException : Exception
    {
        public DeserializationException(string Message)
            : base(Message) { }

        public DeserializationException(string Message, Exception InnerException)
            : base(Message, InnerException) { }
    }
}
