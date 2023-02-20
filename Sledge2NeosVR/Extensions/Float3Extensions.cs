using BaseX;

public static class Float3Extensions
{
    public static bool GetFloat3FromString(string str, out float3 float3)
    {
        if (!Helpers.ParseValveNumberString(str, out string parsed))
        {
            UniLog.Log("failed to parse " + str);
            float3 = float3.Zero;
            return false;
        }

        UniLog.Log("parsed with " + parsed);
        float3 = float3.Parse(parsed);
        return true;
    }
}