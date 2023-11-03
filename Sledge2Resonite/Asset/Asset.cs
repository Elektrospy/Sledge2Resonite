using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sledge2Resonite
{
    internal abstract class Asset<T, U> where U : IAssetProvider
    {
        T asset;
        public bool HasWorldElement { get; private set; }
        public Slot WorldElementSlot { get; private set; }

        public Asset(T asset)
        {
            this.asset = asset;
            HasWorldElement = false;
        }

        public Task<Slot> CreateWorldElement(Slot target, bool createChild, bool generateVisual)
        {
            throw new NotImplementedException();
        }

        abstract protected void AttachAsset(Slot target);

        abstract public void GeneratreVisual();
    }
}
