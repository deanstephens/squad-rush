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
            public Button selectButton;
            public TMP_Text nameText;
            public TMP_Text stateText;
        }

        [System.Serializable]
        public class ModRow
        {
            public Image background;
            public TMP_Text nameText;
            public TMP_Text descText;
            public Button buyButton;
            public TMP_Text buyLabel;
        }

        [Header("Armory")]
        public GameObject loadoutPanel;
        public TMP_Text scrapText;
        public TMP_Text bestText;
        public GunRow[] gunRows;
        public TMP_Text detailName;
        public TMP_Text detailDesc;
        public TMP_Text detailStats;
        public Button upgradeButton;
        public TMP_Text upgradeLabel;
        public ModRow[] modRows;
        public Button startButton;
        public TMP_Text startLabel;
        public Button hubButton;

        string viewedGunId;

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
                r.selectButton.onClick.AddListener(() => ViewGun(r.gunId));
            }
            if (upgradeButton) upgradeButton.onClick.AddListener(OnUpgrade);
            for (int i = 0; i < modRows.Length; i++)
            {
                int idx = i;
                modRows[i].buyButton.onClick.AddListener(() => OnBuyMod(idx));
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

        static readonly Color RowLocked = new Color(0.16f, 0.17f, 0.22f, 0.95f);
        static readonly Color RowUnlocked = new Color(0.2f, 0.26f, 0.4f, 0.95f);
        static readonly Color RowSelected = new Color(0.16f, 0.45f, 0.3f, 0.95f);
        static readonly Color RowViewed = new Color(0.3f, 0.36f, 0.55f, 0.95f);

        public void ShowLoadout()
        {
            SetPanels(true, false, false, false);
            if (bestText) bestText.text = "BEST  " + ArenaManager.FormatTime(MetaProgression.BestArenaTime);
            viewedGunId = MetaProgression.SelectedGun;
            RefreshArmory();
        }

        void ViewGun(string id)
        {
            viewedGunId = id;
            // Selecting an unlocked gun equips it; a locked one is just previewed until unlocked.
            if (MetaProgression.IsGunUnlocked(id)) MetaProgression.SelectedGun = id;
            RefreshArmory();
        }

        void RefreshArmory()
        {
            int scrap = MetaProgression.Scrap;
            string selected = MetaProgression.SelectedGun;
            if (scrapText) scrapText.text = "SCRAP  " + scrap;

            foreach (var row in gunRows)
            {
                var def = GunLibrary.Get(row.gunId);
                if (def == null) continue;
                bool unlocked = MetaProgression.IsGunUnlocked(def.Id);
                bool isSelected = def.Id == selected;
                bool isViewed = def.Id == viewedGunId;

                if (row.nameText) row.nameText.text = def.Name;
                if (row.stateText)
                    row.stateText.text = !unlocked ? "<color=#B8BCD0>LOCKED  " + def.Cost + " SCRAP</color>"
                                       : isSelected ? "EQUIPPED  <size=80%>Lv " + MetaProgression.GunLevel(def.Id) + "</size>"
                                       : "<size=80%>Lv " + MetaProgression.GunLevel(def.Id) + "</size>";
                row.background.color = isSelected ? RowSelected : isViewed ? RowViewed : unlocked ? RowUnlocked : RowLocked;
            }

            var viewed = GunLibrary.Get(viewedGunId) ?? GunLibrary.Get(GunLibrary.DefaultGunId);
            RefreshDetail(viewed, scrap);

            bool canStart = MetaProgression.IsGunUnlocked(selected);
            if (startButton) startButton.interactable = canStart;
            if (startLabel) startLabel.text = "ENTER ARENA\n<size=55%>" + (GunLibrary.Get(selected)?.Name ?? "") + "</size>";
        }

        void RefreshDetail(GunDef def, int scrap)
        {
            bool unlocked = MetaProgression.IsGunUnlocked(def.Id);
            int lvl = MetaProgression.GunLevel(def.Id);
            var stats = GunLibrary.BuildMetaStats(def);

            if (detailName) detailName.text = def.Name + "  <size=60%>Lv " + lvl + " / " + GunDef.MaxLevel + "</size>";
            if (detailDesc) detailDesc.text = def.Description;
            if (detailStats) detailStats.text = def.StatsLine(stats);

            if (upgradeButton && upgradeLabel)
            {
                if (!unlocked)
                {
                    upgradeLabel.text = "UNLOCK  <size=75%>" + def.Cost + " SCRAP</size>";
                    upgradeButton.interactable = scrap >= def.Cost;
                }
                else if (lvl >= GunDef.MaxLevel)
                {
                    upgradeLabel.text = "MAX LEVEL";
                    upgradeButton.interactable = false;
                }
                else
                {
                    int cost = def.UpgradeCost(lvl);
                    upgradeLabel.text = "UPGRADE TO Lv " + (lvl + 1) + "  <size=75%>+" + Mathf.RoundToInt(GunDef.DamagePerLevel * 100f) + "% DMG  +" + Mathf.RoundToInt(GunDef.FireRatePerLevel * 100f) + "% RATE  ·  " + cost + " SCRAP</size>";
                    upgradeButton.interactable = scrap >= cost;
                }
            }

            for (int i = 0; i < modRows.Length; i++)
            {
                var row = modRows[i];
                bool has = i < def.Mods.Count;
                row.background.gameObject.SetActive(has);
                if (!has) continue;
                var mod = def.Mods[i];
                bool owned = MetaProgression.HasMod(mod.Id);
                row.nameText.text = mod.Name;
                row.descText.text = mod.Description;
                row.buyLabel.text = owned ? "OWNED" : "BUY\n<size=75%>" + mod.Cost + " SCRAP</size>";
                row.buyButton.interactable = !owned && unlocked && scrap >= mod.Cost;
                row.background.color = owned ? RowSelected : RowUnlocked;
            }
        }

        void OnUpgrade()
        {
            var def = GunLibrary.Get(viewedGunId);
            if (def == null) return;
            if (!MetaProgression.IsGunUnlocked(def.Id))
            {
                if (MetaProgression.TrySpendScrap(def.Cost))
                {
                    MetaProgression.UnlockGun(def.Id);
                    MetaProgression.SelectedGun = def.Id;
                }
            }
            else MetaProgression.TryUpgradeGun(def);
            RefreshArmory();
        }

        void OnBuyMod(int index)
        {
            var def = GunLibrary.Get(viewedGunId);
            if (def == null || index >= def.Mods.Count) return;
            MetaProgression.TryBuyMod(def.Mods[index]);
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
