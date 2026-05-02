using HarmonyLib;
using MineMogulMod.Modules;

namespace MineMogulMod.Patches
{
    /// <summary>
    /// Patch op ConveyorBelt.AddPhysicsObject zodat ThroughputTracker
    /// elke item-passering kan registreren.
    /// </summary>
    [HarmonyPatch(typeof(ConveyorBelt), nameof(ConveyorBelt.AddPhysicsObject))]
    internal static class ConveyorBelt_AddPhysicsObject_Patch
    {
        static void Postfix(ConveyorBelt __instance)
        {
            ThroughputTracker.Instance?.RecordItem(__instance);
        }
    }
}
