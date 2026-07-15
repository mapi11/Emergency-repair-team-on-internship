using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;

    [Tooltip("Для New Input System нормальные значения: 0.04 - 0.15")]
    [SerializeField] public float mouseSensitivity = 0.08f;

    public bool IsPaused { get; set; }

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
    [SerializeField] private float crouchBodyLowering = 0.45f;

    [SerializeField] private float standingCameraY = 1.55f;
    [SerializeField] private float crouchCameraY = 0.92f;

    [SerializeField] private float crouchSpeed = 10f;

    [Header("Visual Body")]
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftShoulder;
    [SerializeField] private Transform rightShoulder;

    [Header("Arms")]
    [SerializeField] [Range(0f, 1f)] private float shoulderPitchInfluence = 0.35f;

    [Header("Hands")]
    [SerializeField] private Hand leftHand;
    [SerializeField] private Hand rightHand;

    public enum InteractionHand { Right, Left }

    [Header("Interaction")]
    [SerializeField] private InteractionHand interactionHand = InteractionHand.Right;
    [SerializeField] private float interactionRadius = 2.2f;
    [SerializeField] private float interactionViewDot = 0.35f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private float handActivationDistance = 0.12f;

    [Header("Inventory")]
    [SerializeField] private Inventory inventory;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshair;

    [Header("Throw")]
    [SerializeField] private float maxChargeTime = 2f;
    [SerializeField] private ChargeSlider chargeSlider;

    [Header("Multiplayer")]
    [SerializeField] private bool isLocal = true;
    [SerializeField] private int localOnlyLayer = 6;

    [Header("Debug")]
    [SerializeField] private bool isCrouching;
    [SerializeField] private Interactable currentInteractable;

    private CharacterController characterController;
    private Rigidbody rb;

    private Vector2 currentMoveInput;
    private Vector2 moveInputVelocity;

    private float verticalVelocity;
    private float pitch;

    private bool handReachedInteractable;
    private bool isChargingThrow;
    private float throwChargeTime;
    private NetworkInventorySync cachedSync;
    private float shoulderY;
    private float airborneSquash = 1f;
    private float bodyInitialY;
    private Vector3 leftShoulderInitialEuler;
    private Vector3 rightShoulderInitialEuler;

    public bool IsCrouching => isCrouching;

    private Hand InteractionHandRef => interactionHand == InteractionHand.Right ? rightHand : leftHand;
    public Hand CurrentInteractionHand => InteractionHandRef;
    public InteractionHand SelectedInteractionHand => interactionHand;
    public bool IsLocalPlayer => isLocal;
    public float Pitch => pitch;
    public Interactable CurrentInteractable => currentInteractable;
    public bool HasCurrentInteractable => currentInteractable != null;
    public Inventory PlayerInventory => inventory;
    public Transform ActiveHandHoldPivot => InteractionHandRef != null ? InteractionHandRef.HoldPivot : null;

    private void Awake()
    {
        float savedSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", -1f);

        if (savedSensitivity >= 0f)
            mouseSensitivity = savedSensitivity;

        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        characterController.height = standingHeight;

        if (bodyVisual != null)
        {
            bodyInitialY = bodyVisual.localPosition.y;
        }

        if (leftShoulder != null)
        {
            shoulderY = leftShoulder.localPosition.y;
            leftShoulderInitialEuler = leftShoulder.localEulerAngles;
        }

        if (rightShoulder != null)
        {
            rightShoulderInitialEuler = rightShoulder.localEulerAngles;
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

        interactionHand = GameSessionData.SelectedHandIndex == 1 ? InteractionHand.Left : InteractionHand.Right;
    }

    private void Update()
    {
        if (!isLocal)
            return;

        HandleCursorEscape();

        HandleLook();
        HandleArms();
        HandleCrouch();
        HandleMovement();
        HandleInteraction();
        HandleInventoryInput();
        UpdateCrosshair();
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

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

    private void HandleArms()
    {
        float crouchRatio = Mathf.InverseLerp(standingHeight, crouchHeight, characterController.height);
        float crouchArmY = Mathf.Lerp(shoulderY, shoulderY * (crouchHeight / standingHeight), crouchRatio);

        float pitchRatio = pitch / 90f;
        float pitchArmY = crouchArmY - pitchRatio * 0.12f * shoulderPitchInfluence;

        float targetArmY = Mathf.Lerp(crouchArmY, pitchArmY, Mathf.Abs(pitchRatio));

        if (leftShoulder != null)
        {
            Vector3 euler = leftShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            leftShoulder.localEulerAngles = euler;

            Vector3 pos = leftShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            leftShoulder.localPosition = pos;
        }

        if (rightShoulder != null)
        {
            Vector3 euler = rightShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            rightShoulder.localEulerAngles = euler;

            Vector3 pos = rightShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            rightShoulder.localPosition = pos;
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
        bool wantCrouch = IsCrouchPressed();

        if (!wantCrouch && !HasHeadroom())
        {
            wantCrouch = true;
        }

        isCrouching = wantCrouch;

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

            float bodyOffset = (1f - baseScale) * crouchBodyLowering;
            Vector3 bodyPos = bodyVisual.localPosition;
            bodyPos.y = Mathf.Lerp(bodyPos.y, bodyInitialY - bodyOffset, Time.deltaTime * crouchSpeed);
            bodyVisual.localPosition = bodyPos;
        }

    }

    private void HandleInteraction()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (WasInteractPressedThisFrame())
        {
            currentInteractable = FindBestInteractable();
            handReachedInteractable = false;

            if (currentInteractable != null && IsInventoryFull())
                currentInteractable = null;

            Hand hand = InteractionHandRef;
            if (currentInteractable != null && hand != null)
            {
                hand.SetTarget(currentInteractable.HandTarget);
            }
        }

        if (IsInteractPressed() && currentInteractable != null)
        {
            if (!currentInteractable.CanInteract(this))
            {
                StopInteraction();
                return;
            }

            float distanceToPlayer = Vector3.Distance(
                GetInteractionOrigin().position,
                currentInteractable.transform.position
            );

            if (distanceToPlayer > interactionRadius)
            {
                StopInteraction();
                return;
            }

            Hand hand = InteractionHandRef;
            if (hand != null)
            {
                hand.SetTarget(currentInteractable.HandTarget);

                float handDistance = hand.DistanceToTarget;

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

    private bool IsInventoryFull()
    {
        if (inventory == null)
            return true;

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            if (inventory.GetItemAtSlot(i) == null)
                return false;
        }

        return true;
    }

    private void StopInteraction()
    {
        if (currentInteractable != null && handReachedInteractable)
        {
            currentInteractable.OnHandEnd(this);
        }

        currentInteractable = null;
        handReachedInteractable = false;

        Hand hand = InteractionHandRef;
        if (hand != null)
        {
            hand.ClearTarget();
        }
    }

    private void HandleInventoryInput()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            CancelCharge();
            return;
        }

        if (inventory == null)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            CancelCharge();
            inventory.SwitchToSlot(0);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            CancelCharge();
            inventory.SwitchToSlot(1);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            CancelCharge();
            inventory.SwitchToSlot(2);
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            CancelCharge();
            inventory.SwitchToSlot(3);
        }
        else if (keyboard.digit5Key.wasPressedThisFrame)
        {
            CancelCharge();
            inventory.SwitchToSlot(4);
        }

        if (keyboard.qKey.wasPressedThisFrame && !isChargingThrow)
        {
            if (inventory.ActiveSlot < 0 || string.IsNullOrEmpty(inventory.ActiveItemType))
                return;

            isChargingThrow = true;
            throwChargeTime = 0f;

            if (chargeSlider != null)
                chargeSlider.Show(0f);
        }
        else if (keyboard.qKey.isPressed && isChargingThrow)
        {
            throwChargeTime += Time.deltaTime;
            float normalized = Mathf.Clamp01(throwChargeTime / maxChargeTime);

            if (chargeSlider != null)
                chargeSlider.UpdateValue(normalized);
        }
        else if (keyboard.qKey.wasReleasedThisFrame && isChargingThrow)
        {
            isChargingThrow = false;
            float normalized = Mathf.Clamp01(throwChargeTime / maxChargeTime);

            if (chargeSlider != null)
                chargeSlider.Hide();

            Vector3 direction = cameraPivot != null ? cameraPivot.forward :
                                playerCamera != null ? playerCamera.transform.forward : transform.forward;

            if (cachedSync == null)
                cachedSync = GetComponent<NetworkInventorySync>();

            if (cachedSync != null)
                cachedSync.LaunchActiveItem(normalized, direction);
        }
    }

    private void CancelCharge()
    {
        if (!isChargingThrow)
            return;

        isChargingThrow = false;
        throwChargeTime = 0f;

        if (chargeSlider != null)
            chargeSlider.Hide();
    }

    private Interactable FindBestInteractable()
    {
        Transform origin = GetInteractionOrigin();

        Collider[] hits = Physics.OverlapSphere(
            origin.position,
            interactionRadius,
            interactableMask,
            QueryTriggerInteraction.Collide
        );

        Interactable best = null;
        float bestScore = -999f;

        for (int i = 0; i < hits.Length; i++)
        {
            Interactable interactable = hits[i].GetComponentInParent<Interactable>();

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

    private Collider[] headroomHits = new Collider[16];

    private bool HasHeadroom()
    {
        float radius = characterController != null ? characterController.radius * 0.9f : 0.3f;
        float currentTop = transform.position.y + characterController.height;
        float standingTop = transform.position.y + standingHeight;
        float bottom = currentTop + 0.05f;
        float top = standingTop - 0.05f;

        if (bottom >= top)
            return true;

        Vector3 start = new Vector3(transform.position.x, bottom, transform.position.z);
        Vector3 end = new Vector3(transform.position.x, top, transform.position.z);

        int count = Physics.OverlapCapsuleNonAlloc(start, end, radius, headroomHits, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (!headroomHits[i].transform.IsChildOf(transform))
                return false;
        }

        return true;
    }

    private void SetLayerRecursively(Transform obj, int layer)
    {
        obj.gameObject.layer = layer;

        for (int i = 0; i < obj.childCount; i++)
        {
            SetLayerRecursively(obj.GetChild(i), layer);
        }
    }

    private void HandleCursorEscape()
    {
        if (IsPaused)
            return;

        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        bool cursorIsLocked = Cursor.lockState == CursorLockMode.Locked;

        if (cursorIsLocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (crosshair != null)
            {
                crosshair.SetActive(false);
            }

            CancelCharge();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (crosshair != null)
            {
                crosshair.SetActive(true);
            }
        }
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null)
            return;

        crosshair.SetActive(HasInteractableInSight());
    }

    private bool HasInteractableInSight()
    {
        Transform origin = GetInteractionOrigin();

        Collider[] hits = Physics.OverlapSphere(
            origin.position,
            interactionRadius,
            interactableMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Interactable interactable = hits[i].GetComponentInParent<Interactable>();

            if (interactable == null || !interactable.CanInteract(this))
                continue;

            Vector3 toTarget = interactable.transform.position - origin.position;
            float distance = toTarget.magnitude;

            if (distance <= 0.01f)
                continue;

            float dot = Vector3.Dot(origin.forward, toTarget.normalized);

            if (dot >= interactionViewDot)
                return true;
        }

        return false;
    }

    //=============== Network=================

    public void SetLocalPlayer(bool value)
    {
        isLocal = value;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = value;

            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = value;
            }
        }

        if (crosshair != null)
        {
            crosshair.SetActive(value);
        }

        if (value)
        {
            int layerMask = 1 << localOnlyLayer;

            if (headVisual != null)
            {
                SetLayerRecursively(headVisual, localOnlyLayer);
                SetLayerRecursively(bodyVisual, localOnlyLayer);
            }

            if (playerCamera != null)
            {
                playerCamera.cullingMask &= ~layerMask;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ApplyRemoteVisualState(bool remoteCrouching, float remotePitch)
    {
        if (isLocal)
            return;

        isCrouching = remoteCrouching;
        pitch = remotePitch;

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

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
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (headVisual != null && cameraPivot != null)
        {
            headVisual.localPosition = cameraPivot.localPosition;
            headVisual.localRotation = cameraPivot.localRotation;
        }

        ApplyRemoteArmsAndBody();
    }

    private void ApplyRemoteArmsAndBody()
    {
        if (characterController == null)
            return;

        float crouchRatio = Mathf.InverseLerp(
            standingHeight,
            crouchHeight,
            characterController.height
        );

        float crouchArmY = Mathf.Lerp(
            shoulderY,
            shoulderY * (crouchHeight / standingHeight),
            crouchRatio
        );

        float pitchRatio = pitch / 90f;
        float pitchArmY = crouchArmY - pitchRatio * 0.12f * shoulderPitchInfluence;

        float targetArmY = Mathf.Lerp(
            crouchArmY,
            pitchArmY,
            Mathf.Abs(pitchRatio)
        );

        if (leftShoulder != null)
        {
            Vector3 euler = leftShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            leftShoulder.localEulerAngles = euler;

            Vector3 pos = leftShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            leftShoulder.localPosition = pos;
        }

        if (rightShoulder != null)
        {
            Vector3 euler = rightShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            rightShoulder.localEulerAngles = euler;

            Vector3 pos = rightShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            rightShoulder.localPosition = pos;
        }

        if (bodyVisual != null)
        {
            float baseScale = Mathf.Lerp(1f, 0.55f, crouchRatio);

            bodyVisual.localScale = Vector3.Lerp(
                bodyVisual.localScale,
                new Vector3(baseScale, baseScale, baseScale),
                Time.deltaTime * crouchSpeed
            );

            float bodyOffset = (1f - baseScale) * crouchBodyLowering;

            Vector3 bodyPos = bodyVisual.localPosition;
            bodyPos.y = Mathf.Lerp(
                bodyPos.y,
                bodyInitialY - bodyOffset,
                Time.deltaTime * crouchSpeed
            );

            bodyVisual.localPosition = bodyPos;
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