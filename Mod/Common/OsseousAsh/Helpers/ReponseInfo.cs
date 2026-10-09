using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public struct ResponseInfo
    {
        public Host Host;
        public HttpStatusCode StatusCode;
        public bool Success;
        public string Message;
    }
}
