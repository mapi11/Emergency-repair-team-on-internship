using UnityEngine;

public class Hand : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform followRoot;
    [SerializeField] private Vector3 idleLocalPosition = new Vector3(0.35f, -0.45f, 0.75f);
    [SerializeField] private Vector3 idleLocalEuler = new Vector3(20f, 0f, 0f);

    [Header("Motion")]
    [SerializeField] private float followSmoothTime = 0.055f;
    [SerializeField] private float targetFollowSmoothTime = 0.04f;
    [SerializeField] private float maxSpeed = 18f;

    [Header("Swing")]
    [SerializeField] private float swingAmount = 0.08f;
    [SerializeField] private float swingSpeed = 9f;

    [Header("Sprint Sway")]
    [SerializeField] private float sprintSwayAmount = 0.12f;
    [SerializeField] private float sprintSwaySpeed = 14f;
    [SerializeField] private float sprintSpeedThreshold = 5.5f;

    [Header("Pitch Follow")]
    [SerializeField][Range(0f, 1f)] private float pitchInfluence = 0.35f;

    [Header("Arm Line Optional")]
    [SerializeField] private Transform shoulder;
    [SerializeField] private LineRenderer armLine;

    [Header("Item Hold")]
    [SerializeField] private Transform holdPivot;

    [Header("Debug")]
    [SerializeField] private bool hasTarget;
    [SerializeField] private Transform target;
    [SerializeField] private bool isRagdollMode;

    [SerializeField] private bool useNetworkPose;
    [SerializeField] private Vector3 networkPosition;
    [SerializeField] private Quaternion networkRotation;

    private Vector3 velocity;
    private Vector3 previousFollowRootPosition;
    private float swingTime;
    private Vector3 smoothedVelocity;

    public Transform HoldPivot => holdPivot;

    private Quaternion GetYawRotation()
    {
        return Quaternion.Euler(0f, followRoot.eulerAngles.y, 0f);
    }

    private Quaternion GetHandRotation()
    {
        float pitch = followRoot.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
        return Quaternion.Euler(pitch * pitchInfluence, followRoot.eulerAngles.y, 0f);
    }

    public float DistanceToTarget
    {
        get
        {
            if (target == null) return float.MaxValue;
            return Vector3.Distance(transform.position, target.position);
        }
    }

    private void Start()
    {
        if (followRoot == null && Camera.main != null)
        {
            followRoot = Camera.main.transform;
        }

        if (followRoot != null)
        {
            Quaternion handRot = GetHandRotation();
            transform.position = followRoot.position + handRot * idleLocalPosition;
            transform.rotation = handRot * Quaternion.Euler(idleLocalEuler);
            previousFollowRootPosition = followRoot.position;
        }

        SetupLine();
    }

    private void LateUpdate()
    {
        if (useNetworkPose)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                networkPosition,
                Time.deltaTime * 20f
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                networkRotation,
                Time.deltaTime * 20f
            );

            UpdateLine();
            return;
        }

        if (followRoot == null) return;

        UpdateHand();
        UpdateLine();
    }

    private void UpdateHand()
    {
        Vector3 desiredPosition;
        Quaternion desiredRotation;

        if (isRagdollMode)
        {
            desiredPosition = GetRagdollLoosePosition();
            desiredRotation = Quaternion.LookRotation(followRoot.forward, Vector3.up);
        }
        else if (hasTarget && target != null)
        {
            Vector3 swing = ComputeSwing();
            desiredPosition = target.position + swing;
            desiredRotation = target.rotation;
        }
        else
        {
            desiredPosition = GetIdlePosition();
            desiredRotation = GetHandRotation() * Quaternion.Euler(idleLocalEuler);
        }

        float smoothTime = hasTarget ? targetFollowSmoothTime : followSmoothTime;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime,
            maxSpeed
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            Time.deltaTime * 14f
        );

        previousFollowRootPosition = followRoot.position;
    }

    private Vector3 ComputeSwing()
    {
        Vector3 rawVelocity = (followRoot.position - previousFollowRootPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, Time.deltaTime * 12f);

        swingTime += Time.deltaTime * swingSpeed;

        Vector3 swing = -followRoot.InverseTransformDirection(smoothedVelocity) * swingAmount;

        float speed = smoothedVelocity.magnitude;
        if (speed > 0.1f)
        {
            swing += new Vector3(
                Mathf.Sin(swingTime) * 0.025f,
                Mathf.Abs(Mathf.Sin(swingTime * 0.8f)) * -0.035f,
                0f
            );
        }

        if (speed > sprintSpeedThreshold)
        {
            float intensity = Mathf.InverseLerp(sprintSpeedThreshold, sprintSpeedThreshold + 2f, speed);
            swing.x += Mathf.Sin(swingTime * (sprintSwaySpeed / swingSpeed)) * sprintSwayAmount * intensity;
        }

        return Vector3.ClampMagnitude(swing, 0.3f);
    }

    private Vector3 GetIdlePosition()
    {
        return followRoot.position + GetHandRotation() * (idleLocalPosition + ComputeSwing());
    }

    private Vector3 GetRagdollLoosePosition()
    {
        Vector3 looseLocal = idleLocalPosition;
        looseLocal.y -= 0.35f;
        looseLocal.z -= 0.15f;

        float noise = Mathf.Sin(Time.time * 5f) * 0.04f;
        looseLocal.x += noise;

        return followRoot.position + GetHandRotation() * looseLocal;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        hasTarget = target != null;
    }

    public void ClearTarget()
    {
        target = null;
        hasTarget = false;
    }

    public void SetRagdollMode(bool enabled)
    {
        isRagdollMode = enabled;

        if (enabled)
        {
            ClearTarget();
        }
    }

    private void SetupLine()
    {
        if (armLine == null) return;

        armLine.positionCount = 2;
        armLine.useWorldSpace = true;
        armLine.widthMultiplier = 0.08f;
        armLine.numCapVertices = 6;
        armLine.numCornerVertices = 6;
    }

    private void UpdateLine()
    {
        if (armLine == null || shoulder == null) return;

        armLine.SetPosition(0, shoulder.position);
        armLine.SetPosition(1, transform.position);
    }

    public void SetNetworkPoseMode(bool value)
    {
        useNetworkPose = value;
    }

    public void ApplyNetworkPose(Vector3 position, Quaternion rotation)
    {
        networkPosition = position;
        networkRotation = rotation;
    }
}