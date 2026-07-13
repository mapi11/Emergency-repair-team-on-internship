using UnityEngine;

public class PlayerEyesController : MonoBehaviour
{
    [Header("Eye Roots")]
    [SerializeField] private Transform leftEyeRoot;
    [SerializeField] private Transform rightEyeRoot;

    [Header("Pupils To Move")]
    [SerializeField] private Transform leftPupil;
    [SerializeField] private Transform rightPupil;

    [Header("Reference")]
    [SerializeField] private Transform headReference;

    [Header("Look For Players")]
    [SerializeField] private float playerLookRadius = 3.0f;
    [SerializeField] private float standingEyeHeight = 1.45f;
    [SerializeField] private float crouchingEyeHeight = 0.95f;

    [Header("Pupil Movement")]
    [SerializeField] private float pupilMoveDistance = 0.075f;
    [SerializeField] private float horizontalMultiplier = 1.25f;
    [SerializeField] private float verticalMultiplier = 1.65f;
    [SerializeField] private float lookSmooth = 22f;

    [Header("Clamp")]
    [SerializeField] private float maxHorizontal = 1.0f;
    [SerializeField] private float maxVertical = 1.0f;

    [Header("Invert If Needed")]
    [SerializeField] private bool invertHorizontal;
    [SerializeField] private bool invertVertical;

    [Header("Idle / Movement Bounce")]
    [SerializeField] private float idleAmount = 0.008f;
    [SerializeField] private float walkBounceAmount = 0.018f;
    [SerializeField] private float walkSideAmount = 0.018f;
    [SerializeField] private float jumpBounceAmount = 0.035f;
    [SerializeField] private float movementSpeedForMaxBounce = 5f;

    [Header("Debug")]
    [SerializeField] private PlayerController currentTargetPlayer;
    [SerializeField] private Vector2 currentPupilOffset;
    [SerializeField] private Vector3 debugTargetPoint;

    private Vector3 leftPupilStartLocalPosition;
    private Vector3 rightPupilStartLocalPosition;

    private Vector3 lastWorldPosition;
    private Vector3 velocity;

    private PlayerController[] cachedPlayers;
    private float searchTimer;

    public Vector3 EyesWorldCenter => GetOwnEyesCenter();

    private void Awake()
    {
        if (headReference == null)
        {
            headReference = transform;
        }

        if (leftPupil != null)
        {
            leftPupilStartLocalPosition = leftPupil.localPosition;
        }

        if (rightPupil != null)
        {
            rightPupilStartLocalPosition = rightPupil.localPosition;
        }

        lastWorldPosition = transform.position;
    }

    private void Update()
    {
        UpdateVelocity();
        UpdatePlayersCache();

        currentTargetPlayer = FindNearestPlayerInRadius();

        Vector2 targetOffset;

        if (currentTargetPlayer != null)
        {
            targetOffset = GetLookAtPlayerOffset(currentTargetPlayer);
        }
        else
        {
            targetOffset = GetMovementBounceOffset();
        }

        currentPupilOffset = Vector2.Lerp(
            currentPupilOffset,
            targetOffset,
            Time.deltaTime * lookSmooth
        );

        ApplyPupilOffset(currentPupilOffset);
    }

