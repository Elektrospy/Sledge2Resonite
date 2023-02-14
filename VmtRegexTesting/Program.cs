using Sledge2NeosVR;

var text = File.ReadAllText(@"Material.txt");
VMTPreprocessor pro = new VMTPreprocessor();

pro.ParseVmt(text, out string result);

Console.WriteLine(result);