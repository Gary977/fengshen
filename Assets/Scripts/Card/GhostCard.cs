using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GhostCard : MonoBehaviour
{
    public Image cardArt;
    public TextMeshProUGUI cardName;
    public TextMeshProUGUI cardCost;
    public TextMeshProUGUI cardPolarity;

    public void Setup(CardDefinition data)
    {
        if (data == null) return;

        cardArt.sprite = data.cardSprite;
        cardName.text = data.cardName;
        cardCost.text = data.cost.ToString();
        cardPolarity.text = data.polarity.ToString();  // Yin / Yang
    }
}
