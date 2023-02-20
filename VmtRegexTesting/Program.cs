using Sledge2NeosVR;

// var text = File.ReadAllText(@"Material.txt");
// VMTPreprocessor pro = new VMTPreprocessor();
// pro.ParseVmt(text, out string result);

string regex = "         {  128 255 16   }         ";
Float3Extensions.GetFloat3FromString(regex, out string output);

Console.WriteLine(output);