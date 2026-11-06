using System.Collections.Generic;
using UnityEngine;

namespace MMO.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PuppetMotor : MonoBehaviour
    {
        private enum FollowUp { None, Primary, Dodge }
        private const float RecoveryMovementTime = 0.14f;
        private const float DodgeCancelWindow = 0.18f;

        public float walkSpeed = 3.5f, runSpeed = 6.2f, jumpHeight = 1.1f, gravity = -22f, acceleration = 24f;
        public int maxHealth = 100;
        public TargetLock targeting;
        public TrainingTarget LockedTarget => targeting != null ? targeting.ForPlayer(transform) : null;
        private CharacterController controller;
        private PuppetRig rig;
        private KayKitCharacterVisual character;
        public MageCombat Mage { get; private set; }
        public RangerCombat Ranger { get; private set; }
        public PoisonStatus Poison { get; private set; }
        private bool UsesRangedCombat => Mage != null || Ranger != null;
        private bool RangedAction => Mage?.IsCasting == true || Ranger?.IsShooting == true;
        private float RangedRecovery => Mage?.IsCasting == true ? Mage.ActionTimeRemaining : Ranger?.ActionTimeRemaining ?? 0f;
        private float PrimaryCooldown => Mage?.PrimaryCooldown ?? 0f;
        private string RangedStatus => Mage?.Status ?? Ranger?.Status;
        private WarriorKit kit;
        private WarriorAttack attack;
        private Vector3 horizontalVelocity, wishDirection, dodgeDirection, dodgeFacing, attackForward, spawnPosition;
        private AnimationClip dodgeClip;
        private Quaternion spawnRotation;
        private float verticalVelocity, attackTime = -1f, dodgeTime = -1f, nextDodge, lastSpend;
        private float chargeStart = -1f, reservedCharge, guardStart = -10f, nextParry, riposteUntil = -1f;
        private float stunnedUntil, reactionUntil, deathTime, comboUntil, bufferedUntil, noticeUntil;
        private bool impactApplied, plungeLanded, plungeUsed, parryConsumed, externalInput, externalSprint, sprinting;
        private Vector2 externalMovement;
        private int comboStage;
        private string notice;
        private FollowUp bufferedAction;
        private bool aimedStrike;
        private readonly Dictionary<WarriorMove, float> cooldowns = new Dictionary<WarriorMove, float>();
        private readonly HashSet<TrainingTarget> hitTargets = new HashSet<TrainingTarget>();
        public bool IsAttacking => attackTime >= 0f;
        public bool IsCharging => chargeStart >= 0f;
        public bool IsDodging => dodgeTime >= 0f;
        public bool IsGuarding { get; private set; }
        public bool IsStunned => Time.time < stunnedUntil;
        public bool Grounded => controller != null && controller.isGrounded;
        public bool RiposteReady => Time.time < riposteUntil;
        public int Health { get; private set; }
        public float Stamina { get; private set; }
        public float MaxStamina => kit.data.balance.stamina.maximum;
        public float Charge01 => !IsCharging ? 0f : Mathf.Min(Mathf.Clamp01((Time.time - chargeStart) / kit.Get("charged_strike").charge_max_s),
            Mathf.Clamp01(Stamina / (kit.Get("charged_strike").stamina_max - reservedCharge)));
        public WarriorMove CurrentMove => IsAttacking ? attack.move : WarriorMove.None;
        public float AttackProgress => IsAttacking ? attackTime / attack.duration : -1f;
        public float DodgeProgress => IsDodging ? dodgeTime / kit.Get("dodge").duration_s : -1f;
        public float Speed => controller == null ? 0f : Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude;
        public int LandedHits { get; private set; }
        public bool CanSwitchClass => Health > 0 && Grounded && !IsAttacking && !IsCharging && !IsDodging && !IsStunned && !RangedAction;
        public string Action => Health <= 0 ? "AU SOL · R pour reprendre" : Time.time < noticeUntil ? notice :
            IsStunned ? "DÉSÉQUILIBRÉ" : IsCharging ? "CHARGE " + Mathf.RoundToInt(Charge01 * 100f) + "%" :
            IsAttacking ? attack.name.ToUpperInvariant() : IsDodging ? "ESQUIVE" : IsGuarding ? "GARDE" :
            RiposteReady ? "RIPOSTE DISPONIBLE · clic gauche" : !string.IsNullOrEmpty(RangedStatus) ? RangedStatus :
            !Grounded ? "EN L'AIR" : Speed > 0.1f ? "EN MOUVEMENT" : "PRÊT";

        private void Awake()
        {
            ImpactAudio.Prepare();
            kit = WarriorKit.Instance;
            controller = GetComponent<CharacterController>();
            rig = GetComponentInChildren<PuppetRig>();
            character = GetComponentInChildren<KayKitCharacterVisual>();
            Mage = GetComponent<MageCombat>();
            Ranger = GetComponent<RangerCombat>();
            Poison = GetComponent<PoisonStatus>();
            spawnPosition = transform.position; spawnRotation = transform.rotation;
            RestoreResources();
        }

        public void SetExternalInput(Vector2 movement, bool sprint = false)
        { externalInput = true; externalMovement = Vector2.ClampMagnitude(movement, 1f); externalSprint = sprint; }
        public void ReleaseExternalInput() { externalInput = false; externalMovement = Vector2.zero; }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector2 movement = externalInput ? externalMovement : ReadMovement();
            sprinting = movement.sqrMagnitude > 0.01f && (externalInput ? externalSprint : Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            var view = Camera.main != null ? Camera.main.transform : transform;
            wishDirection = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized * movement.y +
                Vector3.ProjectOnPlane(view.right, Vector3.up).normalized * movement.x, 1f);
            if (!externalInput)
            {
                if (Input.GetKeyDown(KeyCode.R)) ResetSandbox();
                if (Input.GetKeyDown(KeyCode.T)) SparringOpponent.ToggleAll();
                if (UsesRangedCombat)
                {
                    if (Input.GetKeyDown(KeyCode.E)) RequestDodge();
                    Mage?.ReadInput();
                    Ranger?.ReadInput();
                }
                else
                {
                    RequestGuard(Input.GetKey(KeyCode.F));
                    if (Input.GetKeyDown(KeyCode.E)) RequestDodge();
                    if (Input.GetKeyDown(KeyCode.C)) BeginHeavyCharge();
                    if (Input.GetKeyUp(KeyCode.C)) ReleaseHeavyCharge();
                    if (Input.GetMouseButtonDown(0)) RequestAttack();
                    if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) RequestSkill(WarriorMove.Thrust);
                    if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) RequestSkill(WarriorMove.Sweep);
                    if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) RequestSkill(WarriorMove.GuardBreaker);
                    if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) RequestSkill(WarriorMove.Retreat);
                }
                if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            }
            if (Health <= 0 && Time.time > deathTime + 3f) ResetSandbox();
            if (Time.time - lastSpend >= kit.data.balance.stamina.regeneration_delay_after_spending_s &&
                !IsAttacking && !IsCharging && !IsGuarding && !IsDodging && Health > 0)
                Stamina = Mathf.Min(MaxStamina, Stamina + kit.data.balance.stamina.regeneration_per_second * dt);

            bool rangedAction = RangedAction;
            Vector3 lockFacing = DirectionToLock();
            if (IsDodging)
                TurnToward(dodgeFacing, 26f, 960f, dt);
            else if (!IsStunned && Health > 0)
            {
                if (Mage?.IsCasting == true && Mage.HasCastAim)
                    TurnToward(Mage.CastFacing, 22f, 960f, dt);
                else if (Ranger?.HasShotAim == true)
                    TurnToward(Ranger.ShotFacing, 22f, 960f, dt);
                else if (IsAttacking && aimedStrike)
                    TurnToward(attackForward, 22f, 960f, dt);
                else if (!IsAttacking && !rangedAction && lockFacing.sqrMagnitude > 0.01f)
                    TurnToward(lockFacing, 18f, IsCharging ? 420f : 720f, dt);
                else if (!IsAttacking && !IsGuarding && !rangedAction && wishDirection.sqrMagnitude > 0.01f)
                    TurnToward(wishDirection, 18f, IsCharging ? 420f : 720f, dt);
            }

            float rangedExit = rangedAction ? RecoveryBlend(RangedRecovery) : 1f;
            float movementScale = IsGuarding ? kit.Get("guard").movement_speed_multiplier :
                rangedAction ? Mathf.Lerp(Ranger?.MovementScale ?? 0.25f, 1f, rangedExit) : IsCharging ? 0.25f : 1f;
            float moveSpeed = sprinting && !IsGuarding && !IsCharging ? Mathf.Lerp(walkSpeed, runSpeed, rangedExit) : walkSpeed;
            Vector3 desired = wishDirection * moveSpeed * movementScale;
            Vector3 actionVelocity = Vector3.zero;
            if (Health <= 0 || IsStunned) horizontalVelocity = Vector3.zero;
            else if (IsDodging)
            {
                var dodge = kit.Get("dodge");
                actionVelocity = dodgeDirection * TravelSpeed(dodgeTime, dodge.duration_s, dodge.speed_m_s * dodge.duration_s, dt);
                ResumeMovement(desired, dodge.duration_s - dodgeTime, dt);
            }
            else if (IsAttacking)
            {
                if (attack.move == WarriorMove.Lunge && attackTime < attack.travelDuration)
                    actionVelocity = attackForward * TravelSpeed(attackTime, attack.travelDuration, attack.travel, dt);
                if (attack.move == WarriorMove.Retreat && attackTime >= attack.impact && attackTime < attack.impact + attack.travelDuration)
                    actionVelocity = attackForward * TravelSpeed(attackTime - attack.impact, attack.travelDuration, attack.travel, dt);
                if (Grounded && impactApplied && actionVelocity == Vector3.zero)
                    ResumeMovement(desired, attack.duration - attackTime, dt);
            }
            else horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desired, acceleration * dt);

            bool wasGrounded = Grounded;
            if (Grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            controller.Move((horizontalVelocity + actionVelocity + Vector3.up * verticalVelocity) * dt);
            if (IsDodging)
            {
                dodgeTime += dt;
                if (dodgeTime >= kit.Get("dodge").duration_s) dodgeTime = -1f;
            }
            if (!wasGrounded && Grounded) plungeUsed = false;
            if (transform.position.y < -8f) { ResetPosition(); RestoreResources(); Mage?.ResetSpells(); Ranger?.ResetShots(); }

            if (IsAttacking)
            {
                if (attack.move == WarriorMove.Plunge && !plungeLanded)
                {
                    if (Grounded) { plungeLanded = true; attackTime = 0f; ApplyAttack(); }
                }
                else
                {
                    float previousTime = attackTime;
                    attackTime += dt;
                    if (attack.move == WarriorMove.Lunge)
                    {
                        if (attackTime >= attack.impact && previousTime < attack.travelDuration) ApplyAttack();
                    }
                    else if (attack.move == WarriorMove.Sweep)
                    {
                        // The active sector moves with the visible blade instead of hitting the whole fan at once.
                        if (attackTime >= attack.impact - 0.1f && previousTime <= attack.impact + 0.1f) ApplyAttack(previousTime);
                    }
                    else if (!impactApplied && attackTime >= attack.impact) ApplyAttack();
                    if (attackTime >= attack.duration) FinishAttack();
                }
            }
            Mage?.Tick(dt);
            Ranger?.Tick(dt);
            PlayBufferedAction();
            if (character != null)
            {
                Vector3 gaitVelocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
                if (actionVelocity != Vector3.zero)
                    gaitVelocity = Vector3.ClampMagnitude(horizontalVelocity, gaitVelocity.magnitude);
                float recoveryGait = IsDodging ? RecoveryBlend(kit.Get("dodge").duration_s - dodgeTime) :
                    IsAttacking ? RecoveryBlend(attack.duration - attackTime) : 0f;
                character.Present(gaitVelocity.magnitude / runSpeed, Grounded, verticalVelocity, CurrentMove, Mathf.Max(0f, attackTime), attack.impact,
                    attack.duration, IsCharging ? Charge01 : -1f, IsGuarding, DodgeProgress, Time.time < reactionUntil, Health <= 0, dt,
                    Mage?.CurrentClip ?? Ranger?.CurrentClip, Mage?.AnimationProgress ?? Ranger?.AnimationProgress ?? 0f,
                    Ranger?.AnimationKey ?? (int)(Mage?.CurrentSpell ?? MageSpell.None),
                    lockFacing == Vector3.zero ? Vector3.zero : transform.InverseTransformDirection(gaitVelocity), dodgeClip, recoveryGait);
            }
            else if (rig != null)
                rig.PoseWarrior(Speed / runSpeed, Grounded, verticalVelocity, CurrentMove, Mathf.Max(0f, attackTime), attack.impact,
                    attack.duration, IsCharging ? Charge01 : -1f, IsGuarding, DodgeProgress, Time.time < reactionUntil, dt, controller.velocity);
        }

        private static float RecoveryBlend(float remaining) =>
            Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(remaining / RecoveryMovementTime));

        private void ResumeMovement(Vector3 desired, float remaining, float dt)
        {
            float recoveryStep = Mathf.Clamp(RecoveryMovementTime - remaining + dt, 0f, dt);
            Vector3 target = bufferedAction == FollowUp.None ? desired : Vector3.zero;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, acceleration * recoveryStep);
        }

        private static float TravelSpeed(float time, float duration, float distance, float dt)
        {
            float from = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
            float to = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time + dt) / duration));
            return distance * (to - from) / dt;
        }

        private Vector3 DirectionToLock()
        {
            var target = LockedTarget;
            return target != null ? Vector3.ProjectOnPlane(target.AimPoint - transform.position, Vector3.up).normalized : Vector3.zero;
        }

        private void TurnToward(Vector3 direction, float response, float maxDegreesPerSecond, float dt)
        {
            Quaternion rotation = transform.rotation;
            Quaternion eased = Quaternion.Slerp(rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-response * dt));
            transform.rotation = Quaternion.RotateTowards(rotation, eased, maxDegreesPerSecond * dt);
        }

        private static Vector2 ReadMovement()
        {
            return Vector2.ClampMagnitude(new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)), 1f);
        }

        internal bool Spend(float cost)
        {
            if (Stamina + 0.001f < cost) { Notify("ENDURANCE INSUFFISANTE"); return false; }
            Stamina = Mathf.Max(0f, Stamina - cost);
            if (cost > 0f) lastSpend = Time.time;
            return true;
        }
        internal void Notify(string text) { notice = text; noticeUntil = Time.time + 0.8f; }
        public float CooldownRemaining(WarriorMove move) => cooldowns.TryGetValue(move, out float until) ? Mathf.Max(0f, until - Time.time) : 0f;
        private bool CanStart => Health > 0 && !IsStunned && !IsDodging && !IsCharging;
        private bool CanCancelRecovery => IsAttacking && (attack.move != WarriorMove.Plunge || plungeLanded) &&
            impactApplied && attackTime >= attack.duration - DodgeCancelWindow;

        private bool BufferAction(FollowUp action, float remaining)
        {
            float window = kit.data.global_rules.input_buffer_s;
            if (remaining > window) return false;
            bufferedAction = action;
            bufferedUntil = Time.time + window;
            return true;
        }

        private void PlayBufferedAction()
        {
            if (bufferedAction == FollowUp.None) return;
            if (Time.time > bufferedUntil || Health <= 0 || IsStunned)
            {
                bufferedAction = FollowUp.None;
                return;
            }
            if (IsDodging || RangedAction || (IsAttacking && (bufferedAction != FollowUp.Dodge || !CanCancelRecovery))) return;
            if (bufferedAction == FollowUp.Primary && PrimaryCooldown > 0f) return;
            FollowUp action = bufferedAction;
            bufferedAction = FollowUp.None;
            if (action == FollowUp.Dodge) RequestDodge();
            else RequestAttack();
        }

        public bool RequestJump()
        {
            if (!Grounded || IsAttacking || IsDodging || IsCharging || IsStunned || Health <= 0 || RangedAction) return false;
            bufferedAction = FollowUp.None;
            IsGuarding = false; comboStage = 0; plungeUsed = false;
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            return true;
        }

        public bool RequestAttack()
        {
            if (IsDodging) return BufferAction(FollowUp.Primary, Mathf.Max(kit.Get("dodge").duration_s - dodgeTime, PrimaryCooldown));
            if (RangedAction) return BufferAction(FollowUp.Primary, Mathf.Max(RangedRecovery, PrimaryCooldown));
            if (Mage != null)
            {
                if (CanStart && Grounded && PrimaryCooldown > 0f && BufferAction(FollowUp.Primary, PrimaryCooldown)) return true;
                return Mage.RequestPrimary();
            }
            if (Ranger != null) return Ranger.RequestShot(RangerShot.Quick);
            if (!CanStart) return false;
            if (IsAttacking)
                return attack.move != WarriorMove.Plunge && BufferAction(FollowUp.Primary, attack.duration - attackTime);
            if (Grounded && RiposteReady) return RequestSkill(WarriorMove.Riposte);
            if (!Grounded) return RequestSkill(WarriorMove.Plunge);
            if (sprinting && Mathf.Min(horizontalVelocity.magnitude, Speed) > 0.5f &&
                CooldownRemaining(WarriorMove.Lunge) <= 0f && Stamina >= kit.Attack(WarriorMove.Lunge).cost)
                return RequestSkill(WarriorMove.Lunge);
            if (Time.time > comboUntil) comboStage = 0;
            return StartAttack(kit.Attack((WarriorMove)((int)WarriorMove.Light1 + comboStage)));
        }

        public bool RequestSkill(WarriorMove move)
        {
            if (UsesRangedCombat) return false;
            if (move == WarriorMove.None || move == WarriorMove.Heavy || move <= WarriorMove.Light3) return false;
            if (!CanStart || IsAttacking) return false;
            if (move == WarriorMove.Plunge ? Grounded || plungeUsed : !Grounded) return false;
            if (move == WarriorMove.Riposte && !RiposteReady) return false;
            if (CooldownRemaining(move) > 0f) { Notify("COMPÉTENCE EN RECHARGE"); return false; }
            return StartAttack(kit.Attack(move));
        }

        private bool StartAttack(WarriorAttack next, bool prepaid = false)
        {
            if (!prepaid && !Spend(next.cost)) return false;
            bufferedAction = FollowUp.None;
            attack = next; attackTime = 0f; impactApplied = false; hitTargets.Clear();
            Vector3 lockFacing = DirectionToLock();
            aimedStrike = lockFacing.sqrMagnitude > 0.01f;
            attackForward = aimedStrike ? lockFacing : transform.forward;
            IsGuarding = false; guardStart = -10f;
            horizontalVelocity = Vector3.zero;
            if (!attack.IsLight) comboStage = 0;
            if (attack.move == WarriorMove.Riposte) riposteUntil = -1f;
            if (attack.move == WarriorMove.Plunge) { plungeLanded = false; plungeUsed = true; }
            cooldowns[attack.move] = Time.time + attack.cooldown;
            return true;
        }

        private void FinishAttack()
        {
            if (attack.IsLight)
            {
                comboStage = attack.move == WarriorMove.Light3 ? 0 : (int)attack.move - (int)WarriorMove.Light1 + 1;
                comboUntil = Time.time + kit.data.global_rules.combo_continue_window_after_recovery_s;
            }
            attackTime = -1f;
        }

        public bool BeginHeavyCharge()
        {
            if (UsesRangedCombat) return false;
            if (!CanStart || IsAttacking || !Grounded) return false;
            reservedCharge = kit.Get("charged_strike").stamina_min;
            if (!Spend(reservedCharge)) return false;
            IsGuarding = false; comboStage = 0; chargeStart = Time.time;
            return true;
        }
        public bool ReleaseHeavyCharge()
        {
            if (!IsCharging) return false;
            float charge = Charge01;
            var next = kit.Attack(WarriorMove.Heavy, charge);
            Spend(Mathf.Max(0f, next.cost - reservedCharge));
            chargeStart = -1f;
            return StartAttack(next, true);
        }

        public bool RequestGuard(bool held)
        {
            if (UsesRangedCombat) return false;
            if (!held) { IsGuarding = false; guardStart = -10f; return true; }
            if (IsGuarding) return true;
            if (!CanStart || !Grounded || (IsAttacking && !CanCancelRecovery) || Stamina <= 0f) return false;
            CancelAttack();
            IsGuarding = true; comboStage = 0;
            parryConsumed = Time.time < nextParry;
            guardStart = Time.time;
            if (!parryConsumed) nextParry = Time.time + kit.Get("perfect_parry").rearm_delay_s;
            return true;
        }

        public bool RequestDodge()
        {
            float cost = kit.Get("dodge").stamina;
            if (!CanStart || !Grounded || Time.time < nextDodge) return false;
            if (RangedAction) return Stamina + 0.001f >= cost && BufferAction(FollowUp.Dodge, RangedRecovery);
            if (IsAttacking && !CanCancelRecovery)
                return impactApplied && Stamina + 0.001f >= cost &&
                    BufferAction(FollowUp.Dodge, attack.duration - DodgeCancelWindow - attackTime);
            if (!Spend(cost)) return false;
            CancelAttack(); IsGuarding = false; comboStage = 0;
            dodgeDirection = wishDirection.sqrMagnitude > 0.01f ? wishDirection.normalized : transform.forward;
            dodgeFacing = dodgeDirection;
            dodgeClip = null;
            Vector3 lockFacing = DirectionToLock();
            if (character != null && lockFacing.sqrMagnitude > 0.01f)
            {
                float yaw = Vector3.SignedAngle(lockFacing, dodgeDirection, Vector3.up);
                dodgeClip = character.SelectDodge(yaw, out float clipYaw);
                // Match the clip's local travel direction to the input, including diagonals.
                dodgeFacing = Quaternion.AngleAxis(-clipYaw, Vector3.up) * dodgeDirection;
            }
            dodgeTime = 0f; nextDodge = Time.time + kit.Get("dodge").cooldown_s;
            return true;
        }

        public HitResult ReceiveHit(CombatHit hit)
        {
            bool wasGuarding = IsGuarding;
            int before = Health;
            HitResult result = ResolveHit(hit);
            CombatImpactVfx.Present(controller, transform.position + Vector3.up, hit, result, result == HitResult.Blocked && !wasGuarding);
            if (Health > 0) hit.damage = before - Health;
            targeting?.orbit?.ReactToImpact(transform, ImpactRecoil.AwayFrom(transform, hit.origin), hit, result);
            return result;
        }

        internal void PresentLandedImpact(CombatHit hit, HitResult result, Vector3 recipient) =>
            targeting?.orbit?.ReactToImpact(transform, recipient - transform.position, hit, result);

        private HitResult ResolveHit(CombatHit hit)
        {
            if (Health <= 0 || hit.damage <= 0) return HitResult.Miss;
            CombatHit incoming = hit;
            if (Mage != null)
            {
                hit.damage = Mage.Absorb(hit.damage);
                if (hit.damage == 0)
                {
                    reactionUntil = Time.time + 0.12f;
                    character?.ReactToImpact(transform, incoming, HitResult.Blocked);
                    return HitResult.Blocked;
                }
            }
            Vector3 toSource = Vector3.ProjectOnPlane(hit.origin - transform.position, Vector3.up);
            bool frontal = toSource.sqrMagnitude < 0.001f || Vector3.Angle(transform.forward, toSource) <= kit.Get("guard").frontal_angle_degrees * 0.5f;
            if (IsGuarding && frontal && hit.parryable)
            {
                var parry = kit.Get("perfect_parry");
                if (!parryConsumed && Time.time - guardStart <= parry.window_s && Stamina >= parry.stamina_per_success)
                {
                    Spend(parry.stamina_per_success); parryConsumed = true; riposteUntil = Time.time + 1f;
                    reactionUntil = Time.time + 0.16f; Notify("PARADE · RIPOSTE !");
                    character?.ReactToImpact(transform, incoming, HitResult.Parried);
                    if (hit.source != null) hit.source.Stagger(0.8f);
                    return HitResult.Parried;
                }
                var guard = kit.Get("guard");
                float cost = guard.stamina_per_block_base + guard.stamina_per_block_incoming_damage_multiplier * hit.damage;
                if (!hit.guardBreaker && Stamina >= cost)
                {
                    Spend(cost);
                    Health = Mathf.Max(0, Health - Mathf.CeilToInt(hit.damage * (1f - guard.damage_reduction)));
                    reactionUntil = Time.time + 0.12f; Notify("COUP BLOQUÉ");
                    character?.ReactToImpact(transform, incoming, HitResult.Blocked);
                    if (Health == 0) KnockDown();
                    return HitResult.Blocked;
                }
                Stamina = 0f; lastSpend = Time.time; stunnedUntil = Time.time + 1f;
                character?.ReactToImpact(transform, incoming, HitResult.GuardBroken);
                TakeDamage(hit.damage); Notify("GARDE BRISÉE");
                return HitResult.GuardBroken;
            }
            stunnedUntil = Time.time + 0.2f;
            character?.ReactToImpact(transform, hit, HitResult.Hit);
            TakeDamage(hit.damage);
            return HitResult.Hit;
        }

        private void TakeDamage(int damage)
        {
            Mage?.Interrupt();
            Ranger?.Interrupt();
            Health = Mathf.Max(0, Health - damage);
            CancelAttack(); chargeStart = -1f; IsGuarding = false; dodgeTime = -1f; comboStage = 0;
            reactionUntil = Time.time + 0.2f;
            if (Health == 0) KnockDown();
        }
        private void KnockDown() { deathTime = Time.time; Poison?.Clear(); CancelAttack(); IsGuarding = false; chargeStart = dodgeTime = -1f; }
        private void CancelAttack() { attackTime = -1f; bufferedAction = FollowUp.None; horizontalVelocity = Vector3.zero; }

        private void ApplyAttack(float previousTime = 0f)
        {
            impactApplied = true;
            if (attack.IsThrust && hitTargets.Count > 0) return;
            Vector3 origin = transform.position + Vector3.up;
            var candidates = new List<Collider>(Physics.OverlapSphere(origin, attack.reach + 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
            candidates.Sort((a, b) => Vector3.Dot(a.bounds.center - origin, attackForward).CompareTo(Vector3.Dot(b.bounds.center - origin, attackForward)));
            foreach (var candidate in candidates)
            {
                var target = candidate.GetComponentInParent<TrainingTarget>();
                if (target == null || target.Health <= 0 || hitTargets.Contains(target)) continue;
                Vector3 delta = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
                Vector3 closest = candidate.ClosestPoint(origin);
                if (Vector3.Distance(closest, origin) > attack.reach) continue;
                if (attack.IsThrust)
                {
                    float z = Vector3.Dot(delta, attackForward);
                    if (z < 0f) continue;
                    Vector3 onLine = origin + attackForward * Mathf.Clamp(z, 0f, attack.reach);
                    if (Vector3.Distance(candidate.ClosestPoint(onLine), onLine) > attack.width * 0.5f) continue;
                }
                else
                {
                    if (Vector3.Angle(attackForward, delta) > attack.angle * 0.5f) continue;
                    if (attack.move == WarriorMove.Sweep)
                    {
                        float from = WarriorAttack.SweepYaw(previousTime - attack.impact, attack.angle);
                        float to = WarriorAttack.SweepYaw(attackTime - attack.impact, attack.angle);
                        float targetAngle = Vector3.SignedAngle(attackForward, delta, Vector3.up);
                        if (targetAngle < Mathf.Min(from, to) - 28f || targetAngle > Mathf.Max(from, to) + 28f) continue;
                    }
                }
                Vector3 destination = target.transform.position + Vector3.up;
                if (Physics.Linecast(origin, destination, out RaycastHit obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    obstruction.collider.GetComponentInParent<TrainingTarget>() != target) continue;
                hitTargets.Add(target);
                var hit = new CombatHit(Mathf.RoundToInt(attack.damage), transform.position, attack.move == WarriorMove.GuardBreaker,
                    bladeTip: character?.BladeTip);
                var result = target.ReceiveHit(hit);
                if (result != HitResult.Miss)
                {
                    LandedHits++;
                    float contact = attack.move == WarriorMove.Sweep ? attackTime : Mathf.Max(attack.impact, previousTime);
                    character?.MarkMeleeContact(attack.move, contact, hit, result);
                    PresentLandedImpact(hit, result, target.transform.position);
                }
                if (attack.IsThrust) break;
            }
        }

        public void RestoreResources()
        { Health = maxHealth; Stamina = MaxStamina; lastSpend = -10f; Poison?.Clear(); }

        internal void TakePoisonDamage(int amount)
        {
            if (Health <= 0) return;
            Health = Mathf.Max(0, Health - amount);
            if (Health > 0) return;
            Mage?.Interrupt();
            Ranger?.Interrupt();
            KnockDown();
        }
        internal int Heal(int amount)
        {
            if (Health <= 0 || amount <= 0) return 0;
            int restored = Mathf.Max(0, Mathf.Min(maxHealth - Health, amount));
            Health += restored;
            return restored;
        }
        public void ResetPosition()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            verticalVelocity = -2f; horizontalVelocity = Vector3.zero;
            CancelAttack(); chargeStart = dodgeTime = -1f; IsGuarding = false;
            nextDodge = nextParry = stunnedUntil = reactionUntil = comboUntil = noticeUntil = 0f;
            character?.ResetImpact();
            riposteUntil = -1f; comboStage = 0; plungeUsed = false; cooldowns.Clear();
        }
        public void ResetSandbox()
        {
            ResetPosition(); RestoreResources(); LandedHits = 0;
            targeting?.Clear();
            Mage?.ResetSpells();
            Ranger?.ResetShots();
            foreach (var ally in FindObjectsByType<PracticeAlly>(FindObjectsSortMode.None)) ally.ResetAlly();
            foreach (var target in FindObjectsByType<TrainingTarget>(FindObjectsSortMode.None)) target.ResetTarget();
            foreach (var opponent in FindObjectsByType<SparringOpponent>(FindObjectsSortMode.None)) opponent.ResetCycle();
            FindFirstObjectByType<TowerFloor>()?.ResetFloor();
            var orbit = FindFirstObjectByType<OrbitCamera>();
            if (orbit != null) orbit.ResetView();
        }

        public void MoveForClassSwitch(Vector3 position, Quaternion facing)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, facing);
            controller.enabled = true;
            CancelAttack();
            verticalVelocity = -2f;
            wishDirection = Vector3.zero;
            chargeStart = dodgeTime = riposteUntil = -1f;
            IsGuarding = false;
            comboStage = 0;
        }
    }
}

