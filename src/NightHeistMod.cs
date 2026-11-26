using System;
using HarmonyLib;
using UnityEngine;

namespace NightHeist
{
	public class NightHeistMod : IMod
	{
		public const string MOD_ID = "NightHeist";

		public static ConfigEntry<int> PatrolBlockChance;
		public static ConfigEntry<int> WeightFoodThief;
		public static ConfigEntry<int> WeightPettyThief;
		public static ConfigEntry<int> WeightThief;

		public static ConfigEntry<int> EmptyCaughtChance;
		public static ConfigEntry<int> SecurityRepPenalty;
		public static ConfigEntry<float> CrimeValueMultiplier;
		public static ConfigEntry<bool> UseBMRepStockMult;
		public static ConfigEntry<bool> ApplyStolenHeat;

		private NightHeistLocation location;

		public void Init(ModManifest manifest)
		{
			InitConfig();
			Debug.Log("[NightHeist] Initialized v" + (manifest != null ? manifest.ModVersion : "1.1.0"));
		}

		public void OnEnable()
		{
			if (location == null)
			{
				location = new NightHeistLocation();
			}
			ModHelper.AddAfterhourLocation(location);
			Debug.Log("[NightHeist] Registered afterhour location: " + location.Id);
		}

		public void OnDisable()
		{
			ModHelper.RemoveAfterhourLocations(MOD_ID);
			Debug.Log("[NightHeist] Unregistered afterhour locations");
		}

		private static void InitConfig()
		{
			ModConfig cfg = ModHelper.GetConfig(MOD_ID);

			PatrolBlockChance = cfg.Define(
				"patrol_block_chance",
				10,
				"Scout & Weights / 踩点与权重",
				delegate { return L("cfg_patrol_block"); },
				0,
				100,
				delegate { return L("cfg_patrol_block_tip"); }
			);

			WeightFoodThief = cfg.Define(
				"weight_food_thief",
				4,
				"Scout & Weights / 踩点与权重",
				delegate { return L("cfg_weight_food"); },
				0,
				20,
				delegate { return L("cfg_weight_food_tip"); }
			);

			WeightPettyThief = cfg.Define(
				"weight_petty_thief",
				3,
				"Scout & Weights / 踩点与权重",
				delegate { return L("cfg_weight_petty"); },
				0,
				20,
				delegate { return L("cfg_weight_petty_tip"); }
			);

			WeightThief = cfg.Define(
				"weight_thief",
				2,
				"Scout & Weights / 踩点与权重",
				delegate { return L("cfg_weight_thief"); },
				0,
				20,
				delegate { return L("cfg_weight_thief_tip"); }
			);

			EmptyCaughtChance = cfg.Define(
				"empty_caught_chance",
				35,
				"Risk & Rules / 风险与规则",
				delegate { return L("cfg_caught_chance"); },
				0,
				100,
				delegate { return L("cfg_caught_chance_tip"); }
			);

			SecurityRepPenalty = cfg.Define(
				"security_rep_penalty",
				3,
				"Risk & Rules / 风险与规则",
				delegate { return L("cfg_sec_penalty"); },
				0,
				20,
				delegate { return L("cfg_sec_penalty_tip"); }
			);

			CrimeValueMultiplier = cfg.Define(
				"crime_value_multiplier",
				1.5f,
				"Risk & Rules / 风险与规则",
				delegate { return L("cfg_crime_mult"); },
				0f,
				5f,
				delegate { return L("cfg_crime_mult_tip"); }
			);

			UseBMRepStockMult = cfg.Define(
				"use_bm_rep_stock_mult",
				true,
				"Risk & Rules / 风险与规则",
				delegate { return L("cfg_use_bm_mult"); },
				delegate { return L("cfg_use_bm_mult_tip"); }
			);

			ApplyStolenHeat = cfg.Define(
				"apply_stolen_heat",
				true,
				"Risk & Rules / 风险与规则",
				delegate { return L("cfg_apply_stolen_heat"); },
				delegate { return L("cfg_apply_stolen_heat_tip"); }
			);
		}

		public static string L(string key, params object[] args)
		{
			return ModHelper.GetLocalized(MOD_ID, key, args);
		}
	}

	[HarmonyPatch(typeof(SecData), "GetCrimeString")]
	public static class SecDataCrimeStringPatch
	{
		public static bool Prefix(string crimeID, ref string __result)
		{
			if (crimeID == HeistLootRoller.CRIME_THEFT)
			{
				__result = NightHeistMod.L("crime_theft");
				return false;
			}
			return true;
		}
	}
}
