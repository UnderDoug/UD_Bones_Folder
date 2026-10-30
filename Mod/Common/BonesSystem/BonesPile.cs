using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.Core;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    [HasReturnsBonesSourcesDelegate]
    public static class BonesPile
    {
        public static BonesFolder DummyFolder = new BonesFolder(IBonesSource.SourceType.None, SavePath);
        public static string SavePath => XRLCore.SavePath;
        public static string LocalBonesPath => DataManager.LocalPath("Bones");
        public static string SyncedBonesPath => DataManager.SyncedPath("Bones");

        public static Dictionary<IBonesSource.SourceType, BonesFolder> BonesFolderCache = new Dictionary<IBonesSource.SourceType, BonesFolder>
        {
            { IBonesSource.SourceType.Local, new BonesFolder(IBonesSource.SourceType.Local, LocalBonesPath) },
            { IBonesSource.SourceType.Synced, new BonesFolder(IBonesSource.SourceType.Synced, SyncedBonesPath) },
        };

        [BonesSourcesDelegate(IBonesSource.SourceType.Local)]
        public static IEnumerable<IBonesSource> LocalBonesSources()
        {
            if (BonesFolderCache.TryGetValue(IBonesSource.SourceType.Local, out var localFolder))
                yield return localFolder;
        }

        [BonesSourcesDelegate(IBonesSource.SourceType.Synced)]
        public static IEnumerable<IBonesSource> SyncedBonesSources()
        {
            if (BonesFolderCache.TryGetValue(IBonesSource.SourceType.Synced, out var syncedFolder))
                yield return syncedFolder;
        }
    }
}
