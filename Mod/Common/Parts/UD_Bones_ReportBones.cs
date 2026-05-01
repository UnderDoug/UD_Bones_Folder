using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Rules;
using XRL.World.Anatomy;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;

using UD_Bones_Folder.Mod.Events;
using static UD_Bones_Folder.Mod.OsseousAsh;

namespace XRL.World.Parts
{
    public class UD_Bones_ReportBones : IScribedPart
    {
        public string LoadedBonesID;
        public int SerializedBaseID;

        public UD_Bones_ReportBones()
            : base()
        { }

        public UD_Bones_ReportBones(string LoadedBonesID, int SerializedBaseID)
            : this()
        {
            this.LoadedBonesID = LoadedBonesID;
            this.SerializedBaseID = SerializedBaseID;
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetInventoryActionsAlwaysEvent.ID
            || ID == InventoryActionEvent.ID
            ;

        public override bool HandleEvent(GetInventoryActionsAlwaysEvent E)
        {
            E.Actions[ReportBonesInventoryAction.Name] = ReportBonesInventoryAction;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == ReportBonesInventoryAction.Command)
            {
                if (TryReportBones(LoadedBonesID, ParentObject).WaitResult())
                {
                    E.RequestInterfaceExit();
                    return true;
                }
            }
            return base.HandleEvent(E);
        }
    }
}
