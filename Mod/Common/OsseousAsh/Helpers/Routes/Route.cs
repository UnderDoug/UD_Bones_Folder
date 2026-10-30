using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public bool Queryable;

        public bool HasParams => Value?.Contains(":") is true;

        public bool IsUnsupported => Method == Host.WebMethods.Unsupported;

        public Route()
            : base()
        { }

        private Route(string Value)
            : this()
        {
            this.Value = Value;
        }

        public Route(
            Host.WebMethods Method,
            string Value,
            string ContentType = "json",
            bool Queryable = false
            )
            : this(Value)
        {
            if (ContentTypes.ContainsKey(ContentType))
                this.ContentType = ContentTypes[ContentType];
            else
                this.ContentType = ContentType;

            this.Method = Method;

            this.Queryable = Queryable;
        }

        public Route(Route Source)
            : this(Source.Method, Source.Value, Source.ContentType, Source.Queryable)
        { }

        public static Route NotSupported(string Message = "Not supported in this version")
            => new Route(Message)
            ;

        public Route Clone()
            => new(this)
            ;

        public string ParseValue(RouteParam[] RouteParams, RouteQuery RouteQuery)
        {
            if (IsUnsupported)
                throw new NotSupportedException(Value);

            if (ContentType.IsNullOrEmpty())
                throw new InvalidOperationException($"{nameof(ContentType)} cannot be null or empty");

            string route = Value;
            foreach ((string key, string value) in RouteParams.IteratorSafe())
                route = route.Replace($":{key}", value);

            if (route.Contains(":"))
                throw new InvalidOperationException($"Final route must have all params replaced: (@ {route.IndexOf(':')}) \"{route}\"");

            return route + RouteQuery?.ToString();
        }

        public string ParseValue(params RouteParam[] RouteParams)
            => ParseValue(RouteParams, null)
            ;

        public string ParseValue(params RouteQueryPair[] RouteQuery)
            => ParseValue(null, new RouteQuery(RouteQuery))
            ;

        public bool SameAs(Route Other)
            => Other != null
            && Method == Other.Method
            && Value == Other.Value
            ;

        public override string ToString()
            => $"{Method}, {Value}, {ContentType}"
            ;

        private void Error(string Message, Exception X = null)
        {
            Utils.Error(Message, X);
        }

        private void Warn(string Message, Exception X = null)
        {
            Utils.Warn(Message, X);
        }

        public void ErrorFailed(
            string Doing,
            string With,
            string URI,
            Exception X = null,
            object Context = null,
            [CallerMemberName] string What = ""
            )
            => Error($"{What} failed {Doing} {Method} {With} for {URI}{(Context != null ? $" {Context}" : null)}", X)
            ;

        public void WarnFailed(
            string Doing,
            string With,
            string URI,
            Exception X = null,
            object Context = null,
            [CallerMemberName] string What = ""
            )
            => Warn($"{What} failed {Doing} {Method} {With} for {URI}{(Context != null ? $" {Context}" : null)}", X)
            ;
    }
}
