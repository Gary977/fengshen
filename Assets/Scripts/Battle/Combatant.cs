using UnityEngine;

[System.Serializable]
public class Combatant
{
    [Header("Identity")]
    public string name;
    public Element innateElement;
    public Polarity innatePolarity;

    [Header("Current State")]
    public Element currentElement;

    public int maxHP = 100;
    public int currentHP = 100;
    public int block = 0;

    [Header("Status: elemental")]
    public int wetTurns = 0;          // 被“潮湿”影响的回合数（和 Water 连锁）
    public int weakTurns = 0;         // 虚弱：攻击伤害在 Resolver 里可以根据它减成 0.75
    public int burnTurns = 0;         // 燃烧剩余回合
    public int burnDamagePerTurn = 0; // 每回合燃烧伤害（直扣 HP，不吃 block）

    [Header("Status: vulnerability")]
    public int vulnerableTurns = 0;   // 易伤：在这里可以对所有输入伤害放大（若需要）

    [Header("Status: shield-like (Flowing Veil)")]
    public int shieldHitsLeft = 0;        // 还能减伤几次
    public int shieldReducePerHit = 0;    // 每次减伤多少

    /// <summary>
    /// 方便外部判断是否死亡。
    /// </summary>
    public bool IsDead => currentHP <= 0;

    // =========================
    // 基础状态操作
    // =========================

    public void ResetElement()
    {
        currentElement = innateElement;
    }

    public void ApplyWet(int turns)
    {
        wetTurns = Mathf.Max(wetTurns, turns);
    }

    public void ApplyWeak(int turns)
    {
        weakTurns = Mathf.Max(weakTurns, turns);
    }

    public void ApplyBurn(int damagePerTurn, int turns)
    {
        burnDamagePerTurn = damagePerTurn;
        burnTurns = turns;
    }

    public void ApplyVulnerable(int turns)
    {
        vulnerableTurns = Mathf.Max(vulnerableTurns, turns);
    }

    /// <summary>
    /// Flowing Veil 这种“下几次伤害减免”的护盾。
    /// </summary>
    public void ApplyShield(int reducePerHit, int hits)
    {
        shieldReducePerHit = reducePerHit;
        shieldHitsLeft = hits;
    }

    // =========================
    // 伤害 / 护盾 / 治疗
    // =========================

    public void TakeDamage(int amount)
    {
        int dmg = Mathf.Max(amount, 0);

        // 1) Flowing Veil 减伤（只对本回合的几次伤害生效）
        if (shieldHitsLeft > 0 && shieldReducePerHit > 0)
        {
            int reduced = Mathf.Max(dmg - shieldReducePerHit, 0);
            Debug.Log($"{name} shield reduced damage {dmg} -> {reduced}");
            dmg = reduced;
            shieldHitsLeft--;
        }

        // 2) 易伤（如果你以后想让易伤只影响“攻击伤害”，
        //    可以在 CardResolver 里做区分；现在默认对所有 TakeDamage 生效）
        if (vulnerableTurns > 0)
        {
            int before = dmg;
            dmg = Mathf.CeilToInt(dmg * 1.25f);
            Debug.Log($"{name} is Vulnerable: {before} -> {dmg}");
        }

        // 3) Block 护甲
        int damageAfterBlock = Mathf.Max(dmg - block, 0);
        block = Mathf.Max(block - dmg, 0);

        // 4) 扣血
        currentHP -= damageAfterBlock;
        if (currentHP < 0) currentHP = 0;

        Debug.Log($"{name} took {damageAfterBlock} dmg (raw {amount}), HP={currentHP}, Block={block}");
    }

    public void GainBlock(int amt)
    {
        if (amt <= 0) return;

        block += amt;
        Debug.Log($"{name} gained {amt} Block (now {block})");
    }

    public void Heal(int amt)
    {
        if (amt <= 0) return;

        int before = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + amt);
        int realHeal = currentHP - before;
        Debug.Log($"{name} healed {realHeal}, HP={currentHP}");
    }

    // =========================
    // 回合结束：状态结算 & 衰减
    // =========================

    /// <summary>
    /// 在一方回合结束时调用。
    /// 处理：燃烧 DOT、状态回合数衰减、一次性护盾清空等。
    /// </summary>
    public void TickEnd()
    {
        // 1) 燃烧：直扣 HP，不吃 Block/Shield（符合“DoT 不被格挡”直觉）
        if (burnTurns > 0 && burnDamagePerTurn > 0)
        {
            int before = currentHP;
            int burnDmg = burnDamagePerTurn;
            currentHP -= burnDmg;
            if (currentHP < 0) currentHP = 0;
            burnTurns--;

            Debug.Log($"{name} burned for {burnDmg}, HP {before} -> {currentHP}");

            if (burnTurns <= 0)
            {
                burnDamagePerTurn = 0;
            }
        }

        // 2) 状态衰减
        if (weakTurns > 0) weakTurns--;
        if (wetTurns > 0) wetTurns--;
        if (vulnerableTurns > 0) vulnerableTurns--;

        // 3) Flowing Veil：这张卡文案写「this turn only」
        //    所以回合结束时，无论还有没有 hit 次数，都清空。
        if (shieldHitsLeft > 0 || shieldReducePerHit > 0)
        {
            Debug.Log($"{name}'s shield effect ended. (hitsLeft={shieldHitsLeft}, reducePerHit={shieldReducePerHit})");
            shieldHitsLeft = 0;
            shieldReducePerHit = 0;
        }
    }
}
