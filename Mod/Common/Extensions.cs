using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using ConsoleLib.Console;

using Kobold;

using UD_Bones_Folder.Mod.UI;

using XRL;
using XRL.Language;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;

namespace UD_Bones_Folder.Mod
{
    public static class Extensions
    {
        public static SaveBonesJSON CreateSaveBonesJSON(this XRLGame Game, IDeathEvent E, GameObject MoonKing)
        {
            var localTimeNow = DateTime.Now;
            long saveTimeValue = localTimeNow.ToUniversalTime().Ticks;
            MoonKing.Render.Visible = true;
            var render = MoonKing.RenderForUI("SaveBonesInfo", true);
            MoonKing.Render.Visible = false;

            var timeSpan = TimeSpan.FromTicks(Game._walltime);

            var zone = MoonKing.CurrentZone;
            string zoneID = zone.ZoneID;
            var terrainObject = zone.GetTerrainObject();
            string zoneTerrainType = zone.Z > 10 ? "underground" : terrainObject.Blueprint;

            string location = null;
            /*
            if (zone.Z <= 10)
            {
                location = terrainObject.Render.DisplayName;
                if (terrainObject.IsPlural)
                    location = $"some {location}";
                else
                    location = $"{Grammar.A(location)}";
            }
            */
            if (zone.Z > 10)
                location += $"{zoneTerrainType}, ";

            string deathReason = E.Reason;
            if (deathReason.EndsWith(".")
                || deathReason.EndsWith("!"))
                deathReason = deathReason[..^1];

            if (deathReason.StartsWith("You"))
            {
                deathReason = deathReason[4..];
                if (deathReason.StartsWith("were"))
                    deathReason = $"=subject.verb:were:afterpronoun={deathReason[4..]}";
                deathReason = $"=subject.Subjective= {deathReason}";
            }
            else
            if (deathReason.StartsWith("you"))
            {
                deathReason = deathReason[4..];
                if (deathReason.StartsWith("were"))
                    deathReason = $"=subject.verb:were:afterpronoun={deathReason[4..]}";
                deathReason = $"=subject.subjective= {deathReason}";
            }

            return new SaveBonesJSON
            {
                SaveVersion = 400,
                GameVersion = Game.GetType().Assembly.GetName().Version.ToString(),
                ID = Game.GameID,
                Name = MoonKing.GetReferenceDisplayName(),
                Level = MoonKing.Statistics["Level"].Value,
                GenoSubType = $"{MoonKing.genotypeEntry.DisplayName} {MoonKing.subtypeEntry.DisplayName}",
                GameMode = Game.GetStringGameState("GameMode", "Classic"),
                CharIcon = render.Tile,
                FColor = render.GetForegroundColorChar(),
                DColor = render.GetDetailColorChar(),

                Location = $"{location}{LoreGenerator.GenerateLandmarkDirectionsTo(zoneID)}",
                InGameTime = $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}",
                Turn = Game.Turns,
                SaveTime = $"{localTimeNow.ToLongDateString()} at {localTimeNow.ToLongTimeString()}",
                ModsEnabled = ModManager.GetRunningMods().ToList(),

                ModVersion = Utils.ThisMod.Manifest.Version.ToString(),

                SaveTimeValue = saveTimeValue,

                ZoneID = zoneID,
                DeathReason = deathReason.StartReplace().AddObject(MoonKing).ToString(),
                GenotypeName = MoonKing.GetGenotype(),
                SubtypeName = MoonKing.GetSubtype(),

                ZoneTerrainType = zoneTerrainType,
                ZoneTier = zone.Tier,
                ZoneRegion = zone.GetRegion(),
            };
        }

