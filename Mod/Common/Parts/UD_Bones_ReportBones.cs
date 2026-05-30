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
            || ID == GetInventoryActionsEvent.ID
            || ID == InventoryActionEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetInventoryActionsEvent E)
        {
            E.Actions[ReportBonesInventoryAction.Name] = ReportBonesInventoryAction;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == ReportBonesInventoryAction.Command)
            {
                if (TryReportBonesAsync(LoadedBonesID, ParentObject).WaitResult())
                {
                    /*if (Config != null)
                    {
                        Config.BlockedBonesIDs ??= new();
                        Config.BlockedBonesIDs.Add(LoadedBonesID);
                        Config.Write();
                    }*/
                    E.RequestInterfaceExit();
                    return true;
                }
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(LoadedBonesID), LoadedBonesID);
            if (BonesManager.System.GetSavedBonesByID(LoadedBonesID) is SaveBonesInfo bonesInfo)
            {
                E.AddEntry(this, nameof(bonesInfo.ModVersion), bonesInfo.ModVersion);
                E.AddEntry(this, nameof(bonesInfo.SaveTimeValue), bonesInfo.SaveTimeValue.Timestamp());

                string fileLocationDataDebugString = "empty";
                if (!bonesInfo.FileLocationDataSet.IsNullOrEmpty())
                    fileLocationDataDebugString = bonesInfo.FileLocationDataSet
                        .Aggregate(
                            seed: "",
                            func: (a, n) => Utils.NewLineDelimitedAggregator(a, n.TaggedDisplayName()))
                        ;

                E.AddEntry(this, nameof(bonesInfo.FileLocationData), fileLocationDataDebugString);
                E.AddEntry(this, nameof(SaveBonesJSON.ZoneID), bonesInfo.GetBonesJSON().ZoneID);
            }
            else
                E.AddEntry(this, nameof(SaveBonesInfo), "Not Found...");

            return base.HandleEvent(E);
        }
    }
}
