using System;
using Mafi;
using Mafi.Collections.ImmutableCollections;

namespace RateCalculator.Extensions;

public static class TimeSelectionOptionExtensions
{
    public static string ToDisplayText(this TimeSelectionOption option) => option switch
    {
        TimeSelectionOption.Seconds60 => "60 seconds",
        TimeSelectionOption.Months3 => "3 months",
        TimeSelectionOption.Months6 => "6 months",
        _ => option.ToString()
    };

    public static Fix32 GetMultiplier(this TimeSelectionOption option)
    {
        return option switch
        {
            TimeSelectionOption.Seconds60 => Fix32.One, // 1 month = 60 seconds
            TimeSelectionOption.Months3 => 3,
            TimeSelectionOption.Months6 => 6,
            _ => Fix32.One
        };
    }

    public static ImmutableArray<TimeSelectionOption> GetOptions()
    {
        return ((TimeSelectionOption[])Enum.GetValues(typeof(TimeSelectionOption))).ToImmutableArray();
    }
}