using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Rigidbody))]
public class SlopPlayerController : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;

    [Tooltip("Для New Input System нормальные значения: 0.04 - 0.15")]
    [SerializeField] private float mouseSensitivity = 0.08f;

    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float sprintSpeed = 6.2f;
    [SerializeField] private float moveSmoothTime = 0.08f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float jumpStickToGroundForce = -2f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float jumpSquash = 0.88f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 1.75f;
    [SerializeField] private float crouchHeight = 1.0f;

    [SerializeField] private float standingCameraY = 1.55f;
    [SerializeField] private float crouchCameraY = 0.92f;

    [SerializeField] private float crouchSpeed = 10f;

    [Header("Visual Body")]
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftShoulder;
    [SerializeField] private Transform rightShoulder;

    [Header("Hands")]
    [SerializeField] private SlopHand leftHand;
    [SerializeField] private SlopHand rightHand;

    [Header("Interaction")]
    [SerializeField] private float interactionRadius = 2.2f;
    [SerializeField] private float interactionViewDot = 0.35f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private float handActivationDistance = 0.12f;

    [Header("Ragdoll / Fall")]
    [SerializeField] private float ragdollDuration = 2.0f;
    [SerializeField] private float standUpHeightOffset = 0.15f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Multiplayer")]
    [SerializeField] private bool isLocal = true;
    [SerializeField] private int localOnlyLayer = 6;

    [Header("Debug")]
    [SerializeField] private bool isCrouching;
    [SerializeField] private bool isRagdoll;
    [SerializeField] private SlopInteractable currentInteractable;

    private CharacterController characterController;
    private Rigidbody rb;
    private CapsuleCollider ragdollCollider;

    private Vector2 currentMoveInput;
    private Vector2 moveInputVelocity;

    private float verticalVelocity;
    private float pitch;

    private bool handReachedInteractable;
    private Coroutine ragdollRoutine;
    private float shoulderY;
    private float storedYaw;
    private float airborneSquash = 1f;

    public bool IsRagdoll => isRagdoll;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        ragdollCollider = GetComponent<CapsuleCollider>();

        if (ragdollCollider == null)
        {
            ragdollCollider = gameObject.AddComponent<CapsuleCollider>();
        }

        ragdollCollider.enabled = false;
        ragdollCollider.height = standingHeight;
        ragdollCollider.radius = characterController.radius;
        ragdollCollider.center = new Vector3(0f, standingHeight * 0.5f, 0f);

        characterController.height = standingHeight;

        if (leftShoulder != null)
        {
            shoulderY = leftShoulder.localPosition.y;
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (isLocal)
        {
            int layerMask = 1 << localOnlyLayer;

            if (headVisual != null)
            {
                SetLayerRecursively(headVisual, localOnlyLayer);
            }

            playerCamera.cullingMask &= ~layerMask;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (isRagdoll)
        {
            UpdateHandsOnly();
            return;
        }

        HandleLook();
        HandleCrouch();
        HandleMovement();
        HandleInteraction();
        HandleRagdollInput();
    }

    private void UpdateHandsOnly()
    {
        if (rightHand != null)
        {
            rightHand.ClearTarget();
        }

        if (leftHand != null)
        {
            leftHand.ClearTarget();
        }
    }

    private void HandleLook()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector2 targetInput = ReadMoveInput();
        targetInput = Vector2.ClampMagnitude(targetInput, 1f);

        currentMoveInput = Vector2.SmoothDamp(
            currentMoveInput,
            targetInput,
            ref moveInputVelocity,
            moveSmoothTime
        );

        bool sprinting = IsSprintPressed() && !isCrouching;
        float speed = sprinting ? sprintSpeed : walkSpeed;

        if (isCrouching)
        {
            speed *= 0.55f;
        }

        Vector3 move =
            transform.right * currentMoveInput.x +
            transform.forward * currentMoveInput.y;

        move *= speed;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = jumpStickToGroundForce;

            if (IsJumpPressed())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        characterController.Move(move * Time.deltaTime);

        float targetAirborne = characterController.isGrounded ? 1f : jumpSquash;
        airborneSquash = Mathf.Lerp(airborneSquash, targetAirborne, Time.deltaTime * crouchSpeed);
    }

    private Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return Vector2.zero;

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        return input;
    }

    private void HandleCrouch()
    {
        isCrouching = IsCrouchPressed();

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float targetCameraY = isCrouching ? crouchCameraY : standingCameraY;

        characterController.height = Mathf.Lerp(
            characterController.height,
            targetHeight,
            Time.deltaTime * crouchSpeed
        );

        characterController.center = new Vector3(
            0f,
            characterController.height * 0.5f,
            0f
        );

        if (cameraPivot != null)
        {
            Vector3 cameraLocalPos = cameraPivot.localPosition;

            cameraLocalPos.y = Mathf.Lerp(
                cameraLocalPos.y,
                targetCameraY,
                Time.deltaTime * crouchSpeed
            );

            cameraPivot.localPosition = cameraLocalPos;
        }

        if (headVisual != null && cameraPivot != null)
        {
            headVisual.localPosition = cameraPivot.localPosition;
            headVisual.localRotation = cameraPivot.localRotation;
        }

        float crouchRatio = Mathf.InverseLerp(standingHeight, crouchHeight, characterController.height);

        if (bodyVisual != null)
        {
            float baseScale = Mathf.Lerp(1f, 0.55f, crouchRatio);
            float finalScale = baseScale * airborneSquash;
            bodyVisual.localScale = Vector3.Lerp(
                bodyVisual.localScale,
                new Vector3(finalScale, finalScale, finalScale),
                Time.deltaTime * crouchSpeed
            );
        }

        float targetShoulderY = Mathf.Lerp(shoulderY, shoulderY * (crouchHeight / standingHeight), crouchRatio);

        if (leftShoulder != null)
        {
            Vector3 pos = leftShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetShoulderY, Time.deltaTime * crouchSpeed);
            leftShoulder.localPosition = pos;
        }

        if (rightShoulder != null)
        {
            Vector3 pos = rightShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetShoulderY, Time.deltaTime * crouchSpeed);
            rightShoulder.localPosition = pos;
        }
    }

    private void HandleInteraction()
    {
        if (WasInteractPressedThisFrame())
        {
            currentInteractable = FindBestInteractable();
            handReachedInteractable = false;

            if (currentInteractable != null && rightHand != null)
            {
                rightHand.SetTarget(currentInteractable.HandTarget);
            }
        }

        if (IsInteractPressed() && currentInteractable != null)
        {
            float distanceToPlayer = Vector3.Distance(
                GetInteractionOrigin().position,
                currentInteractable.transform.position
            );

            if (distanceToPlayer > interactionRadius)
            {
                StopInteraction();
                return;
            }

            if (rightHand != null)
            {
                rightHand.SetTarget(currentInteractable.HandTarget);

                float handDistance = rightHand.DistanceToTarget;

                if (handDistance <= handActivationDistance)
                {
                    if (!handReachedInteractable)
                    {
                        handReachedInteractable = true;
                        currentInteractable.OnHandBegin(this);
                    }

                    currentInteractable.OnHandHold(this, Time.deltaTime);
                }
            }
        }

        if (WasInteractReleasedThisFrame())
        {
            StopInteraction();
        }
    }

    private void StopInteraction()
    {
        if (currentInteractable != null && handReachedInteractable)
        {
            currentInteractable.OnHandEnd(this);
        }

        currentInteractable = null;
        handReachedInteractable = false;

        if (rightHand != null)
        {
            rightHand.ClearTarget();
        }
    }

    private SlopInteractable FindBestInteractable()
    {
        Transform origin = GetInteractionOrigin();

        Collider[] hits = Physics.OverlapSphere(
            origin.position,
            interactionRadius,
            interactableMask,
            QueryTriggerInteraction.Collide
        );

        SlopInteractable best = null;
        float bestScore = -999f;

        for (int i = 0; i < hits.Length; i++)
        {
            SlopInteractable interactable = hits[i].GetComponentInParent<SlopInteractable>();

            if (interactable == null)
                continue;

            if (!interactable.CanInteract(this))
                continue;

            Vector3 toTarget = interactable.transform.position - origin.position;
            float distance = toTarget.magnitude;

            if (distance <= 0.01f)
                continue;

            Vector3 direction = toTarget.normalized;
            float dot = Vector3.Dot(origin.forward, direction);

            if (dot < interactionViewDot)
                continue;

            float score = dot * 2f - distance * 0.25f;

            if (score > bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best;
    }

    private Transform GetInteractionOrigin()
    {
        if (playerCamera != null)
        {
            return playerCamera.transform;
        }

        if (cameraPivot != null)
        {
            return cameraPivot;
        }

        return transform;
    }

    private void HandleRagdollInput()
    {
        if (!WasRagdollPressedThisFrame())
            return;

        StartRagdoll();
    }

    public void StartRagdoll()
    {
        if (isRagdoll)
            return;

        if (ragdollRoutine != null)
        {
            StopCoroutine(ragdollRoutine);
        }

        ragdollRoutine = StartCoroutine(RagdollRoutine());
    }

    private IEnumerator RagdollRoutine()
    {
        isRagdoll = true;

        StopInteraction();

        if (leftHand != null)
        {
            leftHand.SetRagdollMode(true);
        }

        if (rightHand != null)
        {
            rightHand.SetRagdollMode(true);
        }

        storedYaw = transform.eulerAngles.y;
        characterController.enabled = false;

        ragdollCollider.height = standingHeight;
        ragdollCollider.radius = characterController.radius;
        ragdollCollider.center = new Vector3(0f, standingHeight * 0.5f, 0f);
        ragdollCollider.enabled = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;

        rb.linearVelocity = transform.forward * 1.5f + Vector3.up * 0.6f;
        rb.angularVelocity = new Vector3(
            Random.Range(-2.5f, 2.5f),
            Random.Range(-1.5f, 1.5f),
            Random.Range(-2.5f, 2.5f)
        );

        yield return new WaitForSeconds(ragdollDuration);

        StandUp();

        ragdollRoutine = null;
    }

    private void StandUp()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        ragdollCollider.enabled = false;

        Vector3 startPos = transform.position;
        Vector3 standPosition = new Vector3(startPos.x, startPos.y, startPos.z);

        if (Physics.Raycast(
                startPos + Vector3.up * 5f,
                Vector3.down,
                out RaycastHit hit,
                15f,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            standPosition = hit.point + Vector3.up * standUpHeightOffset;
            standPosition.x = startPos.x;
            standPosition.z = startPos.z;
        }
        else
        {
            standPosition.y = Mathf.Max(standPosition.y, standUpHeightOffset + 0.5f);
        }

        transform.SetPositionAndRotation(
            standPosition,
            Quaternion.Euler(0f, storedYaw, 0f)
        );

        characterController.enabled = true;

        characterController.height = standingHeight;
        characterController.center = new Vector3(0f, standingHeight * 0.5f, 0f);

        if (cameraPivot != null)
        {
            Vector3 cameraLocalPos = cameraPivot.localPosition;
            cameraLocalPos.y = standingCameraY;
            cameraPivot.localPosition = cameraLocalPos;
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (headVisual != null && cameraPivot != null)
        {
            headVisual.localPosition = cameraPivot.localPosition;
            headVisual.localRotation = cameraPivot.localRotation;
        }

        if (bodyVisual != null)
        {
            bodyVisual.localScale = Vector3.one;
        }

        if (leftShoulder != null)
        {
            Vector3 pos = leftShoulder.localPosition;
            pos.y = shoulderY;
            leftShoulder.localPosition = pos;
        }

        if (rightShoulder != null)
        {
            Vector3 pos = rightShoulder.localPosition;
            pos.y = shoulderY;
            rightShoulder.localPosition = pos;
        }

        if (leftHand != null)
        {
            leftHand.SetRagdollMode(false);
        }

        if (rightHand != null)
        {
            rightHand.SetRagdollMode(false);
        }

        isRagdoll = false;
    }

    private bool IsJumpPressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        return keyboard.spaceKey.wasPressedThisFrame;
    }

    private bool IsSprintPressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        return keyboard.leftShiftKey.isPressed ||
               keyboard.rightShiftKey.isPressed;
    }

    private bool IsCrouchPressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        return keyboard.leftCtrlKey.isPressed ||
               keyboard.rightCtrlKey.isPressed ||
               keyboard.cKey.isPressed;
    }

    private bool IsInteractPressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        return keyboard.eKey.isPressed;
    }

    private bool WasInteractPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        return keyboard.eKey.wasPressedThisFrame;
    }

    private bool WasInteractReleasedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        return keyboard.eKey.wasReleasedThisFrame;
    }

    private bool WasRagdollPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        return keyboard.gKey.wasPressedThisFrame;
    }

    private void SetLayerRecursively(Transform obj, int layer)
    {
        obj.gameObject.layer = layer;

        for (int i = 0; i < obj.childCount; i++)
        {
            SetLayerRecursively(obj.GetChild(i), layer);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = playerCamera != null ? playerCamera.transform : transform;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, interactionRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawRay(origin.position, origin.forward * interactionRadius);
    }
}