using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sledge2NeosVR;

public class VertexLitGenericParser : PBSSpecularParser
{
    public override Task<PBS_Specular> CreateMaterial(List<KeyValuePair<string, string>> properties)
    {
        var material = base.CreateMaterial(properties);
        return material;
    }
}