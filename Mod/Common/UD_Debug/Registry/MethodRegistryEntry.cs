using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using XRL;

using static UD_Bones_Folder.Mod.Utils;

namespace UD_Bones_Folder.Mod.Logging
{
    public readonly struct MethodRegistryEntry : IEquatable<MethodBase>
    {
        private readonly MethodBase MethodBase;
        private readonly bool Value;

        public readonly string MethodName => MethodBase?.Name ?? "NO_METHOD";
        public readonly Type DeclaringType => MethodBase?.DeclaringType;
        public readonly string DeclaringTypeName => DeclaringType?.Name ?? "NO_TYPE";

        public MethodRegistryEntry(MethodBase MethodBase, bool Value)
            : this()
        {
            this.MethodBase = MethodBase;
            this.Value = Value;
        }

        public readonly void Deconstruct(out MethodBase MethodBase, out bool Value)
        {
            Value = this.Value;
            MethodBase = this.MethodBase;
        }

        public readonly string GetSourceModName()
            =>  ModManager.GetMod(DeclaringType.Assembly)?.DisplayTitleStripped
            ?? "NO_MOD";

        public readonly string GetTypeAndMethodName(bool FullSignature = false)
            => CallChain(DeclaringTypeName, !FullSignature ? MethodName : MethodBase.MethodSignature(true));

        public readonly string ToString(bool IncludeSourceMod, bool FullSignature = false) 
            => (IncludeSourceMod ? "[" + GetSourceModName() + "] - " : null) +
                GetTypeAndMethodName(FullSignature) + ": " + Value;

        public override readonly string ToString() 
            => ToString(false);

        public MethodBase GetMethod()
            => MethodBase;

        public bool GetValue()
            => Value;

        public static explicit operator bool(MethodRegistryEntry Operand)
            => Operand.Value;

        public static explicit operator MethodBase(MethodRegistryEntry Operand)
            => Operand.MethodBase;

        public static explicit operator MethodInfo(MethodRegistryEntry Operand)
            => Operand.MethodBase as MethodInfo;

        public static explicit operator KeyValuePair<MethodBase, bool>(MethodRegistryEntry Operand)
            => new(Operand.MethodBase, Operand.Value);

        public static explicit operator KeyValuePair<MethodInfo, bool>(MethodRegistryEntry Operand)
            => new(Operand.MethodBase as MethodInfo, Operand.Value);

        public static explicit operator MethodRegistryEntry(KeyValuePair<MethodBase, bool> Operand)
            => new(Operand.Key, Operand.Value);

        public static explicit operator MethodRegistryEntry(KeyValuePair<MethodInfo, bool> Operand)
            => new(Operand.Key, Operand.Value);

        public override readonly bool Equals(object obj)
        {
            if (obj is KeyValuePair<MethodBase, bool> kvpMBBObj)
                return Equals(kvpMBBObj);

            if (obj is KeyValuePair<MethodInfo, bool> kvpMIBObj)
                return Equals(kvpMIBObj);

            if (obj is MethodBase methodBaseObj)
                return Equals(methodBaseObj);

            if (obj is MethodInfo methodInfoObj)
                return Equals(methodInfoObj);

            if (obj.GetType().InheritsFrom(typeof(MethodBase)))
                return Equals(obj as MethodBase);

            if (obj is bool boolObj)
                return Value.Equals(boolObj);

            return base.Equals(obj);
        }

        public readonly bool Equals<T>(T Other)
            where T : MethodBase
        {
            if (Other is not T tOther)
                return false;

            if (!MethodBase.SuperficiallyEquivalent(tOther))
                return false;

            if (!MethodBase.MatchingGenerics(tOther))
                return false;

            if (!MethodBase.MatchingParams(tOther))
                return false;

            return true;
        }

        public readonly bool Equals<T>(KeyValuePair<T, bool> obj)
            where T : MethodBase
            => Equals(obj.Key)
            && obj.Value.Equals(Value);


        bool IEquatable<MethodBase>.Equals(MethodBase other)
            => Equals(other);

        public override readonly int GetHashCode()
        {
            int methodBase = MethodBase.GetHashCode();
            int value = Value.GetHashCode();
            return methodBase ^ value;
        }

        public static bool operator ==(MethodRegistryEntry Operand1, MethodBase Operand2) => Operand1.MethodBase == Operand2;
        public static bool operator !=(MethodRegistryEntry Operand1, MethodBase Operand2) => !(Operand1 == Operand2);

        public static bool operator ==(MethodBase Operand1, MethodRegistryEntry Operand2) => Operand2 == Operand1;
        public static bool operator !=(MethodBase Operand1, MethodRegistryEntry Operand2) => Operand2 != Operand1;

        public static bool operator ==(MethodRegistryEntry Operand1, KeyValuePair<MethodBase, bool> Operand2) => Operand1.Equals(Operand2);
        public static bool operator !=(MethodRegistryEntry Operand1, KeyValuePair<MethodBase, bool> Operand2) => !(Operand1 == Operand2);

        public static bool operator ==(KeyValuePair<MethodBase, bool> Operand1, MethodRegistryEntry Operand2) => Operand2 == Operand1;
        public static bool operator !=(KeyValuePair<MethodBase, bool> Operand1, MethodRegistryEntry Operand2) => Operand2 != Operand1;
    }
}