        public static void setBonesData(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            SaveRow.deleteButton ??= new();
            SaveRow.deleteButton.RequireContext<NavigationContext>().parentContext = SaveRow.context.context;

            var bonesInfo = BonesData.BonesInfo;
            var bonesJSON = bonesInfo?.GetBonesJSON();
            SaveRow.imageTinyFrame ??= new();
            string tile = "Text/32.bmp";
            if (bonesJSON != null)
            {
                if (SpriteManager.HasTextureInfo(bonesJSON.CharIcon))
                    tile = bonesJSON.CharIcon;

                SaveRow.imageTinyFrame.sprite = SpriteManager.GetUnitySprite(tile);
                SaveRow.imageTinyFrame.unselectedBorderColor = The.Color.Black;
                SaveRow.imageTinyFrame.selectedBorderColor = The.Color.Yellow;
                SaveRow.imageTinyFrame.unselectedForegroundColor = The.Color.Black;
                SaveRow.imageTinyFrame.unselectedDetailColor = The.Color.Black;

                SaveRow.imageTinyFrame.selectedForegroundColor = The.Color.Gray;
                if (ColorUtility.ColorMap.TryGetValue(bonesJSON.FColor, out var value))
                    SaveRow.imageTinyFrame.selectedForegroundColor = value;

                SaveRow.imageTinyFrame.selectedDetailColor = The.Color.DarkBlack;
                if (ColorUtility.ColorMap.TryGetValue(bonesJSON.DColor, out var value2))
                    SaveRow.imageTinyFrame.selectedDetailColor = value2;
            }
            else
            {
                SaveRow.imageTinyFrame.sprite = SpriteManager.GetUnitySprite(tile);
                SaveRow.imageTinyFrame.unselectedBorderColor = The.Color.Black;
                SaveRow.imageTinyFrame.selectedBorderColor = The.Color.Yellow;
                SaveRow.imageTinyFrame.unselectedForegroundColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.unselectedDetailColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.selectedForegroundColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.selectedDetailColor = UnityEngine.Color.clear;
            }

            if (SaveRow.imageTinyFrame.ThreeColor)
                SaveRow.imageTinyFrame.ThreeColor.SetHFlip(Value: true);

            SaveRow.imageTinyFrame.Sync(force: true);
            SaveRow.TextSkins[0].SetText($"{bonesInfo.Name}::{bonesInfo.Description}".WithColor("W"));
            SaveRow.TextSkins[1].SetText(/*$"{"Location:".WithColor("C")} " + */$"{ColorUtility.CapitalizeExceptFormatting(bonesInfo.Info)}");
            SaveRow.TextSkins[2].SetText($"{bonesInfo.DeathReason} on {bonesInfo.SaveTime}");
            string bonesID = "{" + bonesInfo.ID + "} ";
            SaveRow.TextSkins[3].SetText($"{bonesInfo.Size} {bonesID}".WithColor("K"));
            SaveRow.modsDiffer.SetActive(value: true);
            if (SaveRow.modsDiffer.GetComponentsInChildren<UITextSkin>() is UITextSkin[] modsDifferTextSkins)
            {
                foreach (var modsDifferTextSkin in modsDifferTextSkins)
                {
                    if (modsDifferTextSkin.name == "tct"
                        || modsDifferTextSkin.gameObject.name == "tct")
                    {
                        modsDifferTextSkin.SetText(bonesInfo.ModsDiffer.ToString());
                        break;
                    }
                }
            }
            if (SaveRow.deleteButton.GetComponentsInChildren<UITextSkin>() is UITextSkin[] deleteButtonTextSkins)
            {
                foreach (var deleteButtonTextSkin in deleteButtonTextSkins)
                {
                    if (deleteButtonTextSkin.name == "tct"
                        || deleteButtonTextSkin.gameObject.name == "tct")
                    {
                        deleteButtonTextSkin.SetText("{{y|cremate}}");
                        break;
                    }
                }
            }
            SaveRow.Update();
        }

        public static TAccumulate Aggregate<TAccumulate>(
            this int Number,
            TAccumulate seed,
            Func<TAccumulate, int, TAccumulate> func
            )
        {
            for (int i = 0; i < Number; i++)
                seed = func(seed, i);

            return seed;
        }

        public static string ThisManyTimes(this string @string, int Times = 1)
            => Times.Aggregate("", (a, n) => a + @string)
            ;
        public static string ThisManyTimes(this char @char, int Times = 1)
            => @char.ToString().ThisManyTimes(Times)
            ;

        public static string CallChain(this string String, params string[] Calls)
            => Calls.Aggregate(String, (a, n) => a + "." + n)
            ;

        public static string CallChain(this Type Type, params string[] Calls)
            => Type.Name.CallChain(Calls)
            ;

        public static string Indent(this int Amount, int Factor = 4)
            => Amount > 0
            ? " ".ThisManyTimes(Math.Min(Amount * Math.Max(1, Factor), 16))
            : null
            ;

        public static void PrintComponents(this UnityEngine.GameObject GameObject, string MessageBefore = null, int CurrentDepth = 0)
        {
            if (!MessageBefore.IsNullOrEmpty()
                && MessageBefore[^1] != ' ')
                MessageBefore += " ";
            if (GameObject == null)
            {
                Utils.Log($"{CurrentDepth.Indent(Factor: 4)}{MessageBefore}NULL_{nameof(UnityEngine.GameObject)}");
                return;
            }
            string name = GameObject?.name ?? "NO_NAME";
            Utils.Log($"{CurrentDepth.Indent(Factor: 4)}{MessageBefore}{name} (Components: {GameObject.GetComponentCount()})");
            for (int i = 0; i < GameObject.GetComponentCount(); i++)
            {
                try
                {
                    if (GameObject.GetComponentAtIndex(i) is UnityEngine.Component component)
                    {
                        string extras = component.GetType().Name switch
                        {
                            nameof(UITextSkin) => $"{nameof(UITextSkin.text)}: {(component as UITextSkin).text}",
                            nameof(UnityEngine.RectTransform) => $"{nameof(UnityEngine.RectTransform.rect)}: {(component as UnityEngine.RectTransform).rect}",
                            nameof(UnityEngine.UI.Image) => $"{nameof(UnityEngine.UI.Image.color)}: {(component as UnityEngine.UI.Image).color}",
                            _ => null,
                        };
                        Utils.Log($"{(CurrentDepth + 1).Indent(Factor: 4)}" +
                            $"{component.GetType()?.Name ?? "NO_COMPONENT_TYPE"}|" +
                            $"{component.name} {extras}");
                    }
                    else
                        Utils.Log($"{(CurrentDepth + 1).Indent(Factor: 4)}NO_COMPONENT");
                }
                catch (Exception x)
                {
                    Utils.Error($"{typeof(Extensions).CallChain(nameof(PrintComponents), name)} {nameof(UnityEngine.Component)} {i}", x);
                }
            }
        }
    }
}
