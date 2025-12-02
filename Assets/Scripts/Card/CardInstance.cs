
public class CardInstance {
    public CardDefinition definition;
    public int cost;
    public CardInstance(CardDefinition def) {
        definition = def;
        cost = def.cost;
    }
}
