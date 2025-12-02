using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum TurnOwner
{
    Player,
    Enemy
}

public class BattleHUD : MonoBehaviour
{
    [Header("Round / Turn")]
    [SerializeField] private TMP_Text roundText;   // "Round 1"
    [SerializeField] private TMP_Text phaseText;   // "Player Turn" / "Enemy Turn"

    [Header("Player UI")]
    [SerializeField] private TMP_Text playerHpText;  // "80 / 100"
    [SerializeField] private Image playerHpFill;   // 血条填充 Image (type = Filled)

    [Header("Enemy UI")]
    [SerializeField] private TMP_Text enemyHpText;
    [SerializeField] private Image enemyHpFill;
    // ================================================
    //  新增：Block 显示
    // ================================================
    [Header("Block UI (Optional)")]
    [SerializeField] private TMP_Text playerBlockText;
    [SerializeField] private TMP_Text enemyBlockText;

    [SerializeField] private TMP_Text playerEnergyText;
    [SerializeField] private TMP_Text enemyEnergyText;

    [Header("Status UI (Optional)")]
    [SerializeField] private TMP_Text playerStatusText;
    [SerializeField] private TMP_Text enemyStatusText;

    public void UpdatePlayerBlock(int block)
    {
        if (playerBlockText != null)
            playerBlockText.text = block.ToString();
    }

    public void UpdateEnemyBlock(int block)
    {
        if (enemyBlockText != null)
            enemyBlockText.text = block.ToString();
    }

    private int playerMaxHP = 100;
    private int enemyMaxHP = 100;

    /// <summary>
    /// 在战斗开始时由 BattleManager 调用一次。
    /// </summary>
    public void Init(int playerMax, int enemyMax)
    {
        playerMaxHP = Mathf.Max(1, playerMax);
        enemyMaxHP = Mathf.Max(1, enemyMax);

        UpdatePlayerHP(playerMaxHP);
        UpdateEnemyHP(enemyMaxHP);

        SetRound(1);
        SetTurn(TurnOwner.Player);
    }

    public void SetRound(int round)
    {
        if (roundText != null)
            roundText.text = $"Round {round}";
    }

    public void SetTurn(TurnOwner owner)
    {
        if (phaseText == null) return;

        switch (owner)
        {
            case TurnOwner.Player:
                phaseText.text = "Player Turn";
                break;
            case TurnOwner.Enemy:
                phaseText.text = "Enemy Turn";
                break;
        }
    }

    public void UpdatePlayerHP(int currentHP)
    {
        currentHP = Mathf.Clamp(currentHP, 0, playerMaxHP);

        if (playerHpText != null)
            playerHpText.text = $"{currentHP} / {playerMaxHP}";

        if (playerHpFill != null)
            playerHpFill.fillAmount = (float)currentHP / playerMaxHP;
    }

    public void UpdateEnemyHP(int currentHP)
    {
        currentHP = Mathf.Clamp(currentHP, 0, enemyMaxHP);

        if (enemyHpText != null)
            enemyHpText.text = $"{currentHP} / {enemyMaxHP}";

        if (enemyHpFill != null)
            enemyHpFill.fillAmount = (float)currentHP / enemyMaxHP;
    }

    public void UpdateEnergy(int pEnergy, int pMax, int eEnergy, int eMax)
    {
        if (playerEnergyText != null)
            playerEnergyText.text = $"{pEnergy} / {pMax}";

        if (enemyEnergyText != null)
            enemyEnergyText.text = $"{eEnergy} / {eMax}";
    }

    public void UpdateStatuses(Combatant player, Combatant enemy)
    {
        if (playerStatusText != null)
            playerStatusText.text = FormatStatus(player);

        if (enemyStatusText != null)
            enemyStatusText.text = FormatStatus(enemy);
    }

    private string FormatStatus(Combatant c)
    {
        if (c == null) return "—";

        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();

        if (c.burnTurns > 0 && c.burnDamagePerTurn > 0)
            parts.Add($"Burn {c.burnDamagePerTurn}/{c.burnTurns}");
        else if (c.burnTurns > 0)
            parts.Add($"Burn {c.burnTurns}");

        if (c.wetTurns > 0)
            parts.Add($"Wet {c.wetTurns}");

        if (c.weakTurns > 0)
            parts.Add($"Weak {c.weakTurns}");

        if (c.vulnerableTurns > 0)
            parts.Add($"Vuln {c.vulnerableTurns}");

        if (c.shieldHitsLeft > 0 && c.shieldReducePerHit > 0)
            parts.Add($"Shield -{c.shieldReducePerHit} x{c.shieldHitsLeft}");

        return parts.Count == 0 ? "—" : string.Join(" | ", parts);
    }

}
