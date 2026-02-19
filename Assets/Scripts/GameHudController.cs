using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHudController : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;

    [Header("HUD Widgets")]
    public Slider healthSlider;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI killsText;
    public TextMeshProUGUI collectiblesText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI burstChargeText;
    public TextMeshProUGUI burstUnlockText;

    [Header("Styling")]
    public bool applyHudStyling = true;
    public Color hudPrimaryColor = new Color(0.95f, 0.95f, 0.86f, 1f);
    public Color hudAccentColor = new Color(0.79f, 0.95f, 0.86f, 1f);
    public Color hudOutlineColor = new Color(0f, 0f, 0f, 0.9f);
    public Color healthFillColor = new Color(0.23f, 0.85f, 0.38f, 0.95f);
    public Color healthBackgroundColor = new Color(0.1f, 0.08f, 0.07f, 0.84f);

    private void Start()
    {
        if (player == null || !player.HasInputAuthority)
        {
            player = PlayerController.FindPrimary();
        }

        ApplyHudTheme();
        HookPlayer();
        RefreshAll();
    }

    private void OnDestroy()
    {
        UnhookPlayer();
    }

    private void HookPlayer()
    {
        if (player == null)
        {
            return;
        }

        player.HealthChanged += OnHealthChanged;
        player.KillCountChanged += OnKillCountChanged;
        player.CollectibleChanged += OnCollectibleChanged;
        player.ScoreChanged += OnScoreChanged;
        player.BurstChargeChanged += OnBurstChargeChanged;
    }

    private void UnhookPlayer()
    {
        if (player == null)
        {
            return;
        }

        player.HealthChanged -= OnHealthChanged;
        player.KillCountChanged -= OnKillCountChanged;
        player.CollectibleChanged -= OnCollectibleChanged;
        player.ScoreChanged -= OnScoreChanged;
        player.BurstChargeChanged -= OnBurstChargeChanged;
    }

    private void RefreshAll()
    {
        if (player == null)
        {
            return;
        }

        OnHealthChanged(player.currentHealth, player.maxHealth);
        OnKillCountChanged(player.zombieKillCount);
        OnBurstChargeChanged(player.BurstCharges);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        if (healthText != null)
        {
            healthText.text = "HEALTH  " + current + "/" + max;
        }
    }

    private void OnKillCountChanged(int kills)
    {
        if (killsText != null)
        {
            killsText.text = "KILLS  " + kills;
        }

        if (burstUnlockText != null && player != null)
        {
            burstUnlockText.text = "NEXT BURST IN  " + player.KillsUntilNextBurst;
        }
    }

    private void OnCollectibleChanged(int collectibles)
    {
        if (collectiblesText != null)
        {
            collectiblesText.text = "ORBS  " + collectibles;
        }
    }

    private void OnScoreChanged(int currentScore)
    {
        if (scoreText != null)
        {
            scoreText.text = "SCORE  " + currentScore;
        }
    }

    private void OnBurstChargeChanged(int charges)
    {
        if (burstChargeText != null)
        {
            burstChargeText.text = "BURST  " + charges;
        }

        if (burstUnlockText != null && player != null)
        {
            burstUnlockText.text = "NEXT BURST IN  " + player.KillsUntilNextBurst;
        }
    }

    private void ApplyHudTheme()
    {
        if (!applyHudStyling)
        {
            return;
        }

        TMP_FontAsset hudFont = ImportedStuffAssetUtility.GetGameplayFont();
        ApplyTextStyle(healthText, hudFont, hudPrimaryColor);
        ApplyTextStyle(killsText, hudFont, hudPrimaryColor);
        ApplyTextStyle(collectiblesText, hudFont, hudAccentColor);
        ApplyTextStyle(scoreText, hudFont, hudAccentColor);
        ApplyTextStyle(burstChargeText, hudFont, hudAccentColor);
        ApplyTextStyle(burstUnlockText, hudFont, hudPrimaryColor);

        if (healthSlider != null)
        {
            if (healthSlider.targetGraphic is Image backgroundImage)
            {
                backgroundImage.color = healthBackgroundColor;
            }

            Image fillImage = healthSlider.fillRect != null
                ? healthSlider.fillRect.GetComponent<Image>()
                : null;
            if (fillImage != null)
            {
                fillImage.color = healthFillColor;
            }
        }
    }

    private void ApplyTextStyle(TextMeshProUGUI text, TMP_FontAsset font, Color color)
    {
        if (text == null)
        {
            return;
        }

        ImportedStuffAssetUtility.ApplyUsableFont(text, font);

        text.color = color;
        text.outlineColor = hudOutlineColor;
        text.outlineWidth = Mathf.Max(text.outlineWidth, 0.15f);
    }
}
