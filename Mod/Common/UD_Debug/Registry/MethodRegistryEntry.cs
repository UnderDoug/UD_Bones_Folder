using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using XRL;

using LoggingLevel = UD_Bones_Folder.Mod.Logging.DebugMethodRegistry.LoggingLevel;
using static UD_Bones_Folder.Mod.Utils;

namespace UD_Bones_Folder.Mod.Logging
{
    public readonly struct MethodRegistryEntry : IEquatable<MethodRegistryEntry>, IEquatable<MethodBase>
    {
        private readonly MethodBase MethodBase;
        private readonly bool Value;

        // Not implemented yet.
        private readonly LoggingLevel DevLevel;
        private readonly LoggingLevel Level;

        private readonly bool SelfRegistered;

        private readonly string CallerFilePath;
        private readonly int? CallerLineNumber;

        public readonly string MethodName => MethodBase?.Name ?? "NO_METHOD";
        public readonly Type DeclaringType => MethodBase?.DeclaringType;
        public readonly string DeclaringTypeName => DeclaringType.GetTypeName();

        public MethodRegistryEntry(
            MethodBase MethodBase,
            bool Value,
            bool SelfRegistered,
            [CallerFilePath] string CallerFilePath = "",
            [CallerLineNumber] int? CallerLineNumber = null
            )
        {
            this.MethodBase = MethodBase;
            this.Value = Value;
            this.SelfRegistered = SelfRegistered;
            this.CallerFilePath = CallerFilePath;
            this.CallerLineNumber = CallerLineNumber;

            // Not implemented yet.
            DevLevel = LoggingLevel.Setting;
            Level = LoggingLevel.Setting;
        }

        public MethodRegistryEntry(
            MethodBase MethodBase,
            bool Value,
            [CallerFilePath] string CallerFilePath = "",
            [CallerLineNumber] int? CallerLineNumber = null
            )
            : this(MethodBase, Value, false, CallerFilePath, CallerLineNumber)
        { }

        public readonly void Deconstruct(out MethodBase MethodBase, out bool Value)
        {
            Value = this.Value;
            MethodBase = this.MethodBase;
        }

        public static MethodRegistryEntry NewAsyncEntry(
            Type Type,
            string MethodName,
            Type[] Parameters,
            Type[] MethodGenerics,
            bool Value,
            bool SelfRegistered = false,
            [CallerFilePath] string CallerFilePath = "",
            [CallerLineNumber] int? CallerLineNumber = null
            )
            => new(DebugMethodRegistry.GetAsyncMethod(Type, MethodName, Parameters, MethodGenerics), Value, SelfRegistered, CallerFilePath, CallerLineNumber)
            ;

        public static MethodRegistryEntry NewAsyncEntry(
            Type Type,
            string MethodName,
            Type[] Parameters,
            bool Value,
            bool SelfRegistered = false,
            [CallerFilePath] string CallerFilePath = "",
            [CallerLineNumber] int? CallerLineNumber = null
            )
            => NewAsyncEntry(Type, MethodName, Parameters, null, Value, SelfRegistered, CallerFilePath, CallerLineNumber)
            ;

        public static MethodRegistryEntry NewAsyncEntry(
            Type Type,
            string MethodName,
            bool Value,
            bool SelfRegistered = false,
            [CallerFilePath] string CallerFilePath = "",
            [CallerLineNumber] int? CallerLineNumber = null
            )
            => NewAsyncEntry(Type, MethodName, null, Value, SelfRegistered, CallerFilePath, CallerLineNumber)
            ;

        public readonly string GetCallerFilePosition()
            => !CallerFilePath.IsNullOrEmpty()
            ? $"{DataManager.SanitizePathForDisplay(CallerFilePath)}{(CallerLineNumber.HasValue ? $":{CallerLineNumber}" : null)}"
            : "NO_CALLER"
            ;

        public readonly string GetSourceModName()
            =>  ModManager.GetMod(DeclaringType.Assembly)?.DisplayTitleStripped
            ?? "Base Game"
            ;

        private readonly string GetSourceModString(bool IncludeSourceMod)
            => IncludeSourceMod
            ? $"[{GetSourceModName()}] - "
            : null
            ;

        public readonly string GetTypeAndMethodName(bool FullSignature = false, bool IncludeNamespace = false)
            => CallChain(DeclaringType.GetTypeName(true, IncludeNamespace), !FullSignature ? MethodName : MethodBase.MethodSignature(true))
            ;

        public readonly string GetValueString()
            => Value
            ? "Enabled"
            : "Disabled"
            ;

        public readonly string SelfRegisteredTag()
            => SelfRegistered ? " [self]" : null
            ;

        private readonly string GetSelfRegisteredString(bool IncludeSelfRegistered)
            => IncludeSelfRegistered
            ? SelfRegisteredTag()
            : null
            ;

        public readonly string ToString(bool IncludeSourceMod, bool FullSignature = false, bool IncludeNamespace = false, bool IncludeSelfRegistered = true) 
            => $"{GetSourceModString(IncludeSourceMod)}{GetTypeAndMethodName(FullSignature, IncludeNamespace)}: {GetValueString()}{GetSelfRegisteredString(IncludeSelfRegistered)}"
            ;

        public override readonly string ToString() 
            => ToString(IncludeSourceMod: false)
            ;

        public MethodBase GetMethod()
            => MethodBase
            ;

        public bool GetValue()
            => Value
            ;

        public override readonly bool Equals(object obj)
        {
            if (obj is MethodRegistryEntry methodRegistryEntryObj)
                return Equals(methodRegistryEntryObj);

            if (obj is KeyValuePair<MethodInfo, bool> kvpMIBObj)
                return Equals(kvpMIBObj);

            if (obj is KeyValuePair<MethodBase, bool> kvpMBBObj)
                return Equals(kvpMBBObj);

            if (obj is MethodInfo methodInfoObj)
                return Equals(methodInfoObj);

            if (obj.GetType().InheritsFrom(typeof(MethodBase)))
                return Equals(obj as MethodBase);

            if (obj is MethodBase methodBaseObj)
                return Equals(methodBaseObj);

            if (obj is bool boolObj)
                return Value.Equals(boolObj);

            return base.Equals(obj);
        }

        public readonly bool Equals(MethodRegistryEntry Other, bool Strict)
        {
            if (EitherNull(MethodBase, Other.MethodBase, out bool areEqual))
                return areEqual;

            if (Strict
                && MethodBase.IsGenericMethodDefinition != Other.MethodBase?.IsGenericMethodDefinition)
                return false;

            return Equals(Other.MethodBase, Strict);
        }

        public bool Equals(MethodRegistryEntry Other)
            => Equals(Other, true)
            ;

        public readonly bool Equals<T>(T Other, bool Strict = true)
            where T : MethodBase
        {
            if (this == Other)
                return true;

            if (!MethodBase.IsSuperficiallyEquivalent(Other))
                return false;

            if (Strict
                && !MethodBase.HasMatchingGenerics(Other))
                return false;

            if (!MethodBase.HasMatchingParams(Other, Strict))
                return false;

            return true;
        }

        public readonly bool EqualsGeneric<T>(T Other)
            where T : MethodBase
        {
            if (EitherNull(MethodBase, Other, out bool areEqual))
                return areEqual;

            if (!MethodBase.IsGenericMethodDefinition)
                return false;

            if (this == Other)
                return true;

            if (!MethodBase.IsSuperficiallyEquivalent(Other))
                return false;

            if (!MethodBase.HasMatchingParams(Other, TypeStrict: false))
                return false;

            return true;
        }

        public readonly bool Equals<T>(KeyValuePair<T, bool> obj)
            where T : MethodBase
            => Equals(obj.Key)
            && obj.Value.Equals(Value)
            ;

        bool IEquatable<MethodBase>.Equals(MethodBase other)
            => Equals(other)
            ;

        public override readonly int GetHashCode()
        {
            int methodBase = MethodBase.GetHashCode();
            int value = Value.GetHashCode();
            return methodBase ^ value;
        }

        #region Casting

        public static explicit operator bool(MethodRegistryEntry Operand)
            => Operand.Value
            ;


        public static explicit operator MethodBase(MethodRegistryEntry Operand)
            => Operand.MethodBase
            ;


        public static explicit operator MethodInfo(MethodRegistryEntry Operand)
            => Operand.MethodBase as MethodInfo
            ;


        public static explicit operator KeyValuePair<MethodBase, bool>(MethodRegistryEntry Operand)
            => new(Operand.MethodBase, Operand.Value)
            ;


        public static explicit operator KeyValuePair<MethodInfo, bool>(MethodRegistryEntry Operand)
            => new(Operand.MethodBase as MethodInfo, Operand.Value)
            ;


        public static explicit operator MethodRegistryEntry(KeyValuePair<MethodBase, bool> Operand)
            => new(Operand.Key, Operand.Value)
            ;


        public static explicit operator MethodRegistryEntry(KeyValuePair<MethodInfo, bool> Operand)
            => new(Operand.Key, Operand.Value)
            ;

        #endregion
        #region Operator Overloads

        public static bool operator ==(MethodRegistryEntry Operand1, MethodBase Operand2)
            => Operand1.MethodBase == Operand2
            ;

        public static bool operator !=(MethodRegistryEntry Operand1, MethodBase Operand2)
            => !(Operand1 == Operand2)
            ;

        public static bool operator ==(MethodBase Operand1, MethodRegistryEntry Operand2)
            => Operand2 == Operand1
            ;

        public static bool operator !=(MethodBase Operand1, MethodRegistryEntry Operand2)
            => Operand2 != Operand1
            ;

        public static bool operator ==(MethodRegistryEntry Operand1, KeyValuePair<MethodBase, bool> Operand2)
            => Operand1.Equals(Operand2)
            ;

        public static bool operator !=(MethodRegistryEntry Operand1, KeyValuePair<MethodBase, bool> Operand2)
            => !(Operand1 == Operand2)
            ;

        public static bool operator ==(KeyValuePair<MethodBase, bool> Operand1, MethodRegistryEntry Operand2)
            => Operand2 == Operand1
            ;

        public static bool operator !=(KeyValuePair<MethodBase, bool> Operand1, MethodRegistryEntry Operand2)
            => Operand2 != Operand1
            ;

        #endregion
    }
}
