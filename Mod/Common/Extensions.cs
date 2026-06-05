using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Kobold;

using UnityEngine.UI;

using Platform.IO;

using XRL;
using XRL.Collections;
using XRL.Language;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

using UD_Bones_Folder.Mod.UI;

using static XRL.World.Cell;

using static UD_Bones_Folder.Mod.OsseousAsh;
using static UD_Bones_Folder.Mod.OsseousAsh.Report;

using Range = XRL.Range;
using System.Collections.Concurrent;
using Event = XRL.World.Event;
using XRL.World.Skills;
using XRL.World.Parts.Skill;
using XRL.Rules;
using UD_Bones_Folder.Mod.Parts;
using Genkit;
using XRL.World.WorldBuilders;
using System.Text.RegularExpressions;
using UD_Bones_Folder.Mod.Moderation;

namespace UD_Bones_Folder.Mod
{
    public static class Extensions
    {
        public static SaveBonesJSON CreateSaveBonesJSON(
            this XRLGame Game,
            IDeathEvent E,
            GameObject LunarRegent
            )
        {
            var localTimeNow = DateTime.Now;
            long saveTimeValue = localTimeNow.ToUniversalTime().Ticks;

            bool visible = LunarRegent.Render.Visible;
            LunarRegent.Render.Visible = true;
            LunarRegent.RestorePristineHealth();
            var render = new BonesRender(LunarRegent.RenderForUI("SaveBonesInfo", true), HFlip: true);
            LunarRegent.Render.Visible = visible;

            var colorChars = render.GetColorChars();
            var tileColor = colorChars.foreground;
            var detailColor = colorChars.detail;

            var timeSpan = TimeSpan.FromTicks(Game._walltime);

            var zone = LunarRegent.CurrentZone;
            string zoneID = zone.ZoneID;
            var terrainObject = zone.GetTerrainObject();
            string zoneTerrainType = zone.Z > 10 ? "underground" : terrainObject.Blueprint;

            string location = null;

            if (zone.Z > 10)
                location += $"{zoneTerrainType}, ";

            string deathReason = E.Reason ?? "You died under mysterious circumstances!";
            if (deathReason.EndsWith(".")
                || deathReason.EndsWith("!"))
                deathReason = deathReason[..^1];

            if (deathReason.StartsWith("Your "))
            {
                deathReason = $"=subject.Possessive= {deathReason[5..]}";
            }
            else
            if (deathReason.StartsWith("your "))
            {
                deathReason = $"=subject.possessive= {deathReason[5..]}";
            }
            else
            if (deathReason.StartsWith("You "))
            {
                deathReason = deathReason[4..];
                if (deathReason.StartsWith("were"))
                    deathReason = $"=subject.verb:were:afterpronoun={deathReason[4..]}";
                deathReason = $"=subject.Subjective= {deathReason}";
            }
            else
            if (deathReason.StartsWith("you "))
            {
                deathReason = deathReason[4..];
                if (deathReason.StartsWith("were"))
                    deathReason = $"=subject.verb:were:afterpronoun={deathReason[4..]}";
                deathReason = $"=subject.subjective= {deathReason}";
            }

            if (deathReason.Contains(" Were "))
                deathReason = deathReason.Replace(" Were ", " =subject.Verb:were:afterpronoun= ");

            if (deathReason.Contains(" were "))
                deathReason = deathReason.Replace(" were ", " =subject.verb:were:afterpronoun= ");

            if (deathReason.Contains("Yourself"))
                deathReason = deathReason.Replace("Yourself", "=subject.Reflexive=");

            if (deathReason.Contains("yourself"))
                deathReason = deathReason.Replace("yourself", "=subject.reflexive=");

            if (deathReason.Contains(" Your"))
                deathReason = deathReason.Replace(" Your", " =subject.Possessive=");

            if (deathReason.Contains(" your"))
                deathReason = deathReason.Replace(" your", " =subject.possessive=");

            if (deathReason.Contains(" Yours"))
                deathReason = deathReason.Replace(" Yours", " =subject.SubstantivePossessive=");

            if (deathReason.Contains(" yours"))
                deathReason = deathReason.Replace(" yours", " =subject.substantivePossessive=");

            if (deathReason.Contains(" You're"))
                deathReason = deathReason.Replace(" You're", " =subject.Subjective==subject.verb:'re:afterpronoun=");

            if (deathReason.Contains(" you're"))
                deathReason = deathReason.Replace(" you're", " =subject.subjective==subject.verb:'re:afterpronoun=");

            if (deathReason.Contains(" You are"))
                deathReason = deathReason.Replace(" You are", " =subject.Subjective= =subject.verb:are:afterpronoun=");

            if (deathReason.Contains(" you are"))
                deathReason = deathReason.Replace(" you are", " =subject.subjective= =subject.verb:are:afterpronoun=");

            if (deathReason.Contains(" You."))
                deathReason = deathReason.Replace(" You.", " =subject.Objective=");

            if (deathReason.Contains(" you."))
                deathReason = deathReason.Replace(" you.", " =subject.objective=");

            if (deathReason.Contains(" You"))
                deathReason = deathReason.Replace(" You", " =subject.Subjective=");

            if (deathReason.Contains(" you"))
                deathReason = deathReason.Replace(" you", " =subject.subjective=");

            string genotype = LunarRegent.genotypeEntry?.DisplayName;
            string subtype = LunarRegent.subtypeEntry?.DisplayName;

            string species = LunarRegent?.GetSpecies()?.Capitalize();
            string role = LunarRegent.GetTagOrStringProperty("Role")?.Capitalize();

            string genoSubType;
            if (subtype.IsNullOrEmpty()
                && genotype.IsNullOrEmpty())
                genoSubType = $"{species}{(role.IsNullOrEmpty() ? null : $" {role}")}";
            else
            {
                genoSubType = null;
                if (!genotype.IsNullOrEmpty())
                    genoSubType += genotype;

                if (!subtype.IsNullOrEmpty())
                {
                    if (!genoSubType.IsNullOrEmpty())
                        genoSubType += " ";
                    genoSubType += subtype;
                }
            }

            return new SaveBonesJSON
            {
                OsseousAshID = Config?.ID ?? Guid.Empty,
                OsseousAshHandle = Config?.Handle ?? DefaultOsseousAshHandle,
                SaveVersion = 400,
                GameVersion = Game.GetType().Assembly.GetName().Version.ToString(),
                ID = Game.GameID,
                Name = $"=LunarShader:{UD_Bones_LunarRegent.GetRegalTitle(LunarRegent)}:*= {LunarRegent.GetDisplayName(BaseOnly: true)}",
                Level = LunarRegent.Statistics["Level"].Value,
                GenoSubType = genoSubType,
                GameMode = Game.GetStringGameState("GameMode", "Classic"),
                CharIcon = render.GetTile(),
                FColor = tileColor,
                DColor = detailColor,

                Location = $"{location}{LoreGenerator.GenerateLandmarkDirectionsTo(zoneID)}",
                InGameTime = $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}",
                Turn = Game.Turns,
                SaveTime = $"{localTimeNow.ToLongDateString()} at {localTimeNow.ToLongTimeString()}",
                ModsEnabled = ModManager.GetRunningMods().ToList(),

                ModVersion = Utils.ThisMod.Manifest.Version.ToString(),

                SaveTimeValue = saveTimeValue,

                ZoneID = zoneID,

                BonesSpec = new BonesSpec(LunarRegent, zone),
                Stats = new(),

                DeathReason = deathReason.StartReplace().AddObject(LunarRegent).ToString(),
                GenotypeName = genotype ?? species,
                SubtypeName = subtype ?? role,
                Blueprint = LunarRegent.Blueprint,
            };
        }

        public static void setBonesData(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            SaveRow.SetBonesIcon(BonesData);
            SaveRow.SetBonesText(BonesData);

            BonesManagement.instance.SelectionChoiceLocationBox ??= new();
            SaveRow.SetLocationBox(BonesData)?.SetAsLastSibling();
            SaveRow.SetBonesModsDiffer(BonesData)?.SetAsLastSibling();
            SaveRow.SetBonesDeleteButton(BonesData)?.SetAsLastSibling();

            SaveRow.Update();
        }

