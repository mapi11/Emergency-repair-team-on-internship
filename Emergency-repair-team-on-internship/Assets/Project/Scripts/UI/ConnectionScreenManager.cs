using UnityEngine;
using UnityEngine.UI;

public class ConnectionScreenManager : MonoBehaviour
{
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private RectTransform spinningImage;
    [SerializeField] private Image playerColorImage;
    [SerializeField] private float rotationSpeed = 180f;

    private void Awake()
    {
        if (rootPanel == null)
            rootPanel = gameObject;
    }

    public void Show(Color playerColor)
    {
        rootPanel.SetActive(true);

        if (playerColorImage != null)
            playerColorImage.color = playerColor;
    }

    public void Dismiss()
    {
        Destroy(gameObject);
    }

    private void Update()
    {
        if (spinningImage != null)
            spinningImage.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime);
    }
}
