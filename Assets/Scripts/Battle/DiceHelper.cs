
using UnityEngine;

public static class DiceHelper {
    public static Element RollElement(){
        return (Element)Random.Range(0,5);
    }
    public static Polarity RollPolarity(){
        return Random.value>0.5f?Polarity.Yang:Polarity.Yin;
    }
}
