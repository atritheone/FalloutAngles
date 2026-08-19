// imports
using System.Collections.Generic;
using UnityEngine;



// methods
[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    private const float MinLookDirectionSqr = 0.0001f;
    private const float MinMassKilograms = 0.00001f;
    private const float MillimetersToMeters = 0.001f;
    private const float FallbackProjectileDiameterMeters = 0.009f;
    private const float MinImpactSweepDistance = 0.0001f;
    private const int MaxPredictedImpactHits = 16;
    private const int MaxPooledProjectilesPerPrefab = 64;
    private const int MaxPooledImpactFxPerPrefab = 32;

    private static readonly Dictionary<int, Queue<GameObject>> ProjectilePoolByPrefabId = new Dictionary<int, Queue<GameObject>>();
    private static readonly Dictionary<int, Queue<GameObject>> ImpactFxPoolByPrefabId = new Dictionary<int, Queue<GameObject>>();
    private static readonly RaycastHit[] PredictedImpactHits = new RaycastHit[MaxPredictedImpactHits];

    [Header("Lifetime")]
    // Maximum lifetime before auto-cleanup.
    [Min(0.01f)] [SerializeField] private float lifeTimeSeconds = 6f;

    // Delay before cleanup after an impact.
    [Min(0f)] [SerializeField] private float destroyAfterImpactSeconds = 0.02f;

    [Header("Impact FX")]
    // Optional impact VFX prefab.
    [SerializeField] private GameObject impactFxPrefab;

    // Lifetime for spawned impact FX.
    [Min(0f)] [SerializeField] private float impactFxLifetime = 2f;

    [Header("Ballistic Flight")]
    // If true, apply physically modeled quadratic aerodynamic drag.
    [SerializeField] private bool simulateQuadraticDrag = true;

    // Drag coefficient used by aerodynamic drag (typical bullets are around 0.2 to 0.4).
    [Min(0f)] [SerializeField] private float dragCoefficient = 0.295f;

    // Projectile diameter in meters (0 = estimate from collider bounds at launch).
    [Min(0f)] [SerializeField] private float projectileDiameterMeters = 0f;

    // If true, this projectile ignores prefab gravity and uses gravityScale.
    [SerializeField] private bool overridePrefabGravity;

    // Gravity multiplier used when overridePrefabGravity is enabled.
    [Min(0f)] [SerializeField] private float gravityScale = 1f;

    // If true, projectile orientation follows velocity each frame.
    [SerializeField] private bool alignRotationToVelocity = true;

    // Minimum speed required before velocity alignment rotates the projectile.
    [Min(0f)] [SerializeField] private float minimumSpeedForRotationAlignment = 1f;

    // Air density (kg/m^3) used by drag simulation.
    [Min(0f)] [SerializeField] private float airDensity = 1.225f;

    [Header("Impact Physics")]
    // If true, apply extra impulse to rigidbodies we hit.
    [SerializeField] private bool applyImpactImpulseToHitRigidbody = true;

    // Fraction of projectile momentum transferred into the hit rigidbody.
    [Range(0f, 1f)] [SerializeField] private float impactMomentumTransfer = 0.35f;

    // Multiplier for extra impact impulse after momentum transfer.
    [Min(0f)] [SerializeField] private float impactImpulseScale = 1f;

    // Maximum extra impulse applied at impact (0 = no cap).
    [Min(0f)] [SerializeField] private float maxImpactImpulse = 0f;

    // Chance that a non-ricochet round bounces instead of stopping on a valid surface impact.
    [Range(0f, 1f)] [SerializeField] private float nonRicochetRoundRicochetChance = 0.05f;

    [Header("Combat Damage")]
    // Direct damage applied to character targets on first impact.
    [Min(0f)] [SerializeField] private float directHitDamage = 0f;

    // If true, target damage resistance reduces directHitDamage.
    [SerializeField] private bool respectTargetDamageResistance = true;

    // Cache of the rigidbody used to drive projectile motion.
    private Rigidbody rb;

    // Cache of the collider so we can disable repeat impacts.
    private Collider bulletCollider;

    // True once this projectile has already resolved an impact.
    private bool hasImpacted;

    // True after launch has been called.
    private bool hasLaunched;

    // Cached per-instance offset between look rotation and this prefab's authored root orientation.
    private Quaternion launchRotationOffset = Quaternion.identity;

    // True once launchRotationOffset has been captured for this enabled instance.
    private bool hasLaunchRotationOffset;

    // Optional ammo definition used to override projectile ballistics for this spawned instance.
    private AmmoDefinition configuredAmmoDefinition;

    // Prefab-authored rigidbody defaults restored on enable.
    private float defaultMassKilograms;
    private bool defaultUseGravity;
    private float defaultLinearDamping;
    private float defaultAngularDamping;

    // Per-launch active ballistic parameters.
    private bool useCustomGravityAcceleration;
    private float activeGravityScale;
    private bool useQuadraticDragForLaunch;
    private float activeDragCoefficient;
    private float activeProjectileDiameterMeters;
    private float activeProjectileMassKilograms;
    private float activeImpactMomentumTransfer;
    private float activeImpactImpulseScale;
    private bool activeAllowsRicochet;
    private Collider pendingRicochetCollider;

    // Root transform of the character/object that fired this projectile.
    private Transform instigatorRoot;
    private PlayerSkill damageExperienceSkill = PlayerSkill.None;
    private int sourcePrefabId;
    private bool canReturnToPool;
    private GameObject pooledProjectileRoot;
    private readonly List<Collider> projectileColliders = new List<Collider>(4);
    private readonly List<Collider> instigatorColliders = new List<Collider>(32);
    private readonly List<Collider> ignoredExternalColliders = new List<Collider>(32);


    private void Awake()
    {
        // Cache required components once.
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
        RefreshProjectileColliders();

        Rigidbody rbRef = rb;
        if (rbRef)
        {
            defaultMassKilograms = Mathf.Max(MinMassKilograms, rbRef.mass);
            defaultUseGravity = rbRef.useGravity;
            defaultLinearDamping = rbRef.linearDamping;
            defaultAngularDamping = rbRef.angularDamping;
        }
    }


    private void OnEnable()
    {
        Collider colliderRef = bulletCollider;
        Rigidbody rbRef = rb;

        // Reset state when this object is re-enabled (pool-friendly).
        CancelInvoke(nameof(CleanupProjectile));
        RestoreIgnoredCollisions();
        hasImpacted = false;
        hasLaunched = false;
        hasLaunchRotationOffset = false;
        launchRotationOffset = Quaternion.identity;
        configuredAmmoDefinition = null;
        useCustomGravityAcceleration = false;
        activeGravityScale = defaultUseGravity ? 1f : 0f;
        useQuadraticDragForLaunch = false;
        activeDragCoefficient = 0f;
        activeProjectileDiameterMeters = 0f;
        activeProjectileMassKilograms = defaultMassKilograms;
        activeImpactMomentumTransfer = Mathf.Clamp01(impactMomentumTransfer);
        activeImpactImpulseScale = Mathf.Max(0f, impactImpulseScale);
        activeAllowsRicochet = false;
        pendingRicochetCollider = null;
        instigatorRoot = null;
        damageExperienceSkill = PlayerSkill.None;

        if (colliderRef)
            colliderRef.enabled = true;

        if (rbRef)
        {
            rbRef.isKinematic = false;
            rbRef.constraints = RigidbodyConstraints.None;
            rbRef.angularVelocity = Vector3.zero;
            rbRef.linearVelocity = Vector3.zero;
            rbRef.mass = Mathf.Max(MinMassKilograms, defaultMassKilograms);
            rbRef.useGravity = defaultUseGravity;
            rbRef.linearDamping = defaultLinearDamping;
            rbRef.angularDamping = defaultAngularDamping;
        }
    }

    public void ConfigureBallisticsFromAmmoDefinition(AmmoDefinition ammoDefinition)
    {
        configuredAmmoDefinition = ammoDefinition;
    }

    public void ConfigureDamage(float damageAmount, Transform damageInstigatorRoot = null, PlayerSkill skillForDamageExperience = PlayerSkill.None)
    {
        directHitDamage = Mathf.Max(0f, damageAmount);
        instigatorRoot = damageInstigatorRoot;
        damageExperienceSkill = skillForDamageExperience;
        IgnoreInstigatorCollisions();
    }

    public void Launch(Vector3 velocity)
    {
        Rigidbody rbRef = rb;

        // Stop if rigidbody is unavailable.
        if (!rbRef) return;

        hasLaunched = true;
        hasImpacted = false;

        ResolveLaunchBallistics(configuredAmmoDefinition);

        // Face in travel direction while preserving this prefab's authored root rotation offset.
        if (velocity.sqrMagnitude > MinLookDirectionSqr)
        {
            Quaternion lookRotation = Quaternion.LookRotation(velocity, Vector3.up);

            if (hasLaunchRotationOffset == false)
            {
                launchRotationOffset = Quaternion.Inverse(lookRotation) * transform.rotation;
                hasLaunchRotationOffset = true;
            }

            transform.rotation = lookRotation * launchRotationOffset;
        }

        // Apply velocity immediately.
        rbRef.linearVelocity = velocity;

        // Ensure every launched bullet self-cleans.
        ScheduleCleanup(lifeTimeSeconds);
    }

    private void FixedUpdate()
    {
        Rigidbody rbRef = rb;
        if (!rbRef || !hasLaunched || hasImpacted)
            return;

        Vector3 velocity = rbRef.linearVelocity;
        float speed = velocity.magnitude;
        Vector3 totalAcceleration = Vector3.zero;

        if (!activeAllowsRicochet && TryResolvePredictedImpact(rbRef, velocity, speed))
            return;

        if (useCustomGravityAcceleration)
            totalAcceleration += Physics.gravity * activeGravityScale;

        if (useQuadraticDragForLaunch &&
            speed > 0f &&
            activeDragCoefficient > 0f &&
            activeProjectileDiameterMeters > 0f &&
            activeProjectileMassKilograms > MinMassKilograms)
        {
            float projectileRadius = activeProjectileDiameterMeters * 0.5f;
            float frontalArea = Mathf.PI * projectileRadius * projectileRadius;
            float dragFactor = 0.5f * Mathf.Max(0f, airDensity) * activeDragCoefficient * frontalArea;

            if (dragFactor > 0f)
            {
                float dragAccelerationMagnitude = (dragFactor * speed * speed) / activeProjectileMassKilograms;
                totalAcceleration += -(velocity / speed) * dragAccelerationMagnitude;
            }
        }

        if (totalAcceleration.sqrMagnitude > 0f)
            rbRef.AddForce(totalAcceleration, ForceMode.Acceleration);
    }

    private void LateUpdate()
    {
        if (!alignRotationToVelocity || !hasLaunched || hasImpacted)
            return;

        Rigidbody rbRef = rb;
        if (!rbRef)
            return;

        Vector3 velocity = rbRef.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < Mathf.Max(0f, minimumSpeedForRotationAlignment))
            return;

        Quaternion lookRotation = Quaternion.LookRotation(velocity / speed, Vector3.up);
        transform.rotation = lookRotation * launchRotationOffset;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rbRef = rb;

        // Ignore collisions before launch and any repeated impacts.
        if (!hasLaunched || hasImpacted) return;
        if (ShouldIgnoreCombatRootCollider(collision))
            return;

        Collider hitCollider = collision.collider;
        if (ShouldUseRicochetPhysics(hitCollider))
            return;

        Vector3 impactVelocity = rbRef ? rbRef.linearVelocity : Vector3.zero;
        Rigidbody hitRigidbody = collision.rigidbody;
        Vector3 impactPoint = transform.position;
        Vector3 impactNormal = impactVelocity.sqrMagnitude > MinLookDirectionSqr
            ? -impactVelocity.normalized
            : -transform.forward;

        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            impactPoint = contact.point;
            impactNormal = contact.normal;
        }

        ResolveImpact(hitCollider, hitRigidbody, impactPoint, impactNormal, impactVelocity, rbRef);
    }

    private bool ShouldIgnoreCombatRootCollider(Collision collision)
    {
        if (collision == null)
            return false;

        return ShouldIgnoreCombatRootCollider(collision.collider);
    }

    private bool ShouldIgnoreCombatRootCollider(Collider hitCollider)
    {
        if (!hitCollider)
            return false;

        if (PlayerCombat.IsRootCombatCollider(hitCollider) || NPCCombat.IsRootCombatCollider(hitCollider))
        {
            IgnoreProjectileCollisionWith(hitCollider);

            return true;
        }

        return false;
    }

    private bool TryResolvePredictedImpact(Rigidbody rbRef, Vector3 velocity, float speed)
    {
        if (!rbRef || speed <= 0f)
            return false;

        float sweepDistance = speed * Time.fixedDeltaTime;
        if (sweepDistance <= MinImpactSweepDistance)
            return false;

        Vector3 direction = velocity / speed;
        Vector3 origin = rbRef.position;
        float sweepRadius = Mathf.Max(0f, activeProjectileDiameterMeters * 0.5f);
        int hitCount = sweepRadius > 0f
            ? Physics.SphereCastNonAlloc(origin, sweepRadius, direction, PredictedImpactHits, sweepDistance, ~0, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, direction, PredictedImpactHits, sweepDistance, ~0, QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
            return false;

        int selectedHitIndex = -1;
        float nearestHitDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = PredictedImpactHits[i];
            Collider hitCollider = hit.collider;
            if (!IsValidPredictedImpactHit(hitCollider))
                continue;

            float hitDistance = Mathf.Max(0f, hit.distance);
            if (hitDistance >= nearestHitDistance)
                continue;

            selectedHitIndex = i;
            nearestHitDistance = hitDistance;
        }

        if (selectedHitIndex < 0)
            return false;

        RaycastHit selectedHit = PredictedImpactHits[selectedHitIndex];
        if (ShouldUseRicochetPhysics(selectedHit.collider))
        {
            pendingRicochetCollider = selectedHit.collider;
            return false;
        }

        Vector3 impactNormal = selectedHit.normal.sqrMagnitude > MinLookDirectionSqr
            ? selectedHit.normal
            : -direction;
        Vector3 projectileStopPosition = origin + direction * Mathf.Max(0f, selectedHit.distance);

        transform.position = projectileStopPosition;
        rbRef.position = projectileStopPosition;

        ResolveImpact(selectedHit.collider, selectedHit.rigidbody, selectedHit.point, impactNormal, velocity, rbRef);
        return true;
    }

    private bool IsValidPredictedImpactHit(Collider hitCollider)
    {
        if (!hitCollider)
            return false;

        if (IsProjectileCollider(hitCollider))
            return false;

        if (ignoredExternalColliders.Contains(hitCollider))
            return false;

        if (ShouldIgnoreCombatRootCollider(hitCollider))
            return false;

        return true;
    }

    private bool ShouldUseRicochetPhysics(Collider hitCollider)
    {
        if (!hitCollider)
            return false;

        if (IsCharacterTargetCollider(hitCollider))
            return false;

        if (activeAllowsRicochet)
            return true;

        if (pendingRicochetCollider == hitCollider)
        {
            pendingRicochetCollider = null;
            return true;
        }

        float ricochetChance = Mathf.Clamp01(nonRicochetRoundRicochetChance);
        return ricochetChance > 0f && UnityEngine.Random.value < ricochetChance;
    }

    private bool IsCharacterTargetCollider(Collider hitCollider)
    {
        if (!hitCollider)
            return false;

        return hitCollider.GetComponentInParent<PlayerState>() ||
               hitCollider.GetComponentInParent<NPCState>();
    }

    private bool IsProjectileCollider(Collider candidate)
    {
        if (!candidate)
            return false;

        RefreshProjectileColliders();
        for (int i = 0; i < projectileColliders.Count; i++)
        {
            if (projectileColliders[i] == candidate)
                return true;
        }

        return false;
    }

    private void ResolveLaunchBallistics(AmmoDefinition ammoDefinition)
    {
        Rigidbody rbRef = rb;
        if (!rbRef)
            return;

        float resolvedMassKilograms = Mathf.Max(MinMassKilograms, rbRef.mass);
        bool resolvedQuadraticDrag = simulateQuadraticDrag;
        float resolvedDragCoefficient = Mathf.Max(0f, dragCoefficient);
        float resolvedProjectileDiameterMeters = ResolveProjectileDiameterMeters();
        float resolvedGravityScale = overridePrefabGravity ? Mathf.Max(0f, gravityScale) : (defaultUseGravity ? 1f : 0f);
        float resolvedImpactMomentumTransfer = Mathf.Clamp01(impactMomentumTransfer);
        float resolvedImpactImpulseScale = Mathf.Max(0f, impactImpulseScale);

        bool hasAmmoBallisticsOverrides = ammoDefinition != null && ammoDefinition.HasProjectileBallisticsOverrides();

        // Non-gravity projectile prefabs (for example energy rounds) default to no drag unless ammo explicitly overrides it.
        if (!hasAmmoBallisticsOverrides && !overridePrefabGravity && defaultUseGravity == false)
            resolvedQuadraticDrag = false;

        if (hasAmmoBallisticsOverrides)
        {
            float ammoMassKilograms = Mathf.Max(0f, ammoDefinition.GetProjectileMassKilograms());
            if (ammoMassKilograms > 0f)
                resolvedMassKilograms = ammoMassKilograms;

            float ammoDiameterMillimeters = Mathf.Max(0f, ammoDefinition.GetProjectileDiameterMillimeters());
            if (ammoDiameterMillimeters > 0f)
                resolvedProjectileDiameterMeters = ammoDiameterMillimeters * MillimetersToMeters;

            resolvedDragCoefficient = Mathf.Max(0f, ammoDefinition.GetDragCoefficient());
            resolvedGravityScale = Mathf.Max(0f, ammoDefinition.GetGravityScale());
            resolvedImpactMomentumTransfer = Mathf.Clamp01(ammoDefinition.GetImpactMomentumTransfer());
            resolvedImpactImpulseScale = Mathf.Max(0f, ammoDefinition.GetImpactImpulseScale());
            resolvedQuadraticDrag = ammoDefinition.UseQuadraticDrag();
        }

        if (resolvedProjectileDiameterMeters <= 0f)
            resolvedProjectileDiameterMeters = FallbackProjectileDiameterMeters;

        float prefabGravityScale = defaultUseGravity ? 1f : 0f;
        useCustomGravityAcceleration = !Mathf.Approximately(resolvedGravityScale, prefabGravityScale);
        useQuadraticDragForLaunch = resolvedQuadraticDrag && resolvedDragCoefficient > 0f;
        activeGravityScale = resolvedGravityScale;
        activeDragCoefficient = resolvedDragCoefficient;
        activeProjectileDiameterMeters = resolvedProjectileDiameterMeters;
        activeProjectileMassKilograms = Mathf.Max(MinMassKilograms, resolvedMassKilograms);
        activeImpactMomentumTransfer = resolvedImpactMomentumTransfer;
        activeImpactImpulseScale = resolvedImpactImpulseScale;
        activeAllowsRicochet = ammoDefinition != null && ammoDefinition.AllowsRicochet();

        rbRef.mass = activeProjectileMassKilograms;
        rbRef.useGravity = useCustomGravityAcceleration ? false : defaultUseGravity;
        rbRef.linearDamping = useQuadraticDragForLaunch ? 0f : defaultLinearDamping;
        rbRef.angularDamping = defaultAngularDamping;
    }

    private float ResolveProjectileDiameterMeters()
    {
        if (projectileDiameterMeters > 0f)
            return projectileDiameterMeters;

        float estimatedDiameter = EstimateProjectileDiameterFromCollider();
        if (estimatedDiameter > 0f)
            return estimatedDiameter;

        return FallbackProjectileDiameterMeters;
    }

    private float EstimateProjectileDiameterFromCollider()
    {
        Collider colliderRef = bulletCollider;
        if (!colliderRef)
            return 0f;

        Bounds bounds = colliderRef.bounds;
        Vector3 size = bounds.size;
        float minimumAxisSize = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        return Mathf.Max(0f, minimumAxisSize);
    }


    private void ResolveImpact(
        Collider hitCollider,
        Rigidbody hitRigidbody,
        Vector3 impactPoint,
        Vector3 impactNormal,
        Vector3 impactVelocity,
        Rigidbody projectileRigidbody)
    {
        if (hasImpacted)
            return;

        hasImpacted = true;

        ApplyImpactImpulse(hitRigidbody, impactPoint, impactVelocity, projectileRigidbody);
        ApplyDirectHitDamage(hitCollider);

        // Stop further collision processing on this projectile.
        if (bulletCollider)
            bulletCollider.enabled = false;

        if (projectileRigidbody)
        {
            projectileRigidbody.linearVelocity = Vector3.zero;
            projectileRigidbody.angularVelocity = Vector3.zero;
            projectileRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        SpawnImpactFx(impactPoint, impactNormal);

        // Clean up quickly after impact.
        ScheduleCleanup(destroyAfterImpactSeconds);
    }

    private void ApplyImpactImpulse(Rigidbody hitRigidbody, Vector3 impactPoint, Vector3 impactVelocity, Rigidbody projectileRigidbody)
    {
        if (!applyImpactImpulseToHitRigidbody)
            return;

        if (!hitRigidbody || hitRigidbody.isKinematic)
            return;

        float speed = impactVelocity.magnitude;
        if (speed <= 0f)
            return;

        float projectileMass = projectileRigidbody ? Mathf.Max(0f, projectileRigidbody.mass) : 0f;
        if (projectileMass <= 0f)
            return;

        float impulseMagnitude = projectileMass * speed * Mathf.Clamp01(activeImpactMomentumTransfer) * Mathf.Max(0f, activeImpactImpulseScale);
        if (impulseMagnitude <= 0f)
            return;

        if (maxImpactImpulse > 0f)
            impulseMagnitude = Mathf.Min(impulseMagnitude, maxImpactImpulse);

        Vector3 impulseDirection = impactVelocity.normalized;
        Vector3 impulse = impulseDirection * impulseMagnitude;
        hitRigidbody.AddForceAtPosition(impulse, impactPoint, ForceMode.Impulse);
    }

    private void ApplyDirectHitDamage(Collider hitCollider)
    {
        float rawDamage = Mathf.Max(0f, directHitDamage);
        if (rawDamage <= 0f)
            return;

        if (!hitCollider)
            return;

        if (PlayerCombat.TryApplyProjectileDamage(hitCollider, rawDamage, instigatorRoot, respectTargetDamageResistance, damageExperienceSkill))
            return;

        NPCCombat.TryApplyProjectileDamage(hitCollider, rawDamage, instigatorRoot, respectTargetDamageResistance);
    }

    private void IgnoreInstigatorCollisions()
    {
        if (!instigatorRoot)
            return;

        RefreshProjectileColliders();
        if (projectileColliders.Count == 0)
            return;

        instigatorColliders.Clear();
        instigatorRoot.GetComponentsInChildren<Collider>(true, instigatorColliders);
        if (instigatorColliders.Count == 0)
            return;

        for (int i = 0; i < projectileColliders.Count; i++)
        {
            Collider projectileCollider = projectileColliders[i];
            if (!projectileCollider)
                continue;

            for (int j = 0; j < instigatorColliders.Count; j++)
            {
                Collider instigatorCollider = instigatorColliders[j];
                if (!instigatorCollider || instigatorCollider == projectileCollider)
                    continue;

                Physics.IgnoreCollision(projectileCollider, instigatorCollider, true);
                TrackIgnoredExternalCollider(instigatorCollider);
            }
        }
    }

    private void RefreshProjectileColliders()
    {
        projectileColliders.Clear();
        GetComponentsInChildren<Collider>(true, projectileColliders);
    }

    private void IgnoreProjectileCollisionWith(Collider ignoredCollider)
    {
        if (!ignoredCollider)
            return;

        RefreshProjectileColliders();
        for (int i = 0; i < projectileColliders.Count; i++)
        {
            Collider projectileCollider = projectileColliders[i];
            if (!projectileCollider)
                continue;

            Physics.IgnoreCollision(projectileCollider, ignoredCollider, true);
        }

        TrackIgnoredExternalCollider(ignoredCollider);
    }

    private void TrackIgnoredExternalCollider(Collider ignoredCollider)
    {
        if (!ignoredCollider || ignoredExternalColliders.Contains(ignoredCollider))
            return;

        ignoredExternalColliders.Add(ignoredCollider);
    }

    private void RestoreIgnoredCollisions()
    {
        if (ignoredExternalColliders.Count == 0)
            return;

        RefreshProjectileColliders();
        for (int i = 0; i < projectileColliders.Count; i++)
        {
            Collider projectileCollider = projectileColliders[i];
            if (!projectileCollider)
                continue;

            for (int j = 0; j < ignoredExternalColliders.Count; j++)
            {
                Collider ignoredCollider = ignoredExternalColliders[j];
                if (ignoredCollider)
                    Physics.IgnoreCollision(projectileCollider, ignoredCollider, false);
            }
        }

        ignoredExternalColliders.Clear();
    }

    internal static GameObject SpawnProjectile(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!prefab)
            return null;

        Bullet prefabBullet = prefab.GetComponent<Bullet>();
        if (!prefabBullet)
            prefabBullet = prefab.GetComponentInChildren<Bullet>(true);

        if (!prefabBullet)
            return Instantiate(prefab, position, rotation);

        int prefabId = GetObjectKey(prefab);
        if (ProjectilePoolByPrefabId.TryGetValue(prefabId, out Queue<GameObject> pool))
        {
            while (pool.Count > 0)
            {
                GameObject pooled = pool.Dequeue();
                if (!pooled)
                    continue;

                PrepareSpawnedProjectile(pooled, prefabId, position, rotation);
                return pooled;
            }
        }

        GameObject created = Instantiate(prefab, position, rotation);
        PrepareSpawnedProjectile(created, prefabId, position, rotation);
        return created;
    }

    private static void PrepareSpawnedProjectile(GameObject projectile, int prefabId, Vector3 position, Quaternion rotation)
    {
        projectile.transform.SetPositionAndRotation(position, rotation);

        Bullet bullet = projectile.GetComponent<Bullet>();
        if (!bullet)
            bullet = projectile.GetComponentInChildren<Bullet>(true);

        if (bullet)
            bullet.PrepareForPool(prefabId, projectile);

        if (!projectile.activeSelf)
            projectile.SetActive(true);
    }

    private void PrepareForPool(int prefabId, GameObject projectileRoot)
    {
        sourcePrefabId = prefabId;
        canReturnToPool = prefabId != 0;
        pooledProjectileRoot = projectileRoot ? projectileRoot : gameObject;
    }

    private void ScheduleCleanup(float delaySeconds)
    {
        CancelInvoke(nameof(CleanupProjectile));
        if (delaySeconds <= 0f)
        {
            CleanupProjectile();
            return;
        }

        Invoke(nameof(CleanupProjectile), delaySeconds);
    }

    private void CleanupProjectile()
    {
        CancelInvoke(nameof(CleanupProjectile));
        RestoreIgnoredCollisions();

        GameObject projectileRoot = pooledProjectileRoot ? pooledProjectileRoot : gameObject;
        if (!canReturnToPool || sourcePrefabId == 0)
        {
            Destroy(projectileRoot);
            return;
        }

        if (!ProjectilePoolByPrefabId.TryGetValue(sourcePrefabId, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            ProjectilePoolByPrefabId.Add(sourcePrefabId, pool);
        }

        if (pool.Count >= MaxPooledProjectilesPerPrefab)
        {
            Destroy(projectileRoot);
            return;
        }

        projectileRoot.SetActive(false);
        pool.Enqueue(projectileRoot);
    }


    private void SpawnImpactFx(Vector3 impactPoint, Vector3 impactNormal)
    {
        GameObject fxPrefab = impactFxPrefab;
        if (!fxPrefab) return;

        Vector3 normal = impactNormal.sqrMagnitude > MinLookDirectionSqr ? impactNormal.normalized : Vector3.up;
        Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
        GameObject fx = GetImpactFx(fxPrefab, impactPoint, rotation);

        if (impactFxLifetime > 0f)
            ArmImpactFxReturn(fx, GetObjectKey(fxPrefab), impactFxLifetime);
    }

    private static GameObject GetImpactFx(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int prefabId = GetObjectKey(prefab);
        if (ImpactFxPoolByPrefabId.TryGetValue(prefabId, out Queue<GameObject> pool))
        {
            while (pool.Count > 0)
            {
                GameObject pooled = pool.Dequeue();
                if (!pooled)
                    continue;

                pooled.transform.SetPositionAndRotation(position, rotation);
                pooled.SetActive(true);
                RestartParticleSystems(pooled);
                return pooled;
            }
        }

        GameObject created = Instantiate(prefab, position, rotation);
        created.AddComponent<PooledImpactFxReturn>();
        return created;
    }

    private static void ArmImpactFxReturn(GameObject fx, int prefabId, float lifetime)
    {
        if (!fx)
            return;

        PooledImpactFxReturn poolReturn = fx.GetComponent<PooledImpactFxReturn>();
        if (!poolReturn)
            poolReturn = fx.AddComponent<PooledImpactFxReturn>();

        poolReturn.Arm(prefabId, lifetime);
    }

    internal static void ReturnImpactFx(int prefabId, GameObject fx)
    {
        if (!fx)
            return;

        fx.SetActive(false);

        if (!ImpactFxPoolByPrefabId.TryGetValue(prefabId, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            ImpactFxPoolByPrefabId.Add(prefabId, pool);
        }

        if (pool.Count >= MaxPooledImpactFxPerPrefab)
        {
            Destroy(fx);
            return;
        }

        pool.Enqueue(fx);
    }

    private static void RestartParticleSystems(GameObject root)
    {
        ParticleSystem particleSystem = root.GetComponent<ParticleSystem>();
        if (particleSystem)
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private static int GetObjectKey(UnityEngine.Object target)
    {
        return target ? target.GetEntityId().GetHashCode() : 0;
    }
}

internal sealed class PooledImpactFxReturn : MonoBehaviour
{
    private int prefabId;

    public void Arm(int sourcePrefabId, float lifetime)
    {
        prefabId = sourcePrefabId;
        CancelInvoke();
        if (lifetime > 0f)
            Invoke(nameof(ReturnToPool), lifetime);
    }

    private void ReturnToPool()
    {
        Bullet.ReturnImpactFx(prefabId, gameObject);
    }
}
