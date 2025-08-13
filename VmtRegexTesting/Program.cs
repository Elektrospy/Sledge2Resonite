using Elements.Core;
using Sledge2Resonite;
using System.Globalization;

internal class Program
{
    private static void Main(string[] args)
    {
        var text = File.ReadAllText(@"Material.txt");
        VMTPreprocessor pro = new VMTPreprocessor();
        pro.ParseVmt(text, out string result);

        string[] testStringList = {
    "{128 255 16}",
    "{ 128 255 16}",
    "{128 255 16 }",
    "{ 128 255 16 }",
    " {128 255 16}",
    "{ 128 255 16} ",
    " {128 255 16 } ",
    "{ 128 255 16 }",
    "{.1 .5 .8}",
    "{ .1 .5 .8}",
    "{.1 .5 .8 }",
    "{ .1 .5 .8 }",
    " {.1 .5 .8}",
    "{ .1 .5 .8} ",
    " {.1 .5 .8 } ",
    "{ .12 .54 .86 }",
    "{.12 .54 .86}",
    "{ .12 .54 .86}",
    "{.12 .54 .86 }",
    "{ .12 .54 .86 }",
    " {.12 .54 .86}",
    "{ .12 .54 .86} ",
    " {.12 .54 .86 } ",
    "{ .12 .54 .86 }",
    "{ .12 .54 .86 }",
    "{.125 .541 .869}",
    "{ .125 .541 .869}",
    "{.125 .541 .869 }",
    "{ .125 .541 .869 }",
    " {.125 .541 .869}",
    "{ .125 .541 .869} ",
    " {.125 .541 .869 } ",
    "{ .125 .541 .869 }"
};


        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.NumberDecimalSeparator = ".";

        for (int i = 0; i < testStringList.Length; i++)
        {
            Float3Extensions.GetFloat3FromString(testStringList[i], out float3 output);
            Console.WriteLine($"{i}: {output.x.ToString(nfi)} {output.y.ToString(nfi)} {output.z.ToString(nfi)}\n");
        }
    }
}