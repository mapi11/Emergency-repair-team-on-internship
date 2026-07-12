using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;
    }
}