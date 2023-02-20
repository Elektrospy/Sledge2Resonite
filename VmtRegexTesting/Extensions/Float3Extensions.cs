using System.Text.RegularExpressions;

public static class Float3Extensions
{
    public static bool GetFloat3FromString(string str, out string float3)
    {
        if (!((str.Contains("[") && str.Contains("]")) ||
              (str.Contains("{") && str.Contains("}"))))
        {
            float3 = string.Empty;
            return false;
        }

        str = str.Replace("{", "[");
        str = str.Replace("}", "]");
        str = str.Trim();

        const string magic1 = "((?<=([[]))[ ]*)|([ ]*(?=([]])))"; //matches all spaces behind [ or { and spaces before ] or }
        const string magic2 = "[ ]*"; //matches all spaces between numbers
        str = Regex.Replace(str, magic1, "");
        str = Regex.Replace(str, magic2, ";");

        // float3 parse expected string format: "[0;0;0]"
        if (str.Contains("."))
        {
            float3 = str;
            return true;
        }
        else
        {
            float3 = Helpers.DivideNumbersBy255(str);
            return true;
        }
    }
}