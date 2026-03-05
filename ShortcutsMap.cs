using Mafi.Unity.InputControl;
using UnityEngine;

namespace RateCalculator;

public class ShortcutsMap
{
    public static ShortcutsMap Instance { get; } = new();
    
    [Kb(KbCategory.Tools, "rateCalculator_openWindow", "RateCalculator Main window", "RateCalculator Main Window", true, false)]
    public KeyBindings OpenRateCalcWindow { get; set; } = KeyBindings.FromKey(KbCategory.Tools, ShortcutMode.Game, KeyCode.F9);
    
    [Kb(KbCategory.Tools, "rateCalculator_marqueSelect", "RateCalculator selection tool", "RateCalculator selection tool", true, false)]
    public KeyBindings OpenCalc { get; set; } = KeyBindings.FromKey(KbCategory.Tools, ShortcutMode.Game, KeyCode.Y);
}