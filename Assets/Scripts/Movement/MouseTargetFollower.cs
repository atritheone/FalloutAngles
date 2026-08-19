// imports
using UnityEngine;
using UnityEngine.InputSystem;



// class
[DefaultExecutionOrder(50)]
public class MouseTargetFollower : MonoBehaviour
{
    private const float MinStableAimDirectionSqr = 0.0001f;
    private const float MinRayPlaneDirectionY = 0.0001f;
    
    // variables
    // Camera used to generate a ray from the mouse cursor.
    [SerializeField] private Camera mainCamera;

    // The orbit controller that decides combat/normal and orbit-busy state.
    [SerializeField] private CameraRigOrbit orbitController;

    // Movement controller used to determine movement intent (input), not physics velocity.
    [SerializeField] private PlayerMovement playerMovement;

    // Layers that represent "things you can actually aim at".
    [SerializeField] private LayerMask worldHitLayers = ~0;

    // Layers that represent "ground only".
    [SerializeField] private LayerMask groundHitLayers = ~0;

    // If true, raycast can hit anything at any distance.
    [SerializeField] private bool useInfiniteRaycastDistance = true;

    // Maximum raycast distance (used only when infinite distance is off).
    [SerializeField] private float maxDistance = 500f;

    // If we are aiming into sky / nothing, place the target this far down the ray.
    [SerializeField] private float skyAimDistance = 500f;

    // Small offset so the target doesn't z-fight with surfaces.
    [SerializeField] private float surfaceOffset = 0.02f;

    // If the ray's Y is above this, we consider it "aiming up" and DO NOT use ground.
    [SerializeField] private float groundAllowRayDirectionY = -0.02f;

    // How long (seconds) we blend the aim target after unfreezing.
    [SerializeField] private float unfreezeSmoothTime = 0.12f;

    // Tracks whether we were frozen last frame.
    private bool wasFrozen;

    // Whether we are currently blending from frozen -> queued aim.
    private bool isBlending;

    // Time spent blending so far.
    private float blendTimer;

    // Aim target position at the start of the blend.
    private Vector3 blendStartPos;

    // Aim target position we want to blend toward (captured on unfreeze).
    private Vector3 blendTargetPos;

    // Latest computed aim point (even while frozen).
    private Vector3 queuedAimPos;

    // Cached world hit used by other systems to avoid duplicate combat raycasts.
    private RaycastHit latestWorldHit;

    // Whether latestWorldHit is valid for the current frame.
    private bool hasLatestWorldHit;

    // Latest camera ray used to position this target, cached for stable yaw solving.
    private Ray latestAimRay;

    // Whether latestAimRay is valid for the current frame.
    private bool hasLatestAimRay;

    // Cached transform reference to avoid repeated property lookup.
    private Transform cachedTransform;

    // Cached query trigger interaction setting.
    private static readonly QueryTriggerInteraction QueryIgnore = QueryTriggerInteraction.Ignore;

    
    
    // methods
    private void Awake()
    {
        cachedTransform = transform;

        // If no camera is assigned, use the scene's Main Camera.
        if (!mainCamera)
            mainCamera = Camera.main;

        // If no orbit controller is assigned, try to find one in the scene.
        if (!orbitController)
            orbitController = FindAnyObjectByType<CameraRigOrbit>();

        // If no movement controller is assigned, try to find one in the scene.
        if (!playerMovement)
            playerMovement = FindAnyObjectByType<PlayerMovement>();
    }


    private void Update()
    {
        Transform self = cachedTransform;
        Camera cam = mainCamera;

        // Stop if we still don't have a camera reference.
        if (!cam) return;

        // If we are NOT in combat mode, do not drive the aim target at all.
        CameraRigOrbit orbit = orbitController;
        if (orbit && !orbit.CombatMode)
        {
            // Clear freeze/blend bookkeeping so combat re-entry is clean.
            wasFrozen = false;

            isBlending = false;
            hasLatestWorldHit = false;
            hasLatestAimRay = false;

            return;
        }

        // Stop if there is no mouse device detected.
        if (!TryGetMousePosition(out Vector2 mousePos)) return;

        // Decide whether we should freeze aim application.
        bool freeze = ShouldFreezeAim();

        // If we are frozen, do NOT move the target transform (but keep queuing aim).
        if (freeze)
        {
            // Compute and store the aim position while frozen so we can blend to it later.
            // Do not update the stable aim ray while frozen; IK/body aim should stay locked too.
            queuedAimPos = ComputeAimWorldPosition(mousePos, false);

            // Remember that we were frozen (so we can detect the unfreeze edge).
            wasFrozen = true;

            // Cancel any blend while actively frozen.
            isBlending = false;

            return;
        }

        // If we were frozen last frame and now we are not, begin a smooth blend.
        if (wasFrozen)
        {
            // Capture the queued aim position as the blend target (lock it once).
            queuedAimPos = ComputeAimWorldPosition(mousePos);

            // Capture the frozen position as the blend start.
            blendStartPos = self.position;

            // Lock the blend target on unfreeze.
            blendTargetPos = queuedAimPos;

            // Reset blend timer.
            blendTimer = 0f;

            // Enter blending mode.
            isBlending = true;

            // Clear frozen state.
            wasFrozen = false;
        }

        // If we are blending, smoothly move the target to the queued position.
        if (isBlending)
        {
            float dt = Time.deltaTime;

            // Advance blend timer.
            blendTimer += dt;

            // Normalized progress (0 -> 1).
            float t = unfreezeSmoothTime > 0f ? Mathf.Clamp01(blendTimer / unfreezeSmoothTime) : 1f;

            // Smooth easing.
            t = Mathf.SmoothStep(0f, 1f, t);

            // Blend from frozen position to queued position.
            self.position = Vector3.Lerp(blendStartPos, blendTargetPos, t);

            // End blend when complete.
            if (t >= 1f)
                isBlending = false;

            return;
        }

        // Normal behavior: apply aim instantly (no smoothing).
        queuedAimPos = ComputeAimWorldPosition(mousePos);
        self.position = queuedAimPos;
    }


