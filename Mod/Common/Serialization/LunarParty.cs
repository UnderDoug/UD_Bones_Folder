using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod
{
    public class LunarParty : IDisposable
    {
        public GameObject LunarRegent;

        public HashSet<GameObject> LunarCourtiers;

        public LunarParty()
        { }

        public LunarParty(LunarPartyIDs CachedLunarPartyIDs)
            : this()
        {
            if (!CachedLunarPartyIDs.TryPullCachedLunarParty(out LunarRegent, out LunarCourtiers))
                throw new ArgumentException("LunarRegent was not found in cache", nameof(CachedLunarPartyIDs));
        }

        public static LunarParty UncacheLunarParty(LunarPartyIDs CachedLunarPartyIDs)
            => CachedLunarPartyIDs.TryPullCachedLunarParty(out GameObject lunarRegent, out HashSet<GameObject> lunarCourtiers)
            ? new LunarParty
            {
                LunarRegent = lunarRegent,
                LunarCourtiers = lunarCourtiers,
            }
            : null
            ;

        public LunarPartyIDs CacheLunarParty()
            => LunarRegent != null
            ? new LunarPartyIDs
            {
                LunarRegent = The.ZoneManager.CacheObject(LunarRegent, cacheTwiceOk: true, replaceIfAlreadyCached: true),
                LunarCourtiers = !LunarCourtiers.IsNullOrEmpty()
                    ? LunarCourtiers.Aggregate(
                        seed: new HashSet<string>(),
                        func: delegate (HashSet<string> a, GameObject n)
                        {
                            a.Add(The.ZoneManager.CacheObject(n, cacheTwiceOk: true, replaceIfAlreadyCached: true));
                            return a;
                        })
                    : null,
            }
            : null
            ;

        public LunarPartyIDs CacheLunarCourtiers()
            => !LunarCourtiers.IsNullOrEmpty()
            ? new LunarPartyIDs
            {
                LunarCourtiers = LunarCourtiers.Aggregate(
                    seed: new HashSet<string>(),
                    func: delegate (HashSet<string> a, GameObject n)
                    {
                        a.Add(The.ZoneManager.CacheObject(n, cacheTwiceOk: true, replaceIfAlreadyCached: true));
                        return a;
                    }),
            }
            : null
            ;

        public LunarPartyIDs CacheLunarPartyThenDispose()
        {
            var cachedLunarParty = CacheLunarParty();
            Dispose();
            return cachedLunarParty;
        }

        public void Dispose()
        {
            LunarRegent = null;
            LunarCourtiers = null;
        }
    }
}
