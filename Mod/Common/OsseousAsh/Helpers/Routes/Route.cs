using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

using XRL.Collections;
using XRL.World;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class Route
    {
        public static StringMap<string> ContentTypes = new()
        {
            { "json", "application/json" },
            { "octet-stream", "application/octet-stream" },
            { "gzip", "application/octet-stream" },
            { "gz", "application/octet-stream" },
            { "bytearray", "application/octet-stream" },
            { "byte[]", "application/octet-stream" },
            { "bytes", "application/octet-stream" },
        };

        public Host.WebMethods Method;
        public string Value;
        public string ContentType;

        public Route()
            : base()
        { }

        private Route(string Value)
            : this()
        {
            this.Value = Value;
        }

        public Route(Host.WebMethods Method, string Value, string ContentType = "json")
            : this(Value)
        {
            if (ContentTypes.ContainsKey(ContentType))
                this.ContentType = ContentTypes[ContentType];
            else
                this.ContentType = ContentType;

            this.Method = Method;
        }

        public Route(Route Source)
            : this(Source.Method, Source.Value, Source.ContentType)
        { }

        public static Route NotSupported(string Message = "Not supported in this version")
            => new Route(Message)
            ;

        public Route Clone()
            => new(this)
            ;

        public string ParseValue(params RouteParam[] Parameters)
        {
            if (Method == Host.WebMethods.Unsupported)
                throw new NotSupportedException(Value);

            string route = Value;
            foreach ((string key, string value) in Parameters.IteratorSafe())
                route = route.Replace($":{key}", value);

            if (route.Contains(":"))
                throw new InvalidOperationException($"Final route must have all params replaced: (@ {route.IndexOf(':')}) \"{route}\"");

            return route;
        }

        public bool SameAs(Route Other)
            => Other != null
            && Method == Other.Method
            && Value == Other.Value
            ;

        public override string ToString()
            => $"{Method}, {Value}, {ContentType}"
            ;
    }
}
