using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sledge2Resonite;

public class LightMappedGeneric : PBSSpecularParser
{
    public override Task<PBS_Specular> ParseMaterial(List<KeyValuePair<string, string>> properties, string name)
    {
        var material = base.ParseMaterial(properties, name);
        return material;
    }
}