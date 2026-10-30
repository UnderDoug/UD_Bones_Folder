using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using XRL;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HasReturnsBonesSourcesDelegateAttribute : Attribute
    {
    }

    /// <summary>
    /// Methods decorated with this attribute should meet the following requirements:<br />
    /// - be <see langword="static"/>,<br />
    /// - <see langword="return"/> an <see cref="IEnumerable{}"/> of <see cref="IBonesSource"/>, and<br />
    /// - have no parameters.<br />
    /// <br />
    /// It will be cached as a delegatge and called any time the <see cref="BonesManager"/> wants to interact with an available set of Bones.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class BonesSourcesDelegateAttribute : Attribute
    {
        public static int EXTREMELY_EARLY => -5000;
        public static int VERY_EARLY => -1000;
        public static int EARLY => -100;
        public static int SLIGHTLY_EARLY => -10;
        public static int SLIGHTLY_LATE => -SLIGHTLY_EARLY;
        public static int LATE => -EARLY;
        public static int VERY_LATE => -VERY_EARLY;
        public static int EXTREMELY_LATE => -EXTREMELY_EARLY;

        /// <summary>
        /// Indicates the general location from and to which the retreived Bones Sources will save and load bones.
        /// </summary>
        public IBonesSource.SourceType Type;

        /// <summary>
        /// Smaller = Earlier. Matching Priority values result in arbitrary execution order.
        /// </summary>
        public int Priority;

        public BonesSourcesDelegateAttribute()
            : base()
        { }

        public BonesSourcesDelegateAttribute(IBonesSource.SourceType Type, int Priority = 0)
            : base()
        {
            this.Type = Type;
            this.Priority = Priority;
        }
    }

    public delegate IEnumerable<IBonesSource> BonesSourcesDelegate();

    internal struct BonesSourcesDelegateEntry
    {
        internal static int TYPE_PRIORITY_MULTIPLIER => 50000;

        /// <summary>
        /// Indicates the general location from and to which the retreived Bones Sources will save and load bones.
        /// </summary>
        public readonly IBonesSource.SourceType Type;

        /// <summary>
        /// Smaller = Earlier. Matching Priority values result in arbitrary execution order.
        /// </summary>
        public readonly int Priority;

        public readonly BonesSourcesDelegate GetBonesSources;

        public BonesSourcesDelegateEntry(IBonesSource.SourceType Type, int Priority, BonesSourcesDelegate GetBonesSources)
        {
            this.Type = Type;
            this.Priority = Priority;
            this.GetBonesSources = GetBonesSources;
        }

        private static string InvalidMethod(string Message = null)
            => $"Invalid method decorated with {nameof(BonesSourcesDelegateAttribute)}{(Message.IsNullOrEmpty() ? "." : $"; {Message}")}"
            ;

        public static BonesSourcesDelegateEntry Create(
            IBonesSource.SourceType Type,
            int Priority,
            MethodInfo DelegateMethod
            )
        {
            if (!DelegateMethod.IsStatic)
                throw new DelegateModifierException(
                    Modifier: DelegateModifierException.ModifierType.Static,
                    Message: InvalidMethod("Must be static."));

            var targetReturnType = typeof(IEnumerable<IBonesSource>);
            if (!targetReturnType.IsAssignableFrom(DelegateMethod.ReturnType))
                throw new DelegateReturnTypeException(
                    Type: DelegateMethod.ReturnType,
                    RequiredType: targetReturnType,
                    Message: InvalidMethod($"Must return type from which {targetReturnType} is assignable, but returns {DelegateMethod.ReturnType}."));

            if (!DelegateMethod.GetParameters().IsNullOrEmpty())
                throw new DelegateParameterException(InvalidMethod("Must not have any parameters."));

            return new BonesSourcesDelegateEntry(
                Type: Type,
                Priority: ((int)Type * TYPE_PRIORITY_MULTIPLIER) + Priority,
                GetBonesSources: (BonesSourcesDelegate)DelegateMethod.CreateDelegate(typeof(BonesSourcesDelegate))
                );
        }

        public static bool TryCreate(
            IBonesSource.SourceType Type,
            int Priority,
            MethodInfo DelegateMethod,
            out BonesSourcesDelegateEntry DelegateEntry,
            ModInfo ModInfo = null
            )
        {
            var modInfo = ModInfo ?? Utils.ThisMod;
            try
            {
                DelegateEntry = Create(Type, Priority, DelegateMethod);
                return true;
            }
            catch (Exception x)
            {
                modInfo.Warn($"Failed creating {nameof(DelegateEntry)} for {DelegateMethod?.Name ?? "MISSING_METHOD"}: {x}");
                DelegateEntry = default;
                return false;
            }
        }
    }
}
