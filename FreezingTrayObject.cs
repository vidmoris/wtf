using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace Ocelot.BlueCrystalCooking
{
    public class FreezingTrayObject
    {
        public Transform transform;
        public ulong owner;
        public ulong group;
        public float angle_x;
        public float angle_y;
        public float angle_z;
        public Vector3 pos;
        public int freezingSeconds;
        public FreezingTrayObject(Transform transform, Vector3 pos, ulong owner, ulong group, float angle_x, float angle_y, float angle_z, int freezingSeconds)
        {
            this.transform = transform;
            this.pos = pos;
            this.owner = owner;
            this.group = group;
            this.angle_x = angle_x;
            this.angle_y = angle_y;
            this.angle_z = angle_z;
            this.freezingSeconds = freezingSeconds;
        }
    }
}
