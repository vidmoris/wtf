using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


namespace Ocelot.BlueCrystalCooking.functions
{
    public static class MethBagFunctions
    {
        public static void OnConsumeAction(Player instigatingPlayer, ItemConsumeableAsset consumeableAsset)
        {
            UnturnedPlayer player = UnturnedPlayer.FromPlayer(instigatingPlayer);
            if (player == null || consumeableAsset == null) return;


            if (consumeableAsset.id == BlueCrystalCookingPlugin.Instance.Configuration.Instance.BlueCrystalBagId)
            {
                BlueCrystalCookingPlugin.Instance.drugeffectPlayersList.Add(new DrugeffectTimeObject(player.Id));


                if (BlueCrystalCookingPlugin.Instance.Configuration.Instance.UseDrugEffectSpeed)
                {
                    player.Player.movement.sendPluginSpeedMultiplier(BlueCrystalCookingPlugin.Instance.Configuration.Instance.DrugEffectSpeedMultiplier);
                }


                if (BlueCrystalCookingPlugin.Instance.Configuration.Instance.UseDrugEffectJump)
                {
                    player.Player.movement.sendPluginJumpMultiplier(BlueCrystalCookingPlugin.Instance.Configuration.Instance.DrugEffectJumpMultiplier);
                }
            }
        }


        public static void Update()
        {
            if (BlueCrystalCookingPlugin.Instance == null || BlueCrystalCookingPlugin.Instance.drugeffectPlayersList == null) return;


            foreach (var drugeffect in BlueCrystalCookingPlugin.Instance.drugeffectPlayersList.ToList())
            {
                if (drugeffect == null) continue;


                if (BlueCrystalCookingPlugin.GetCurrentTime() - drugeffect.time >= BlueCrystalCookingPlugin.Instance.Configuration.Instance.DrugEffectDurationSecs)
                {
                    BlueCrystalCookingPlugin.Instance.drugeffectPlayersList.Remove(drugeffect);


                    UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(ulong.Parse(drugeffect.playerId)));
                    if (player != null && player.Player != null && player.Player.movement != null)
                    {
                        if (BlueCrystalCookingPlugin.Instance.Configuration.Instance.UseDrugEffectSpeed)
                        {
                            player.Player.movement.sendPluginSpeedMultiplier(1f);
                        }


                        if (BlueCrystalCookingPlugin.Instance.Configuration.Instance.UseDrugEffectJump)
                        {
                            player.Player.movement.sendPluginJumpMultiplier(1f);
                        }
                    }
                }
            }
        }
    }
}
