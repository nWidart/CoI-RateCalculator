using Mafi.Core.Mods;

namespace RateCalculator;

public sealed class RateCalculatorMod : DataOnlyMod
{
    public RateCalculatorMod(ModManifest manifest) : base(manifest)
    {
        // No-op
    }

    public override void RegisterPrototypes(ProtoRegistrator registrator)
    {
        // No-op
    }
}