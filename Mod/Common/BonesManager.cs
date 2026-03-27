using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Cysharp.Text;

using UnityEngine;

using Qud.API;
using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.Core;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;

using GameObject = XRL.World.GameObject;
using ColorUtility = ConsoleLib.Console.ColorUtility;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Event = XRL.World.Event;

namespace Bones.Mod
{
    [HasGameBasedStaticCache]
    [HasWishCommand]
    [Serializable]
    public class BonesManager : IScribedSystem
    {
        internal static string BonesPath => DataManager.SavePath("Bones");

        [GameBasedStaticCache(CreateInstance = false)]
        public static BonesManager System;

        public List<string> RunningMods;

        public bool Initialized;

        [NonSerialized]
        public string BonesDirectory;

        [NonSerialized]
        public Task SaveTask;

        public BonesManager()
        { }

        private static BonesManager InitializeSystem() => new();

        [GameBasedCacheInit]
        public static void BonesManagerSystemInit()
        {
            if (System == null)
                System = The.Game?.RequireSystem(InitializeSystem);
            else
                The.Game?.AddSystem(System);

            if (System == null)
                System = The.Game?.RequireSystem(InitializeSystem);
            else
                The.Game?.AddSystem(System);

            bool success = false;
            try
            {
                if (System != null)
                    Loading.LoadTask($"Preparing Bones", System.PrepareBonesPile);

                success = true;
            }
            catch (Exception x)
            {
                MetricsManager.LogCallingModError($"{nameof(BonesManagerSystemInit)}: {x}");
                success = false;
            }
            finally
            {
                if (The.Game != null)
                    MetricsManager.LogModInfo(ModManager.GetMod(), $"{nameof(BonesManager)}.{nameof(BonesManagerSystemInit)} finished... {(success ? "success!" : "failure!")}");
            }
        }
        public void PrepareBonesPile()
        {
            RunningMods = ModManager.GetRunningMods().ToList();
        }

        public string GetBonesDirectory(string FileName = null)
        {
            if (BonesDirectory == null)
            {
                string path = Path.Combine(BonesPath, The.Game.GameID);
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception x)
                {
                    MetricsManager.LogCallingModError(x);
                    return null;
                }
                BonesDirectory = path;
            }
            if (!FileName.IsNullOrEmpty())
                return Path.Combine(BonesDirectory, FileName);

            return BonesDirectory;
        }

        public string GetSaveFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.sav.gz")
            ;

        public string GetCacheFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.json")
            ;

        public Task CollectBones(string GameName, IDeathEvent DeathEvent)
        {
            var task = SaveTask;

            if (task != null
                && !task.IsCompleted)
                return null;

            string message = "Hoarding Bones";

            Loading.Status status = Loading.StartTask(message);

            if (The.Game is XRLGame game
                && DeathEvent?.Dying?.CurrentZone is Zone currentZone
                && SerializationWriter.Get() is SerializationWriter writer)
            {
                // Clear object ID's so they get a new one once re-serialzed (hopefully this works...)
                foreach (var zoneGO in currentZone.GetObjects())
                {
                    zoneGO._BaseID = 0;
                    zoneGO.RemoveStringProperty("id");
                }

                string bonesFilePath = GetSaveFileFullPath(GameName);
                string bonesCachePath = GetCacheFileFullPath(GameName);
                try
                {
                    if (game.WallTime != null)
                    {
                        game._walltime += game.WallTime.ElapsedTicks;
                        game.WallTime.Reset();
                        game.WallTime.Start();
                    }
                    else
                    {
                        game.WallTime = new Stopwatch();
                        game.WallTime.Start();
                    }

                    var saveBonesInfo = new SaveBonesJSON(DeathEvent, The.Game.SaveGameInfo());

                    writer.Start(400);
                    writer.Write(123457);

                    writer.Write(The.Game.GetType().Assembly.GetName().Version.ToString());

                    writer.WriteBonesZone(currentZone);

                    writer.FinalizeWrite();

                    MetricsManager.LogInfo($"Done {message} in {game.WallTime.ElapsedMilliseconds}ms");
                    bool restoreBackup = false;
                    try
                    {
                        File.WriteAllText(bonesCachePath, JsonUtility.ToJson(saveBonesInfo, prettyPrint: true));
                        if (File.Exists(bonesFilePath))
                        {
                            File.Copy(bonesFilePath, bonesFilePath + ".bak", overwrite: true);
                            restoreBackup = true;
                        }
                        using (var bonesFile = File.Create(bonesFilePath))
                        {
                            using var gZipStream = new GZipStream(bonesFile, CompressionLevel.Fastest);
                            byte[] buffer = writer.Stream.GetBuffer();
                            gZipStream.Write(buffer, 0, (int)writer.Stream.Position);
                        }

                        game.CheckSave(bonesFilePath);
                    }
                    catch (Exception x)
                    {
                        game.SaveGameError(bonesFilePath, x, restoreBackup);
                    }
                    finally
                    {
                        SerializationWriter.Release(writer);
                    }
                    task = null;
                }
                catch (Exception x)
                {
                    SerializationWriter.Release(writer);
                    game.SaveGameError(bonesFilePath, x);
                    task = (SaveTask = Task.FromException(x));
                }
                finally
                {
                    MemoryHelper.GCCollectMax();
                    status.Dispose();
                }
            }
            return task;
        }

