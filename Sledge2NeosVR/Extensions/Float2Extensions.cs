using BaseX;
using System.Globalization;

public static class Float2Extensions
{
    public static bool GetFloat2FromString(string str, out float2 float2)
    {
        if (!Helpers.ParseValveNumberString(str, out string parsed))
        {
            float2 = float2.Zero;
            return false;
        }

        if (parsed.Contains("."))
        {
            UniLog.Log("has dot");
            float2 = float2.Parse(parsed, CultureInfo.InvariantCulture);
            return true;
        }
        else
        {
            UniLog.Log("has no dot");
            float2 = float2.Parse(Helpers.DivideNumbersBy255(parsed), CultureInfo.InvariantCulture);
            return true;
        }
    }
}