        public static void SetBonesIcon(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;
            var bonesJSON = bonesInfo?.GetBonesJSON();

            var bonesRender = bonesInfo.Render;

            var colorChars = bonesRender.GetColorChars();
            var tileColor = colorChars.foreground;
            var detailColor = colorChars.detail;

            SaveRow.imageTinyFrame ??= new();

            if (bonesJSON != null)
            {
                SaveRow.imageTinyFrame.sprite = SpriteManager.GetUnitySprite(bonesRender.GetTile());
                SaveRow.imageTinyFrame.unselectedBorderColor = The.Color.Black;
                SaveRow.imageTinyFrame.selectedBorderColor = The.Color.Yellow;
                SaveRow.imageTinyFrame.unselectedForegroundColor = The.Color.Black;
                SaveRow.imageTinyFrame.unselectedDetailColor = The.Color.Black;

                SaveRow.imageTinyFrame.selectedForegroundColor = The.Color.Gray;
                if (ColorUtility.ColorMap.TryGetValue(tileColor, out var fColor))
                    SaveRow.imageTinyFrame.selectedForegroundColor = fColor;

                SaveRow.imageTinyFrame.selectedDetailColor = The.Color.DarkBlack;
                if (ColorUtility.ColorMap.TryGetValue(detailColor, out var dColor))
                    SaveRow.imageTinyFrame.selectedDetailColor = dColor;
            }
            else
            {
                SaveRow.imageTinyFrame.sprite = SpriteManager.GetUnitySprite(bonesRender.GetTile());
                SaveRow.imageTinyFrame.unselectedBorderColor = The.Color.Black;
                SaveRow.imageTinyFrame.selectedBorderColor = The.Color.Yellow;
                SaveRow.imageTinyFrame.unselectedForegroundColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.unselectedDetailColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.selectedForegroundColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.selectedDetailColor = UnityEngine.Color.clear;
            }

            if (SaveRow.imageTinyFrame.ThreeColor)
                SaveRow.imageTinyFrame.ThreeColor.SetHFlip(Value: false);

            SaveRow.imageTinyFrame.Sync(force: true);
        }

        public static void SetBonesText(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;

            SaveRow.TextSkins[0].SetText(bonesInfo.GetBonesMenuDataRowString(0));
            SaveRow.TextSkins[1].SetText(bonesInfo.GetBonesMenuDataRowString(1));
            SaveRow.TextSkins[2].SetText(bonesInfo.GetBonesMenuDataRowString(2));
            SaveRow.TextSkins[3].SetText(bonesInfo.GetBonesMenuDataRowString(3));
        }

        public static UnityEngine.Transform SetLocationBox(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;

            if (!BonesManagement.instance.SelectionChoiceLocationBox.TryGetValue(SaveRow, out var locationBox))
            {
                locationBox = UnityEngine.GameObject.Instantiate(SaveRow.modsDiffer);
                BonesManagement.instance.SetParentTransform(locationBox.transform, SaveRow.modsDiffer.transform.parent);
                locationBox.SetActive(value: true);
                locationBox.transform.SetAsFirstSibling();
                BonesManagement.instance.SelectionChoiceLocationBox[SaveRow] = locationBox;
            }
            locationBox?.SetActive(value: true);
            if (locationBox?.GetComponentsInChildren<UITextSkin>() is UITextSkin[] locationBoxTextSkins)
            {
                foreach (var locationBoxTextSkin in locationBoxTextSkins)
                {
                    if (locationBoxTextSkin.name == "tct"
                        || locationBoxTextSkin.gameObject.name == "tct")
                    {
                        string typeColor = bonesInfo.FileLocationData.GetFileLocationDataTypeColor();
                        string text = bonesInfo.FileLocationData.Type.ToString().ToLower().Colored(typeColor);
                        locationBoxTextSkin.SetText(text);
                        break;
                    }
                }
            }
            BonesManagement.instance.SelectionChoiceLocationBox[SaveRow] = locationBox;
            return locationBox?.transform;
        }

        public static UnityEngine.Transform SetBonesModsDiffer(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;

            SaveRow.modsDiffer.SetActive(value: true);
            if (SaveRow.modsDiffer.GetComponentsInChildren<UITextSkin>() is UITextSkin[] locationBoxTextSkins)
            {
                foreach (var locationBoxTextSkin in locationBoxTextSkins)
                {
                    if (locationBoxTextSkin.name == "tct"
                        || locationBoxTextSkin.gameObject.name == "tct")
                    {
                        locationBoxTextSkin.SetText(bonesInfo.ModsDiffer.ToString());
                        break;
                    }
                }
            }
            return SaveRow.modsDiffer.transform;
        }

        public static UnityEngine.Transform SetBonesDeleteButton(this SaveManagementRow SaveRow, BonesInfoData BonesData, bool PerformSetButtonActive = false)
        {
            var bonesInfo = BonesData.BonesInfo;
            SaveRow.deleteButton ??= new();
            SaveRow.deleteButton.RequireContext<NavigationContext>().parentContext = SaveRow.context.context;

            UITextSkin deleteButtonTextSkin = null;
            var deleteButtonImages = SaveRow.deleteButton.GetComponentsInChildren<Image>().IteratorSafe();
            if (SaveRow.deleteButton.GetComponentsInChildren<UITextSkin>() is UITextSkin[] deleteButtonTextSkins)
            {
                foreach (var textSkin in deleteButtonTextSkins)
                {
                    if (textSkin.name == "tct"
                        || textSkin.gameObject.name == "tct")
                    {
                        deleteButtonTextSkin = textSkin;
                        break;
                    }
                }
            }
            deleteButtonTextSkin?.SetText("{{y|cremate}}");

            if (!bonesInfo.IsCrematable)
            {
                if (deleteButtonTextSkin is not null)
                    deleteButtonTextSkin.color = UnityEngine.Color.clear;

                foreach (var deleteButtonImage in deleteButtonImages)
                    deleteButtonImage.color = UnityEngine.Color.clear;
            }

            if (PerformSetButtonActive)
            {
                SaveRow.deleteButton.context.disabled = !bonesInfo.IsCrematable;
                SaveRow.deleteButton.enabled = bonesInfo.IsCrematable;
                SaveRow.deleteButton.gameObject.SetActive(value: bonesInfo.IsCrematable);
            }

            deleteButtonTextSkin.Apply();
            return SaveRow.deleteButton.transform;
        }

        public static string Colored(this string Text, string Color)
            => Color != null
            ? Text?.WithColor(Color)
            : Text
            ;

        public static string ToLiteral(this string String, bool Quotes = false)
        {
            if (String.IsNullOrEmpty())
                return null;

            string output = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(String, false);

            if (Quotes)
                output = $"\"{output}\"";

            return output;
        }

        public enum TimespanUnits
        {
            Microsecond,
            Millisecond,
            Second,
            Minute,
            Hour,
            Day,
            Week,
            Month,
            Year,
        }

        public enum TimespanUnitFractionStyles
        {
            Trunc,
            Decimal,
            NextUnit,
            Cascade,
        }

        public static string MicrosecondsString(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            if (SmallestCascade < TimespanUnits.Microsecond)
                return null;

            string unit = TimespanUnits.Microsecond.ToString().ToLower();
            var microseconds = Duration.TotalMilliseconds / 1000;

            return Style switch
            {
                TimespanUnitFractionStyles.Decimal => Math.Round(microseconds, Math.Max(0, Digits)).Things(unit),
                _ => microseconds.Things(unit),
            };
        }