        public IEnumerable<SaveBonesInfo> GetSavedBonesInfo()
        {
            using var saveGameInfo = ScopeDisposedList<SaveBonesInfo>.GetFromPool();
            try
            {
                if (!Directory.Exists(BonesPath))
                    yield break;

                foreach (string bonesFolder in Directory.EnumerateDirectories(BonesPath))
                    if (SaveBonesInfo.GetSaveBonesInfo(bonesFolder) is SaveBonesInfo bonesInfo)
                        saveGameInfo.Add(bonesInfo);
            }
            catch (Exception x)
            {
                SeriousBonesError(x, BonesPath);
                saveGameInfo.Clear();
            }

            if (!saveGameInfo.IsNullOrEmpty())
                foreach (var bonesInfo in saveGameInfo)
                    yield return bonesInfo;
        }

        private static void SeriousBonesError(Exception Ex, string Path)
        {
            MetricsManager.LogError("Error checking for save files", Ex);
            using var sB = ZString.CreateStringBuilder();
            if (Ex is UnauthorizedAccessException
                || Ex is IOException)
            {
                sB.AppendLine("There was a permission error while trying to access your bones directory.");
                sB.AppendLine();
                sB.AppendLine(ColorUtility.EscapeFormatting(Ex.Message));
            }
            else
            {
                sB.AppendLine("There was an error while trying to access your bones directory.");
                sB.AppendLine();
                sB.Append("Directory: ");
                sB.AppendLine(ColorUtility.EscapeFormatting(Path));
            }
            sB.AppendLine();
            sB.AppendLine("Caves of Qud will run, but Bones won't be available. Please check your directory’s permissions.");

            Popup.WaitNewPopupMessage(
                message: sB.ToString(),
                buttons: new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = "Bummer!",
                        hotkey = "Accept,Cancel",
                        command = "Cancel"
                    }
                },
                title: "Error reading bones location.");
        }

        public BonesData ExhumeMoonKing(
            string ZoneID,
            SaveBonesInfo SaveBonesInfo
            )
        {
            string savGz = $"{BonesSaver.BonesName}.sav.gz";
            string savGzBak = $"{savGz}.bak";
            string fullBonesPath = Path.Combine(SaveBonesInfo.Directory, savGz);
            string fullBonesBak = Path.Combine(SaveBonesInfo.Directory, savGzBak);
            string bonesBakDisplay = DataManager.SanitizePathForDisplay(fullBonesBak);
            string directoryDisplay = DataManager.SanitizePathForDisplay(SaveBonesInfo.Directory);
            int versionNumber = -1;
            string versionString = "{unknown}";
            BonesData bonesData = null;
            try
            {
                if (!File.Exists(fullBonesPath))
                {
                    fullBonesPath = Path.Combine(SaveBonesInfo.Directory, $"{BonesSaver.BonesName}.sav");
                    if (!File.Exists(fullBonesPath))
                    {
                        Utils.Error($"No saved game exists. ({directoryDisplay})");
                        return bonesData;
                    }
                }

                var reader = SerializationReader.Get();

                var status = Loading.StartTask("Exhuming Moon King");

                try
                {
                    using (FileStream fileStream = File.OpenRead(fullBonesPath))
                    {
                        var stream = reader.Stream;
                        bool isGZip = false;
                        if (fileStream.Length >= 2)
                        {
                            Span<byte> buffer = stackalloc byte[2];
                            fileStream.Read(buffer);

                            if (buffer[0] == 31
                                && buffer[1] == 139)
                                isGZip = true;

                            fileStream.Position = 0L;
                        }
                        if (isGZip)
                        {
                            using var gZipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                            gZipStream.CopyTo(stream);
                        }
                        else
                            fileStream.CopyTo(stream);

                        stream.Position = 0L;
                    }

                    reader.Start();
                    if (reader.ReadInt32() != 123457)
                    {
                        versionString = "2.0.167.0 or prior";
                        throw new Exception("Bones file is the incorrect version.");
                    }

                    versionNumber = reader.FileVersion;
                    versionString = reader.ReadString();
                    try
                    {
                        if (versionNumber != 400)
                        {
                            string backupPath = fullBonesPath + $"_upgradebackup_{versionNumber}.gz";
                            if (!File.Exists(backupPath))
                            {
                                File.Copy(fullBonesPath, backupPath);
                                string cacheDBPath = Path.Combine(SaveBonesInfo.Directory, "Cache.db");
                                string cacheDBBackupPath = cacheDBPath + $"_upgradebackup_{versionNumber}.gz";
                                if (File.Exists(cacheDBPath)
                                    && !File.Exists(cacheDBBackupPath))
                                    File.Copy(cacheDBPath, cacheDBBackupPath);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"bones upgrade backup: {x}");
                    }

                    if (reader.FileVersion < 395
                        || reader.FileVersion > 400)
                        throw new Exception($"Bones file is the incorrect version ({versionString}).");

                    string bonesID = SaveBonesInfo.ID;
                    Zone loadedZone = null;
                    try
                    {
                        loadedZone = reader.ReadBonesZone(/*ZoneID*/);
                        if (loadedZone != null
                            && loadedZone.Z > 21)
                        {
                            loadedZone.Z = Stat.Random(16, 21);
                        }
                        reader.FinalizeRead();
                        /*
                        if (loadedZone != null)
                        {
                            if (The.ActionManager is ActionManager actionManager
                                && actionManager.ActionQueue is RingDeque<GameObject> actionQueue)
                            {
                                foreach (var loadedObject in loadedZone.GetObjects())
                                {
                                    if (actionQueue.Contains(loadedObject))
                                    {
                                        loadedObject.ApplyActiveRegistrar();
                                        if (loadedObject.Abilities != null)
                                            actionManager.AbilityObjects.Add(loadedObject);
                                    }
                                }
                            }
                        }
                        */
                        bonesData = new(bonesID, ZoneID, loadedZone);
                    }
                    catch (Exception x)
                    {
                        Utils.Error(x);
                        bonesData = null;
                    }
                    if (reader.Errors > 0)
                    {
                        Popup.DisplayLoadError(reader, "bones file", reader.Errors);
                    }
                }
                catch (Exception x)
                {
                    Utils.Error(x);
                    bonesData = null;
                }
                finally
                {
                    SerializationReader.Release(reader);
                    status.Dispose();
                }

            }
            catch (Exception x)
            {
                string message = $"That bones file appears to be corrupt, " +
                    $"you can try to restore the backup in your bones folder ({bonesBakDisplay}) " +
                    $"by removing the 'bak' file extension.";

                if (ModManager.TryGetStackMod(x, out var Mod, out var Frame))
                {
                    if (Frame.GetMethod() is MethodBase method)
                    {
                        string culpritMethod = method.DeclaringType?.FullName + "." + method.Name;
                        Mod.Error(culpritMethod + "::" + x);
                        message = "That bones file is likely not loading because of a mod error from " + Mod.DisplayTitleStripped + " (" + culpritMethod + "), make sure the correct mods are enabled or contact the mod author.";
                    }
                }
                else
                {
                    if (versionNumber < 400)
                        message = $"That bones file looks like it's from an older save format revision ({versionString}). Sorry!\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";
                    else
                    if (versionNumber > 400)
                        message = $"That bones file looks like it's from a newer save format revision ({versionString}).\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";

                    MetricsManager.LogException($"{nameof(BonesManager)}.{nameof(ExhumeMoonKing)}::", x, "serialization_error");
                }

                Utils.Error(message);

                throw;
            }

            return bonesData;
        }

        public SaveBonesInfo GetSavedBonesByID(string BonesID)
        {
            foreach (var savedBones in GetSavedBonesInfo())
            {
                if (savedBones.ID == BonesID)
                    return savedBones;
            }
            return null;
        }

        public bool TryGetSavedBonesByID(string BonesID, out SaveGameInfo SavedBonesInfo)
            => (SavedBonesInfo = GetSavedBonesByID(BonesID)) != null
            ;

        public void CremateMoonKing(string BonesID)
        {
            if (TryGetSavedBonesByID(BonesID, out var savedBones))
                savedBones.Delete();
        }

        public static GameObject CreateMoonKing(
            GameObject Player,
            Cell TargetCell
            )
        {
            string prefix = "Moon";
            string regalTerm = "Regent";
            if (Player.GetGender() is Gender playerGender)
            {
                switch (playerGender.Name)
                {
                    case "Female":
                    case "female":
                        regalTerm = "Queen";
                        break;
                    case "Male":
                    case "male":
                        regalTerm = "King";
                        break;
                    default:
                        break;
                }
                if (playerGender.Plural)
                    regalTerm = regalTerm.Pluralize();
            }
            prefix = $"{prefix} {regalTerm}";

            if (!Player.CanBeReplicated(Player, BonesSaver.BonesName, Temporary: false))
                return null;

            string factions = "Entropic-100,Mean-100,Playerhater-99";

            var moonKing = Player.DeepCopy();

            if (Player.IsPlayer())
                moonKing.SetStringProperty("PlayerCopy", "true");

            moonKing.SetStringProperty("EvilTwin", "true");
            moonKing.SetStringProperty(BonesSaver.BonesName, "true");
            moonKing.SetIntProperty("Entropic", 1);

            var brain = moonKing.Brain;
            brain.PartyLeader = null;
            brain.Hibernating = false;
            brain.Staying = false;
            brain.Passive = false;
            brain.Factions = factions;
            brain.Allegiance.Hostile = true;
            brain.Allegiance.Calm = false;
            brain.PerformEquip();

            if (Player != null)
            {
                brain.AddOpinion<OpinionMollify>(Player);
                Player.AddOpinion<OpinionMollify>(moonKing);
            }

            moonKing.DisplayName = $"{prefix} {moonKing.DisplayNameOnlyDirect}";

            if (TargetCell == null)
            {
                moonKing.Obliterate();
                return null;
            }

            if (moonKing.Render is Render render)
                render.Visible = false;

            TargetCell.AddObject(moonKing);

            moonKing.ApplyEffect(new MoonKingFever(regalTerm));

            moonKing.MakeActive();
            
            return moonKing;
        }

        public static void ApplyHostility(GameObject Actor, Brain Brain, int Depth)
        {
            if (Actor != null && Depth < 100)
            {
                Brain.AddOpinion<OpinionInscrutable>(Actor);
                ApplyHostility(Actor.PartyLeader, Brain, Depth + 1);
                if (Actor.TryGetEffect<Dominated>(out var Effect))
                {
                    ApplyHostility(Effect.Dominator, Brain, Depth + 1);
                }
            }
        }
    }
}
