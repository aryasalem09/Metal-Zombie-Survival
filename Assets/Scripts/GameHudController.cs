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

    private void Start()
    {
        if (player == null || !player.HasInputAuthority)
        {
            player = PlayerController.FindPrimary();
        }

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
            healthText.text = current + " / " + max;
        }
    }

    private void OnKillCountChanged(int kills)
    {
        if (killsText != null)
        {
            killsText.text = kills.ToString();
        }

        if (burstUnlockText != null && player != null)
        {
            burstUnlockText.text = "Next Burst In: " + player.KillsUntilNextBurst;
        }
    }

    private void OnCollectibleChanged(int collectibles)
    {
        if (collectiblesText != null)
        {
            collectiblesText.text = collectibles.ToString();
        }
    }

    private void OnScoreChanged(int currentScore)
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }
    }

    private void OnBurstChargeChanged(int charges)
    {
        if (burstChargeText != null)
        {
            burstChargeText.text = charges.ToString();
        }

        if (burstUnlockText != null && player != null)
        {
            burstUnlockText.text = "Next Burst In: " + player.KillsUntilNextBurst;
        }
    }
}