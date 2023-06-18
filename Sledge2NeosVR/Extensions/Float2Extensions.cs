using BaseX;
using System.Globalization;

public static class Float2Extensions
{
    public static bool GetFloat2FromString(string str, out float2 float2)
    {
        if (!Utils.ParseValveNumberString(str, out string parsed))
        {
            float2 = float2.Zero;
            return false;
        }

        if (parsed.Contains("."))
        {
            float2 = float2.Parse(parsed, CultureInfo.InvariantCulture);
            return true;
        }
        else
        {
            float2 = float2.Parse(Utils.DivideNumbersBy255(parsed), CultureInfo.InvariantCulture);
            return true;
        }
    }
}