using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static Unity.Collections.Unicode;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("Database")]
    public CardDatabase database;

    [Header("Hand Manager")]
    public HandManager hand;

    [Header("Combatants")]
    public CombatantHolder playerObj;
    public CombatantHolder enemyObj;

    private Combatant player;
    private Combatant enemy;

    [Header("UI")]
    [SerializeField] private BattleHUD hud;

    private int turn = 1;
    private bool playerTurn = true;

    private int pMax = 3, eMax = 3;
    private int pEnergy, eEnergy;
    private int pReserve = 0, eReserve = 0;

    public Combatant Player => player;
    public Combatant Enemy => enemy;

    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        CardUI.hoverCanvas = GameObject.Find("HoverCanvas").transform;

        player = playerObj.data;
        enemy = enemyObj.data;

        player.ResetElement();
        enemy.ResetElement();

        hud.Init(player.maxHP, enemy.maxHP);

        turn = 1;
        playerTurn = true;

        StartPlayerTurn();
    }

    void StartPlayerTurn()
    {
        hud.SetRound(turn);
        hud.SetTurn(TurnOwner.Player);

        // 能量：你之前的规则 pMax 从 3 开始，每轮+1，最多6
        pMax = turn == 1 ? 3 : Mathf.Min(6, pMax + 1);
        pEnergy = pMax + (pReserve > 0 ? 1 : 0);
        pReserve = 0;

        // 抽3张牌到玩家手牌（用你 HandManager 的 List<CardInstance>）
        DrawCards(player, hand.playerHand, 3);

        UpdateHUD();
    }
    void StartEnemyTurn()
    {
        hud.SetTurn(TurnOwner.Enemy);

        eMax = turn == 1 ? 3 : Mathf.Min(6, eMax + 1);
        eEnergy = eMax + (eReserve > 0 ? 1 : 0);
        eReserve = 0;

        // 敌人也抽3张牌到自己的 List<CardInstance>
        DrawCards(enemy, hand.enemyHand, 3);

        UpdateHUD();

        // 启动敌人自动出牌协程
        StartCoroutine(EnemyActRoutine());
    }


    void DrawCards(Combatant who, List<CardInstance> handList, int count)
    {
        for (int i = 0; i < count; i++)
        {
            hand.AddCard(who, handList);   // 用你 HandManager 现有的 AddCard 逻辑
        }
    }



    public void PlayerPlayCard(CardInstance inst)
    {
        // 只在玩家阶段允许出牌
        if (!playerTurn) return;

        if (pEnergy < inst.cost)
        {
            Debug.Log("能量不足，不能出牌");
            return;
        }

        pEnergy -= inst.cost;

        // 执行卡牌效果
        CardResolver.Play(inst, player, enemy);

        // 从手牌+UI里移除（用你 HandManager 里写好的 RemoveCard(inst)）
        hand.RemoveCard(inst);

        UpdateHUD();

        // 敌人死了就可以后续做胜利逻辑
        if (enemy.IsDead)
        {
            Debug.Log("Enemy defeated!");
            return;
        }
    }


    IEnumerator EnemyActRoutine()
    {
        yield return new WaitForSeconds(0.6f);

        while (true)
        {
            List<CardInstance> enemyCards = hand.enemyHand;

            // 找出能出的卡
            List<CardInstance> playable = enemyCards.FindAll(c => c.cost <= eEnergy);

            if (playable.Count == 0)
                break;

            CardInstance chosen = playable[Random.Range(0, playable.Count)];

            eEnergy -= chosen.cost;

            CardResolver.Play(chosen, enemy, player);

            hand.enemyHand.Remove(chosen);

            UpdateHUD();

            if (player.IsDead)
            {
                Debug.Log("Player Defeated!");
                yield break;
            }

            yield return new WaitForSeconds(0.4f);
        }

        // 敌人阶段结束 = 本回合(round)结束
        EndTurn();
    }


    void EndTurn()
    {
        // 双方 DOT / 状态结算
        player.TickEnd();
        enemy.TickEnd();

        UpdateHUD();

        // 下一个 round
        turn++;
        playerTurn = true;

        StartPlayerTurn();
    }


    // ============================================================
    // HUD 更新（HP + Block）
    // ============================================================
    void UpdateHUD()
    {
        hud.UpdatePlayerHP(player.currentHP);
        hud.UpdateEnemyHP(enemy.currentHP);

        hud.UpdatePlayerBlock(player.block);
        hud.UpdateEnemyBlock(enemy.block);

        UpdateEnergyDisplay();
        hud.UpdateStatuses(player, enemy);

    }


    public void OnEndTurnButton()
    {
        // 只有在玩家阶段才能点
        if (!playerTurn) return;

        playerTurn = false;

        // 直接开始敌人阶段
        StartEnemyTurn();
    }

    private void UpdateEnergyDisplay()
    {
        hud.UpdateEnergy(pEnergy, pMax, eEnergy, eMax);
    }

}
