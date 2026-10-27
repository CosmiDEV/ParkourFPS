using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Correr")]
    [Tooltip("Velocidad máxima en el suelo (metros por segundo).")]
    public float maxSpeed = 12f;
    [Tooltip("Qué tan rápido aceleras en el suelo.")]
    public float groundAcceleration = 70f;
    [Tooltip("Qué tan rápido frenas en el suelo al soltar las teclas.")]
    public float groundDeceleration = 60f;
    [Tooltip("Frenado en el suelo cuando vas MÁS rápido que maxSpeed (por ejemplo al salir de un slide). Bajo = conservas el impulso.")]
    public float overspeedDeceleration = 20f;
    [Tooltip("Control en el aire: cuánto puedes empujarte hacia donde apuntas.")]
    public float airAcceleration = 25f;
    [Tooltip("Frenado en el aire cuando no presionas nada (bajo = conservas el impulso).")]
    public float airDeceleration = 3f;

    [Header("Salto")]
    [Tooltip("Altura del salto en metros.")]
    public float jumpHeight = 2.2f;
    [Tooltip("Gravedad (valor positivo).")]
    public float gravity = 25f;
    [Tooltip("Multiplicador de gravedad al caer (más alto = caes más rápido).")]
    public float fallMultiplier = 1.6f;
    [Tooltip("Multiplicador si sueltas el botón mientras subes (salto corto).")]
    public float lowJumpMultiplier = 2f;
    public float maxFallSpeed = 40f;
    [Tooltip("Tiempo extra para saltar justo después de dejar una plataforma.")]
    public float coyoteTime = 0.12f;
    [Tooltip("Si presionas saltar un poco antes de tocar el suelo, el salto se guarda.")]
    public float jumpBufferTime = 0.12f;

    [Header("Doble salto")]
    public int maxAirJumps = 1;
    [Range(0.5f, 1.2f)]
    [Tooltip("Fuerza del salto en el aire respecto al salto normal.")]
    public float airJumpMultiplier = 0.9f;

    [Header("Deslizamiento (Ctrl / C)")]
    [Tooltip("Velocidad mínima para empezar a deslizarte (si vas más lento solo te agachas).")]
    public float slideMinSpeed = 7f;
    [Tooltip("Impulso extra al empezar el slide (m/s).")]
    public float slideBoost = 4f;
    [Tooltip("Velocidad máxima que puedes alcanzar deslizándote.")]
    public float slideMaxSpeed = 22f;
    [Tooltip("Fricción del slide en plano (m/s² que pierdes). Bajo = deslizas más lejos.")]
    public float slideFriction = 6f;
    [Tooltip("Por debajo de esta velocidad el slide termina (y quedas agachado si sigues con Ctrl).")]
    public float slideEndSpeed = 3f;
    [Tooltip("Cuánto puedes girar mientras te deslizas (grados por segundo).")]
    public float slideSteer = 70f;
    [Tooltip("Cuánto aceleras al bajar pendientes (1 = gravedad real).")]
    public float slopeGain = 1f;
    [Tooltip("Multiplicador de velocidad al saltar desde un slide.")]
    public float slideJumpBoost = 1.1f;
    [Tooltip("Tiempo antes de poder usar el impulso de otro slide (evita acumular velocidad infinita).")]
    public float slideCooldown = 0.5f;
    [Tooltip("Si presionas Ctrl un poco antes de poder deslizarte (ej. justo antes de aterrizar), se guarda.")]
    public float slideBufferTime = 0.2f;
    [Tooltip("Duración mínima del slide aunque sueltes Ctrl rápido (evita parpadeos).")]
    public float slideMinTime = 0.25f;
    [Tooltip("Altura de la cápsula al deslizarte o agacharte (el estándar es 1.8).")]
    public float slideHeight = 1.0f;
    [Tooltip("Velocidad al caminar agachado.")]
    public float crouchSpeed = 5f;

    [Header("Paredes")]
    [Tooltip("Qué capas cuentan como pared. Por defecto, todas.")]
    public LayerMask wallMask = ~0;
    [Tooltip("Distancia extra (sobre el radio del jugador) para detectar una pared.")]
    public float wallCheckDistance = 0.5f;
    [Tooltip("Velocidad hacia arriba del salto en pared.")]
    public float wallJumpUpSpeed = 9.5f;
    [Tooltip("Velocidad con la que te empuja lejos de la pared.")]
    public float wallJumpPushSpeed = 9f;
    [Range(0f, 1f)]
    [Tooltip("Cuánta velocidad lateral (a lo largo de la pared) conservas al saltar.")]
    public float wallJumpKeepSpeed = 0.7f;
    [Tooltip("Tiempo mínimo entre dos saltos en pared.")]
    public float wallJumpCooldown = 0.2f;
    [Tooltip("Velocidad máxima de caída mientras estás pegado a una pared.")]
    public float wallSlideSpeed = 3f;
    [Tooltip("Si está activo, solo frenas la caída junto a una pared cuando empujas hacia ella (W/A/D hacia la pared). Evita el efecto de 'planear' cerca de paredes y bordes.")]
    public bool wallSlideNeedsInput = true;
    [Tooltip("Si está activo, el salto en pared te devuelve el doble salto.")]
    public bool wallJumpResetsAirJumps = true;
    [Tooltip("Tiempo mínimo en el aire antes de poder pegarte a una pared (evita falsos positivos por parpadeos del suelo).")]
    public float wallMinAirTime = 0.12f;
    [Tooltip("Altura mínima sobre el suelo (bajo tus pies) para poder pegarte a una pared.")]
    public float wallMinHeight = 0.6f;

    [Header("Suelo")]
    [Tooltip("Margen (m) bajo los pies para confirmar que pisas suelo real. Sube si se siente que 'no detecta' el suelo en bordes.")]
    public float groundProbeDistance = 0.2f;

    [Header("Agua / Nadar")]
    [Tooltip("Velocidad al nadar (m/s).")]
    public float swimSpeed = 14f;
    [Tooltip("Velocidad al subir (Espacio) o bajar (Ctrl / C) en el agua.")]
    public float swimVerticalSpeed = 4.5f;
    [Tooltip("Qué tan rápido aceleras nadando.")]
    public float swimAcceleration = 14f;
    [Tooltip("Qué tan rápido frenas en el agua al soltar las teclas.")]
    public float swimDrag = 8f;
    [Tooltip("Velocidad a la que te hundes lentamente si no haces nada (0 = flotas en el sitio).")]
    public float idleSinkSpeed = 0.4f;
    [Tooltip("Cuánto debe estar el centro del cuerpo bajo la superficie para empezar a nadar (m).")]
    public float swimEnterDepth = 0.1f;
    [Tooltip("Profundidad del centro del cuerpo cuando flotas en la superficie (m). Con 0.45 la cabeza queda fuera del agua.")]
    public float surfaceFloatDepth = 0.45f;
    [Tooltip("Margen sobre la superficie dentro del cual Espacio te hace saltar fuera del agua (m).")]
    public float surfaceExitMargin = 0.35f;
    [Tooltip("Velocidad hacia arriba al saltar fuera del agua (para subir a un borde).")]
    public float waterExitJumpSpeed = 11.5f;
    [Tooltip("Velocidad hacia adelante mínima al saltar fuera del agua si estás empujando una dirección.")]
    public float waterExitPush = 6f;
    [Tooltip("Tiempo tras saltar fuera del agua en el que no puedes volver a entrar a nadar (evita 'pegarte' al agua).")]
    public float waterExitLockTime = 0.3f;
    [Tooltip("Cuánta velocidad de caída conservas al caer al agua (0 = frenas de golpe, 1 = nada).")]
    [Range(0f, 1f)] public float splashDamping = 0.35f;

    // Estado interno
    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    bool grounded;
    bool wasGrounded;
    Vector3 groundNormal = Vector3.up;
    float coyoteCounter;
    float jumpBufferCounter;
    float wallJumpTimer;
    float airTime;
    int airJumpsUsed;

    // Entrada
    Vector2 moveInput;
    bool jumpPressed;
    bool jumpHeld;
    bool slidePressed;
    bool slideHeld;

    // Paredes
    string dbgWallInfo = "";
    bool onWall;
    bool wallPushing;
    Vector3 wallNormal;
    float wallSide;

    // Deslizamiento / agachado
    bool sliding;
    bool crouched;
    float slideTimer;
    float slideCooldownTimer;
    float slideBufferCounter;
    float standHeight;
    Vector3 standCenter;
    readonly Collider[] overlapBuffer = new Collider[8];
    const float SlideAirGrace = 0.1f;

    // Agua
    bool swimming;
    float waterSurfaceY;
    float swimExitTimer;
    Transform cameraTransform;

    // Datos para otros scripts
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    public bool IsGrounded => grounded;
    public bool IsOnWallAirborne => onWall && !grounded;
    public bool IsSliding => sliding;
    public bool IsCrouched => crouched;
    public bool IsSwimming => swimming;
    public float WallSide => wallSide;
    public Vector2 MoveInput => moveInput;
    public int AirJumpsLeft => Mathf.Max(0, maxAirJumps - airJumpsUsed);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        standHeight = controller.height;
        standCenter = controller.center;

        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null) cameraTransform = cam.transform;
    }

    // Ciclo principal
    void Update()
    {
        float dt = Time.deltaTime;

        ReadInput();

        // Suelo
        Vector3 gn = Vector3.up;
        grounded = controller.isGrounded && verticalVelocity <= 0.5f && HasGroundBelow(out gn);
        groundNormal = grounded ? gn : Vector3.up;
        airTime = grounded ? 0f : airTime + dt;

        // Debug
        if (grounded && !wasGrounded)
        {
            string under = Physics.Raycast(transform.position, Vector3.down, out RaycastHit dbg, 3f, ~0,
                                           QueryTriggerInteraction.Ignore) ? dbg.collider.name : "NADA";
            Debug.Log($"[DEBUG Suelo] y={transform.position.y:F2} bajo='{under}' airTime previo={airTime:F2}");
        }

        // Agua
        UpdateWaterState(dt);
        if (swimming)
        {
            SwimUpdate(dt);
            wasGrounded = grounded;
            return;
        }

        // Estado del suelo
        if (grounded)
        {
            coyoteCounter = coyoteTime;
            airJumpsUsed = 0;
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;

                Vector3 flat = new Vector3(groundNormal.x, 0f, groundNormal.z);
                if (flat.sqrMagnitude > 0.0001f && Vector3.Dot(horizontalVelocity, flat) > 0f)
                {
                    float downhillSpeed = Vector3.Project(horizontalVelocity, flat.normalized).magnitude;
                    float tan = flat.magnitude / Mathf.Max(0.1f, groundNormal.y);
                    verticalVelocity = -(2f + downhillSpeed * tan);
                }
            }
        }
        else
        {
            coyoteCounter -= dt;
        }

        // Deslizamiento
        UpdateSlideState(dt);

        // Paredes
        DetectWall();

        // Saltos
        HandleJumps(dt);

        // Movimiento horizontal
        HandleHorizontal(dt);

        // Gravedad
        HandleGravity(dt);

        // Mover
        Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(motion * dt);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        wasGrounded = grounded;
    }

    // Agua y natación
    void UpdateWaterState(float dt)
    {
        swimExitTimer -= dt;
        bool wasSwimming = swimming;
        swimming = false;
        if (swimExitTimer > 0f) return;

        Vector3 center = transform.position + controller.center;
        if (WaterVolume.TryGetWater(center, out float surfaceY))
        {
            waterSurfaceY = surfaceY;
            swimming = surfaceY - center.y > swimEnterDepth;
        }

        if (swimming && !wasSwimming) EnterWater();
    }

    void EnterWater()
    {
        EndSlide();
        SetCrouched(false);
        onWall = false;
        wallPushing = false;
        airJumpsUsed = 0;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;

        verticalVelocity *= splashDamping;
        horizontalVelocity *= 0.7f;
    }

    void SwimUpdate(float dt)
    {
        Vector3 center = transform.position + controller.center;
        float depth = waterSurfaceY - center.y;

        Vector3 fwd = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 wish = fwd * moveInput.y + transform.right * moveInput.x;
        if (wish.sqrMagnitude > 1f) wish.Normalize();

        float vertical = 0f;
        if (jumpHeld) vertical += 1f;
        if (slideHeld) vertical -= 1f;

        Vector3 target = wish * swimSpeed + Vector3.up * (vertical * swimVerticalSpeed);
        bool hasInput = moveInput != Vector2.zero || vertical != 0f;
        if (!hasInput) target.y = -idleSinkSpeed;
        target = Vector3.ClampMagnitude(target, Mathf.Max(swimSpeed, swimVerticalSpeed));

        Vector3 v = horizontalVelocity + Vector3.up * verticalVelocity;
        v = Vector3.MoveTowards(v, target, (hasInput ? swimAcceleration : swimDrag) * dt);

        if (depth <= surfaceFloatDepth && v.y > 0f) v.y = 0f;

        // Salir del agua
        if (jumpPressed && depth <= surfaceFloatDepth + surfaceExitMargin)
        {
            Vector3 flat = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (flat.sqrMagnitude > 0.01f)
            {
                float sp = Mathf.Max(new Vector3(v.x, 0f, v.z).magnitude, waterExitPush);
                Vector3 f = flat.normalized * sp;
                v.x = f.x;
                v.z = f.z;
            }
            v.y = waterExitJumpSpeed;
            swimExitTimer = waterExitLockTime;
            airJumpsUsed = 0;
            coyoteCounter = 0f;
            jumpBufferCounter = 0f;
        }

        horizontalVelocity = new Vector3(v.x, 0f, v.z);
        verticalVelocity = v.y;

        CollisionFlags flags = controller.Move(v * dt);
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    // Detección de suelo
    bool HasGroundBelow(out Vector3 normal)
    {
        normal = Vector3.up;
        Vector3 c = transform.position + controller.center;
        float dist = controller.height * 0.5f + controller.skinWidth + groundProbeDistance;
        float r = controller.radius * 0.6f;

        for (int i = 0; i < 5; i++)
        {
            Vector3 offset = Vector3.zero;
            if (i == 1) offset = transform.right * r;
            else if (i == 2) offset = -transform.right * r;
            else if (i == 3) offset = transform.forward * r;
            else if (i == 4) offset = -transform.forward * r;

            if (Physics.Raycast(c + offset, Vector3.down, out RaycastHit hit, dist,
                                ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.5f)
            {
                normal = hit.normal;
                return true;
            }
        }
        return false;
    }

    // Entrada
    void ReadInput()
    {
        moveInput = Vector2.zero;
        jumpPressed = false;
        jumpHeld = false;
        slidePressed = false;
        slideHeld = false;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.isPressed) moveInput.y += 1f;
        if (kb.sKey.isPressed) moveInput.y -= 1f;
        if (kb.dKey.isPressed) moveInput.x += 1f;
        if (kb.aKey.isPressed) moveInput.x -= 1f;
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        jumpPressed = kb.spaceKey.wasPressedThisFrame;
        jumpHeld = kb.spaceKey.isPressed;

        slidePressed = kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
        slideHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
    }

    // Deslizamiento y agachado
    void UpdateSlideState(float dt)
    {
        slideCooldownTimer -= dt;
        slideBufferCounter -= dt;
        if (slidePressed) slideBufferCounter = slideBufferTime;

        float speed = horizontalVelocity.magnitude;
        bool landed = grounded && !wasGrounded;

        if (sliding)
        {
            slideTimer += dt;
            bool released = !slideHeld && slideTimer >= slideMinTime;
            bool tooSlow = speed < slideEndSpeed;
            bool airborneTooLong = airTime > SlideAirGrace;
            if (released || tooSlow || airborneTooLong) EndSlide();
        }
        else if (grounded && slideHeld)
        {
            bool wantsSlide = slideBufferCounter > 0f || landed;
            if (wantsSlide && speed >= slideMinSpeed && slideCooldownTimer <= 0f)
                StartSlide();
            else
                SetCrouched(true);
        }

        if (!sliding && crouched && (!slideHeld || !grounded) && CanStand())
            SetCrouched(false);
    }

    void StartSlide()
    {
        sliding = true;
        slideTimer = 0f;
        slideBufferCounter = 0f;
        SetCrouched(true);

        Vector3 dir = horizontalVelocity.sqrMagnitude > 0.01f ? horizontalVelocity.normalized : transform.forward;
        float cur = horizontalVelocity.magnitude;
        float boosted = cur >= slideMaxSpeed ? cur : Mathf.Min(cur + slideBoost, slideMaxSpeed);
        horizontalVelocity = dir * boosted;
    }

    void EndSlide()
    {
        if (!sliding) return;
        sliding = false;
        slideCooldownTimer = slideCooldown;
    }

    void SlideMove(float dt, Vector3 wish, bool hasInput)
    {
        if (!grounded) return;

        Vector3 slopeAccel = Vector3.ProjectOnPlane(Vector3.down * gravity, groundNormal);
        slopeAccel.y = 0f;
        horizontalVelocity += slopeAccel * slopeGain * dt;

        float sp = Mathf.Max(0f, horizontalVelocity.magnitude - slideFriction * dt);

        Vector3 dir = horizontalVelocity.sqrMagnitude > 0.0001f ? horizontalVelocity.normalized : transform.forward;
        if (hasInput)
            dir = Vector3.RotateTowards(dir, wish.normalized, slideSteer * Mathf.Deg2Rad * dt, 0f);

        horizontalVelocity = dir * Mathf.Min(sp, slideMaxSpeed);
    }

    void SetCrouched(bool value)
    {
        if (crouched == value) return;
        crouched = value;

        float h = value ? slideHeight : standHeight;
        controller.height = h;
        controller.center = standCenter + Vector3.down * ((standHeight - h) * 0.5f);
    }

    bool CanStand()
    {
        if (!crouched) return true;

        float r = controller.radius * 0.9f;
        float standTop = standCenter.y + standHeight * 0.5f;
        float crouchTop = standCenter.y - (standHeight - slideHeight) * 0.5f + slideHeight * 0.5f;

        float upperY = standTop - r;
        float lowerY = Mathf.Min(upperY, crouchTop + r);

        Vector3 a = transform.position + Vector3.up * upperY;
        Vector3 b = transform.position + Vector3.up * lowerY;

        int n = Physics.OverlapCapsuleNonAlloc(a, b, r, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (overlapBuffer[i] != controller) return false;
        return true;
    }

    // Paredes
    void DetectWall()
    {
        onWall = false;
        wallPushing = false;
        wallSide = 0f;
        if (grounded) return;

        if (airTime < wallMinAirTime) return;
        float probe = controller.height * 0.5f + wallMinHeight;
        if (Physics.Raycast(transform.position, Vector3.down, probe, wallMask, QueryTriggerInteraction.Ignore))
            return;

        float dist = controller.radius + wallCheckDistance;
        float best = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = GetDirection(i);
            if (RaycastIgnoreSelf(transform.position, dir, dist, wallMask, out RaycastHit hit))
            {
                if (Mathf.Abs(hit.normal.y) > 0.3f) continue;

                if (hit.distance < best)
                {
                    dbgWallInfo = $"collider='{hit.collider.name}' (padre='{(hit.collider.transform.parent != null ? hit.collider.transform.parent.name : "-")}') dist={hit.distance:F2} punto={hit.point} normal={hit.normal} dir={i}";
                    best = hit.distance;
                    wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                    onWall = true;
                }
            }
        }

        if (onWall)
        {
            float d = Vector3.Dot(-wallNormal, transform.right);
            wallSide = Mathf.Abs(d) > 0.3f ? Mathf.Sign(d) : 0f;

            Vector3 wish = transform.right * moveInput.x + transform.forward * moveInput.y;
            wallPushing = wish.sqrMagnitude > 0.001f && Vector3.Dot(wish.normalized, -wallNormal) > 0.3f;
        }
    }

    readonly RaycastHit[] rayBuffer = new RaycastHit[8];
    bool RaycastIgnoreSelf(Vector3 origin, Vector3 dir, float dist, int mask, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;
        int n = Physics.RaycastNonAlloc(origin, dir, rayBuffer, dist, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayBuffer[i];
            if (h.collider == controller || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
        }
        return found;
    }

    Vector3 GetDirection(int i)
    {
        switch (i)
        {
            case 0: return transform.right;
            case 1: return -transform.right;
            case 2: return transform.forward;
            default: return -transform.forward;
        }
    }

    // Saltos
    void HandleJumps(float dt)
    {
        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= dt;

        wallJumpTimer -= dt;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f && CanStand())
        {
            // Salto normal
            Debug.Log($"[DEBUG Salto] 1-SUELO/COYOTE  grounded={grounded} coyote={coyoteCounter:F3} airJumpsUsed={airJumpsUsed} y={transform.position.y:F2}");
            verticalVelocity = JumpVelocity();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;

            if (sliding)
            {
                float sp = horizontalVelocity.magnitude;
                if (sp > 0.1f)
                    horizontalVelocity = horizontalVelocity.normalized *
                        Mathf.Min(sp * slideJumpBoost, Mathf.Max(sp, slideMaxSpeed));
                EndSlide();
            }
            SetCrouched(false);
        }
        else if (jumpBufferCounter > 0f && onWall && wallJumpTimer <= 0f)
        {
            // Salto en pared
            Debug.Log($"[DEBUG Salto] 2-PARED  airJumpsUsed={airJumpsUsed} pos={transform.position} -> {dbgWallInfo}");
            Vector3 along = Vector3.ProjectOnPlane(horizontalVelocity, wallNormal);
            horizontalVelocity = along * wallJumpKeepSpeed + wallNormal * wallJumpPushSpeed;
            verticalVelocity = wallJumpUpSpeed;
            wallJumpTimer = wallJumpCooldown;
            jumpBufferCounter = 0f;
            onWall = false;
            if (wallJumpResetsAirJumps) airJumpsUsed = 0;
        }
        else if (jumpPressed && !grounded && airJumpsUsed < maxAirJumps)
        {
            // Doble salto
            Debug.Log($"[DEBUG Salto] 3-AIRE (doble)  usados antes={airJumpsUsed} y={transform.position.y:F2}");
            verticalVelocity = JumpVelocity() * airJumpMultiplier;
            airJumpsUsed++;
            jumpBufferCounter = 0f;
        }
    }

    float JumpVelocity()
    {
        return Mathf.Sqrt(2f * gravity * jumpHeight);
    }

    // Movimiento horizontal
    void HandleHorizontal(float dt)
    {
        Vector3 wish = transform.right * moveInput.x + transform.forward * moveInput.y;
        bool hasInput = wish.sqrMagnitude > 0.001f;

        if (sliding)
        {
            SlideMove(dt, wish, hasInput);
            return;
        }

        if (grounded)
        {
            float topSpeed = crouched ? crouchSpeed : maxSpeed;
            Vector3 target = wish * topSpeed;
            float accel = hasInput ? groundAcceleration : groundDeceleration;

            if (hasInput && horizontalVelocity.magnitude > topSpeed * 1.02f)
                accel = overspeedDeceleration;

            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, accel * dt);
        }
        else
        {
            if (hasInput)
            {
                Vector3 wishDir = wish.normalized;
                float current = Vector3.Dot(horizontalVelocity, wishDir);
                float add = Mathf.Min(airAcceleration * dt, Mathf.Max(0f, maxSpeed - current));
                horizontalVelocity += wishDir * add;
            }
            else
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, airDeceleration * dt);
            }
        }
    }

    // Gravedad
    void HandleGravity(float dt)
    {
        float g = gravity;
        if (verticalVelocity < 0f) g *= fallMultiplier;
        else if (verticalVelocity > 0f && !jumpHeld) g *= lowJumpMultiplier;

        verticalVelocity -= g * dt;
        verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);

        if (onWall && !grounded && (wallPushing || !wallSlideNeedsInput) && verticalVelocity < -wallSlideSpeed)
            verticalVelocity = -wallSlideSpeed;
    }

    // Colisiones
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Mathf.Abs(hit.normal.y) < 0.3f)
        {
            Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            float into = Vector3.Dot(horizontalVelocity, n);
            if (into < 0f) horizontalVelocity -= n * into;
        }
    }

    // Reinicio de estado
    public void ResetState()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        airJumpsUsed = 0;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        wallJumpTimer = 0f;
        airTime = 0f;

        sliding = false;
        slideCooldownTimer = 0f;
        slideBufferCounter = 0f;
        SetCrouched(false);
        wasGrounded = false;
        groundNormal = Vector3.up;

        swimming = false;
        swimExitTimer = 0f;
    }
}
