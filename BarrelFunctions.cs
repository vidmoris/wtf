using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


namespace Ocelot.BlueCrystalCooking.functions
{
    public static class BarrelFunctions
    {
        private static readonly Random _rng = new Random();


        public static void OnGestureChanged(UnturnedPlayer player, EPlayerGesture gesture)
        {
            if (player == null || player.Player == null)
                return;

            var config = BlueCrystalCookingPlugin.Instance.Configuration.Instance;

            if (!Physics.Raycast(player.Player.look.aim.position, player.Player.look.aim.forward, out RaycastHit raycastHit, 2f, RayMasks.BARRICADE))
                return;

            // A raycast hits a child collider, not the barricade's root model. Resolve the actual
            // barricade drop via its root component so tryGetInfo can match the model transform exactly.
            BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(raycastHit.transform);
            if (drop == null || drop.asset == null)
                return;

            ushort id = drop.asset.id;

            // tryGetInfo now reliably succeeds because we pass the drop's own root model transform.
            BarricadeManager.tryGetInfo(drop.model, out byte x, out byte y, out ushort plant, out ushort index, out BarricadeRegion region);


            // 1. PUNCH FROZEN TRAY (METH BAG LOGIC)
            if (id == config.FrozenTrayId)
            {
                int amount = GetBagAmount(config);
                player.GiveItem(config.BlueCrystalBagId, (byte)amount);


                if (config.EnableBlueCrystalFreezeEffect)
                {
                    TriggerEffect(config.BlueCrystalFreezeEffectId, drop.model.position);
                }


                UnturnedChat.Say(player, BlueCrystalCookingPlugin.Instance.Translate("bluecrystalbags_obtained", amount), UnityEngine.Color.white);
                BarricadeManager.destroyBarricade(region, x, y, plant, index);
                return;
            }


            // 2. PUNCH TO PICKUP CHEMICALS OR EMPTY/LIQUID TRAYS
            if (IsPickupableIngredient(id, config))
            {
                player.GiveItem(id, 1);
                BarricadeManager.destroyBarricade(region, x, y, plant, index);
                return;
            }


            // 3. BARREL STIR LOGIC
            if (id == config.BarrelObjectId)
            {
                if (!BlueCrystalCookingPlugin.Instance.placedBarrelsTransformsIngredients.TryGetValue(drop.model, out var barrelObj))
                    return;

                uint ingredientCount = 0;
                foreach (var ingredientId in config.drugIngredientIds)
                {
                    if (barrelObj.ingredients.Contains(ingredientId))
                        ingredientCount++;
                }

                if (ingredientCount == (uint)config.drugIngredientIds.Count)
                {
                    if (config.EnableBarrelStirEffect)
                    {
                        TriggerEffect(config.BarrelStirEffectId, drop.model.position);
                    }
                    barrelObj.progress += config.StirProgressAddPercentage;
                }
                else
                {
                    UnturnedChat.Say(player, BlueCrystalCookingPlugin.Instance.Translate("not_enough_ingredients"), UnityEngine.Color.white);
                    return;
                }

                if (barrelObj.progress >= 100)
                {
                    barrelObj.progress = 0;
                    foreach (var ingredientId in config.drugIngredientIds)
                    {
                        barrelObj.ingredients.Remove(ingredientId);
                    }


                    UnturnedChat.Say(player, BlueCrystalCookingPlugin.Instance.Translate("stir_successful"), UnityEngine.Color.white);


                    ItemBarricadeAsset trayAsset = (ItemBarricadeAsset)Assets.find(EAssetType.ITEM, config.BlueCrystalTrayId);
                    if (trayAsset != null)
                    {
                        // Use Unturned's real rotation helper so the -90 degree offset for non-door
                        // barricades is applied. Quaternion.identity left trays spawning vertical.
                        BarricadeManager.dropNonPlantedBarricade(
                            new Barricade(trayAsset),
                            player.Position,
                            BarricadeManager.getRotation(trayAsset, 0f, 0f, 0f),
                            (ulong)player.CSteamID,
                            (ulong)player.Player.quests.groupID
                        );
                    }
                }
            }
        }


