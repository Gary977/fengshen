
using UnityEngine;

[CreateAssetMenu(menuName="Cards/Card Definition")]
public class CardDefinition : ScriptableObject {
    public CardId id;
    public Sprite cardSprite;

    public string cardName;
    public Element element;
    public Polarity polarity;
    public int cost;
    public CardType type;
    [TextArea] public string description;

    public int baseDamage;
    public int bonusDamage;
    public float elementBonus = 0.25f;

    public int heal;
    public int baseBlock;
    public int conditionalBlock;

    public int burnTick;
    public int burnTurns;

    public int reducePerHit;
    public int hitCount;

    public int wetTurns;           // For Tide Calling
    public int weakTurns;          // For Earthen Seal
    public int vulnerableTurns;    // For Vine Surge or future cards

    public int HpMultiplier;

}
