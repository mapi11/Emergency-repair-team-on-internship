using UnityEngine;

public class EyeTracker : MonoBehaviour
{
    [Header("Eyes")]
    [SerializeField] private RectTransform leftEyeCenter;
    [SerializeField] private RectTransform rightEyeCenter;
    [SerializeField] private RectTransform leftPupil;
    [SerializeField] private RectTransform rightPupil;
    [SerializeField] private float maxPupilOffset = 10f;
    [SerializeField] private float eyeTrackingSpeed = 8f;

    private void Update()
    {
        if (leftEyeCenter == null || rightEyeCenter == null || leftPupil == null || rightPupil == null)
            return;

        MovePupil(leftPupil, leftEyeCenter);
        MovePupil(rightPupil, rightEyeCenter);
    }

    private void MovePupil(RectTransform pupil, RectTransform eyeCenter)
    {
        Vector3 cursorPos = Input.mousePosition;
        Canvas canvas = eyeCenter.GetComponentInParent<Canvas>();

        if (canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera cam = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;

            if (cam == null)
                cam = Camera.main;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cursorPos, cam, out Vector2 canvasPos))
            return;

        Vector2 eyePos = canvasRect.InverseTransformPoint(eyeCenter.position);
        Vector2 dir = canvasPos - eyePos;
        float dist = dir.magnitude;

        if (dist > maxPupilOffset)
            dir = dir.normalized * maxPupilOffset;

        Vector3 targetPos = pupil.localPosition;
        targetPos.x = dir.x;
        targetPos.y = dir.y;
        pupil.localPosition = Vector3.Lerp(pupil.localPosition, targetPos, eyeTrackingSpeed * Time.deltaTime);
    }
}