    private bool ShouldFreezeAim()
    {
        CameraRigOrbit orbit = orbitController;

        // Freeze targeting for the full combat MMB orbit, including while moving.
        if (orbit && orbit.IsCombatManualOrbitHeld)
            return true;

        // Otherwise do not freeze.
        return false;
    }


    private Vector3 ComputeAimWorldPosition(Vector2 mousePos, bool cacheAimRay = true)
    {
        Camera cam = mainCamera;

        // Create a ray from the mouse position into the world.
        Ray ray = cam.ScreenPointToRay(mousePos);
        if (cacheAimRay)
        {
            latestAimRay = ray;
            hasLatestAimRay = true;
        }

        // Choose the raycast distance.
        float rayDistance = useInfiniteRaycastDistance ? Mathf.Infinity : maxDistance;

        // Try hit "world" first (walls/props/enemies).
        if (Physics.Raycast(ray, out RaycastHit worldHit, rayDistance, worldHitLayers, QueryIgnore))
        {
            latestWorldHit = worldHit;
            hasLatestWorldHit = true;

            // Start at the exact hit point.
            Vector3 pos = worldHit.point;

            // Push the target slightly off the surface to avoid flicker.
            pos += worldHit.normal * surfaceOffset;

            // Return computed position.
            return pos;
        }

        hasLatestWorldHit = false;

        // If we are aiming DOWN (or roughly flat), allow ground fallback.
        if (ray.direction.y <= groundAllowRayDirectionY)
        {
            // Try hit ground only.
            if (Physics.Raycast(ray, out RaycastHit groundHit, rayDistance, groundHitLayers, QueryIgnore))
            {
                // Start at the ground hit point.
                Vector3 pos = groundHit.point;

                // Push the target slightly off the surface to avoid flicker.
                pos += groundHit.normal * surfaceOffset;

                // Return computed position.
                return pos;
            }
        }

        // If nothing was hit (or we are aiming up), place the target in the sky along the ray.
        return ray.GetPoint(skyAimDistance);
    }

    public bool TryGetLatestWorldHit(out RaycastHit worldHit)
    {
        if (hasLatestWorldHit)
        {
            worldHit = latestWorldHit;
            return true;
        }

        worldHit = default;
        return false;
    }

    public bool TryGetStableFlatAimDirection(Vector3 originPosition, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (!hasLatestAimRay)
            return false;

        Ray ray = latestAimRay;

        if (Mathf.Abs(ray.direction.y) > MinRayPlaneDirectionY)
        {
            Plane aimPlane = new Plane(Vector3.up, originPosition);
            if (aimPlane.Raycast(ray, out float distance) && distance > 0f)
            {
                Vector3 toPlanePoint = ray.GetPoint(distance) - originPosition;
                toPlanePoint.y = 0f;

                if (toPlanePoint.sqrMagnitude > MinStableAimDirectionSqr)
                {
                    direction = toPlanePoint.normalized;
                    return true;
                }
            }
        }

        Vector3 flatRayDirection = ray.direction;
        flatRayDirection.y = 0f;

        if (flatRayDirection.sqrMagnitude <= MinStableAimDirectionSqr)
            return false;

        direction = flatRayDirection.normalized;
        return true;
    }

    public bool TryGetStableAimPoint(float aimDistance, out Vector3 point)
    {
        point = Vector3.zero;

        if (!hasLatestAimRay)
            return false;

        point = latestAimRay.GetPoint(Mathf.Max(0.01f, aimDistance));
        return true;
    }

    private static bool TryGetMousePosition(out Vector2 mousePos)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            mousePos = default;
            return false;
        }

        mousePos = mouse.position.ReadValue();
        return true;
    }
}