    private void UpdateVelocity()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        velocity = (transform.position - lastWorldPosition) / deltaTime;
        lastWorldPosition = transform.position;
    }

    private void UpdatePlayersCache()
    {
        searchTimer -= Time.deltaTime;

        if (searchTimer > 0f)
            return;

        searchTimer = 0.25f;
        cachedPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
    }

    private PlayerController FindNearestPlayerInRadius()
    {
        if (cachedPlayers == null)
            return null;

        PlayerController bestPlayer = null;
        float bestDistance = playerLookRadius;

        Vector3 myPosition = transform.position;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            PlayerController player = cachedPlayers[i];

            if (player == null)
                continue;

            if (player.transform == transform)
                continue;

            if (!player.gameObject.activeInHierarchy)
                continue;

            float distance = Vector3.Distance(myPosition, player.transform.position);

            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            bestPlayer = player;
        }

        return bestPlayer;
    }

    private Vector2 GetLookAtPlayerOffset(PlayerController targetPlayer)
    {
        Vector3 eyesCenter = GetOwnEyesCenter();
        Vector3 targetPoint = GetTargetEyesPoint(targetPlayer);

        debugTargetPoint = targetPoint;

        Vector3 directionWorld = targetPoint - eyesCenter;

        if (directionWorld.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        directionWorld.Normalize();

        Vector3 directionLocal = headReference.InverseTransformDirection(directionWorld);

        float x = Mathf.Clamp(directionLocal.x * horizontalMultiplier, -maxHorizontal, maxHorizontal);
        float y = Mathf.Clamp(directionLocal.y * verticalMultiplier, -maxVertical, maxVertical);

        if (invertHorizontal)
        {
            x = -x;
        }

        if (invertVertical)
        {
            y = -y;
        }

        return new Vector2(x, y) * pupilMoveDistance;
    }

    private Vector3 GetTargetEyesPoint(PlayerController targetPlayer)
    {
        PlayerEyesController targetEyes = targetPlayer.GetComponent<PlayerEyesController>();

        if (targetEyes != null)
        {
            return targetEyes.EyesWorldCenter;
        }

        float height = standingEyeHeight;

        if (targetPlayer.IsCrouching)
        {
            height = crouchingEyeHeight;
        }

        return targetPlayer.transform.position + Vector3.up * height;
    }

    private Vector3 GetOwnEyesCenter()
    {
        if (leftPupil != null && rightPupil != null)
        {
            return (leftPupil.position + rightPupil.position) * 0.5f;
        }

        if (leftEyeRoot != null && rightEyeRoot != null)
        {
            return (leftEyeRoot.position + rightEyeRoot.position) * 0.5f;
        }

        if (leftPupil != null)
        {
            return leftPupil.position;
        }

        if (rightPupil != null)
        {
            return rightPupil.position;
        }

        return transform.position + Vector3.up * standingEyeHeight;
    }

    private Vector2 GetMovementBounceOffset()
    {
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float horizontalSpeed = horizontalVelocity.magnitude;

        float speedRatio = Mathf.Clamp01(horizontalSpeed / movementSpeedForMaxBounce);

        Vector3 localVelocity = transform.InverseTransformDirection(horizontalVelocity);

        float sideOffset = 0f;

        if (horizontalSpeed > 0.05f)
        {
            sideOffset = -Mathf.Clamp(localVelocity.x, -1f, 1f) * walkSideAmount;
        }

        float walkBob = Mathf.Sin(Time.time * Mathf.Lerp(3f, 9f, speedRatio))
                        * walkBounceAmount
                        * speedRatio;

        float idleX = Mathf.Sin(Time.time * 1.7f) * idleAmount;
        float idleY = Mathf.Cos(Time.time * 1.3f) * idleAmount;

        float jumpBob = Mathf.Clamp(-velocity.y * 0.012f, -jumpBounceAmount, jumpBounceAmount);

        return new Vector2(
            sideOffset + idleX,
            walkBob + idleY + jumpBob
        );
    }

    private void ApplyPupilOffset(Vector2 offset)
    {
        Vector3 localOffset = new Vector3(offset.x, offset.y, 0f);

        if (leftPupil != null)
        {
            leftPupil.localPosition = Vector3.Lerp(
                leftPupil.localPosition,
                leftPupilStartLocalPosition + localOffset,
                Time.deltaTime * lookSmooth
            );
        }

        if (rightPupil != null)
        {
            rightPupil.localPosition = Vector3.Lerp(
                rightPupil.localPosition,
                rightPupilStartLocalPosition + localOffset,
                Time.deltaTime * lookSmooth
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, playerLookRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(debugTargetPoint, 0.06f);
    }
}