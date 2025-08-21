using Elements.Core;

public static class Float2Extensions
{
    public static bool GetFloat2FromString(string str, out float2 float2_out)
    {
        if (!Helpers.ParseValveNumberString(str, out string parsed))
        {
            float2_out = float2.Zero;
            return false;
        }

        if (parsed.Contains('.'))
        {
            float2_out = float2.Parse(parsed);
            return true;
        }
        else
        {
            float2_out = float2.Parse(Helpers.DivideNumbersBy255(parsed));
            return true;
        }
    }
}