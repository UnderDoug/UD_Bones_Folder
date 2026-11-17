using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.Logging
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class HasRegisterMethodForDebugAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RegistersMethodsForDebugAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class RegisterMethodForDebugAttribute : Attribute
    {
        // Indicates whether the method should print its debug entries to the log,
        // provided Options.DebugEnableLogging is also true, and Debug.SilenceLogging is false.
        /// <summary>
        /// Indicates whether the method should print its debug entries to the log, provided <see cref="Options.DebugEnableLogging"/> is also <see langword="true"/>, and <see cref="Debug.SilenceLogging"/> is <see langword="false"/>.
        /// </summary>
        public bool DoDebug = true;

        // Makes a version of the decorated method using these types as the generic arguments and registers that.
        // Leave blank to register the generic definition, which the registry will fall back to in the absence of a more specific entry.
        //
        // Use multiple instances of the attribute to target multiple generic version.<br/>
        // eg. If the method matched a Func<T, TResult> and you only wanted to target the method when T is a string and TResult is a bool,
        //      you would assign a newType[] { typeof(string), typeof(bool) }
        /// <summary>
        /// Makes a version of the decorated method using these types as the generic arguments and registers that. Leave blank to register the generic definition, which the registry will fall back to in the absence of a more specific entry.
        /// </summary>
        /// <remarks>
        /// Use multiple instances of the attribute to target multiple generic version.<br/>
        /// eg. <example> If the method matched a <see cref="Func{T, TResult}}"/> and you only wanted to target the method when <see cref="{T}"/> is a <see cref="string"/> and <see cref="{TResult}"/> is a <see cref="bool"/>, you would assign a <see langword="new"/> <see cref="Type[]"/>[] { <see langword="typeof"/>(<see cref="string"/>), <see langword="typeof"/>(<see cref="bool"/>) }</example>
        /// </remarks>
        public Type[] MethodGenerics;

        public RegisterMethodForDebugAttribute()
            : base()
        { }

        public RegisterMethodForDebugAttribute(bool DoDebug)
            : this()
        {
            this.DoDebug = DoDebug;
        }

        public RegisterMethodForDebugAttribute(Type[] MethodGenerics, bool DoDebug)
            : this(DoDebug)
        {
            this.MethodGenerics = MethodGenerics;
        }

        public RegisterMethodForDebugAttribute(bool DoDebug, params Type[] MethodGenerics)
            : this(MethodGenerics, DoDebug)
        { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class RegisterAsyncMethodForDebugAttribute : RegisterMethodForDebugAttribute
    {
        public RegisterAsyncMethodForDebugAttribute()
            : base()
        { }

        public RegisterAsyncMethodForDebugAttribute(bool DoDebug)
            : base(DoDebug)
        { }

        public RegisterAsyncMethodForDebugAttribute(Type[] MethodGenerics, bool DoDebug)
            : base(MethodGenerics, DoDebug)
        { }

        public RegisterAsyncMethodForDebugAttribute(bool DoDebug, params Type[] MethodGenerics)
            : base(MethodGenerics, DoDebug)
        { }
    }
}
