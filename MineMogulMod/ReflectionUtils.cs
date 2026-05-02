using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MineMogulMod
{
    /// <summary>
    /// Centrale helper voor reflectie-toegang tot private game-velden.
    /// </summary>
    internal static class ReflectionUtils
    {
        private static readonly BindingFlags Flags =
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        // ── ConveyorBelt ──────────────────────────────────────────────────────

        private static FieldInfo? _beltObjectsField;
        public static int GetPhysicsObjectCount(ConveyorBelt belt)
        {
            _beltObjectsField ??= typeof(ConveyorBelt).GetField("_physicsObjectsOnBelt", Flags);
            var list = _beltObjectsField?.GetValue(belt) as IList;
            return list?.Count ?? 0;
        }

        // ── SorterFilterBasket ────────────────────────────────────────────────

        private static FieldInfo? _filterCriteriaField;
        public static int GetFilterCriteriaCount(SorterFilterBasket basket)
        {
            _filterCriteriaField ??= typeof(SorterFilterBasket).GetField("_filterCriteria", Flags);
            var list = _filterCriteriaField?.GetValue(basket) as IList;
            return list?.Count ?? 0;
        }

        public static IList? GetFilterCriteriaRaw(SorterFilterBasket basket)
        {
            _filterCriteriaField ??= typeof(SorterFilterBasket).GetField("_filterCriteria", Flags);
            return _filterCriteriaField?.GetValue(basket) as IList;
        }

        public static void InvokeUpdateFilter(SorterFilterBasket basket)
        {
            typeof(SorterFilterBasket)
                .GetMethod("UpdateFilter", Flags)
                ?.Invoke(basket, null);
        }

        // ── PolishingMachine ──────────────────────────────────────────────────

        private static FieldInfo? _polishSpeedField;
        public static float GetPolisherStandardSpeed(PolishingMachine machine)
        {
            _polishSpeedField ??= typeof(PolishingMachine).GetField("_standardConveyorSpeed", Flags);
            return (float?)_polishSpeedField?.GetValue(machine) ?? 0f;
        }
    }
}
