using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    public class DelegateReturnTypeException : Exception
    {
        public Type RequiredType;
        public Type Type;

        public DelegateReturnTypeException(string Message)
            : base(Message)
        { }

        public DelegateReturnTypeException(Type RequiredType, string Message)
            : this(Message)
        {
            this.RequiredType = RequiredType;
        }

        public DelegateReturnTypeException(Type Type, Type RequiredType, string Message)
            : this(RequiredType, Message)
        {
            this.Type = Type;
        }
    }

    public class DelegateModifierException : Exception
    {
        public enum ModifierType
        {
            Public,
            Protected,
            Internal,
            Private,
            Static,
            Virtual,
            Sealed,
            Override,
            Abstract,
            Extern,
            Readonly,
        }

        public ModifierType[] Modifiers;

        public DelegateModifierException(string Message)
            : base(Message)
        { }

        public DelegateModifierException(ModifierType Modifier, string Message)
            : this(Message)
        {
            Modifiers = new ModifierType[1] { Modifier };
        }

        public DelegateModifierException(ModifierType[] Modifiers, string Message)
            : this(Message)
        {
            this.Modifiers = Modifiers;
        }
    }

    public class DelegateParameterException : Exception
    {
        public ParameterInfo[] ParameterInfos;

        public DelegateParameterException(string Message)
            : base(Message)
        { }

        public DelegateParameterException(ParameterInfo ParameterInfo, string Message)
            : this(Message)
        {
            ParameterInfos = new ParameterInfo[1] { ParameterInfo };
        }

        public DelegateParameterException(ParameterInfo[] ParameterInfos, string Message)
            : this(Message)
        {
            this.ParameterInfos = ParameterInfos;
        }
    }
}
