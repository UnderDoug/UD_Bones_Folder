using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class RouteQueryPair : IEquatable<RouteQueryPair>, ICoalescible<RouteQueryPair>
    {
        public string Key;
        public string Value;

        public RouteQueryPair(string Key, object Value)
        {
            this.Key = Key;
            this.Value = Value?.ToString();
        }

        public void Deconstruct(out string Key, out string Value)
        {
            Key = this.Key;
            Value = this.Value;
        }

        public bool Equals(RouteQueryPair Other)
            => Key == Other?.Key
            ;

        public RouteQueryPair Coalesce(RouteQueryPair Other)
            => Key != Other?.Key
                || Value == Other?.Value
            ? this
            : Other
            ;

        public override string ToString()
            => !Key.IsNullOrEmpty()
            ? $"{Key}={Value?.ToString() ?? "null"}"
            : null
            ;
    }
}