        public static string MillisecondsString(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            if (SmallestCascade < TimespanUnits.Millisecond)
                return null;

            string unit = TimespanUnits.Millisecond.ToString().ToLower();

            switch (Style)
            {
                case TimespanUnitFractionStyles.Decimal:
                    return Math.Round(Duration.TotalMilliseconds, Math.Max(0, Digits)).Things(unit);

                case TimespanUnitFractionStyles.NextUnit:
                    {
                        var microseconds = Duration.TotalMilliseconds / 1000;

                        string nextUnit = Duration.MicrosecondsString(Style: TimespanUnitFractionStyles.Trunc, SmallestCascade: SmallestCascade);
                        if (microseconds % 1000 != 0
                            && !nextUnit.IsNullOrEmpty())
                            nextUnit = $" and {nextUnit}";

                        return $"{Duration.Milliseconds.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Cascade:
                    {
                        string nextUnit = Duration.MicrosecondsString(Style: Style, SmallestCascade: SmallestCascade);

                        if (SmallestCascade == TimespanUnits.Microsecond)
                            nextUnit = $"and {nextUnit}";

                        nextUnit = $", {nextUnit}";

                        return $"{Duration.Milliseconds.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Trunc:
                default:
                    return Duration.Milliseconds.Things(unit);
            }
        }

        public static string SecondsString(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            if (SmallestCascade < TimespanUnits.Second)
                return null;

            string unit = TimespanUnits.Second.ToString().ToLower();
            switch (Style)
            {
                case TimespanUnitFractionStyles.Decimal:
                    return Math.Round(Duration.TotalSeconds, Math.Max(0, Digits)).Things(unit);

                case TimespanUnitFractionStyles.NextUnit:
                    {
                        string nextUnit = Duration.MillisecondsString(Style: TimespanUnitFractionStyles.Trunc, SmallestCascade: SmallestCascade);
                        if (Duration.Milliseconds % 1000 != 0
                            && !nextUnit.IsNullOrEmpty())
                            nextUnit = $" and {nextUnit}";

                        return $"{Duration.Seconds.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Cascade:
                    {
                        string nextUnit = Duration.MillisecondsString(Style: Style, SmallestCascade: SmallestCascade);

                        if (SmallestCascade == TimespanUnits.Millisecond)
                            nextUnit = $"and {nextUnit}";

                        nextUnit = $", {nextUnit}";

                        return $"{Duration.Seconds.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Trunc:
                default:
                    return Duration.Seconds.Things(unit);
            }
        }

        public static string MinutesString(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            if (SmallestCascade < TimespanUnits.Day)
                return null;

            string unit = TimespanUnits.Minute.ToString().ToLower();

            switch (Style)
            {
                case TimespanUnitFractionStyles.Decimal:
                    return Math.Round(Duration.TotalMinutes, Math.Max(0, Digits)).Things(unit);

                case TimespanUnitFractionStyles.NextUnit:
                    {
                        string nextUnit = Duration.SecondsString(Style: TimespanUnitFractionStyles.Trunc, SmallestCascade: SmallestCascade);
                        if (Duration.Seconds % 60 != 0
                            && !nextUnit.IsNullOrEmpty())
                            nextUnit = $" and {nextUnit}";

                        return $"{Duration.Minutes.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Cascade:
                    {
                        string nextUnit = Duration.SecondsString(Style: Style, SmallestCascade: SmallestCascade);

                        if (SmallestCascade == TimespanUnits.Second)
                            nextUnit = $"and {nextUnit}";

                        nextUnit = $", {nextUnit}";

                        return $"{Duration.Minutes.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Trunc:
                default:
                    return Duration.Minutes.Things(unit);
            }
        }

        public static string HoursString(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            if (SmallestCascade < TimespanUnits.Day)
                return null;

            string unit = TimespanUnits.Hour.ToString().ToLower();

            switch (Style)
            {
                case TimespanUnitFractionStyles.Decimal:
                    return Math.Round(Duration.TotalHours, Math.Max(0, Digits)).Things(unit);

                case TimespanUnitFractionStyles.NextUnit:
                    {
                        string nextUnit = Duration.MinutesString(Style: TimespanUnitFractionStyles.Trunc, SmallestCascade: SmallestCascade);
                        if (Duration.Minutes % 60 != 0
                            && !nextUnit.IsNullOrEmpty())
                            nextUnit = $" and {nextUnit}";

                        return $"{Duration.Hours.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Cascade:
                    {
                        string nextUnit = Duration.MinutesString(Style: Style, SmallestCascade: SmallestCascade);

                        if (SmallestCascade == TimespanUnits.Minute)
                            nextUnit = $"and {nextUnit}";

                        nextUnit = $", {nextUnit}";

                        return $"{Duration.Hours.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Trunc:
                default:
                    return Duration.Hours.Things(unit);
            }
        }

        public static string DaysString(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            if (SmallestCascade < TimespanUnits.Day)
                return null;

            string unit = TimespanUnits.Day.ToString().ToLower();

            switch (Style)
            {
                case TimespanUnitFractionStyles.Decimal:
                    return Math.Round(Duration.TotalDays, Math.Max(0, Digits)).Things(unit);

                case TimespanUnitFractionStyles.NextUnit:
                    {
                        string nextUnit = Duration.HoursString(Style: TimespanUnitFractionStyles.Trunc, SmallestCascade: SmallestCascade);
                        if (Duration.Hours % 45 != 0
                            && !nextUnit.IsNullOrEmpty())
                            nextUnit = $" and {nextUnit}";

                        return $"{Duration.Days.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Cascade:
                    {
                        string nextUnit = Duration.HoursString(Style: Style, SmallestCascade: SmallestCascade);

                        if (SmallestCascade == TimespanUnits.Hour)
                            nextUnit = $"and {nextUnit}";

                        nextUnit = $", {nextUnit}";

                        return $"{Duration.Days.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Trunc:
                default:
                    return Duration.Days.Things(unit);
            }
        }

        public static string WeeksString(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            if (SmallestCascade < TimespanUnits.Week)
                return null;

            string unit = TimespanUnits.Week.ToString().ToLower();

            switch (Style)
            {
                case TimespanUnitFractionStyles.Decimal:
                    return Math.Round(Duration.TotalDays, Math.Max(0, Digits)).Things(unit);

                case TimespanUnitFractionStyles.NextUnit:
                    {
                        string nextUnit = Duration.HoursString(Style: TimespanUnitFractionStyles.Trunc, SmallestCascade: SmallestCascade);
                        if (Duration.Hours % 45 != 0
                            && !nextUnit.IsNullOrEmpty())
                            nextUnit = $" and {nextUnit}";

                        return $"{Duration.Days.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Cascade:
                    {
                        string nextUnit = Duration.HoursString(Style: Style, SmallestCascade: SmallestCascade);

                        if (SmallestCascade == TimespanUnits.Hour)
                            nextUnit = $"and {nextUnit}";

                        nextUnit = $", {nextUnit}";

                        return $"{Duration.Days.Things(unit)}{nextUnit}";
                    }

                case TimespanUnitFractionStyles.Trunc:
                default:
                    return Duration.Days.Things(unit);
            }
        }

        public static string ValueUnits(
            this TimeSpan Duration,
            int Digits = 2,
            TimespanUnitFractionStyles Style = TimespanUnitFractionStyles.NextUnit,
            TimespanUnits SmallestUnit = TimespanUnits.Microsecond,
            TimespanUnits BiggestUnit = TimespanUnits.Year,
            TimespanUnits SmallestCascade = TimespanUnits.Microsecond
            )
        {
            string durationUnit = "year";
            double durationValue = Duration.TotalDays / 365.25;

            if (SmallestUnit > BiggestUnit)
                throw new ArgumentOutOfRangeException(nameof(BiggestUnit), $"Must be bigger than {nameof(SmallestUnit)}");

            if (Duration.TotalDays < Math.Floor(365.25))
            {
                durationUnit = "month";
                durationValue = Duration.TotalDays / (365.25 / 12);
            }
            if (Duration.TotalDays < Math.Floor(365.25 / 12))
            {
                durationUnit = "week";
                durationValue = Duration.TotalDays / 7;
            }
            if (Duration.TotalDays < 7)
            {
                durationUnit = "day";
                durationValue = Duration.TotalDays;
            }
            if (Duration.TotalDays < 1)
            {
                durationUnit = "hour";
                durationValue = Duration.TotalHours;
            }
            if (Duration.TotalHours < 1)
            {
                durationUnit = "minute";
                durationValue = Duration.TotalMinutes;
            }
            if (Duration.TotalMinutes < 1)
            {
                durationUnit = "second";
                durationValue = Duration.TotalSeconds;
            }
            if (Duration.TotalSeconds < 1)
            {
                durationUnit = "millisecond";
                durationValue = Duration.TotalMilliseconds;
            }
            if (Duration.TotalMilliseconds < 1)
            {
                durationUnit = "microsecond";
                durationValue = Duration.TotalMilliseconds / 1000;
            }
            if (Duration.TotalMilliseconds / 1000 < 0)
            {
                durationUnit = "microsecond";
                durationValue = 0;
            }
            return Math.Round(durationValue, Math.Max(0, Digits)).Things(durationUnit);
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

        public static string Indent(this int Amount, int Factor = 4, int MaxIndent = 4, bool NBSP = false)
        {
            if (!NBSP)
                return Amount > 0
                    ? " ".ThisManyTimes(Math.Min(Amount * Math.Max(1, Factor), MaxIndent * Factor))
                    : null
                    ;
            else
                return Amount > 0
                    ? $"=ud_nbsp:{Math.Min(Amount * Math.Max(1, Factor), MaxIndent * Factor)}=".StartReplace().ToString()
                    : null
                    ;
        }

        public static StringBuilder AppendIndent(this StringBuilder SB, int Amount = 0, int Factor = 4, int MaxIndent = 4, bool AsNBSP = false)
            => SB.Append(Amount.Indent(Factor, MaxIndent, AsNBSP))
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

        public static IEnumerable<T> Loggregate<T>(
            this IEnumerable<T> Source,
            Func<T, string> Proc = null,
            string Empty = null,
            Func<string, string> PostProc = null
            )
            => Utils.Loggregate(
                Source: Source,
                Proc: Proc,
                Empty: Empty,
                PostProc: PostProc)
            ;

        public static IEnumerable<KeyValuePair<GameObject, T>> GetObjectsAndPartsWithPartDescendedFrom<T>(this Zone Zone)
            where T : IPart
        {
            if (Zone == null)
                yield break;

            for (int i = 0; i < Zone.Height; i++)
            {
                for (int j = 0; j < Zone.Width; j++)
                {
                    if (Zone.Map[j][i] is not Cell cell
                        || cell.Objects is not ObjectRack objects)
                        continue;

                    for (int k = 0; k < objects.Count; k++)
                    {
                        if (objects[k] is not GameObject gameObject
                            || gameObject.GetPartDescendedFrom<T>() is not T tPart)
                            continue;

                        yield return new(gameObject, tPart);
                    }
                }
            }
        }

        public static T Log<T>(this T Message, T LogInsteadIfNull = default)
        {
            if (!(Message?.ToString()).IsNullOrEmpty()
                || !(LogInsteadIfNull?.ToString()).IsNullOrEmpty())
                Utils.Log(Message ?? LogInsteadIfNull);
            return Message;
        }

        public static HashSet<T> UnionWith<T>(this HashSet<T> Set, IEnumerable<IEnumerable<T>> Sets)
        {
            foreach (var set in Sets)
                Set.UnionWith(set);

            return Set;
        }

        public static HashSet<T> GetUnionOfSets<T>(this IEnumerable<IEnumerable<T>> Sets)
            => new HashSet<T>().UnionWith(Sets)
            ;

        public static HashSet<T> IntersectWithUnless<T>(
            this HashSet<T> Set,
            IEnumerable<T> Other,
            Predicate<IEnumerable<T>> Case
            )
        {
            if (Case?.Invoke(Other) is not true)
                Set.IntersectWith(Other);
            return Set;
        }

        public static HashSet<T> IntersectWithUnlessEmptyOrNull<T>(
            this HashSet<T> Set,
            IEnumerable<T> Other
            )
            => Set.IntersectWithUnless(
                Other: Other,
                Case: o => o.IsNullOrEmpty())
            ;

        public static bool IsTile(this string Tile)
        {
            try
            {
                return Utils.TileExistsAsync(Tile).WaitResult();
            }
            catch (Exception x)
            {
                Utils.Error(nameof(IsTile), x);
            }
            try
            {
                return Utils.TileExists(Tile);
            }
            catch (Exception x)
            {
                Utils.Error(nameof(IsTile), x);
            }
            return false;
        }

        public static string ThisThese(this GameObject Object)
            => Object.IsPlural
            ? $"These"
            : $"This"
            ;

        public static string ThisTheseDescriptiveCategory(this GameObject Object)
        {
            string noun = Object.GetDescriptiveCategory();

            if (Object.IsPlural)
                noun = Grammar.Pluralize(noun);

            return $"{Object.ThisThese()} {noun}";
        }

        public static string IndefiniteArticleDescriptiveCategory(
            this GameObject Object,
            bool Capitalize = false,
            string Adjective = null
            )
        {
            string noun = Object.GetDescriptiveCategory();

            string indefiniteArticle;
            if (Object.IsPlural)
            {
                noun = noun.Pluralize();
                indefiniteArticle = "some";
            }
            else
            if (!Grammar.IndefiniteArticleShouldBeAn(Adjective ?? noun))
                indefiniteArticle = "a";
            else
                indefiniteArticle = "an";

            Adjective += " ";
            if (!Adjective.StartsWith(" "))
                Adjective = $" {Adjective}";

            string output = $"{indefiniteArticle}{Adjective}{noun}";
            if (Capitalize)
                output = output.Capitalize();

            return output;
        }

        public static string IndefiniteArticle(this string Word, bool Capitalize = false)
        {
            string indefiniteArticle;
            if (!Grammar.IndefiniteArticleShouldBeAn(Word))
                indefiniteArticle = "a";
            else
                indefiniteArticle = "an";

            if (Capitalize)
                indefiniteArticle = indefiniteArticle.Capitalize();

            return indefiniteArticle;
        }

        public static void PerformActionRecursively(this GameObject Object, Action<GameObject> Action, int Depth = 0)
        {
            Action?.Invoke(Object);

            if (Object.GetInventoryAndEquipmentAndDefaultEquipment() is List<GameObject> inventoryObjects)
                for (int i = 0; i < inventoryObjects.Count; i++)
                    inventoryObjects[i].PerformActionRecursively(Action, Depth + 1);

            if (Object.GetInstalledCybernetics() is List<GameObject> installedCybernetics)
                for (int i = 0; i < installedCybernetics.Count; i++)
                    installedCybernetics[i].PerformActionRecursively(Action, Depth + 1);

            if (Object.GetContents() is List<GameObject> contentsObject)
                for (int i = 0; i < contentsObject.Count; i++)
                    contentsObject[i].PerformActionRecursively(Action, Depth + 1);
        }

        public static void ApplyRegistrar(this GameObject Object, bool Active = false, bool Recursive = true, int Depth = 0)
        {
            //Utils.Log($"{Depth.Indent()}{nameof(ApplyRegistrar)}: {Object?.DebugName ?? "NO_OBJECT??"}");

            if (Recursive)
            {
                if (Object.GetInventoryAndEquipmentAndDefaultEquipment() is List<GameObject> inventoryObjects)
                    for (int i = 0; i < inventoryObjects.Count; i++)
                        inventoryObjects[i].ApplyRegistrar(Active, Recursive, Depth + 1);

                if (Object.GetInstalledCybernetics() is List<GameObject> installedCybernetics)
                    for (int i = 0; i < installedCybernetics.Count; i++)
                        installedCybernetics[i].ApplyRegistrar(Active, Recursive, Depth + 1);

                if (Object.GetContents() is List<GameObject> contentsObject)
                    for (int i = 0; i < contentsObject.Count; i++)
                        contentsObject[i].ApplyRegistrar(Active, Recursive, Depth + 1);
            }

            if (Object?.PartsList is PartRack partsList)
            {
                for (int i = 0; i < partsList.Count; i++)
                    if (partsList[i] is IPart part)
                        part.ApplyRegistrar(Object, Active);
            }

            if (Object?._Effects is EffectRack _effects)
            {
                for (int i = 0; i < _effects.Count; i++)
                    if (_effects[i] is Effect effect)
                        effect.ApplyRegistrar(Object, Active);
            }
        }

        public static string GetBonesID(this GameObject Object, string Default)
            => Object != null
                && Object.GetFirstPartDescendedFrom<UD_Bones_BaseLunarPart>() is UD_Bones_BaseLunarPart baseLunarPart
            ? baseLunarPart.BonesID
            : Default
            ;

        public static string GetBonesID(this GameObject Object)
            => Object.GetBonesID(The.Game?.GameID)
            ;

        public static bool IsLunarRegent(this GameObject Object, string FromBonesID = null)
            => Object != null
            && Object.TryGetPart(out UD_Bones_LunarRegent lunarRegent)
            && (FromBonesID.IsNullOrEmpty()
                || lunarRegent.BonesID.EqualsNoCase(FromBonesID))
            ;

        public static bool IsLunarCourtier(this GameObject Object, string FromBonesID = null)
            => Object != null
            && Object.TryGetPart(out UD_Bones_LunarCourtier lunarCourtier)
            && (FromBonesID.IsNullOrEmpty()
                || lunarCourtier.BonesID.EqualsNoCase(FromBonesID))
            ;

        public static void TryFeverWarp(
            this GameObject BonesObject,
            SaveBonesInfo BonesInfo = null,
            bool Recursive = true,
            int Depth = 0
            )
        {
            //Utils.Log($"{Depth.Indent()}{nameof(FeverWarp)}: {BonesObject?.DebugName ?? "NO_OBJECT??"}");

            if (Recursive)
            {
                if (BonesObject.GetInventoryAndEquipmentAndDefaultEquipment() is List<GameObject> inventoryObjects)
                    for (int i = 0; i < inventoryObjects.Count; i++)
                        inventoryObjects[i].TryFeverWarp(BonesInfo, Depth: Depth + 1);

                if (BonesObject.GetInstalledCybernetics() is List<GameObject> installedCybernetics)
                    for (int i = 0; i < installedCybernetics.Count; i++)
                        installedCybernetics[i].TryFeverWarp(BonesInfo, Depth: Depth + 1);

                if (BonesObject.GetContents() is List<GameObject> contentsObject)
                    for (int i = 0; i < contentsObject.Count; i++)
                        contentsObject[i].TryFeverWarp(BonesInfo, Depth: Depth + 1);
            }

            if (BonesInfo != null)
            {
                if (BonesObject.NeedsFeverWarped(
                    TileOnly: out bool tileOnly,
                    HasBadWord: out bool hasBadWord,
                    BadWordName: out BadDisplayName badWordName,
                    BadWordDesc: out bool badWordDesc,
                    BonesInfo: BonesInfo))
                {
                    if (BonesObject?.HasPart<UD_Bones_FeverWarped>() is false)
                        BonesObject.AddPart(
                            P: new UD_Bones_FeverWarped(TileOnly: tileOnly, HasBadWord: hasBadWord)
                            {
                                BadWordName = badWordName,
                                BadWordDesc = badWordDesc,
                                Persists = true,
                            }.OverrideBonesIDTyped<UD_Bones_FeverWarped>(BonesInfo?.ID));
                }
                else
                {
                    try
                    {
                        _ = GameObjectFactory.Factory.Blueprints[BonesObject.Blueprint];
                    }
                    catch
                    {
                        if (BonesObject?.HasPart<UD_Bones_FeverWarped>() is false)
                            BonesObject.AddPart(
                                P: new UD_Bones_FeverWarped(TileOnly: false, HasBadWord: false)
                                {
                                    Persists = true,
                                }.OverrideBonesIDTyped<UD_Bones_FeverWarped>(BonesInfo?.ID));
                    }
                }
            }
        }

        public static bool NeedsFeverWarped(
            this GameObject BonesObject,
            out bool TileOnly,
            out bool HasBadWord,
            out BadDisplayName BadWordName,
            out bool BadWordDesc,
            SaveBonesInfo BonesInfo = null
            )
        {
            TileOnly = false;
            BadWordName = null;
            BadWordDesc = false;
            HasBadWord = false;

            if (BonesObject == null)
                return false;

            HasBadWord = (BonesInfo?.IsDownloaded is true)
                && (BonesObject.HasBadWord(out BadWordName, out BadWordDesc, Options.ModerationMinimumSeverityLevel)
                    || BonesObject.GetStringProperty(
                            Name: $"{nameof(UD_Bones_FeverWarped)}::{nameof(HasBadWord)}",
                            Default: $"{false}")
                        .EqualsNoCase($"{true}"));

            if (!BonesObject.IsLunarRegent(BonesInfo?.ID)
                || HasBadWord)
            {
                if (BonesObject.TryGetPart(out UD_Bones_FeverWarped feverWarped))
                {
                    feverWarped.HasBadWord = HasBadWord;
                    feverWarped.BadWordName = BadWordName;
                    feverWarped.BadWordDesc = BadWordDesc;
                    feverWarped.BadWordSeverityOption = Options.ModerationMinimumSeverityLevel;
                    TileOnly = !HasBadWord && feverWarped.IsTileOnly();
                    return false;
                }

                if (BonesObject.GetStringProperty(nameof(UD_Bones_FeverWarped), $"{false}").EqualsNoCase($"{true}"))
                {
                    TileOnly = !HasBadWord && BonesObject.GetStringProperty($"{nameof(UD_Bones_FeverWarped)}::{nameof(TileOnly)}", $"{false}").EqualsNoCase($"{true}");
                    return true;
                }

                bool blueprintExists = true;

                SerializationExtensions.OptionallyPerformWithoutMetrics(() => blueprintExists = GameObjectFactory.Factory.HasBlueprint(BonesObject.Blueprint));

                if (BonesObject.GetTile() is string bonesTile
                    && !bonesTile.IsTile())
                {
                    TileOnly = !HasBadWord && blueprintExists;
                    return true;
                }

                if (!blueprintExists)
                    return true;

                if (HasBadWord)
                    return true;
            }
            return false;
        }

        public static void SetEquipmentFrameColors(this GameObject GameObject, string TopLeft_Left_Right_BottomRight = null)
            => GameObject.SetStringProperty(Const.EQ_FRAME_COLORS, TopLeft_Left_Right_BottomRight, RemoveIfNull: true)
            ;

        public static string GetEquipmentFrameColors(this GameObject GameObject, string Default = null)
            => GameObject.GetStringProperty(Const.EQ_FRAME_COLORS, Default)
            ;

        public static bool IsEquipment(this GameObject GameObject)
        {
            if (GameObject.GetBlueprint()?.InheritsFromSafe("Item") is not true)
                return false;

            if (GameObject.HasPart<Armor>())
                return true;

            if (GameObject.HasPart<CyberneticsBaseItem>())
                return true;

            if (GameObject.HasPart<MissileWeapon>())
                return true;

            if (GameObject.TryGetPart(out MeleeWeapon mw)
                && !mw.IsImprovisedWeapon())
                return true;

            if (GameObject.HasPart<ThrownWeapon>()
                && !GameObject.HasTagOrProperty("HideThrownWeaponPerformance"))
                return true;

            if (GameObject.HasTagOrProperty("UsesSlots"))
                return true;

            return false;
        }

        public static int NegSafeModulo(this int Number, int Value)
            => (Value + (Number % Value)) % Value;

        public static IEnumerable<Range> GetRanges(this IEnumerable<string> Source, int Offset = 0, int? MaxLengthOverride = null)
        {
            if (Source.IsNullOrEmpty())
                yield break;

            int sourceLength = Source.Aggregate(0, (a, n) => a + n.Length);
            int maxLength = Math.Min(MaxLengthOverride ?? sourceLength, sourceLength);
            int currentPos = Offset;
            int finalPos = maxLength - 1;
            foreach (string element in Source)
            {
                if (currentPos >= finalPos)
                    break;

                int elementEndPos = Math.Min(currentPos + element.Length, finalPos);
                yield return new(currentPos, elementEndPos);

                currentPos = elementEndPos;
            }
        }

        public static bool IsMad(this GameObject GameObject)
            => GameObject
                ?.GetPropertyOrTag(Const.IS_MAD_PROP, $"{false}")
                ?.EqualsNoCase($"{true}") is true;

        public static void SetMad(this GameObject GameObject, bool? Value)
            => GameObject?.SetStringProperty(Const.IS_MAD_PROP, Value?.ToString());

        public static bool HasLunarRegentBonesID(this GameObject Object, string BonesID = null)
        {
            if (Object.GetPart<UD_Bones_LunarRegent>()?.BonesID is not string bonesID)
                return false;

            return BonesID == null
                || bonesID == BonesID;
        }

        public static T WaitResult<T>(this Task<T> Task)
        {
            if (Task == null)
                return default;

            Task.Wait();

            return Task.Result;
        }

        public static bool DirectoryExistsSafe(this string Path)
            => !Path.IsNullOrEmpty()
            && Directory.Exists(Path)
            ;

        public static bool ContainsAny(this string String, params string[] Strings)
            => Strings.IsNullOrEmpty()
            ? String.IsNullOrEmpty()
            : Strings.Any(s => String.Contains(s))
            ;

        public static bool ContainsAny(this string String, IEnumerable<string> Strings)
            => Strings.IsNullOrEmpty()
            ? String.IsNullOrEmpty()
            : Strings.Any(s => String.Contains(s))
            ;

        public static bool ContainsNoCase(this string String, string Value)
        {
            if (Value.IsNullOrEmpty())
                return String.IsNullOrEmpty();

            if (String.IsNullOrEmpty())
                return Value.IsNullOrEmpty();

            if (Value.Length >= String.Length)
                return String.EqualsNoCase(Value);

            int valLen = Value.Length;
            int strLen = String.Length;
            int end = strLen - valLen;
            bool contains = false;
            for (int i = 0; i < end; i++)
            {
                int toLen = i + valLen - 1;
                if (toLen > String.Length - 1)
                    break;

                Utils.Log($"{String}, {Value}; {i}: {String[i..toLen]}");
                if (String[i..toLen].EqualsNoCase(Value))
                    contains = true;
            }
            return contains;
        }

        public static bool InheritsFrom(
            [NotNullWhen(true)] this Type Type,
            [NotNullWhen(true)] Type OtherType,
            bool IncludeSelf = true)
            => Type != null
            && OtherType != null
            && ((IncludeSelf
                    && Type == OtherType)
                || OtherType.IsSubclassOf(Type)
                || Type.IsAssignableFrom(OtherType)
                || (Type.YieldInheritedTypes().ToList() is List<Type> inheritedTypes
                    && inheritedTypes.Contains(OtherType)));

        public static bool TryFindLunarRegent(this Zone Z, string BonesID, out GameObject LunarRegent, out UD_Bones_LunarRegent LunarRegentPart)
        {
            LunarRegentPart = null;
            UD_Bones_LunarRegent lunarRegentPart = null;
            LunarRegent = Z.GetObjects(go => (lunarRegentPart = go.GetPart<UD_Bones_LunarRegent>())?.BonesID == BonesID).FirstOrDefault();
            LunarRegentPart = lunarRegentPart;
            return LunarRegent != null
                && LunarRegentPart != null;
        }

        public static bool TryFindLunarRegent(this Zone Z, string BonesID, out GameObject LunarRegent)
            => Z.TryFindLunarRegent(BonesID, out LunarRegent, out _)
            ;

        public static bool TryEnsureObject(this GameObjectReference GameObjectReference, out GameObject Object)
        {
            if (GameObjectReference == null)
            {
                Object = null;
                return false;
            }
            if (!GameObject.Validate(ref GameObjectReference.Object))
            {
                if (GameObject.FindByID(GameObjectReference.ID) is GameObject foundObject)
                    GameObjectReference.Set(foundObject);
                else
                    GameObjectReference.Clear();
            }
            return (Object = GameObjectReference.Object) != null;
        }

        public static async Task<byte[]> ReadAllBytesAsync(this System.IO.Stream Stream)
        {
            if (Stream is System.IO.MemoryStream inMemoryStream)
                return inMemoryStream.ToArray();

            using (var outStream = new System.IO.MemoryStream())
            {
                await Stream.CopyToAsync(outStream);
                return outStream.ToArray();
            }
        }

        public static byte[] ReadAllBytes(this System.IO.Stream Stream)
            => Stream.ReadAllBytesAsync().WaitResult()
            ;

        public static async Task<byte[]> ReadAllBytesAsync(this System.IO.StreamReader StreamReader)
            => await StreamReader.BaseStream.ReadAllBytesAsync()
            ;

        public static byte[] ReadAllBytes(this System.IO.StreamReader StreamReader)
            => StreamReader.ReadAllBytesAsync().WaitResult()
            ;

        public static void SplitOut(this string String, string Delimiter, out string Key, out string Value)
        {
            Key = String;
            Value = null;

            if (String.IsNullOrEmpty())
                return;

            if (Delimiter.IsNullOrEmpty())
                return;

            if (!String.Contains(Delimiter))
                return;

            var pieces = String.Split(Delimiter);

            Key = pieces[0];

            var latterPieces = pieces[1..];
            Value = latterPieces.Aggregate("", (a, n) => Utils.DelimitedAggregator(a, n, Delimiter));
        }

        public static bool TrySplitOut(this string String, string Delimiter, out string Key, out string Value)
        {
            String.SplitOut(Delimiter, out Key, out Value);
            return !Key.IsNullOrEmpty()
                && !Value.IsNullOrEmpty()
                ;
        }

        public static bool IsEmptyOrDefault(this Guid Guid)
            => Guid == default
            || Guid == Guid.Empty
            ;

        public static StringBuilder AppendPair(this StringBuilder SB, object Key, object Value)
            => SB.Append(Key?.ToString()).Append(": ").Append(Value?.ToString())
            ;

        public static char GetNextHotKey(
            this IEnumerable<char> Source,
            IEnumerable<char> Excluding = null,
            char StartAt = 'a',
            char FinishAt = 'z'
            )
        {
            char lastHotkey = Source.LastOrDefault(c => Excluding?.Contains(c) is not true);

            if (lastHotkey == default)
                return StartAt;

            if (lastHotkey != ' ')
            {
                if (lastHotkey == '\0'
                    || lastHotkey == default)
                    lastHotkey = StartAt;

                while (Source.Contains(lastHotkey)
                    && lastHotkey <= FinishAt)
                    lastHotkey++;

                if (lastHotkey > FinishAt)
                    lastHotkey = ' ';
            }
            return lastHotkey;
        }

        public static int FirstIndexOrDefault<T>(
            this IEnumerable<T> Source,
            Predicate<T> OnBasis = null,
            int Default = 0
            )
        {
            int index = -1;
            foreach (T element in Source.IteratorSafe())
            {
                index++;
                if (OnBasis?.Invoke(element) is not false)
                    return index;
            }
            return Default;
        }

        public static async Task<TResult> AwaitResultIfNotIsCompletedSuccessfully<TResult>(this Task<TResult> ResultTask)
            => (ResultTask?.IsCompletedSuccessfully) is true
            ? ResultTask.Result
            : await ResultTask
            ;

        public static string GetCheckbox(this bool Value)
            => $"[{(Value ? "■" : " ")}]";

        public static string GetCheckboxText(this bool Value, string Label)
            => $"{Value.GetCheckbox()} {Label}";

        public static bool IsTwixt(this int Value, int LowerInclusive, int UpperExclusive)
            => Value >= LowerInclusive
            && Value < UpperExclusive
            ;

        public static ulong ToUInt64<T>(this T Value)
            where T : Enum
            => Convert.GetTypeCode(Value) switch
            {
                TypeCode.SByte or
                TypeCode.Int16 or
                TypeCode.Int32 or
                TypeCode.Int64 => (ulong)Convert.ToInt64(Value, CultureInfo.InvariantCulture),

                TypeCode.Boolean or
                TypeCode.Char or
                TypeCode.Byte or
                TypeCode.UInt16 or
                TypeCode.UInt32 or
                TypeCode.UInt64 => Convert.ToUInt64(Value, CultureInfo.InvariantCulture),

                _ => throw new InvalidOperationException("Unknown enum type."),
            };

        public static bool IsTwixtInclusive<T>(this T Value, T Lower, T Upper)
            where T : Enum
            => Value.ToUInt64() >= Lower.ToUInt64()
            && Value.ToUInt64() <= Upper.ToUInt64()
            ;

        public static bool? ToNullableBool(this UIUtils.CascadableResult Value)
        {
            if (Value <= UIUtils.CascadableResult.Continue)
                return true;

            if (Value <= UIUtils.CascadableResult.Back)
                return false;

            return null;
        }

        public static bool ToBool(this UIUtils.CascadableResult Value)
            => Value <= UIUtils.CascadableResult.Continue
            ;

        public static bool IsContinue(this UIUtils.CascadableResult Value)
            => Value.IsTwixtInclusive(UIUtils.CascadableResult.Continue, UIUtils.CascadableResult.Continue)
            ;

        public static bool IsBack(this UIUtils.CascadableResult Value)
            => Value.IsTwixtInclusive(UIUtils.CascadableResult.Back, UIUtils.CascadableResult.BackSilent)
            ;

        public static bool IsCancel(this UIUtils.CascadableResult Value)
            => Value >= UIUtils.CascadableResult.Cancel
            ;

        public static bool IsSilent(this UIUtils.CascadableResult Value)
            => ((int)Value % 2) == ((int)UIUtils.CascadableResult.BackSilent % 2)
            ;

        public static UIUtils.CascadableResult ToCascadableResult(this bool? Value, bool Silent)
        {
            if (Value.GetValueOrDefault())
                return UIUtils.CascadableResult.Continue;

            var result = Value.HasValue
                ? UIUtils.CascadableResult.Back
                : UIUtils.CascadableResult.Cancel
                ;

            if (Silent)
                result++;

            return result;
        }

        public static StringBuilder AppendLineEnd(this StringBuilder SB)
            => SB.AppendLine().Append("=ud_nbsp=".StartReplace().ToString())
            ;

        public static StringBuilder AppendRule(this StringBuilder SB, object Value)
            => Value != null
            ? SB.AppendColored("rules", Value.ToString())
            : SB
            ;

        public static StringBuilder AppendQuote(this StringBuilder SB, object Value)
            => SB.Append("\"").Append(Value).Append("\"")
            ;

        public static StringBuilder AppendBullet(
            this StringBuilder SB,
            string Color = null,
            string Bullet = "\u0007"
            )
        {
            if (Color.IsNullOrEmpty())
                SB.Append(Bullet);
            else
                SB.AppendColored(Color, Bullet);

            return SB.Append(" ");
        }

        public static StringBuilder AppendBulletLine(
            this StringBuilder SB,
            string Color = null,
            string Bullet = "\u0007"
            )
            => SB.AppendLine().AppendBullet(Color, Bullet);

        public static StringBuilder AppendBonesReportedObject(this StringBuilder SB, ObjectReportDetails ReportedObject, int Indent = 0)
            => SB.AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.Blueprint), ReportedObject.Blueprint)
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.SerializedBaseID), ReportedObject.SerializedBaseID)
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.DisplayName), "[redacted as a precaution]")
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.IsTheLunarRegent), ReportedObject.IsLunarRegent)
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.IsLunarRegent), ReportedObject.IsLunarRegent)
            ;

        public static StringBuilder AppendBonesReport(this StringBuilder SB, Report Report, string Heading = null)
        {
            if (!Heading.IsNullOrEmpty())
                SB.Append(Heading)
                    .AppendLine();

            if (Report == null)
                SB.Append("Report is null...");
            else
            {
                SB.AppendPair(nameof(Report.BonesID), Report.BonesID)
                    .AppendLine().AppendPair(nameof(Report.Blocked), Report.Blocked)
                    .AppendLine().AppendPair(nameof(Report.Type), Report.Type);

                SB.AppendLine();
                if (Report.IsSpecificObject)
                {
                    SB.Append(nameof(Report.ObjectDetails)).Append(":")
                        .AppendLine().AppendBonesReportedObject(Report.ObjectDetails, Indent: 1);
                }
                else
                    SB.AppendPair("For Specific Object", Report.IsSpecificObject);

                SB.AppendLine().Append(nameof(Report.Description)).Append(":")
                    .AppendLine();

                if (Report.Description.IsNullOrEmpty())
                    SB.AppendIndent(1, AsNBSP: true).Append("none");
                else
                    SB.AppendQuote(Report.Description);
            }
            return SB;
        }

        public static IEnumerable<string> GetNotResistedDamageTypes(this GameObject Object)
        {
            foreach ((var statName, var stat) in Object?.Statistics.IteratorSafe())
            {
                if (statName.Contains("Resistance")
                    && stat.Value < 25)
                {
                    yield return statName[..^("Resistance".Length)];
                }
            }

            if ((Object?.FireEvent("ApplyPoison") is true)
                && (Object?.FireEvent("CanApplyPoison") is true))
                yield return "Poison";

            yield return "Bludgeoning";

            yield return "Plasma";
            yield return "Light";
            yield return "Laser";
            yield return "Bleeding";
            yield return "Asphyxiation";
            yield return "Metabolic";
            yield return "Drain";
            yield return "Psionic";
            yield return "Mental";
            yield return "Thorns";
            yield return "Collision";
            yield return "Bite";
            yield return "Illusion";
            yield return "reflected";
        }

        public static HostCollection FirstWithHostMatching(this Rack<HostCollection> Hosts, Predicate<Host> Condition)
            => Hosts?.FirstOrDefault(hc => hc.Any(Condition.Invoke))
            ;

        public static Host FirstHostMatching(this Rack<HostCollection> Hosts, Predicate<Host> Condition)
            => Hosts?.FirstWithHostMatching(Condition.Invoke)?.FirstOrDefault(Condition.Invoke)
            ;

        public static string TimeAgo(this DateTime DateTime, string PostFix = null)
        {
            var timeAgo = DateTime.Now.ToUniversalTime() - DateTime.ToUniversalTime();

            if (timeAgo.Ticks <= 0)
                return "just now";

            return $"{timeAgo.ValueUnits()}{(!PostFix.IsNullOrEmpty() ? $" {PostFix}" : null)}";
        }

        public static string DateTimeString(this DateTime Time, bool LongDate, bool LongTime)
            => $"{(LongDate ? Time.ToLongDateString() : Time.ToShortDateString())} {(LongTime ? Time.ToLongTimeString() : Time.ToShortTimeString())}"
            ;

        public static string ShortDateTimeString(this DateTime Time)
            => Time.DateTimeString(false, false)
            ;

        public static string LongDateTimeString(this DateTime Time)
            => Time.DateTimeString(true, true)
            ;

        public static string Timestamp(this DateTime Time)
            => Time.ToUniversalTime().ToString("u")
            ;

        public static List<Cell> GetEmptyReachableCellsNInFromEdge(this Zone Z, int N)
            => Z.GetEmptyReachableCells(c => Utils.CellIsNInFromEdge(c, Z, N))
            ;

        public static List<Cell> GetEmptyCellsNInFromEdge(this Zone Z, int N)
            => Z.GetEmptyCells(c => Utils.CellIsNInFromEdge(c, Z, N))
            ;

        public static List<GameObjectBlueprint> SafelyGetBlueprintsInheritingFrom(
            this GameObjectFactory Factory,
            string Name,
            bool ExcludeBase = true,
            bool IncludeSelf = false
            )
        {
            List<GameObjectBlueprint> outputList = new();
            foreach (GameObjectBlueprint blueprint in Factory.BlueprintList)
                if (blueprint.InheritsFromSafe(Name, IncludeSelf)
                    && (!ExcludeBase
                        || !blueprint.IsBaseBlueprint()))
                    outputList.Add(blueprint);

            return outputList;
        }
        public static List<string> InheritanceRoots => new()
        {
            nameof(Object),
            "SultanMuralController",
        };
        public static bool InheritsFromSafe(
            this GameObjectBlueprint GameObjectBlueprint,
            string what,
            bool IncludeSelf = true
            )
        {
            if (IncludeSelf
                && GameObjectBlueprint?.Name == what)
                return true;

            string parentBlueprint = GameObjectBlueprint.Inherits;
            while (!parentBlueprint.IsNullOrEmpty())
            {
                if (parentBlueprint == what)
                    return true;

                string inherits = parentBlueprint;
                parentBlueprint = GameObjectFactory.Factory?.GetBlueprintIfExists(parentBlueprint)?.Inherits;
                if (parentBlueprint.IsNullOrEmpty()
                    && !InheritanceRoots.Contains(inherits))
                {
                    Utils.Warn($"{nameof(Extensions)}.{nameof(InheritsFromSafe)}(\"{what}\"):" +
                        $" bluprint ancestor \"{inherits}\" does not exist in blueprint list." +
                        $" The first mention of this blueprint in this log should reveal the mod with this inheritance issue.");
                }
            }
            return false;
        }

        public static bool AdjustCyberneticsLicensePoints(this GameObject Creature, int Amount)
        {
            if (Creature?.IsTrueKin() is not true)
                return false;

            Creature.ModIntProperty("CyberneticsLicenses", Amount);
            return true;
        }

        public static int GetCyberneticsLicensePoints(this GameObject Creature)
            => Creature.GetIntProperty("CyberneticsLicenses")
            ;

        public static int GetFreeCyberneticsLicensePoints(this GameObject Creature)
            => Creature.GetIntProperty("FreeCyberneticsLicenses")
            ;

        public static int GetNonFreeCyberneticsLicensePoints(this GameObject Creature)
            => Creature.GetCyberneticsLicensePoints() - Creature.GetFreeCyberneticsLicensePoints()
            ;

        public static int GetUsedCyberneticsLicensePoints(this GameObject Creature)
        {
            int usedPoints = 0;
            Creature?.Body?.SafeForeachInstalledCybernetics(delegate (GameObject implant)
            {
                if (implant.TryGetPart(out CyberneticsBaseItem implantPart))
                    usedPoints += implantPart.Cost;
            });
            return usedPoints;
        }

        public static int GetCyberneticsLicensePointUpgradeCost(this GameObject Creature)
            => Creature.GetNonFreeCyberneticsLicensePoints() switch
            {
                >= 24 => 4,
                >= 16 => 3,
                >= 8 => 2,
                _ => 1,
            }
            ;

        public static void AdjustCyberneticsLicensePointsFromWedges(this GameObject Creature, int Amount, out int Remaining)
        {
            if (Creature?.IsTrueKin() is true)
            {
                int upgradeCost = Creature.GetCyberneticsLicensePointUpgradeCost();
                while (Amount >= upgradeCost)
                {
                    if (!Creature.AdjustCyberneticsLicensePoints(1))
                        break;

                    Amount -= upgradeCost;
                    upgradeCost = Creature.GetCyberneticsLicensePointUpgradeCost();
                }
            }
            Remaining = Amount;
        }

        public static void AdjustCyberneticsLicensePointsFromWedges(this GameObject Creature, int Amount)
            => Creature.AdjustCyberneticsLicensePointsFromWedges(Amount, out _)
            ;

        public static void RemoveAll<T>(this ScopeDisposedList<T> Source, Predicate<T> Where)
        {
            while (Source.FirstOrDefault(t => Where?.Invoke(t) is not false) is T element)
                Source.Remove(element);
        }

        public static bool GainAP(this GameObject Creatrue, int Amount)
        {
            if (Creatrue != null
                && Creatrue.Statistics.TryGetValue("AP", out var aPStat))
            {
                aPStat.BaseValue += Amount;
                Creatrue.FireEvent(Event.New("GainedAP", "Amount", Amount));
                return true;
            }
            return false;
        }

        public static string ThisThese(this int Amount)
            => Amount == 1
            ? "this"
            : "these"
            ;

        public static StringBuilder AppendThisThese(this StringBuilder SB, int Amount, bool Capitalize = false)
            => SB.Append(Capitalize ? Amount.ThisThese().Capitalize() : Amount.ThisThese())
            ;

        public static StringBuilder AppendThings(this StringBuilder SB, int Amount, string Thing = "thing")
        {
            if (Amount != 1)
                Thing = Thing.Pluralize();

            return SB.Append(Thing);
        }

        public static StringBuilder AppendUnavailableMods(this StringBuilder SB, IEnumerable<string> UnavailableMods)
        {
            int unavailableCount = UnavailableMods.Count();
            return UnavailableMods
                .Select(ModManager.GetModTitle)
                    .Aggregate(
                        seed: SB.AppendLine().AppendThisThese(unavailableCount, true).Append(" ").Append(unavailableCount).Append(" ").AppendColored("red", "unavailable")
                            .AppendThings(unavailableCount, " mod").Append(" are ").AppendColored("red", "enabled").Append(" in this bones file:"),
                        func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("red", n))
                    .AppendLine().AppendColored("w", "You won't be able to enable these mods as they are missing outright.")
                    ;
        }

        public static int Fibonacci(this int N)
            => (N > 1)
            ? Fibonacci(N - 1) + Fibonacci(N - 2)
            : N
            ;

        public static string GetBonesWorldZoneID(this Zone Z)
            => ZoneID.Assemble(Const.BONES_WORLD, Z.wX, Z.wY, Z.X, Z.Y, Z.Z)
            ;

        public static string GetJoppaWorldZoneID(this Zone Z)
            => ZoneID.Assemble("JoppaWorld", Z.wX, Z.wY, Z.X, Z.Y, Z.Z)
            ;

        public static void SendJoppaZoneToBonesWorld(this Zone Z)
            => Z.ZoneID = Z.GetBonesWorldZoneID()
            ;

        public static void SendBonesZoneToJoppaWorld(this Zone Z)
            => Z.ZoneID = Z.GetJoppaWorldZoneID()
            ;

        public static void ToggleBonesWorld(this Zone Z)
        {
            if (Z.ZoneWorld != Const.BONES_WORLD)
                Z.SendJoppaZoneToBonesWorld();
            else
                Z.SendBonesZoneToJoppaWorld();
        }

        public static string ColorIfMatches<T>(this T Value, T Target, string MatchColor, string NotMatchColor = null)
            where T : Enum
        {
            if (Equals(Value, Target))
                return Value.ToString().WithColor(MatchColor);
            else
                return Value.ToString().WithColor(NotMatchColor);
        }

        public static void AddRange<T>(this ICollection<T> Source, IEnumerable<T> Range, Predicate<T> Where)
        {
            if (Source == null
                || Range.IsNullOrEmpty())
                return;

            foreach (var element in Range)
                if (Where?.Invoke(element) is not false)
                    Source.Add(element);
        }

        public static void AddRange<T>(this ICollection<T> Source, IEnumerable<T> Range)
            => Source.AddRange(Range, null)
            ;

        public static string FeverWarped(this string Text, bool DoShaderReplace = true)
            => UD_Bones_FeverWarped.FeverWarpText(Text, DoShaderReplace)
            ;

        public static bool TryGetSelectionElementsAtIndex<TDataElement, TComponent>(
            this FrameworkScroller ScrollChild,
            int Index,
            IList<FrameworkDataElement> Choices,
            out FrameworkUnityScrollChild SelectionClone,
            out TDataElement DataElement,
            out TComponent Component
            )
            where TDataElement : FrameworkDataElement
            where TComponent : IFrameworkControl
        {
            SelectionClone = null;
            DataElement = null;
            Component = default;
            try
            {
                if (ScrollChild?.selectionClones.IsNullOrEmpty() is not false)
                    return false;

                if (Index >= ScrollChild.selectionClones.Count)
                    return false;

                if (Choices.IsNullOrEmpty())
                    return false;

                if (Index >= Choices.Count)
                    return false;

                if (Index < 0)
                    return false;

                SelectionClone = ScrollChild.selectionClones[Index];
                DataElement = Choices[Index] as TDataElement;
                Component = SelectionClone.gameObject.GetComponent<TComponent>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static BallBag<T> ToBallBag<T>(this Dictionary<T, int> Dictionary, Random Rnd)
        {
            if (Dictionary == null)
                return null;

            var ballBag = new BallBag<T>(Rnd ?? Stat.Rnd, Dictionary.Count);
            foreach ((T item, int weight) in Dictionary)
                ballBag.Add(item, weight);

            return ballBag;
        }

        public static BallBag<T> ToBallBag<T>(this Dictionary<T, int> Dictionary)
            => Dictionary.ToBallBag(Stat.Rnd)
            ;

        public static IEnumerable<T> IteratorSafe<T>(this IEnumerable<T> Source)
            => Source ?? Enumerable.Empty<T>()
            ;

        public static bool HasObjectWithRegisteredEvent(this Zone Zone, string Event)
        {
            foreach (var gameObject in Zone.YieldObjects())
                if (gameObject.HasRegisteredEvent(Event))
                    return true;

            return false;
        }

        public static bool HasPart(this GameObject Object, Predicate<IPart> Where)
        {
            if (Object == null)
                return false;

            for (int i = 0; i < Object.PartsList.Count; i++)
                if (Where?.Invoke(Object.PartsList[i]) is not false)
                    return true;

            return false;
        }

        public static bool HasEffect(this GameObject Object, Predicate<Effect> Where)
        {
            if (Object == null)
                return false;

            for (int i = 0; i < Object.Effects.Count; i++)
                if (Where?.Invoke(Object.Effects[i]) is not false)
                    return true;

            return false;
        }

        public static bool CanBeLunarFragile(this GameObject Object, bool RequireAllTrue = false)
        {
            if (Object == null)
                return false;

            int count = 0;
            int trueCount = 0;
            foreach (var lunarPart in Object.GetPartsDescendedFrom<ILunarObjectPart>())
            {
                count++;
                if (lunarPart.CanBeFragile)
                {
                    if (!RequireAllTrue)
                        return true;

                    trueCount++;
                }
            }
            return count == trueCount;
        }

        public static Location2D PeekLocationOfTier(
            this JoppaWorldBuilder Builder,
            int Tier,
            bool MutableOnly = true
            )
        {
            List<Location2D> parasangs;
            int attempts = 0;
            while (!Builder.worldInfo.tierLocations.TryGetValue(Tier, out parasangs))
            {
                Utils.Warn($"Couldn't find location of tier {Tier}");
                Tier--;
                if (Tier < 1)
                    Tier = 8;

                attempts++;
                if (attempts > 9)
                    return null;
            }

            using var locationZones = ScopeDisposedList<Location2D>.GetFromPool();
            foreach (var parasang in parasangs)
            {
                foreach (var location in parasang.YieldParasangZoneLocations())
                    locationZones.Add(location);
            }
            locationZones.ShuffleInPlace();
            foreach (var location in locationZones.IteratorSafe())
                if (!MutableOnly
                    || Builder.mutableMap.GetMutable(location) > 0)
                    return location;

            return null;
        }

        public static IEnumerable<Location2D> YieldParasangZoneLocations(this Location2D Parasang)
        {
            if (Parasang == null)
                yield break;

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    if (Location2D.Get(Parasang.X * 3 + i, Parasang.Y * 3 + j) is Location2D location)
                        yield return location;
        }

        public static int GetCenterZoneX(this Location2D WorldLocation)
            => Math.Clamp(WorldLocation.X * 3 + 1, 0, 239)
            ;

        public static int GetCenterZoneY(this Location2D WorldLocation)
            => Math.Clamp(WorldLocation.Y * 3 + 1, 0, 74)
            ;

        public static Location2D GetCenterZone(this Location2D WorldLocation)
            => Location2D.Get(WorldLocation.GetCenterZoneX(), WorldLocation.GetCenterZoneY())
            ;

        public static IEnumerable<Cell> GetCellsInACosmeticCircle(this Zone Zone, int X, int Y, int Radius)
        {
            if (X < 0
                || Y < 0)
                yield break;

            int yRadius = (int)Math.Max(1.0, Radius * 0.66);
            float radiusSquared = Radius * Radius;
            int minX = X - Radius;
            int maxX = X + Radius;
            int minY = Y - yRadius;
            int maxY = Y + yRadius;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float xD = Math.Abs(x - X);
                    float yD = Math.Abs(y - Y) * 1.3333f;
                    float d = (xD * xD) + (yD * yD);

                    if (radiusSquared > d
                        && Zone.GetCell(x, y) != null)
                        yield return Zone.GetCell(x, y);
                }
            }
        }

        public static IEnumerable<Cell> GetCellsInACosmeticCircleSilent(this Cell Cell, int Radius)
            => Cell.ParentZone.GetCellsInACosmeticCircle(Cell.X, Cell.Y, Radius)
            ;

        public static bool MatchesCaptureGroups(this string Pattern, string Text, RegexOptions Options, bool MegaPattern = false)
        {
            if (Text.IsNullOrEmpty())
                return false;

            if (Pattern.IsNullOrEmpty())
                return false;

            if (Regex.Matches(Text, Pattern, Options) is MatchCollection matches)
            {
                bool any = false;
                using var captureList = ScopeDisposedList<string>.GetFromPool();
                for (int i = 0; i < matches.Count; i++)
                {
                    if (matches[i] is Match match
                        && match.Groups is GroupCollection matchGroups
                        && matchGroups.Count > 1)
                    {
                        for (int j = 1; j < matchGroups.Count; j++)
                        {
                            if (matchGroups[j] is Group group
                                && group.Captures is CaptureCollection captures
                                && !captures.IsNullOrEmpty())
                            {
                                captureList.AddRange(captures.Select(c => c.Value));
                                any = true;
                            }
                        }
                    }
                }
                if (any)
                {
                    Utils.Warn($"Found the following {nameof(BadWord)}:");
                    Utils.Log($"{nameof(Text)}: {Text}");
                    Utils.Log($"{nameof(Pattern)}: {(!MegaPattern ? Pattern : nameof(MegaPattern))}");
                    captureList.IteratorSafe().Loggregate(
                        Proc: c => $": {c}",
                        Empty: ": none",
                        PostProc: s => $"{1.Indent()}{s}");
                }
                return any;
            }

            return false;
        }

        public static bool TryGetPartBlueprint<T>(this GameObjectBlueprint Model, out GamePartBlueprint Result)
            where T : IPart
        {
            Result = null;
            if (Model.Parts.IsNullOrEmpty())
                return false;

            return Model.Parts.TryGetValue(typeof(T).Name, out Result)
                ;
        }

        public static bool IsNullOrDefault([NotNullWhen(false)] this int? value)
            => value == null
            || value == (int)default
            ;

        public static string NameOrMissing(this GameObjectBlueprint Blueprint)
            => Blueprint?.Name ?? "MISSING_BLUEPRINT"
            ;
    }
}
