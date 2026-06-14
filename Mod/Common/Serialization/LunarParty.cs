using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod
{
    public class LunarParty : IDisposable
    {
        public GameObject LunarRegent;

        public CoalescibleSet<GameObject> LunarCourtiers;

        public LunarParty()
        { }

        public LunarParty(LunarPartyIDs CachedLunarPartyIDs)
            : this()
        {
            if (!CachedLunarPartyIDs.TryPullCachedLunarParty(out LunarRegent, out LunarCourtiers))
                throw new ArgumentException("LunarRegent was not found in cache", nameof(CachedLunarPartyIDs));
        }

        public static LunarParty UncacheLunarParty(LunarPartyIDs CachedLunarPartyIDs)
            => CachedLunarPartyIDs.TryPullCachedLunarParty(out GameObject lunarRegent, out CoalescibleSet<GameObject> lunarCourtiers)
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
                        seed: new StringSet(),
                        func: delegate (StringSet a, GameObject n)
                        {
                            a.Add(The.ZoneManager.CacheObject(n, cacheTwiceOk: true, replaceIfAlreadyCached: true));
                            return a;
                        })
                    : null,
            }
            : null
            ;

        public LunarParty SetLunarRegent(GameObject LunarRegent, bool WarnAlreadyAssigned = true)
        {
            if (this.LunarRegent != null
                && this.LunarRegent != LunarRegent
                && WarnAlreadyAssigned)
            {
                Utils.Warn($"Probable error loading {nameof(this.LunarRegent)} {LunarRegent.DebugName}, " +
                    $"{nameof(this.LunarRegent)} already assigned {this.LunarRegent.DebugName}");
            }
            this.LunarRegent = LunarRegent;
            return this;
        }

        public LunarPartyIDs CacheLunarCourtiers()
            => !LunarCourtiers.IsNullOrEmpty()
            ? new LunarPartyIDs
            {
                LunarCourtiers = LunarCourtiers.Aggregate(
                    seed: new StringSet(),
                    func: delegate (StringSet a, GameObject n)
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

        public void Obliterate(
            string Reason = null,
            bool Silent = false,
            string ThirdPersonReason = null
            )
        {
            LunarRegent?.Obliterate(Reason, Silent, ThirdPersonReason);
            foreach (var lunarCourtier in LunarCourtiers.IteratorSafe())
                lunarCourtier?.Obliterate(Reason, Silent, ThirdPersonReason);
            LunarCourtiers?.Clear();
            Dispose();
        }

        public void Dispose()
        {
            LunarRegent = null;
            LunarCourtiers = null;
        }
    }
}
