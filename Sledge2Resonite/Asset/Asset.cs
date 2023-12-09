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
        protected T asset;
        protected U worldAsset;
        public bool HasWorldElement { get; private set; }
        public bool HasVisual { get; private set; }
        public Slot WorldElementSlot { get; private set; }
        public string name { get; set; }

        public Asset(T asset)
        {
            this.asset = asset;
            HasWorldElement = false;
            HasVisual = false;
        }

        public async Task<Slot> CreateWorldElement(Slot target, bool createChild, bool generateVisual)
        {
            if (target == null) throw new NullReferenceException("target cannot be null");
            

            //await to world so we can modify it
            await default(ToWorld);
            if(createChild)
            {
                target = target.AddSlot(name);
            }

            await AttachAsset(target);

            if(generateVisual)
            {
                await GenerateVisual(target);
            }

            return target;
        }
        
        /// <summary>
        /// Create the asset on the specified slot
        /// </summary>
        /// <param name="target">Slot to create the asset on</param>
        /// <returns>The worldspace asset attached to the slot</returns>
        abstract protected Task<U> AttachAsset(Slot target);


        public async Task GenerateVisual(Slot target)
        {
            if (!HasVisual)
            {
                await CreateVisual(target);
                HasVisual = true;
            }
        }

        abstract protected Task CreateVisual(Slot target);
    }
}
