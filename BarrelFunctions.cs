using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


namespace Ocelot.BlueCrystalCooking.functions
{
    public static class BarrelFunctions
    {
        // Effect played and player killed when a duplicate chemical is dropped into a barrel.
        private const ushort DuplicateIngredientEffectId = 45;


        public static void OnGestureChanged(UnturnedPlayer player, EPlayerGesture gesture, bool allowStir)
        {
            if (player == null || player.Player == null)
                return;

            var config = BlueCrystalCookingPlugin.Instance.Configuration.Instance;

            // Resolve the barricade the player is aiming at. A thin forward ray (like vanilla) is
            // tried first for close range, then a forgiving sphere sweep so small/thin chemicals
            // are still caught when the server-side aim is a frame behind or slightly off-centre.
            BarricadeDrop drop = ResolveAimBarricade(player);
            if (drop == null || drop.asset == null)
                return;

            ushort id = drop.asset.id;

            // Resolve the region coordinates for the (non-obsolete) destroyBarricade(drop, x, y, plant) overload.
            BarricadeManager.tryGetRegion(drop.model, out byte x, out byte y, out ushort plant, out BarricadeRegion _);


            // 1. PUNCH FROZEN TRAY (METH BAG LOGIC)
            if (id == config.FrozenTrayId)
            {
                player.GiveItem(config.BlueCrystalBagId, 1);


                if (config.EnableBlueCrystalFreezeEffect)
                {
                    TriggerEffect(config.BlueCrystalFreezeEffectId, drop.model.position);
                }


                UnturnedChat.Say(player, BlueCrystalCookingPlugin.Instance.Translate("bluecrystalbags_obtained"), UnityEngine.Color.white);
                BarricadeManager.destroyBarricade(drop, x, y, plant);
                return;
            }


            // 2. PUNCH TO PICKUP CHEMICALS OR EMPTY/LIQUID TRAYS
            if (IsPickupableIngredient(id, config))
            {
                player.GiveItem(id, 1);
                BarricadeManager.destroyBarricade(drop, x, y, plant);
                return;
            }


            // 3. POINT AT BARREL TO RETRIEVE THE CHEMICALS INSIDE (point only; punches stir)
            if (!allowStir && id == config.BarrelObjectId)
            {
                if (!BlueCrystalCookingPlugin.Instance.placedBarrelsTransformsIngredients.TryGetValue(drop.model, out var barrelObj))
                    return;

                if (barrelObj.ingredients.Count == 0)
                {
                    UnturnedChat.Say(player, BlueCrystalCookingPlugin.Instance.Translate("barrel_empty"), UnityEngine.Color.white);
                    return;
                }

                foreach (var ingredientId in barrelObj.ingredients)
                {
                    player.GiveItem(ingredientId, 1);
                }
                barrelObj.ingredients.Clear();
                barrelObj.progress = 0;
                UnturnedChat.Say(player, BlueCrystalCookingPlugin.Instance.Translate("chemicals_retrieved"), UnityEngine.Color.white);
                return;
            }


            // 4. BARREL STIR LOGIC (punches only; the point emote is reserved for picking things up)
            if (allowStir && id == config.BarrelObjectId)
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

                        // Only one of each chemical (10103/10104/10105) may go into a barrel. Adding a
                        // duplicate triggers a violent reaction: effect 45 at the barrel, the barrel
                        // breaks, the offending ingredient is consumed, and the placer is killed.
                        //
                        // The reaction is DEFERRED so the normal deploy + equipment.use() flow finishes
                        // consuming the item first. Killing synchronously inside the deploy hook clears
                        // the player's equipment before use() runs, so removeItem is skipped and the
                        // chemical is left in the inventory (and ends up on the corpse).
                        if (barrelObj.ingredients.Contains(barricade.asset.id))
                        {
                            if (ownerPlayer != null)
                            {
                                UnturnedChat.Say(ownerPlayer, BlueCrystalCookingPlugin.Instance.Translate("duplicate_ingredient"), UnityEngine.Color.white);
                            }

                            Vector3 barrelPos = barrelDrop.model.position;
                            Transform barrelModel = barrelDrop.model;
                            ushort ingredientId = barricade.asset.id;

                            BlueCrystalCookingPlugin.Instance.Wait(0.2f, () =>
                            {
                                // Effect first, while the barrel position is still meaningful.
                                TriggerEffect(DuplicateIngredientEffectId, barrelPos);

                                // The barrel breaks in the explosion.
                                DestroyBarricadeByModel(barrelModel);
                                BlueCrystalCookingPlugin.Instance.placedBarrelsTransformsIngredients.Remove(barrelModel);

                                // Destroy the spawned chemical barricade (the one that caused the reaction).
                                BarricadeDrop ingredientDrop = BlueCrystalCookingPlugin.Instance.FindDropNear(pos, ingredientId);
                                if (ingredientDrop != null
                                    && BarricadeManager.tryGetRegion(ingredientDrop.model, out byte xi, out byte yi, out ushort pi, out BarricadeRegion _))
                                {
                                    BarricadeManager.destroyBarricade(ingredientDrop, xi, yi, pi);
                                }

                                // By now equipment.use() has consumed the item, so the kill won't leave
                                // a ghost chemical behind in the corpse inventory.
                                KillPlacer(ownerPlayer);
                            });
                            return;
                        }

                        if (ownerPlayer != null)
                        {
                            UnturnedChat.Say(ownerPlayer, BlueCrystalCookingPlugin.Instance.Translate("ingredient_added", asset.FriendlyName), UnityEngine.Color.white);
                        }
                        barrelObj.ingredients.Add(barricade.asset.id);


                        BlueCrystalCookingPlugin.Instance.Wait(0.2f, () =>
                        {
                            BarricadeDrop ingredientDrop = BlueCrystalCookingPlugin.Instance.FindDropNear(pos, barricade.asset.id);
                            if (ingredientDrop != null
                                && BarricadeManager.tryGetRegion(ingredientDrop.model, out byte xi, out byte yi, out ushort pi, out BarricadeRegion _))
                            {
                                BarricadeManager.destroyBarricade(ingredientDrop, xi, yi, pi);
                            }
                        });
                    }
                }
            }
        }


        public static void BarricadeDamaged(Transform barricadeTransform, ushort pendingTotalDamage)
        {
        }


        private static BarricadeDrop ResolveAimBarricade(UnturnedPlayer player)
        {
            Ray ray = new Ray(player.Player.look.aim.position, player.Player.look.aim.forward);

            // Thin ray first: matches vanilla punch range and handles close targets precisely.
            if (Physics.Raycast(ray, out RaycastHit hit, 2.5f, RayMasks.BARRICADE))
            {
                BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(hit.transform);
                if (drop != null)
                    return drop;
            }

            // Forgiving sphere sweep: catches small/thin colliders (chemicals) the centre ray grazes
            // past, and tolerates a frame of aim desync between client and server.
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.5f, 2.5f, RayMasks.BARRICADE);
            if (hits != null && hits.Length > 0)
            {
                foreach (RaycastHit h in hits.OrderBy(h => h.distance))
                {
                    BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(h.transform);
                    if (drop != null)
                        return drop;
                }
            }

            return null;
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


        private static void KillPlacer(UnturnedPlayer player)
        {
            if (player == null || player.Player == null || player.Player.life == null)
                return;

            // 255 damage guarantees a kill regardless of max health. BURNING fits the reactive
            // chemical-explosion flavour; the server is credited as the killer.
            player.Player.life.askDamage(255, Vector3.up, EDeathCause.BURNING, ELimb.SPINE, Provider.server, out EPlayerKill _);
        }


        private static void DestroyBarricadeByModel(Transform model)
        {
            if (model == null || !model.gameObject.activeInHierarchy)
                return;

            BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(model);
            if (drop != null
                && BarricadeManager.tryGetRegion(model, out byte x, out byte y, out ushort plant, out BarricadeRegion _))
            {
                BarricadeManager.destroyBarricade(drop, x, y, plant);
            }
        }
    }
}
