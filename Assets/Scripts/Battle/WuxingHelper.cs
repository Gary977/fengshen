
using UnityEngine;

public static class WuxingHelper {
    public static bool IsKe(Element atk, Element def){
        return (atk==Element.Wood && def==Element.Earth)
            || (atk==Element.Earth && def==Element.Water)
            || (atk==Element.Water && def==Element.Fire)
            || (atk==Element.Fire && def==Element.Metal)
            || (atk==Element.Metal && def==Element.Wood);
    }

    public static float GetMultiplier(Combatant user, CardDefinition card, Combatant target){
        float m=1f;
        if(user.innatePolarity==card.polarity){
            m*=1.1f;
            if(user.innateElement==card.element) m*=1.1f;
        }
        if(IsKe(card.element,target.currentElement)) m*=1.2f;
        if(IsKe(target.currentElement,card.element)) m*=0.8f;
        return m;
    }

    public static int Apply(int baseVal, float m){
        return Mathf.CeilToInt(baseVal*m);
    }
}
