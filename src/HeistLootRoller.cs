using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHeist
{
	public enum HeistOutcome
	{
		FoodThief,
		PettyThief,
		Thief,
		EmptyHanded
	}

	public class HeistOpportunity
	{
		public HeistOutcome TargetType;
		public string DepartmentKey;

		public string GetTargetDisplayName()
		{
			switch (TargetType)
			{
				case HeistOutcome.FoodThief:
					return NightHeistMod.L("target_food_supplier");
				case HeistOutcome.PettyThief:
					return NightHeistMod.L("target_petty");
				case HeistOutcome.Thief:
					if (!string.IsNullOrEmpty(DepartmentKey))
					{
						return NightHeistMod.L(DepartmentKey);
					}
					return NightHeistMod.L("dept_security");
				default:
					return NightHeistMod.L("role_empty");
			}
		}

		public string GetButtonLabel()
		{
			switch (TargetType)
			{
				case HeistOutcome.FoodThief:
					return NightHeistMod.L("choice_food_supplier");
				case HeistOutcome.PettyThief:
					return NightHeistMod.L("choice_petty");
				case HeistOutcome.Thief:
					return NightHeistMod.L("choice_dept", GetTargetDisplayName());
				default:
					return GetTargetDisplayName();
			}
		}
	}

	public class ScoutResult
	{
		public bool BlockedByPatrol;
		public bool CaughtBySecurity;
		public int SecurityRepLost;
		public List<HeistOpportunity> Opportunities = new List<HeistOpportunity>();
	}

	public class HeistResult
	{
		public HeistOutcome Outcome;
		public string TargetName;
		public int PlacedCount;
		public int OverflowCount;
		public long TotalLootValue;
		public int CrimeRecorded;
		public bool CaughtBySecurity;
		public int SecurityRepLost;
	}

	public static class HeistLootRoller
	{
		public const string CRIME_THEFT = "THEFT";

		private static readonly string[] DEPARTMENT_KEYS = new string[]
		{
			"dept_security",
			"dept_engineering",
			"dept_service",
			"dept_medical",
			"dept_command"
		};

		public static ScoutResult ScoutOpportunities()
		{
			ScoutResult scout = new ScoutResult();
			int blockChance = (NightHeistMod.PatrolBlockChance != null)
				? Mathf.Clamp(NightHeistMod.PatrolBlockChance.Value, 0, 100)
				: 10;

			if (blockChance > 0 && RNG.Roll(blockChance))
			{
				scout.BlockedByPatrol = true;
				RollPatrolCaught(scout);
				return scout;
			}

			scout.Opportunities = DrawDistinctOpportunities(2);
			return scout;
		}

		public static HeistResult ExecuteOpportunity(HeistOpportunity opportunity)
		{
			HeistResult result = new HeistResult();
			if (opportunity == null || opportunity.TargetType == HeistOutcome.EmptyHanded)
			{
				result.Outcome = HeistOutcome.EmptyHanded;
				return result;
			}

			result.Outcome = opportunity.TargetType;
			result.TargetName = opportunity.GetTargetDisplayName();

			List<GameItem> loot = GenerateLootForOutcome(opportunity.TargetType);
			int forcedHeat = (opportunity.TargetType == HeistOutcome.FoodThief) ? 25 : 0;
			PlaceLootOnAfterhourGround(loot, forcedHeat, result);
			RecordTheftCrime(result);
			return result;
		}

		private static List<HeistOpportunity> DrawDistinctOpportunities(int count)
		{
			int wFood = GetSafeWeight(NightHeistMod.WeightFoodThief, 4);
			int wPetty = GetSafeWeight(NightHeistMod.WeightPettyThief, 3);
			int wThief = GetSafeWeight(NightHeistMod.WeightThief, 2);

			if (wFood + wPetty + wThief <= 0)
			{
				wFood = 4;
				wPetty = 3;
				wThief = 2;
			}

			List<HeistOutcome> poolTypes = new List<HeistOutcome>();
			List<int> poolWeights = new List<int>();

			if (wFood > 0)
			{
				poolTypes.Add(HeistOutcome.FoodThief);
				poolWeights.Add(wFood);
			}
			if (wPetty > 0)
			{
				poolTypes.Add(HeistOutcome.PettyThief);
				poolWeights.Add(wPetty);
			}
			if (wThief > 0)
			{
				poolTypes.Add(HeistOutcome.Thief);
				poolWeights.Add(wThief);
			}

			List<HeistOpportunity> picked = new List<HeistOpportunity>();
			int draws = Mathf.Min(count, poolTypes.Count);
			for (int d = 0; d < draws; d++)
			{
				int total = 0;
				for (int i = 0; i < poolWeights.Count; i++)
				{
					total += poolWeights[i];
				}
				if (total <= 0)
				{
					break;
				}

				int roll = UnityEngine.Random.Range(0, total);
				int chosenIdx = poolTypes.Count - 1;
				for (int i = 0; i < poolWeights.Count; i++)
				{
					if (roll < poolWeights[i])
					{
						chosenIdx = i;
						break;
					}
					roll -= poolWeights[i];
				}

				HeistOutcome type = poolTypes[chosenIdx];
				poolTypes.RemoveAt(chosenIdx);
				poolWeights.RemoveAt(chosenIdx);

				HeistOpportunity opp = new HeistOpportunity();
				opp.TargetType = type;
				if (type == HeistOutcome.Thief)
				{
					opp.DepartmentKey = DEPARTMENT_KEYS[UnityEngine.Random.Range(0, DEPARTMENT_KEYS.Length)];
				}
				picked.Add(opp);
			}

			return picked;
		}

		private static int GetSafeWeight(ConfigEntry<int> entry, int fallback)
		{
			if (entry == null)
			{
				return fallback;
			}
			return Mathf.Max(0, entry.Value);
		}

		private static float GetStockMultiplier()
		{
			bool useMult = (NightHeistMod.UseBMRepStockMult == null) || NightHeistMod.UseBMRepStockMult.Value;
			if (!useMult)
			{
				return 1f;
			}
			try
			{
				if (PlayerStore.Instance != null && StoreReputation.GetBMFaction() != null)
				{
					return StoreClientList.GetBMRepStockMult();
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[NightHeist] Could not read BM rep stock multiplier: " + ex.Message);
			}
			return 1f;
		}

		private static void RollPatrolCaught(ScoutResult scout)
		{
			int caughtChance = (NightHeistMod.EmptyCaughtChance != null) ? Mathf.Clamp(NightHeistMod.EmptyCaughtChance.Value, 0, 100) : 35;
			int penalty = (NightHeistMod.SecurityRepPenalty != null) ? Mathf.Max(0, NightHeistMod.SecurityRepPenalty.Value) : 3;

			if (caughtChance > 0 && penalty > 0 && RNG.Roll(caughtChance))
			{
				scout.CaughtBySecurity = true;
				scout.SecurityRepLost = penalty;
				try
				{
					StoreReputation.ModSecReputation(-penalty);
				}
				catch (Exception ex)
				{
					Debug.LogError("[NightHeist] Failed to deduct Security reputation: " + ex);
				}
			}
		}

		private static void RecordTheftCrime(HeistResult result)
		{
			if (result == null || result.TotalLootValue <= 0)
			{
				return;
			}

			float mult = (NightHeistMod.CrimeValueMultiplier != null) ? Mathf.Max(0f, NightHeistMod.CrimeValueMultiplier.Value) : 1.5f;
			if (mult <= 0f)
			{
				return;
			}

			PlayerStore store = PlayerStore.Instance;
			if (store == null || !store.isSecurityRecordIntroduced || store.secData == null)
			{
				return;
			}

			int rawCrime = Mathf.RoundToInt(result.TotalLootValue * mult);
			if (rawCrime <= 0)
			{
				return;
			}

			try
			{
				int beforeTotal = store.secData.GetTotalCrimeValue();
				store.secData.CommitCrime(CRIME_THEFT, rawCrime);
				int afterTotal = store.secData.GetTotalCrimeValue();
				result.CrimeRecorded = Mathf.Max(0, afterTotal - beforeTotal);
				Debug.Log("[NightHeist] Recorded THEFT crime: lootValue=" + result.TotalLootValue + ", mult=" + mult.ToString("F2") + ", raw=" + rawCrime + ", applied=" + result.CrimeRecorded);
			}
			catch (Exception ex)
			{
				Debug.LogError("[NightHeist] Failed to record theft crime: " + ex);
			}
		}

		private static List<GameItem> GenerateLootForOutcome(HeistOutcome outcome)
		{
			float mult = GetStockMultiplier();
			switch (outcome)
			{
				case HeistOutcome.FoodThief:
					return RollFoodThiefLoot(mult);
				case HeistOutcome.PettyThief:
					return RollPettyThiefLoot(mult);
				case HeistOutcome.Thief:
					return RollThiefLoot(mult);
				default:
					return new List<GameItem>();
			}
		}

		/// <summary>
		/// Mirrors StoreClientList.CreateFoodThief() without reducing Commissary stock.
		/// </summary>
		private static List<GameItem> RollFoodThiefLoot(float mult)
		{
			List<GameItem> items = new List<GameItem>();

			RollItemById(items, 15f * mult, "galaxy_blend");
			RollItemById(items, 10f * mult, "galaxy_blend");
			RollItemById(items, 8f * mult, "galaxy_blend");

			RollItemById(items, 8f * mult, "processed_milk");
			RollItemById(items, 8f * mult, "processed_milk");
			RollItemById(items, 12f * mult, "processed_milk");
			RollItemById(items, 10f * mult, "processed_milk");

			RollItemById(items, 10f * mult, "processed_meat");
			RollItemById(items, 10f * mult, "processed_meat");
			RollItemById(items, 10f * mult, "processed_meat");
			RollItemById(items, 10f * mult, "processed_meat");

			RollItemById(items, 10f * mult, "nutrifruit");
			RollItemById(items, 10f * mult, "nutrifruit");
			RollItemById(items, 10f * mult, "nutrifruit");
			RollItemById(items, 10f * mult, "nutrifruit");
			RollItemById(items, 20f * mult, "nutrifruit");

			RollItemById(items, 10f * mult, "processed_juice");
			RollItemById(items, 5f * mult, "processed_juice");
			RollItemById(items, 5f * mult, "processed_juice");
			RollItemById(items, 5f * mult, "processed_juice");

			// Vanilla guaranteed nutrifruit
			AddSafe(items, SpawnById("nutrifruit"));

			return items;
		}

		/// <summary>
		/// Mirrors StoreClientList.CreatePettyThief().
		/// </summary>
		private static List<GameItem> RollPettyThiefLoot(float mult)
		{
			List<GameItem> items = new List<GameItem>();

			for (int i = 0; i < 4; i++)
			{
				if (RNG.Roll(25f * mult))
				{
					AddSafe(items, SpawnFromTableSafe("packedFoodTable"));
				}
			}

			for (int i = 0; i < 4; i++)
			{
				if (RNG.Roll(25f * mult))
				{
					AddSafe(items, SpawnFromTableSafe("householdTable"));
				}
			}

			for (int i = 0; i < 2; i++)
			{
				if (RNG.Roll(25f * mult))
				{
					AddSafe(items, SpawnFromTableSafe("medicalTable"));
				}
			}

			if (NetworkUpgrade.IsWinemakerUnlocked())
			{
				for (int i = 0; i < 3; i++)
				{
					RollItemById(items, 25f * mult, "wine_yeast_red");
				}
			}

			// Vanilla guaranteed packedFoodTable roll
			AddSafe(items, SpawnFromTableSafe("packedFoodTable"));

			return items;
		}

		/// <summary>
		/// Mirrors StoreClientList.CreateThief().
		/// </summary>
		private static List<GameItem> RollThiefLoot(float mult)
		{
			List<GameItem> items = new List<GameItem>();

			RollItemById(items, 5f * mult, "bottled_water_premium");
			RollItemById(items, 5f * mult, "skincare_cream");
			RollItemById(items, 6f * mult, "neuroactive_perfume");
			RollItemById(items, 4f * mult, "pheromone_perfume");
			RollItemById(items, 5f * mult, "turbo_booster_adv");
			RollItemById(items, 5f * mult, "cmd_keycard");
			RollItemById(items, 15f * mult, "scratcher_stack");
			RollItemById(items, 10f * mult, "sec_keycard");
			RollItemById(items, 15f * mult, "med_keycard");
			RollItemById(items, 15f * mult, "eng_keycard");
			RollItemById(items, 20f * mult, "ser_keycard");

			RollItemById(items, 12f * mult, "random_performance_module");
			RollItemById(items, 12f * mult, "random_efficiency_module");
			RollItemById(items, 12f * mult, "random_quality_module");
			RollItemById(items, 8f * mult, "random_overclock_module");
			RollItemById(items, 8f * mult, "random_eco_module");
			RollItemById(items, 8f * mult, "random_fineness_module");
			RollItemById(items, 12f * mult, "portable_water_purifier");
			RollItemById(items, 12f * mult, "water_filter_adv");

			if (RNG.Roll(20f * mult))
			{
				AddSafe(items, CreateGenuineInjectorSafe());
			}

			if (StoreReputation.IsPerkUnlocked("BM_RELIABLE") || RNG.Roll(30f * mult))
			{
				AddSafe(items, RollThiefLootCrate());
			}

			RollItemById(items, 5f * mult, "crypto_module_sec");
			RollItemById(items, 2f * mult, "crypto_module_cmd");
			RollItemById(items, 7f * mult, "crypto_module_med");
			RollItemById(items, 7f * mult, "crypto_module_eng");
			RollItemById(items, 10f * mult, "crypto_module_ser");

			// Vanilla fallback when nothing rolled
			if (items.Count == 0)
			{
				string fallbackId = RNG.Roll(50) ? "common_chemical" : "common_medical";
				AddSafe(items, SpawnById(fallbackId));
				AddSafe(items, SpawnById(fallbackId));
				AddSafe(items, SpawnById(fallbackId));
			}

			return items;
		}

		private static GameItem RollThiefLootCrate()
		{
			try
			{
				int r = RNG.GetRandomInt(0, 100);
				if (r <= 20)
				{
					return PreBuiltItemHelper.LootCrateEvidence();
				}
				if (r <= 40)
				{
					return PreBuiltItemHelper.LootCrateMedical();
				}
				if (r <= 60)
				{
					return PreBuiltItemHelper.LootCrateEngineering();
				}
				if (r <= 75)
				{
					return PreBuiltItemHelper.LootCrateSecurity();
				}
				return PreBuiltItemHelper.LootCrateService();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[NightHeist] Failed to spawn thief loot crate: " + ex.Message);
				return null;
			}
		}

		private static GameItem CreateGenuineInjectorSafe()
		{
			try
			{
				return PreBuiltItemHelper.CreateRealGenuineInjector();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[NightHeist] Failed to spawn genuine injector: " + ex.Message);
				return null;
			}
		}

		private static void RollItemById(List<GameItem> list, float chance, string itemId)
		{
			if (RNG.Roll(chance))
			{
				AddSafe(list, SpawnById(itemId));
			}
		}

		private static GameItem SpawnById(string itemId)
		{
			try
			{
				return ItemSpawner.Spawn(itemId);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[NightHeist] Failed to spawn item '" + itemId + "': " + ex.Message);
				return null;
			}
		}

		private static GameItem SpawnFromTableSafe(string tableId)
		{
			try
			{
				return ItemSpawner.SpawnFromTable(tableId);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[NightHeist] Failed to spawn from table '" + tableId + "': " + ex.Message);
				return null;
			}
		}

		private static void AddSafe(List<GameItem> list, GameItem item)
		{
			if (item != null)
			{
				list.Add(item);
			}
		}

		private static long GetItemBaseValueSafe(GameItem item)
		{
			if (item == null)
			{
				return 0L;
			}
			try
			{
				long v = item.GetBaseValue();
				return (v > 0L) ? v : 0L;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[NightHeist] Could not read item base value for '" + item.identifier + "': " + ex.Message);
				return 0L;
			}
		}

		private static void PlaceLootOnAfterhourGround(List<GameItem> items, int forcedHeat, HeistResult result)
		{
			if (EmporiumEntry.Instance == null || EmporiumEntry.Instance.afterhourInventory == null)
			{
				Debug.LogError("[NightHeist] afterhourInventory is null; cannot place stolen items.");
				return;
			}

			bool applyHeat = (NightHeistMod.ApplyStolenHeat == null || NightHeistMod.ApplyStolenHeat.Value)
				&& PlayerStore.Instance != null
				&& PlayerStore.Instance.isStolenItemIntroduced;

			for (int i = 0; i < items.Count; i++)
			{
				GameItem item = items[i];
				if (item == null)
				{
					continue;
				}

				long itemValue = GetItemBaseValueSafe(item);

				// afterhourInventory only accepts owned items
				GeneralHelper.SetItemOwned(item, true);

				if (applyHeat)
				{
					StolenHelper.InitStolenItem(item, forcedHeat);
				}

				if (GraphUtils.TryAcceptAllOrDestroy(EmporiumEntry.Instance.afterhourInventory, item, "night heist ground full"))
				{
					result.PlacedCount++;
					result.TotalLootValue += itemValue;
				}
				else
				{
					result.OverflowCount++;
				}
			}
		}
	}
}
