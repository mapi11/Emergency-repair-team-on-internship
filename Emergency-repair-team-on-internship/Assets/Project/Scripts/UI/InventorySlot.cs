using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image objectImg;
    [SerializeField] private TMP_Text itemNameTxt;

    public Image ObjectImg => objectImg;
    public TMP_Text ItemNameTxt => itemNameTxt;
}
