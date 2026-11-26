using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NightHeist
{
	public class NightHeistLocation : AfterhourLocation
	{
		private enum NightPhase
		{
			ReadyToScout,
			ChoosingTarget,
			PatrolBlocked,
			HeistCompleted,
			Abandoned
		}

		public const string LOCATION_ID = "night_heist";

		private const string EL_STATUS = "heist_status";
		private const string EL_ODDS = "heist_odds";
		private const string EL_RISK = "heist_risk";
		private const string EL_SCOUT = "heist_scout";
		private const string EL_CHOICE_0 = "heist_choice_0";
		private const string EL_CHOICE_1 = "heist_choice_1";
		private const string EL_ABANDON = "heist_abandon";

		private CustomUIWindow panel;
		private GameObject customStageObj;
		private NightPhase phase;
		private ScoutResult scoutResult;
		private HeistResult lastResult;

		public override string Id
		{
			get { return LOCATION_ID; }
		}

		public override string Source
		{
			get { return NightHeistMod.MOD_ID; }
		}

		public override string ImageKey
		{
			get { return NightHeistMod.MOD_ID + ":night_heist_card"; }
		}

		public override string BackgroundKey
		{
			get { return NightHeistMod.MOD_ID + ":night_heist_bg"; }
		}

		public override string AmbienceId
		{
			get { return AfterhourLocation.DUMPING_GROUND; }
		}

		public override GameObject StageObject
		{
			get
			{
				GameObject stage = EnsureCustomStage();
				if (stage != null)
				{
					return stage;
				}
				if (MapUIManager.Instance == null)
				{
					return null;
				}
				return MapUIManager.Instance.scavengingScene;
			}
		}

		private GameObject EnsureCustomStage()
		{
			if (CustomUIManager.Instance == null)
			{
				return null;
			}

			Sprite bgSprite = CustomUIManager.Instance.GetSprite(BackgroundKey, true);
			if (bgSprite == null)
			{
				return null;
			}

			if (customStageObj == null)
			{
				customStageObj = new GameObject("NightHeistStage");
				customStageObj.SetActive(false);
				customStageObj.AddComponent<SpriteRenderer>();
			}

			SpriteRenderer sr = customStageObj.GetComponent<SpriteRenderer>();
			sr.sprite = bgSprite;

			Vector3 targetPos = Vector3.zero;
			float targetWidth = 19.2f;
			float targetHeight = 10.8f;
			int sortingOrder = BackgroundSortingOrder;
			bool hasRef = false;

			if (MapUIManager.Instance != null && MapUIManager.Instance.scavengingScene != null)
			{
				SpriteRenderer refSr = FindLargestSpriteRenderer(MapUIManager.Instance.scavengingScene);
				if (refSr != null && refSr.sprite != null)
				{
					Vector3 refSpriteSize = refSr.sprite.bounds.size;
					Vector3 refLossy = refSr.transform.lossyScale;
					float rw = Mathf.Abs(refSpriteSize.x * refLossy.x);
					float rh = Mathf.Abs(refSpriteSize.y * refLossy.y);
					if (rw > 0.1f && rh > 0.1f)
					{
						targetWidth = rw;
						targetHeight = rh;
						targetPos = refSr.transform.position;
						sr.sortingLayerID = refSr.sortingLayerID;
						sortingOrder = refSr.sortingOrder;
						hasRef = true;
					}
				}
			}

			if (!hasRef && Camera.main != null && Camera.main.orthographic)
			{
				targetHeight = Camera.main.orthographicSize * 2f;
				targetWidth = targetHeight * Camera.main.aspect;
				targetPos = new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y, 0f);
			}

			sr.sortingOrder = sortingOrder;
			Vector3 bgSize = bgSprite.bounds.size;
			if (bgSize.x > 0.001f && bgSize.y > 0.001f)
			{
				float scale = Mathf.Max(targetWidth / bgSize.x, targetHeight / bgSize.y);
				customStageObj.transform.localScale = new Vector3(scale, scale, 1f);
			}
			customStageObj.transform.position = targetPos;

			return customStageObj;
		}

		private static SpriteRenderer FindLargestSpriteRenderer(GameObject root)
		{
			SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
			SpriteRenderer best = null;
			float bestArea = 0f;
			for (int i = 0; i < renderers.Length; i++)
			{
				SpriteRenderer r = renderers[i];
				if (r == null || r.sprite == null)
				{
					continue;
				}
				Vector3 s = r.sprite.bounds.size;
				Vector3 ls = r.transform.lossyScale;
				float area = Mathf.Abs(s.x * ls.x * s.y * ls.y);
				if (area > bestArea)
				{
					bestArea = area;
					best = r;
				}
			}
			return best;
		}

		public override GameObject PanelObject
		{
			get { return null; }
		}

		public override bool UsesAfterhourBag
		{
			get { return true; }
		}

		public override string GetTitle()
		{
			return NightHeistMod.L("location_title");
		}

		public override string GetPanelTitle()
		{
			return NightHeistMod.L("panel_title");
		}

		public override bool IsDiscovered()
		{
			return true;
		}

		public override void Arrive()
		{
			phase = NightPhase.ReadyToScout;
			scoutResult = null;
			lastResult = null;
		}

		public override void Leave()
		{
			panel = null;
		}

		public override void BuildPanel(CustomUIBuilder builder)
		{
			panel = builder.Handle;
			builder.AddLabel("", EL_STATUS);
			builder.AddLabel("", EL_ODDS);
			builder.AddLabel("", EL_RISK);
			builder.AddSpace(8f);
			builder.AddButton(NightHeistMod.L("action_scout"), OnScoutClicked, EL_SCOUT);
			builder.AddButton("", delegate { OnChoiceClicked(0); }, EL_CHOICE_0);
			builder.AddButton("", delegate { OnChoiceClicked(1); }, EL_CHOICE_1);
			builder.AddButton(NightHeistMod.L("action_abandon"), OnAbandonClicked, EL_ABANDON);
			RefreshPanel();
		}

		private void OnScoutClicked()
		{
			if (phase != NightPhase.ReadyToScout)
			{
				return;
			}

			scoutResult = HeistLootRoller.ScoutOpportunities();
			if (scoutResult.BlockedByPatrol)
			{
				phase = NightPhase.PatrolBlocked;
				if (scoutResult.CaughtBySecurity)
				{
					PlaySoundSafe("PING_SONAR");
					NotifySafe(NightHeistMod.L("notify_patrol_caught", scoutResult.SecurityRepLost), "sec");
				}
				else
				{
					PlaySoundSafe("debris");
					NotifySafe(NightHeistMod.L("notify_patrol_blocked"), "default");
				}
			}
			else
			{
				phase = NightPhase.ChoosingTarget;
				PlaySoundSafe("click_vintage");
			}

			RefreshPanel();
		}

		private void OnChoiceClicked(int index)
		{
			if (phase != NightPhase.ChoosingTarget || scoutResult == null || scoutResult.Opportunities == null)
			{
				return;
			}
			if (index < 0 || index >= scoutResult.Opportunities.Count)
			{
				return;
			}

			HeistOpportunity chosen = scoutResult.Opportunities[index];
			lastResult = HeistLootRoller.ExecuteOpportunity(chosen);
			phase = NightPhase.HeistCompleted;

			PlaySoundSafe("textile_drop");
			string targetName = !string.IsNullOrEmpty(lastResult.TargetName) ? lastResult.TargetName : chosen.GetTargetDisplayName();
			if (lastResult.CrimeRecorded > 0)
			{
				NotifySafe(NightHeistMod.L("notify_loot_crime", targetName, lastResult.PlacedCount, lastResult.CrimeRecorded), "default");
			}
			else
			{
				NotifySafe(NightHeistMod.L("notify_loot", targetName, lastResult.PlacedCount), "default");
			}

			RefreshPanel();
		}

		private void OnAbandonClicked()
		{
			if (phase != NightPhase.ChoosingTarget)
			{
				return;
			}

			phase = NightPhase.Abandoned;
			PlaySoundSafe("debris");
			NotifySafe(NightHeistMod.L("notify_abandoned"), "default");
			RefreshPanel();
		}

		private void RefreshPanel()
		{
			if (panel == null || !panel.IsAlive)
			{
				return;
			}

			int blockChance = (NightHeistMod.PatrolBlockChance != null) ? Mathf.Clamp(NightHeistMod.PatrolBlockChance.Value, 0, 100) : 10;
			int findChance = 100 - blockChance;
			int caughtChance = (NightHeistMod.EmptyCaughtChance != null) ? Mathf.Clamp(NightHeistMod.EmptyCaughtChance.Value, 0, 100) : 35;
			int secPenalty = (NightHeistMod.SecurityRepPenalty != null) ? Mathf.Max(0, NightHeistMod.SecurityRepPenalty.Value) : 3;
			float crimeMult = (NightHeistMod.CrimeValueMultiplier != null) ? Mathf.Max(0f, NightHeistMod.CrimeValueMultiplier.Value) : 1.5f;
			bool crimeActive = crimeMult > 0f && PlayerStore.Instance != null && PlayerStore.Instance.isSecurityRecordIntroduced;

			SetTextSafe(EL_STATUS, BuildStatusText());
			SetTextSafe(EL_ODDS, NightHeistMod.L("odds_line", findChance, blockChance));
			if (crimeActive)
			{
				string multText = crimeMult.ToString("0.##", CultureInfo.InvariantCulture);
				SetTextSafe(EL_RISK, NightHeistMod.L("risk_line_crime", caughtChance, secPenalty, multText));
			}
			else
			{
				SetTextSafe(EL_RISK, NightHeistMod.L("risk_line", caughtChance, secPenalty));
			}

			CustomUIElement scoutBtn = panel.Get(EL_SCOUT);
			CustomUIElement choice0Btn = panel.Get(EL_CHOICE_0);
			CustomUIElement choice1Btn = panel.Get(EL_CHOICE_1);
			CustomUIElement abandonBtn = panel.Get(EL_ABANDON);

			if (phase == NightPhase.ReadyToScout)
			{
				if (scoutBtn != null)
				{
					scoutBtn.SetVisible(true);
					scoutBtn.SetInteractable(true);
					scoutBtn.SetText(NightHeistMod.L("action_scout"));
				}
				SetVisibleSafe(choice0Btn, false);
				SetVisibleSafe(choice1Btn, false);
				SetVisibleSafe(abandonBtn, false);
			}
			else if (phase == NightPhase.ChoosingTarget)
			{
				SetVisibleSafe(scoutBtn, false);
				List<HeistOpportunity> opps = (scoutResult != null) ? scoutResult.Opportunities : null;
				SetupChoiceButton(choice0Btn, opps, 0);
				SetupChoiceButton(choice1Btn, opps, 1);
				if (abandonBtn != null)
				{
					abandonBtn.SetVisible(true);
					abandonBtn.SetInteractable(true);
					abandonBtn.SetText(NightHeistMod.L("action_abandon"));
				}
			}
			else
			{
				if (scoutBtn != null)
				{
					scoutBtn.SetVisible(true);
					scoutBtn.SetInteractable(false);
					scoutBtn.SetText(phase == NightPhase.Abandoned ? NightHeistMod.L("action_abandoned") : NightHeistMod.L("action_done"));
				}
				SetVisibleSafe(choice0Btn, false);
				SetVisibleSafe(choice1Btn, false);
				SetVisibleSafe(abandonBtn, false);
			}

			CustomUIBuilder.RebuildLayout(panel.ContentRoot);
			CustomUIBuilder.RebuildLayout(panel.Rect);
		}

		private static void SetupChoiceButton(CustomUIElement btn, List<HeistOpportunity> opps, int idx)
		{
			if (btn == null)
			{
				return;
			}
			if (opps != null && idx < opps.Count && opps[idx] != null)
			{
				btn.SetVisible(true);
				btn.SetInteractable(true);
				btn.SetText(opps[idx].GetButtonLabel());
			}
			else
			{
				btn.SetVisible(false);
			}
		}

		private static void SetVisibleSafe(CustomUIElement btn, bool visible)
		{
			if (btn != null)
			{
				btn.SetVisible(visible);
			}
		}

		private string BuildStatusText()
		{
			switch (phase)
			{
				case NightPhase.ReadyToScout:
					return NightHeistMod.L("status_ready");
				case NightPhase.ChoosingTarget:
					return NightHeistMod.L("status_choose_target");
				case NightPhase.PatrolBlocked:
					if (scoutResult != null && scoutResult.CaughtBySecurity)
					{
						return NightHeistMod.L("status_patrol_caught", scoutResult.SecurityRepLost);
					}
					return NightHeistMod.L("status_patrol_blocked");
				case NightPhase.Abandoned:
					return NightHeistMod.L("status_abandoned");
				case NightPhase.HeistCompleted:
					if (lastResult == null)
					{
						return NightHeistMod.L("action_done");
					}
					string targetName = !string.IsNullOrEmpty(lastResult.TargetName) ? lastResult.TargetName : NightHeistMod.L("location_title");
					if (lastResult.OverflowCount > 0)
					{
						if (lastResult.CrimeRecorded > 0)
						{
							return NightHeistMod.L("status_result_loot_partial_crime", targetName, lastResult.PlacedCount, lastResult.OverflowCount, lastResult.CrimeRecorded);
						}
						return NightHeistMod.L("status_result_loot_partial", targetName, lastResult.PlacedCount, lastResult.OverflowCount);
					}
					if (lastResult.CrimeRecorded > 0)
					{
						return NightHeistMod.L("status_result_loot_crime", targetName, lastResult.PlacedCount, lastResult.CrimeRecorded);
					}
					return NightHeistMod.L("status_result_loot", targetName, lastResult.PlacedCount);
				default:
					return "";
			}
		}

		private void SetTextSafe(string elementId, string value)
		{
			CustomUIElement el = panel.Get(elementId);
			if (el != null && el.text != null && el.text.text != value)
			{
				el.SetText(value);
			}
		}

		private static void PlaySoundSafe(string soundKey)
		{
			if (AudioManager.Instance != null)
			{
				AudioManager.Instance.Play2(soundKey);
			}
		}

		private static void NotifySafe(string message, string color)
		{
			if (StoreUIManager.Instance != null)
			{
				StoreUIManager.Instance.Notify(message, color);
			}
		}
	}
}
