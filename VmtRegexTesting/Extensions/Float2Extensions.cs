using System.Text.RegularExpressions;

public static class Float2Extensions
{
    public static bool GetFloat2FromString(string str, out string float2)
    {
        if (!str.Contains("[") && !str.Contains("{"))
        {
            float2 = string.Empty;
            return false;
        }

        string pattern = @"\s*([\{\[])\s*|\s*([\}\]])\s*|\s+";
        string replacement = ";";
        string result = Regex.Replace(str, pattern, replacement);
        // float2 parse expected string format: "[0;0]"

        if (str.Contains("."))
        {
            float2 = result;
            return true;
        }
        else
        {
            result = Helpers.DivideNumbersBy255(result);
            float2 = result;
            return true;
        }
    }
}