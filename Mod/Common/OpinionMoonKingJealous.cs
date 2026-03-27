using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;
using XRL.World.AI;

namespace Bones.Mod
{
    [Serializable]
    public class OpinionMoonKingJealous : IOpinionSubject
    {
        public override bool WantFieldReflection => false;

        public override int BaseValue => -1000;

        public override void Write(SerializationWriter Writer)
        {
            base.Write(Writer);
            Writer.Write(Magnitude);
            Writer.WriteOptimized(Time);
        }

        public override void Read(SerializationReader Reader)
        {
            base.Read(Reader);
            Magnitude = Reader.ReadSingle();
            Time = Reader.ReadOptimizedInt64();
        }

        public override string GetText(GameObject Actor)
            => "Thinks =subject.subjective==subject.verb:'re:afterpronoun= the {{rainbow|Moon King}} but =subject.subjective= not me!"
                .StartReplace()
                .AddObject(Actor)
                .ToString()
            ;
    }
}
