using BaseX;
using System.Globalization;

public static class Float3Extensions
{
    public static bool GetFloat3FromString(string str, out float3 float3)
    {
        if (!Helpers.ParseValveNumberString(str, out string parsed))
        {
            float3 = float3.Zero;
            return false;
        }

        float3 = float3.Parse(parsed, CultureInfo.InvariantCulture);
        return true;
    }
}