using BaseX;

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
            float2 = float2.Parse(parsed);
            return true;
        }
        else
        {
            float2 = float2.Parse(Helpers.DivideNumbersBy255(parsed));
            return true;
        }
    }
}