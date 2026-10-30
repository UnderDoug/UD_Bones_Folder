using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class RouteQuery : CoalescibleSet<RouteQueryPair>
    {
        public RouteQuery()
            : base()
        { }

        public RouteQuery(params RouteQueryPair[] RouteQuery)
            : base(RouteQuery)
        { }

        public RouteQuery(object Source)
            : base()
        {
            if (Source != null)
            {
                foreach (var field in Source.GetType().GetCachedFields())
                {
                    if (!field.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).IsNullOrEmpty())
                        continue;

                    Add(new(field.Name, field.GetValue(Source)));
                }
                foreach (var property in Source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).IsNullOrEmpty())
                        continue;

                    Add(new(property.Name, property.GetValue(Source)));
                }
            }
        }

        public override string ToString()
            => this?.Aggregate((string)null, Utils.RouteQueryPairAggregator)
            ;
    }
}
