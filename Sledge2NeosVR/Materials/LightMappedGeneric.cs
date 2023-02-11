using FrooxEngine;
using System.Collections.Generic;

namespace Sledge2NeosVR;

public abstract class LightMappedGeneric : PBSMaterialParser<IPBS_Specular>
{
    // More details at: https://developer.valvesoftware.com/wiki/VertexLitGeneric
    public override bool CreateMaterial(List<KeyValuePair<string, string>> properties)
    {
        return true;
    }
}