        public static void OnBarricadeDeployed(Barricade barricade, ItemBarricadeAsset asset, Transform hit, ref Vector3 point, ref float angle_x, ref float angle_y, ref float angle_z, ref ulong owner, ref ulong group, ref bool shouldAllow)
        {
            Vector3 pos = point;
            ulong ownerBarricade = owner;
            var config = BlueCrystalCookingPlugin.Instance.Configuration.Instance;


            if (barricade.asset.id == config.BarrelObjectId)
            {
                BlueCrystalCookingPlugin.Instance.Wait(0.2f, () =>
                {
                    BarricadeDrop barrelDrop = BlueCrystalCookingPlugin.Instance.FindDropNear(pos, config.BarrelObjectId);
                    if (barrelDrop != null && !BlueCrystalCookingPlugin.Instance.placedBarrelsTransformsIngredients.ContainsKey(barrelDrop.model))
                    {
                        BlueCrystalCookingPlugin.Instance.placedBarrelsTransformsIngredients.Add(
                            barrelDrop.model,
                            new BarrelObject(new List<ushort>(), 0)
                        );
                    }
                });
            }


            foreach (var drugObject in config.drugIngredientIds)
            {
                if (drugObject != barricade.asset.id)
                    continue;

                // Ingredient dropped on top of a barrel: raycast straight down to find the barrel below.
                if (Physics.Raycast(pos, Vector3.down, out RaycastHit raycastHit, 10f, RayMasks.BARRICADE))
                {
                    BarricadeDrop barrelDrop = BarricadeManager.FindBarricadeByRootTransform(raycastHit.transform);
                    if (barrelDrop == null || barrelDrop.asset.id != config.BarrelObjectId)
                        continue;

                    if (BlueCrystalCookingPlugin.Instance.placedBarrelsTransformsIngredients.TryGetValue(barrelDrop.model, out var barrelObj))
                    {
                        var ownerPlayer = UnturnedPlayer.FromCSteamID(new CSteamID(ownerBarricade));
                        if (ownerPlayer != null)
                        {
                            UnturnedChat.Say(ownerPlayer, BlueCrystalCookingPlugin.Instance.Translate("ingredient_added", asset.FriendlyName), UnityEngine.Color.white);
                        }
                        barrelObj.ingredients.Add(barricade.asset.id);


                        BlueCrystalCookingPlugin.Instance.Wait(0.2f, () =>
                        {
                            BarricadeDrop ingredientDrop = BlueCrystalCookingPlugin.Instance.FindDropNear(pos, barricade.asset.id);
                            if (ingredientDrop != null
                                && BarricadeManager.tryGetInfo(ingredientDrop.model, out byte xi, out byte yi, out ushort pi, out ushort ii, out BarricadeRegion ri))
                            {
                                BarricadeManager.destroyBarricade(ri, xi, yi, pi, ii);
                            }
                        });
                    }
                }
            }
        }


        public static void BarricadeDamaged(Transform barricadeTransform, ushort pendingTotalDamage)
        {
        }


        private static int GetBagAmount(BlueCrystalCookingConfiguration config)
        {
            int min = config.BlueCrystalBagsAmountMin;
            int max = config.BlueCrystalBagsAmountMax;
            if (max < min) max = min;
            int amount = _rng.Next(min, max + 1);
            return amount < 1 ? 1 : amount;
        }


        private static bool IsPickupableIngredient(ushort id, BlueCrystalCookingConfiguration config)
        {
            return id == 10103 || id == 10104 || id == 10105
                || id == config.BlueCrystalTrayId || id == config.LiquidTrayId;
        }


        private static void TriggerEffect(ushort effectId, Vector3 position)
        {
            EffectAsset effectAsset = (EffectAsset)Assets.find(EAssetType.EFFECT, effectId);
            if (effectAsset != null)
            {
                var effectParams = new TriggerEffectParameters(effectAsset)
                {
                    position = position,
                    relevantDistance = EffectManager.MEDIUM
                };
                EffectManager.triggerEffect(effectParams);
            }
        }
    }
}
