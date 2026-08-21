using System;


namespace Ocelot.BlueCrystalCooking
{
    public class DrugeffectTimeObject
    {
        public long time = BlueCrystalCookingPlugin.GetCurrentTime();
        public string playerId;


        public DrugeffectTimeObject(string playerId)
        {
            this.playerId = playerId;
        }
    }
}
