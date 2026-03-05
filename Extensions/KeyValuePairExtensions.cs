namespace RateCalculator.Extensions;

using System.Collections.Generic;

/*
 Allows for:
   foreach (var (key, value) in dict)
   {
       // use key and value
   }
 */
public static class KeyValuePairExtensions
{
    public static void Deconstruct<TKey, TValue>(
        this KeyValuePair<TKey, TValue> kvp,
        out TKey key,
        out TValue value)
    {
        key = kvp.Key;
        value = kvp.Value;
    }
}