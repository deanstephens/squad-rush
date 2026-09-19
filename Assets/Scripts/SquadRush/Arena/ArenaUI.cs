using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquadRush.Arena
{
    /// <summary>Arena screens: Armory/loadout, HUD, level-up choice, game over.</summary>
    public class ArenaUI : MonoBehaviour
    {
        [System.Serializable]
        public class GunRow
        {
            public string gunId;
            public Image background;
            public TMP_Text nameText;
            public TMP_Text statsText;
            public Button actionButton;
            public TMP_Text actionLabel;
        }

        [Header("Loadout")]
        public GameObject loadoutPanel;
        public TMP_Text scrapText;
        public TMP_Text slotsText;
        public TMP_Text bestText;
        public GunRow[] gunRows;
        public Button startButton;
        public Button hubButton;

        [Header("HUD")]
        public GameObject hudPanel;
        public Image hpFill;
        public TMP_Text hpText;
        public TMP_Text timerText;
        public TMP_Text levelText;
        public Image xpFill;
        public TMP_Text killsText;
        public TMP_Text coinsText;

        [Header("Level Up")]
        public GameObject levelUpPanel;
        public Button[] upgradeButtons;
        public TMP_Text[] upgradeTitles;
        public TMP_Text[] upgradeDescs;

        [Header("Game Over")]
        public GameObject gameOverPanel;
        public TMP_Text resultText;
        public Button retryButton;
        public Button armoryButton;

        static readonly Color RowLocked = new Color(0.16f, 0.17f, 0.22f, 0.95f);
        static readonly Color RowUnlocked = new Color(0.2f, 0.26f, 0.4f, 0.95f);
        static readonly Color RowEquipped = new Color(0.16f, 0.45f, 0.3f, 0.95f);

        void Awake()
        {
            if (startButton) startButton.onClick.AddListener(() => ArenaManager.Instance.StartRun());
            if (hubButton) hubButton.onClick.AddListener(() => ArenaManager.Instance.BackToHub());
            if (retryButton) retryButton.onClick.AddListener(() => ArenaManager.Instance.Retry());
            if (armoryButton) armoryButton.onClick.AddListener(() => ArenaManager.Instance.BackToArmory());

            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int idx = i;
                upgradeButtons[i].onClick.AddListener(() => ArenaManager.Instance.ChooseUpgrade(idx));
            }
            foreach (var row in gunRows)
            {
                var r = row;
                r.actionButton.onClick.AddListener(() => OnGunAction(r));
            }
        }

        void SetPanels(bool loadout, bool hud, bool levelUp, bool over)
        {
            if (loadoutPanel) loadoutPanel.SetActive(loadout);
            if (hudPanel) hudPanel.SetActive(hud);
            if (levelUpPanel) levelUpPanel.SetActive(levelUp);
            if (gameOverPanel) gameOverPanel.SetActive(over);
        }

        // ---------------------------------------------------------------- armory

        public void ShowLoadout()
        {
            SetPanels(true, false, false, false);
            if (bestText) bestText.text = "BEST  " + ArenaManager.FormatTime(MetaProgression.BestArenaTime);
            RefreshArmory();
        }

        void RefreshArmory()
        {
            var equipped = MetaProgression.EquippedGuns;
            int scrap = MetaProgression.Scrap;
            if (scrapText) scrapText.text = "SCRAP  " + scrap;
            if (slotsText) slotsText.text = "LOADOUT  " + equipped.Count + " / " + MetaProgression.LoadoutSlots;

            foreach (var row in gunRows)
            {
                var def = GunLibrary.Get(row.gunId);
                if (def == null) continue;
                bool unlocked = MetaProgression.IsGunUnlocked(def.Id);
                bool isEquipped = equipped.Contains(def.Id);

                if (row.nameText) row.nameText.text = def.Name;
                if (row.statsText) row.statsText.text = def.StatsLine() + "\n<size=85%><color=#B8BCD0>" + def.Description + "</color></size>";

                if (!unlocked)
                {
                    row.background.color = RowLocked;
                    row.actionLabel.text = "UNLOCK\n<size=75%>" + def.Cost + " SCRAP</size>";
                    row.actionButton.interactable = scrap >= def.Cost;
                }
                else if (isEquipped)
                {
                    row.background.color = RowEquipped;
                    row.actionLabel.text = "EQUIPPED";
                    row.actionButton.interactable = equipped.Count > 1;
                }
                else
                {
                    row.background.color = RowUnlocked;
                    row.actionLabel.text = "EQUIP";
                    row.actionButton.interactable = equipped.Count < MetaProgression.LoadoutSlots;
                }
            }
        }

        void OnGunAction(GunRow row)
        {
            var def = GunLibrary.Get(row.gunId);
            if (def == null) return;
            var equipped = MetaProgression.EquippedGuns;

            if (!MetaProgression.IsGunUnlocked(def.Id))
            {
                if (MetaProgression.TrySpendScrap(def.Cost))
                {
                    MetaProgression.UnlockGun(def.Id);
                    if (equipped.Count < MetaProgression.LoadoutSlots)
                    {
                        equipped.Add(def.Id);
                        MetaProgression.EquippedGuns = equipped;
                    }
                }
            }
            else if (equipped.Contains(def.Id))
            {
                if (equipped.Count > 1)
                {
                    equipped.Remove(def.Id);
                    MetaProgression.EquippedGuns = equipped;
                }
            }
            else if (equipped.Count < MetaProgression.LoadoutSlots)
            {
                equipped.Add(def.Id);
                MetaProgression.EquippedGuns = equipped;
            }
            RefreshArmory();
        }

        // ---------------------------------------------------------------- hud

        public void ShowHud()
        {
            SetPanels(false, true, false, false);
            UpdateHud();
        }

        public void UpdateHud()
        {
            var am = ArenaManager.Instance;
            if (am == null || am.player == null) return;
            var p = am.player;

            if (hpFill) hpFill.fillAmount = p.maxHp > 0f ? Mathf.Clamp01(p.hp / p.maxHp) : 0f;
            if (hpText) hpText.text = Mathf.CeilToInt(p.hp) + " / " + Mathf.CeilToInt(p.maxHp);
            if (timerText) timerText.text = ArenaManager.FormatTime(am.TimeSurvived);
            if (levelText) levelText.text = "LV " + am.Level;
            if (xpFill) xpFill.fillAmount = am.XpToNext > 0f ? Mathf.Clamp01(am.Xp / am.XpToNext) : 0f;
            if (killsText) killsText.text = am.Kills + " KILLS";
            if (coinsText) coinsText.text = "$" + am.CoinsThisRun;
        }

        // ---------------------------------------------------------------- level up

        public void ShowLevelUp(ArenaUpgrade[] options)
        {
            SetPanels(false, true, true, false);
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                bool has = i < options.Length;
                upgradeButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                if (i < upgradeTitles.Length && upgradeTitles[i]) upgradeTitles[i].text = options[i].Title;
                if (i < upgradeDescs.Length && upgradeDescs[i]) upgradeDescs[i].text = options[i].Description;
            }
        }

        // ---------------------------------------------------------------- game over

        public void ShowGameOver(float time, int kills, int coins, float best)
        {
            SetPanels(false, false, false, true);
            if (resultText)
                resultText.text = "SURVIVED " + ArenaManager.FormatTime(time) + "\n" +
                                  kills + " KILLS\n" +
                                  "+$" + coins + "\n" +
                                  "<size=60%>BEST " + ArenaManager.FormatTime(best) + "</size>";
        }
    }
}
