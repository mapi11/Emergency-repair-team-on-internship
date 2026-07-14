using UnityEngine;
using UnityEngine.UI;

public class ChargeSlider : MonoBehaviour
{
    [SerializeField] private Slider slider;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Show(float initialValue)
    {
        gameObject.SetActive(true);
        if (slider != null) slider.value = initialValue;
    }

    public void UpdateValue(float normalized)
    {
        if (slider != null) slider.value = normalized;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
