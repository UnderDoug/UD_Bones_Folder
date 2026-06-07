using System;
using System.Collections.Generic;
using System.Text;

using XRL.CharacterBuilds;

namespace UD_Bones_Folder.Mod.UI
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class HasBonesModeModuleActionAttribute : Attribute
    {
        public HasBonesModeModuleActionAttribute()
        { }
    }

    /// <summary>
    /// Flags a method to be converted into a <see cref="Action{T1, T2}"/> for use during a Bones Mode embark to programatically manipulate the <see cref="AbstractBuilderModuleWindowBase"/> or its <see cref="AbstractQudEmbarkBuilderModule"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class BonesModeModuleActionAttribute : Attribute
    {
        /// <summary>
        /// The type of <see cref="AbstractBuilderModuleWindowBase"/> to target. Leaving <see langword="null"/> will call the decorated method on every window and module in the process.
        /// </summary>
        public Type TargetWindow;

        public BonesModeModuleActionAttribute()
        { }

        public BonesModeModuleActionAttribute(Type TargetWindow)
        {
            this.TargetWindow = TargetWindow;
            if (!TargetWindow.InheritsFrom(typeof(AbstractBuilderModuleWindowBase)))
                Utils.Warn($"{nameof(BonesModeModuleActionAttribute)}.{nameof(TargetWindow)} assigned a {nameof(Type)} " +
                    $"that cannot be assigned to {nameof(AbstractBuilderModuleWindowBase)}, and will likely never be used.");
        }
    }
}
