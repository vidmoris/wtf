using Ocelot.BlueCrystalCooking.functions;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;


namespace Ocelot.BlueCrystalCooking
{
    public class BlueCrystalCookingPlugin : RocketPlugin<BlueCrystalCookingConfiguration>
    {
        public static BlueCrystalCookingPlugin Instance;
        public const string VERSION = "2.0.0";


        private int frameCounter = 0;
        public long timer = 0;
        public Dictionary<Transform, BarrelObject> placedBarrelsTransformsIngredients = new Dictionary<Transform, BarrelObject>();
        public List<DrugeffectTimeObject> drugeffectPlayersList = new List<DrugeffectTimeObject>();
        public List<FreezingTrayObject> freezingTrays = new List<FreezingTrayObject>();


        protected override void Load()
        {
            Instance = this;
            Logger.Log("BlueCrystalCookingPlugin v" + VERSION + " by Ocelot loaded! Enjoy! :)", ConsoleColor.Yellow);


            BarricadeManager.onDeployBarricadeRequested += OnBarricadeDeployed;
            BarricadeDrop.OnSalvageRequested_Global += OnBarricadeSalvaged;
            PlayerAnimator.OnGestureChanged_Global += OnGestureChanged;
            UseableConsumeable.onConsumePerformed += OnConsumeAction;
            BarricadeManager.onDamageBarricadeRequested += OnBarricadeDamaged;


            if (Level.isLoaded)
            {
                AddExistingBarrels(1);
            }
            else
            {
                Level.onLevelLoaded += AddExistingBarrels;
            }
        }


        private void OnBarricadeSalvaged(BarricadeDrop drop, SteamPlayer instigatorClient, ref bool shouldAllow)
        {
            if (drop.asset.id == Configuration.Instance.BarrelObjectId)
            {
                if (placedBarrelsTransformsIngredients.TryGetValue(drop.model, out var barrelObj))
                {
                    placedBarrelsTransformsIngredients.Remove(drop.model);
                }
            }
        }


        protected override void Unload()
        {
            BarricadeManager.onDeployBarricadeRequested -= OnBarricadeDeployed;
            BarricadeDrop.OnSalvageRequested_Global -= OnBarricadeSalvaged;
            PlayerAnimator.OnGestureChanged_Global -= OnGestureChanged;
            UseableConsumeable.onConsumePerformed -= OnConsumeAction;
            BarricadeManager.onDamageBarricadeRequested -= OnBarricadeDamaged;
        }


        private void OnConsumeAction(Player player, ItemConsumeableAsset asset)
        {
            MethBagFunctions.OnConsumeAction(player, asset);
        }


        private void OnGestureChanged(PlayerAnimator animator, EPlayerGesture gesture)
        {
            if (gesture == EPlayerGesture.PUNCH_LEFT || gesture == EPlayerGesture.PUNCH_RIGHT)
            {
                var player = UnturnedPlayer.FromPlayer(animator.player);
                if (player != null)
                {
                    // FIXED: Now we only call one consolidated function to prevent ghost-hit errors
                    BarrelFunctions.OnGestureChanged(player, gesture);
                }
            }
        }


        private void OnBarricadeDeployed(Barricade barricade, ItemBarricadeAsset asset, Transform hit, ref Vector3 point, ref float angle_x, ref float angle_y, ref float angle_z, ref ulong owner, ref ulong group, ref bool shouldAllow)
        {
            BarrelFunctions.OnBarricadeDeployed(barricade, asset, hit, ref point, ref angle_x, ref angle_y, ref angle_z, ref owner, ref group, ref shouldAllow);
            FreezerFunctions.OnBarricadeDeployed(barricade, asset, hit, ref point, ref angle_x, ref angle_y, ref angle_z, ref owner, ref group, ref shouldAllow);
        }


        private void OnBarricadeDamaged(CSteamID instigator, Transform barricadeTransform, ref ushort pendingDamage, ref bool shouldAllow, EDamageOrigin damageOrigin)
        {
            BarrelFunctions.BarricadeDamaged(barricadeTransform, pendingDamage);
        }


        /// <summary>
        /// Finds the barricade drop of the given asset id closest to a world position.
        /// Replaces the old exact-match helpers (GetPlacedObjectTransform / GetBarricadeDataAtPosition /
        /// GetAllObjects) which relied on Vector3 == equality and routinely failed because the position
        /// recorded at deploy time does not exactly equal the spawned model's transform position.
        /// </summary>
        public BarricadeDrop FindDropNear(Vector3 position, ushort assetId)
        {
            BarricadeDrop nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (var region in BarricadeManager.regions)
            {
                foreach (var drop in region.drops)
                {
                    if (drop == null || drop.asset == null || drop.asset.id != assetId)
                        continue;

                    float sqr = (drop.model.position - position).sqrMagnitude;
                    if (sqr < nearestSqr)
                    {
                        nearestSqr = sqr;
                        nearest = drop;
                    }
                }
            }
            return nearest;
        }


        private void AddExistingBarrels(int level)
        {
            Logger.Log("Adding map barrels to list...", ConsoleColor.Green);
            foreach (var region in BarricadeManager.regions)
            {
                foreach (var drop in region.drops)
                {
                    if (drop.asset.id == Configuration.Instance.BarrelObjectId)
                    {
                        if (!placedBarrelsTransformsIngredients.ContainsKey(drop.model))
                        {
                            // Each barrel gets its own ingredient list. (Previously every barrel shared
                            // one list instance, so adding an ingredient to one barrel added it to all.)
                            placedBarrelsTransformsIngredients.Add(drop.model, new BarrelObject(new List<ushort>(), 0));
                        }
                        else
                        {
                            Logger.Log("Duplicated entry detected, skipping object. (No need to worry)", ConsoleColor.Yellow);
                        }
                    }
                }
            }
            Logger.Log("All barrels added.", ConsoleColor.Green);
        }


        public override TranslationList DefaultTranslations => new TranslationList
        {
            {"not_enough_ingredients", "There are <color=#ff3c19>not enough ingredients</color> in the barrel to stir them into blue crystal." },
            {"ingredient_added", "You have <color=#75ff19>added {0}</color> to the barrel." },
            {"stir_successful", "You have <color=#75ff19>successfully mixed</color> the ingredients into a tray filled with <color=#1969ff>liquid blue crystal</color>." },
            {"bluecrystalbags_obtained", "You have <color=#75ff19>successfully obtained {0} bags</color> filled with <color=#1969ff>blue crystal</color>." }
        };


        private void Update()
        {
            frameCounter++;
            if (frameCounter % 5 != 0) return;


            long now = GetCurrentTime();
            if (now - timer >= 1)
            {
                timer = now;
                MethBagFunctions.Update();
                FreezerFunctions.Update();
            }
        }


        public static long GetCurrentTime()
        {
            return (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }


        public void Wait(float seconds, System.Action action)
        {
            StartCoroutine(_wait(seconds, action));
        }


        private IEnumerator _wait(float time, System.Action callback)
        {
            yield return new WaitForSeconds(time);
            callback();
        }
    }
}
