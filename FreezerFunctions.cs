using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;
using System.Linq;


namespace Ocelot.BlueCrystalCooking.functions
{
    public static class FreezerFunctions
    {
        public static void OnBarricadeDeployed(Barricade barricade, ItemBarricadeAsset asset, Transform hit, ref Vector3 point, ref float angle_x, ref float angle_y, ref float angle_z, ref ulong owner, ref ulong group, ref bool shouldAllow)
        {
            Vector3 pos = point;
            var config = BlueCrystalCookingPlugin.Instance.Configuration.Instance;

            if (barricade.asset.id != config.LiquidTrayId)
                return;

            // A liquid tray placed directly on top of a freezer: raycast down to find the freezer barricade.
            if (!Physics.Raycast(pos, Vector3.down, out RaycastHit raycastHit, 10f, RayMasks.BARRICADE))
                return;

            BarricadeDrop freezerDrop = BarricadeManager.FindBarricadeByRootTransform(raycastHit.transform);
            if (freezerDrop == null || freezerDrop.asset.id != config.FreezerId)
                return;

            ulong ownerTray = owner;
            ulong groupTray = group;
            float angle_x_tray = angle_x;
            float angle_y_tray = angle_y;
            float angle_z_tray = angle_z;


            BlueCrystalCookingPlugin.Instance.Wait(0.5f, () =>
            {
                BarricadeDrop trayDrop = BlueCrystalCookingPlugin.Instance.FindDropNear(pos, config.LiquidTrayId);
                if (trayDrop != null)
                {
                    BlueCrystalCookingPlugin.Instance.freezingTrays.Add(
                        new FreezingTrayObject(trayDrop.model, pos, ownerTray, groupTray, angle_x_tray, angle_y_tray, angle_z_tray, 0));
                }
            });
        }


        public static void Update()
        {
            var config = BlueCrystalCookingPlugin.Instance.Configuration.Instance;

            foreach (var tray in BlueCrystalCookingPlugin.Instance.freezingTrays.ToList())
            {
                // Cleanup if the tray was punched and picked up before it finished freezing.
                if (tray.transform == null)
                {
                    BlueCrystalCookingPlugin.Instance.freezingTrays.Remove(tray);
                    continue;
                }


                bool canFreeze = false;


                if (config.FreezerNeedsPower)
                {
                    foreach (var Generator in PowerTool.checkGenerators(tray.pos, PowerTool.MAX_POWER_RANGE, ushort.MaxValue))
                    {
                        if (Generator.fuel > 0 && Generator.isPowered && Generator.wirerange >= (tray.pos - Generator.transform.position).magnitude)
                        {
                            canFreeze = true;
                            break;
                        }
                    }
                }
                else
                {
                    canFreeze = true;
                }


                if (!canFreeze)
                    continue;

                tray.freezingSeconds += 1;
                if (tray.freezingSeconds < config.BlueCrystalTrayFreezingTimeSecs)
                    continue;

                if (!BarricadeManager.tryGetInfo(tray.transform, out byte x, out byte y, out ushort plant, out ushort index, out BarricadeRegion region))
                {
                    // Fail-safe cleanup if the tray no longer exists.
                    BlueCrystalCookingPlugin.Instance.freezingTrays.Remove(tray);
                    continue;
                }

                BarricadeManager.destroyBarricade(region, x, y, plant, index);

                ItemBarricadeAsset frozenTrayAsset = (ItemBarricadeAsset)Assets.find(EAssetType.ITEM, config.FrozenTrayId);
                if (frozenTrayAsset != null)
                {
                    // Use Unturned's real rotation helper so the -90 degree offset for non-door
                    // barricades is applied. Quaternion.Euler(angle_x, angle_y, angle_z) left the
                    // frozen tray spawning vertically off the ground.
                    BarricadeManager.dropNonPlantedBarricade(
                        new Barricade(frozenTrayAsset),
                        tray.pos,
                        BarricadeManager.getRotation(frozenTrayAsset, tray.angle_x, tray.angle_y, tray.angle_z),
                        tray.owner, tray.group
                    );
                }

                if (config.EnableBlueCrystalFreezeEffect)
                {
                    EffectAsset effectAsset = (EffectAsset)Assets.find(EAssetType.EFFECT, config.BlueCrystalFreezeEffectId);
                    if (effectAsset != null)
                    {
                        var effectParams = new TriggerEffectParameters(effectAsset)
                        {
                            position = tray.pos,
                            relevantDistance = EffectManager.MEDIUM
                        };
                        EffectManager.triggerEffect(effectParams);
                    }
                }

                BlueCrystalCookingPlugin.Instance.freezingTrays.Remove(tray);
            }
        }
    }
}
