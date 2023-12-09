using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sledge.Formats.Texture.Vtf;
using FrooxEngine;

namespace Sledge2Resonite
{
    internal class VtfAsset : Asset<VtfFile, StaticTexture2D>
    {
        public VtfAsset(VtfFile asset) : base(asset)
        {
        }

        public override void GeneratreVisual()
        {
            throw new NotImplementedException();
        }

        protected override void AttachAsset(Slot target)
        {
            throw new NotImplementedException();
        }
    }
}
