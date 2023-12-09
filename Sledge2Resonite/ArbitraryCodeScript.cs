using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

namespace Sledge2Resonite
{
    public class ArbitraryCodeScript
    {
        public ReferenceField<IWorldElement> target;

        public string UpdateMeshColliderToItsRenderer(
        {
Slot targetSlot = (Slot)target.Reference.Target;
int changeCount = 0;

var renderers = targetSlot.GetComponentsInChildren<MeshRenderer>();

for (int i = 0; i < renderers.Count; i++)
{
    try
    {


        var renderer = renderers[i];
        var rendererSlot = renderer.Slot;
        var collider = rendererSlot.GetComponent<MeshCollider>();
        if (collider != null && collider.Mesh.Target != renderer.Mesh.Target)
        {
            collider.Mesh.TrySet(renderer.Mesh.Target);
            changeCount++;
        }
    }
    catch (Exception e) { }
}


return $"Changed {changeCount} meshColliders to their renderers, processed {renderers.Count} renderers in total";
        }

        public string ComponentTypeCount()
        {
            Slot targetslot = (Slot)target.Reference.Target;

            var renderers = targetslot.GetComponentsInChildren<MeshRenderer>();

            return $"found {renderers.Count} meshRenderers";
        }
    }
}
