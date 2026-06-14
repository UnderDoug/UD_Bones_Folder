using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class LunarPartyIDs : IComposite, IDisposable
    {
        public string LunarRegent;

        [NonSerialized]
        public StringSet LunarCourtiers;

        public LunarPartyIDs()
        { }

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteOptimized(LunarCourtiers == null ? -1 : LunarCourtiers.Count);
            foreach (var lunarCourtier in LunarCourtiers.IteratorSafe())
                Writer.WriteOptimized(lunarCourtier);
        }

        public virtual void Read(SerializationReader Reader)
        {
            int count = Reader.ReadOptimizedInt32();
            if (count >= 0)
            {
                LunarCourtiers = new();
                for (int i = 0; i < count; i++)
                    LunarCourtiers.Add(Reader.ReadOptimizedString());
            }
        }

        public bool IsCached(bool Strict = false)
        {
            if (LunarRegent.IsNullOrEmpty())
                return false;

            if (!The.ZoneManager.CachedObjects.ContainsKey(LunarRegent))
                return false;

            if (!Strict)
                return true;

            if (!LunarCourtiers.IsNullOrEmpty())
                foreach (var lunarCourtierID in LunarCourtiers)
                    if (!The.ZoneManager.CachedObjects.ContainsKey(lunarCourtierID))
                        return false;

            return true;
        }

        public bool TryPullCachedLunarParty(out GameObject LunarRegent, out CoalescibleSet<GameObject> LunarCourtiers)
        {
            LunarRegent = null;
            LunarCourtiers = null;
            if (this.LunarRegent.IsNullOrEmpty())
                return false;

            if (The.ZoneManager.PullCachedObject(this.LunarRegent, DeepCopy: false) is not GameObject cachedLunarRegent)
                return false;

            LunarRegent = cachedLunarRegent;

            if (!this.LunarCourtiers.IsNullOrEmpty())
            {
                foreach (var lunarCourtierID in this.LunarCourtiers)
                {
                    if (The.ZoneManager.PullCachedObject(lunarCourtierID, DeepCopy: false) is GameObject cachedLunarCourtier)
                    {
                        LunarCourtiers ??= new CoalescibleSet<GameObject>();
                        LunarCourtiers.Add(cachedLunarCourtier);
                    }
                }
            }
            return true;
        }

        public bool TryPullCachedLunarCourtiers(out CoalescibleSet<GameObject> LunarCourtiers)
        {
            LunarCourtiers = null;
            if (!this.LunarCourtiers.IsNullOrEmpty())
            {
                foreach (var lunarCourtierID in this.LunarCourtiers)
                {
                    if (The.ZoneManager.PullCachedObject(lunarCourtierID, DeepCopy: false) is GameObject cachedLunarCourtier)
                    {
                        LunarCourtiers ??= new();
                        LunarCourtiers.Add(cachedLunarCourtier);
                    }
                }
                return true;
            }
            return false;
        }

        public bool TryFindLunarParty(out GameObject LunarRegent, out CoalescibleSet<GameObject> LunarCourtiers)
        {
            LunarRegent = null;
            LunarCourtiers = null;
            if (this.LunarRegent.IsNullOrEmpty())
                return false;

            if (GameObject.FindByID(this.LunarRegent) is not GameObject foundLunarRegent)
                return false;

            LunarRegent = foundLunarRegent;

            if (!this.LunarCourtiers.IsNullOrEmpty())
            {
                foreach (var lunarCourtierID in this.LunarCourtiers)
                {
                    if (GameObject.FindByID(lunarCourtierID) is GameObject foundLunarCourtier)
                    {
                        LunarCourtiers ??= new();
                        LunarCourtiers.Add(foundLunarCourtier);
                    }
                }
            }
            return true;
        }

        public bool TryGetLunarParty(out GameObject LunarRegent, out CoalescibleSet<GameObject> LunarCourtiers)
        {
            LunarRegent = null;
            LunarCourtiers = null;

            bool retreivedFromCache = false;
            if (IsCached())
                if (TryPullCachedLunarParty(out LunarRegent, out LunarCourtiers))
                    retreivedFromCache = true;

            if (IsCached(Strict: true))
                return retreivedFromCache;

            if (TryFindLunarParty(out _, out CoalescibleSet<GameObject> foundLunarCourtiers))
            {
                if (LunarCourtiers.IsNullOrEmpty())
                    LunarCourtiers = foundLunarCourtiers;
                else
                {
                    foundLunarCourtiers.AddRange(LunarCourtiers, e => !foundLunarCourtiers.Contains(e));
                    LunarCourtiers = foundLunarCourtiers;
                }
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            LunarRegent = null;
            LunarCourtiers = null;
        }
    }
}
