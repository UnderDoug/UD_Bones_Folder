using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Bones_Folder.Mod
{
    public class CrossGameObject
    {
        public GameObject Clone;
        public GameObject Original;

        public void Deconstruct(out GameObject Clone, out GameObject Original)
        {
            Clone = this.Clone;
            Original = this.Original;
        }

        public bool Matches(GameObject Object)
            => Matches(Object?.BaseID ?? -1)
            ;

        public bool Matches(int BaseID)
            => BaseID > 0
            && (BaseID == Original?.BaseID
                || BaseID == Clone?.BaseID)
            ;

        public static CrossGameObject CreateFrom(GameObject Original)
            => new CrossGameObject()
                .SetClone(Original.DeepCopy(CopyEffects: true, CopyID: false))
                .SetOriginal(Original)
            ;

        public CrossGameObject SetClone(GameObject Clone)
        {
            this.Clone = Clone;
            return this;
        }

        public CrossGameObject SetOriginal(GameObject Original)
        {
            this.Original = Original;
            return this;
        }

        public static implicit operator KeyValuePair<GameObject, GameObject>(CrossGameObject CrossGameObject)
            => new(CrossGameObject.Original, CrossGameObject.Clone);
    }
}
