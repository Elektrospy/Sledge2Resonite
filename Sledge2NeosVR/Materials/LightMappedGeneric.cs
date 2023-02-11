using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sledge2NeosVR;

public class LightMappedGeneric : PBSMaterialParser<IPBS_Specular>
{
    protected override Uri ShaderURL => throw new NotImplementedException();

    // More details at: https://developer.valvesoftware.com/wiki/LightmappedGeneric
    public async Task<PBS_Material> CreateMaterialFromProperties(List<KeyValuePair<string, string>> properties)
    {
        PBS_Material resultMaterial = await CreateMaterial(properties);
        return resultMaterial;
    }
}