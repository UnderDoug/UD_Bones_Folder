using System;

using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.Effects;

namespace UD_Bones_Folder.Mod
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
            => $"Thinks =subject.subjective==subject.verb:'re:afterpronoun= the {UD_Bones_MoonKingFever.REGAL_TITLE} but =subject.subjective= not me!"
                .StartReplace()
                .AddObject(Actor)
                .ToString()
            ;
    }
}
