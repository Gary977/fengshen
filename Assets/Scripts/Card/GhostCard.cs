using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GhostCard : MonoBehaviour
{
    public Image cardArt;


    public void Setup(CardDefinition data)
    {
        if (data == null) return;

        cardArt.sprite = data.cardSprite;


    }
}
