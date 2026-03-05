using Mafi;

namespace RateCalculator;

public class ProductBalance
{
    public Fix32 Produced;
    public Fix32 Consumed;

    public Fix32 Net => Produced - Consumed;

    /*
     * Reference table
     * | Produced | Consumed | Availability         |
       | -------- | -------- | -------------------- |
       | 0        | 72       | **1.0** (ingredient) |
       | 24       | 72       | **0.33**             |
       | 72       | 72       | **1.0**              |
     */
    public Fix32 Availability
    {
        get
        {
            if (Consumed == Fix32.Zero)
                return Fix32.One;

            if (Produced == Fix32.Zero)
                return Fix32.One;

            return Fix32.One.Min(Produced / Consumed);
        }
    }

    public bool IsIngredientOnly => Produced == Fix32.Zero && Consumed > Fix32.Zero;
    public bool IsProductOnly => Produced > Fix32.Zero && Consumed == Fix32.Zero;
    public bool IsIntermediate => Produced > Fix32.Zero && Consumed > Fix32.Zero;
}