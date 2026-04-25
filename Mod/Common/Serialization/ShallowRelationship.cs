using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    public class ShallowRelationship
    {
        public Type AllyReasonType;
        public int LeaderID;
        public int FollowerID;

        public static bool TryGetFrom(GameObject Follower, out ShallowRelationship Relationship)
        {
            Relationship = null;
            if (Follower?.Brain?.Allegiance is not AllegianceSet allegiance
                || allegiance.SourceID == 0)
                return false;

            Relationship = new()
            {
                AllyReasonType = allegiance.Reason.GetType(),
                LeaderID = allegiance.SourceID,
                FollowerID = Follower.BaseID,
            };
            Utils.Log($"{Follower.DebugName} is follower of {Relationship.LeaderID} for reason {Relationship.AllyReasonType.Name ?? "NO_TYPE"}");
            return true;
        }

        public bool PerformAllyship(CrossGameObject CrossGameLeader, CrossGameObject CrossGameFollower)
        {
            if (CrossGameFollower?.Clone is not GameObject newFollower
                || CrossGameFollower?.Original?.BaseID is not int originalFollowerID
                || FollowerID != originalFollowerID
                || CrossGameLeader?.Clone is not GameObject newLeader
                || CrossGameLeader?.Original?.BaseID is not int originalLeaderID
                || LeaderID != originalLeaderID
                || newFollower.Brain is not Brain brain
                || Activator.CreateInstance(AllyReasonType) is not IAllyReason allyReason)
                return false;

            brain.Allegiance?.Clear();

            brain.TakeAllegiance(newLeader, allyReason);
            brain.SetPartyLeader(newLeader, Silent: true);

            string newFollowerName = newFollower?.DebugName ?? "NO_COURTIER";
            string newLeaderName = newLeader?.DebugName ?? "NO_REGENT";
            string allyReasonName = allyReason.GetType().Name;
            Utils.Log($"{newFollowerName} made follower of {newLeaderName} for reason {allyReasonName}.");
            return true;
        }
    }
}
