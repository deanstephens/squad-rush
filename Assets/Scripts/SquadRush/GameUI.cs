using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquadRush
{
    /// <summary>All screens: HUD, main menu with the upgrade shop, perk picker, and game over.</summary>
    public class GameUI : MonoBehaviour
    {
        [Header("HUD")]
        public GameObject hudPanel;
        public TMP_Text unitsText;
        public TMP_Text distanceText;
        public TMP_Text coinsText;
        public TMP_Text statsText;

        [Header("Menu")]
        public GameObject menuPanel;
        public TMP_Text bestText;
        public TMP_Text bankText;
        public Button playButton;
        public Button[] upgradeButtons;
        public TMP_Text[] upgradeLabels;

        [Header("Game Over")]
        public GameObject gameOverPanel;
        public TMP_Text resultText;
        public Button retryButton;
        public Button menuButton;

        [Header("Perks")]
        public GameObject perkPanel;
        public Button[] perkButtons;
        public TMP_Text[] perkTitles;
        public TMP_Text[] perkDescs;

        Perk[] offered;

        void Awake()
        {
            if (playButton) playButton.onClick.AddListener(() => GameManager.Instance.StartRun());
            if (retryButton) retryButton.onClick.AddListener(() => GameManager.Instance.Retry());
            if (menuButton) menuButton.onClick.AddListener(() => GameManager.Instance.BackToMenu());

            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int idx = i;
                upgradeButtons[i].onClick.AddListener(() => Buy((UpgradeType)idx));
            }
            for (int i = 0; i < perkButtons.Length; i++)
            {
                int idx = i;
                perkButtons[i].onClick.AddListener(() => PickPerk(idx));
            }
        }

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.squad != null) gm.squad.Changed += UpdateHud;
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.squad != null) gm.squad.Changed -= UpdateHud;
        }

        void SetPanels(bool hud, bool menu, bool over, bool perks)
        {
            if (hudPanel) hudPanel.SetActive(hud);
            if (menuPanel) menuPanel.SetActive(menu);
            if (gameOverPanel) gameOverPanel.SetActive(over);
            if (perkPanel) perkPanel.SetActive(perks);
        }

        public void ShowMenu()
        {
            SetPanels(false, true, false, false);
            if (bestText) bestText.text = "BEST  " + MetaProgression.BestDistance.ToString("0") + " m";
            RefreshShop();
        }

        public void ShowHud()
        {
            SetPanels(true, false, false, false);
            UpdateHud();
        }

        public void UpdateHud()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var s = gm.squad;

            if (unitsText) unitsText.text = "UNITS  " + s.UnitCount + (s.shields > 0 ? "  ◆" + s.shields : "");
            if (distanceText) distanceText.text = gm.Distance.ToString("0") + " m";
            if (coinsText) coinsText.text = "$" + gm.CoinsThisRun;
            if (statsText) statsText.text = "DMG " + s.damage.ToString("0.0") + "   RATE " + s.fireRate.ToString("0.0") + "/s";
        }

        public void ShowGameOver(float distance, int coins, float best)
        {
            SetPanels(false, false, true, false);
            if (resultText)
                resultText.text = distance.ToString("0") + " m\n" +
                                  "+$" + coins + "\n" +
                                  "<size=60%>BEST " + best.ToString("0") + " m</size>";
        }

        public void ShowPerkChoice(Perk[] perks)
        {
            offered = perks;
            SetPanels(false, false, false, true);
            for (int i = 0; i < perkButtons.Length; i++)
            {
                bool has = i < perks.Length;
                perkButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                if (i < perkTitles.Length && perkTitles[i]) perkTitles[i].text = perks[i].Title;
                if (i < perkDescs.Length && perkDescs[i]) perkDescs[i].text = perks[i].Description;
            }
        }

        void PickPerk(int idx)
        {
            if (offered == null || idx >= offered.Length) return;
            GameManager.Instance.ChoosePerk(offered[idx]);
        }

        void Buy(UpgradeType t)
        {
            if (MetaProgression.TryBuy(t))
                GameManager.Instance.squad.ApplyMeta();
            RefreshShop();
        }

        void RefreshShop()
        {
            if (bankText) bankText.text = "$" + MetaProgression.Coins;
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                var t = (UpgradeType)i;
                int lvl = MetaProgression.Level(t);
                bool maxed = lvl >= MetaProgression.MaxLevel;
                string cost = maxed ? "MAX" : "$" + MetaProgression.Cost(t);
                if (i < upgradeLabels.Length && upgradeLabels[i])
                    upgradeLabels[i].text = MetaProgression.Title(t) + "  <size=70%>Lv." + lvl + "  (" + MetaProgression.CurrentValue(t) + ")</size>\n" + cost;
                upgradeButtons[i].interactable = !maxed && MetaProgression.Coins >= MetaProgression.Cost(t);
            }
        }
    }
}
