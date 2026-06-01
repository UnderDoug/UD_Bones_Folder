using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Qud.API;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    [Serializable]
    public class ZoneBonesAllocationSpec : IComposite
    {
        private static string PropPrefix => $"{Const.MOD_PREFIX}AllocationType_";
        public static string LoadTypeProp_None => $"{PropPrefix}{ZoneBonesAllocation.AllocationTypes.None}";
        public static string LoadTypeProp_Party => $"{PropPrefix}{ZoneBonesAllocation.AllocationTypes.Party}";
        public static string LoadTypeProp_Bubble => $"{PropPrefix}{ZoneBonesAllocation.AllocationTypes.Bubble}";
        public static string LoadTypeProp_Zone => $"{PropPrefix}{ZoneBonesAllocation.AllocationTypes.Zone}";

        [NonSerialized]
        public HashSet<string> ZoneProperties;
        [NonSerialized]
        public HashSet<string> ZoneParts;
        [NonSerialized]
        public HashSet<string> ZoneBuilders;
        [NonSerialized]
        public HashSet<string> MapNoteCategories;
        [NonSerialized]
        public HashSet<string> MapNoteAttributes;

        [NonSerialized]
        protected List<Predicate<Zone>> ConditionDelegates;

        public ZoneBonesAllocationSpec()
        { }

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteStringHashSet(ZoneProperties);
            Writer.WriteStringHashSet(ZoneParts);
            Writer.WriteStringHashSet(ZoneBuilders);
            Writer.WriteStringHashSet(MapNoteCategories);
            Writer.WriteStringHashSet(MapNoteAttributes);
        }

        public virtual void Read(SerializationReader Reader)
        {
            ZoneProperties = Reader.ReadStringHashSet();
            ZoneParts = Reader.ReadStringHashSet();
            ZoneBuilders = Reader.ReadStringHashSet();
            MapNoteCategories = Reader.ReadStringHashSet();
            MapNoteAttributes = Reader.ReadStringHashSet();
        }

        public bool HasMatchingZoneProperty(Zone Zone, ZoneBonesAllocation Allocation)
        {
            if (Zone == null)
                return false;

            foreach (var zoneProp in ZoneProperties.IteratorSafe())
            {
                if (Zone.HasZoneProperty(zoneProp))
                {
                    Utils.Log($"{2.Indent()}{nameof(Zone)} {Zone} {nameof(HasMatchingZoneProperty)}: {zoneProp} (\"{Zone.GetZoneProperty(zoneProp) ?? "null"}\")");
                    return true;
                }
            }

            return false;
        }

        public bool HasMatchingZonePart(Zone Zone, ZoneBonesAllocation Allocation)
        {
            if (Zone == null)
                return false;

            foreach (var zonePart in ZoneParts.IteratorSafe())
            {
                if (!Allocation.ZoneParts.IteratorSafe().Any(p => p.Name == zonePart))
                {
                    Utils.Log($"{2.Indent()}{nameof(Zone)} {Zone} {nameof(HasMatchingZonePart)}: {zonePart}");
                    return true;
                }
            }

            return false;
        }

        public bool HasMatchingZoneBuilder(Zone Zone, ZoneBonesAllocation Allocation)
        {
            if (Zone == null)
                return false;

            if (Allocation.ZoneBuilders.IsNullOrEmpty())
                return false;

            foreach (var zoneBuilder in ZoneBuilders.IteratorSafe())
            {
                if (Allocation.ZoneBuilders.Any(b => b.Class == zoneBuilder))
                {
                    Utils.Log($"{2.Indent()}{nameof(Zone)} {Zone} {nameof(HasMatchingZoneBuilder)}: {zoneBuilder}");
                    return true;
                }
            }

            return false;
        }

        public bool HasMatchingMapNoteCategory(Zone Zone, ZoneBonesAllocation Allocation)
        {
            if (Zone == null)
                return false;

            var journalEntries = Allocation.JournalEntries.IteratorSafe();

            foreach (var journalEntry in journalEntries)
            {
                if (journalEntry is not JournalMapNote mapNote)
                    continue;

                foreach (var mapNoteCategory in MapNoteCategories.IteratorSafe())
                {
                    if (mapNoteCategory.EqualsNoCase(mapNote.Category))
                    { 
                        Utils.Log($"{2.Indent()}{nameof(HasMatchingMapNoteCategory)}: {mapNoteCategory}");
                        return true;
                    }
                }
            }

            return false;
        }

        public bool HasMatchingMapNoteAttribute(Zone Zone, ZoneBonesAllocation Allocation)
        {
            if (Zone == null)
                return false;

            var journalEntries = Allocation.JournalEntries.IteratorSafe();

            foreach (var journalEntry in journalEntries)
            {
                if (journalEntry is not JournalMapNote mapNote)
                    continue;

                foreach (var mapNoteAttribute in MapNoteAttributes.IteratorSafe())
                {
                    foreach (var attribute in mapNote.Attributes.IteratorSafe())
                    {
                        if (mapNoteAttribute.EqualsNoCase(attribute))
                        {
                            Utils.Log($"{2.Indent()}{nameof(Zone)} {Zone} {nameof(HasMatchingMapNoteAttribute)}: {attribute}");
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool HasMatchingZoneCondition(Zone Zone)
        {
            if (Zone == null)
                return false;

            foreach (var condition in ConditionDelegates.IteratorSafe())
            {
                if (condition?.Invoke(Zone) is not false)
                {
                    Utils.Log($"{2.Indent()}{nameof(Zone)} {Zone} {nameof(HasMatchingZoneCondition)}");
                    return true;
                }
            }

            return false;
        }

        public static void InitConditionDelegates(ZoneBonesAllocationSpec Spec, ZoneBonesAllocation.AllocationTypes ForType)
        {
            if (Spec == null)
                throw new NullReferenceException($"{nameof(Spec)} must not be null");

            Utils.Info($"{nameof(InitConditionDelegates)}({nameof(ForType)}: {ForType})");

            var conditionDelegates = Spec.ConditionDelegates ??= new();
            conditionDelegates.Clear();

            var methods = ModManager.GetMethodsWithAttribute(typeof(ZoneBonesAllocationSpecConditionAttribute), typeof(HasZoneBonesAllocationSpecConditionAttribute));

            foreach (var method in methods.IteratorSafe())
            {
                foreach (var attribute in method.GetCustomAttributes<ZoneBonesAllocationSpecConditionAttribute>())
                {
                    if (ForType == attribute.WhenTrue
                        && method.ReturnType == typeof(bool)
                        && method.GetParameters() is ParameterInfo[] parameters
                        && parameters.Length == 1
                        && parameters[0].ParameterType == typeof(Zone))
                        conditionDelegates.Add((Predicate<Zone>)method.CreateDelegate(typeof(Predicate<Zone>)));
                }
            }
        }

        public ZoneBonesAllocationSpec InitConditionDelegates(ZoneBonesAllocation.AllocationTypes ForType)
        {
            InitConditionDelegates(this, ForType);
            return this;
        }

        public bool MeetsSpec(Zone Zone, ZoneBonesAllocation Allocation, ZoneBonesAllocation.AllocationTypes? Type = null)
        {
            Utils.Log($"{1.Indent()}{nameof(MeetsSpec)}{(Type.HasValue ? $"({nameof(Type)}: {Type})" : null)}");
            if (HasMatchingZoneProperty(Zone, Allocation))
                return true;

            if (HasMatchingZonePart(Zone, Allocation))
                return true;

            if (HasMatchingZoneBuilder(Zone, Allocation))
                return true;

            if (HasMatchingMapNoteCategory(Zone, Allocation))
                return true;

            if (HasMatchingMapNoteAttribute(Zone, Allocation))
                return true;

            if (HasMatchingZoneCondition(Zone))
                return true;

            return false;
        }
    }
}
