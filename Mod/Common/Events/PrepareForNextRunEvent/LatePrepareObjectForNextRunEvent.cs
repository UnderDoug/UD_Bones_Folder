using System.Collections.Generic;

using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.Events
{
    public class LatePrepareObjectForNextRunEvent : IPrepareObjectForNextRunEvent<LatePrepareObjectForNextRunEvent>
    {
        #region Debug Registration
        [RegistersMethodsForDebug]
        public static void doDebugRegistryFalse(DebugMethodRegistry Registry)
            => Registry.RegisterEach(
                Type: typeof(IPrepareObjectForNextRunEvent<LatePrepareObjectForNextRunEvent>),
                MethodNameValues: new Dictionary<string, bool>
                {
                    { nameof(Send), false },
                });
        #endregion

        public LatePrepareObjectForNextRunEvent()
            : base()
        { }
    }
}
