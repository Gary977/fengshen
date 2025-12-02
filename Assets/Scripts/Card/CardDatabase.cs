
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Cards/Card Database")]
public class CardDatabase : ScriptableObject {
    public List<CardDefinition> allCards;

    private Dictionary<CardId, CardDefinition> byId;
    private Dictionary<(Element, Polarity), CardDefinition> byElementPolarity;

    public void Init() {
        byId = new Dictionary<CardId, CardDefinition>();
        byElementPolarity = new Dictionary<(Element, Polarity), CardDefinition>();
        foreach (var c in allCards) {
            byId[c.id] = c;
            byElementPolarity[(c.element, c.polarity)] = c;
        }
    }

    public CardDefinition GetById(CardId id) {
        if (byId == null) Init();
        return byId[id];
    }
    public CardDefinition GetByPair(Element e, Polarity p) {
        if (byElementPolarity == null) Init();
        return byElementPolarity[(e,p)];
    }
}
