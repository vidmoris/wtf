using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace Ocelot.BlueCrystalCooking
{
    public class BarrelObject
    {
        public List<ushort> ingredients = new List<ushort>();
        public uint progress = 0;
        public BarrelObject(List<ushort> ingredients, uint progress)
        {
            this.ingredients = ingredients;
            this.progress = progress;
        }
    }
}
