using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MMO.Prototype
{
    // The combat controller owns timing and displacement. The imported clips only pose the model.
    public sealed class KayKitCharacterVisual : MonoBehaviour
    {
        public Animator animator;
        public Transform motionRoot;
        public AnimationClip idle, walk, run, guard, guardHit, hit, dodge, jumpStart, jumpAir, jumpLand, death;
        public AnimationClip strafeLeft, strafeRight, backstep;
        public AnimationClip dodgeLeft, dodgeRight, dodgeBack;
        public AnimationClip[] attacks;
        public float[] contactFrames;
        public TrailRenderer swordTrail;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer, gait, lowerGait;
        private AnimationLayerMixerPlayable outputLayers;
        private AvatarMask lowerBody;
        private AnimationClip[] gaitClips;
        private readonly AnimationClipPlayable[] gaitPlayers = new AnimationClipPlayable[6];
        private readonly AnimationClipPlayable[] lowerGaitPlayers = new AnimationClipPlayable[6];
        private readonly float[] gaitWeights = new float[6];
        private readonly float[] desiredGait = new float[6];
        private const int LayerCount = 5; // One locomotion input plus up to four overlapping actions.
        private readonly ActionPose[] poses = new ActionPose[LayerCount];
        private readonly float[] weights = new float[LayerCount];
        private readonly float[] startWeights = new float[LayerCount];
        private AnimationClip currentClip;
        private int state = -1, destination;
        private float stateAge, blendAge, blendDuration, previousFraction;
        private float gaitPhase, idleTime, gaitSpeed, airborneTime, landingTime, landingDuration;
        private float rootHeightWeight = 1f;
        private Transform hips, chest;
        private Vector3 hipsBase;
        private Quaternion chestBase;
        private ImpactRecoil recoil;
        private bool impactPoseApplied, frontalImpact;
        private float impactUntil;
        private WarriorMove contactMove;
        private float contactTime, contactPause;

        private struct ActionPose
        {
            public AnimationClipPlayable Player;
            public float Sample, Rate, Length, GaitCarry;
            public bool Loop;
        }

        public string CurrentClip => currentClip != null ? currentClip.name : "";
        internal Transform BladeTip => swordTrail != null ? swordTrail.transform : null;

        private void Awake()
        {
            hips = motionRoot != null ? motionRoot.Find("hips") : null;
            chest = hips != null ? hips.Find("spine/chest") : null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create(name + " motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, LayerCount);
            gaitClips = new[] { idle, walk, run, strafeLeft, strafeRight, backstep };
            gait = CreateGaitMixer(gaitPlayers);
            graph.Connect(gait, 0, mixer, 0);

            lowerBody = CreateLowerBodyMask();
            Playable output = mixer;
            if (lowerBody != null)
            {
                // Animation graphs require one parent per stream. Keep the lower-body gait in sync on its own branch.
                lowerGait = CreateGaitMixer(lowerGaitPlayers);
                outputLayers = AnimationLayerMixerPlayable.Create(graph, 2);
                graph.Connect(mixer, 0, outputLayers, 0);
                graph.Connect(lowerGait, 0, outputLayers, 1);
                outputLayers.SetInputWeight(0, 1f);
                outputLayers.SetLayerMaskFromAvatarMask(1, lowerBody);
                output = outputLayers;
            }

            ResetPose();
            AnimationPlayableOutput.Create(graph, "Character", animator).SetSourcePlayable(output);
            graph.Play();
        }

        private AnimationMixerPlayable CreateGaitMixer(AnimationClipPlayable[] players)
        {
            var blend = AnimationMixerPlayable.Create(graph, players.Length);
            for (int i = 0; i < gaitClips.Length; i++)
            {
                if (gaitClips[i] == null) continue;
                players[i] = AnimationClipPlayable.Create(graph, gaitClips[i]);
                players[i].SetSpeed(0d);
                graph.Connect(players[i], 0, blend, i);
            }
            return blend;
        }

        private void OnDisable()
        {
            ResetImpact();
            if (graph.IsValid()) ResetPose();
            if (swordTrail == null) return;
            swordTrail.emitting = false;
            swordTrail.Clear();
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (lowerBody != null) Destroy(lowerBody);
        }

        public AnimationClip SelectDodge(float yaw, out float clipYaw)
        {
            clipYaw = 0f;
            float angle = Mathf.Abs(yaw);
            if (angle >= 135f && dodgeBack != null)
            {
                clipYaw = 180f;
                return dodgeBack;
            }
            if (angle >= 45f && angle < 135f)
            {
                AnimationClip side = yaw > 0f ? dodgeRight : dodgeLeft;
                if (side != null)
                {
                    clipYaw = Mathf.Sign(yaw) * 90f;
                    return side;
                }
            }
            return dodge;
        }

        internal void ReactToImpact(Transform body, CombatHit hit, HitResult result)
        {
            Vector3 away = ImpactRecoil.AwayFrom(body, hit.origin);
            recoil.Kick(away, ImpactRecoil.Strength(hit, result));
            frontalImpact = result != HitResult.Blocked && Vector3.Dot(body.forward, -away) > 0.65f;
            impactUntil = Time.time + 0.23f;
        }

        internal void MarkMeleeContact(WarriorMove move, float time, CombatHit hit, HitResult result)
        {
            if (result == HitResult.Miss || contactMove == move) return;
            contactMove = move;
            contactTime = time;
            contactPause = Mathf.Lerp(0.020f, 0.063f, ImpactRecoil.Strength(hit, result));
        }

        internal void ResetImpact()
        {
            RestoreImpactPose();
            recoil = default;
            impactUntil = 0f;
            contactMove = WarriorMove.None;
        }

        private void RestoreImpactPose()
        {
            if (!impactPoseApplied) return;
            hips.localPosition = hipsBase;
            chest.localRotation = chestBase;
            impactPoseApplied = false;
        }

        private void ApplyImpactPose(float dt, bool down)
        {
            Vector3 offset = recoil.Advance(dt);
            if (down || hips == null || chest == null || offset == Vector3.zero) return;
            float amount = offset.magnitude;
            Vector3 axis = Vector3.Cross(Vector3.up, offset / amount);
            hipsBase = hips.localPosition;
            chestBase = chest.localRotation;
            hips.localPosition += hips.parent.InverseTransformDirection(offset) * 0.075f - Vector3.up * (amount * 0.043f);
            chest.localRotation = Quaternion.AngleAxis(amount * 23f, chest.parent.InverseTransformDirection(axis)) * chestBase;
            impactPoseApplied = true;
        }

        public void Present(float speed, bool grounded, float vertical, WarriorMove move, float time, float impact, float duration,
            float charge, bool guarding, float dodgeProgress, bool reacting, bool down, float dt,
            AnimationClip casting = null, float castProgress = 0f, int castKey = 0, Vector3 localLockMovement = default,
            AnimationClip dodgeClip = null, float recoveryGait = 0f)
        {
            if (!graph.IsValid()) return;
            RestoreImpactPose();
            if (move != contactMove || time < contactTime) contactMove = WarriorMove.None;
            AnimationClip gaitClip = UpdateGait(speed, localLockMovement, dt);
            if (!grounded) airborneTime += dt;
            else
            {
                if (airborneTime >= 0.075f)
                    landingTime = landingDuration = Mathf.Lerp(0.20f, 0.14f, Mathf.Clamp01(speed));
                airborneTime = 0f;
            }
            landingTime = Mathf.Max(0f, landingTime - dt);

            AnimationClip clip;
            int next;
            float fraction = -1f;
            float transition = 0.14f;
            bool loop = false;
            float rate = 1f;

            if (down) { clip = death; next = 20; transition = 0.11f; }
            else if (dodgeProgress >= 0f)
            {
                clip = dodgeClip ?? dodge;
                next = 21;
                fraction = dodgeProgress;
                transition = 0.085f;
            }
            else if (casting != null)
            {
                clip = casting;
                next = 40 + castKey;
                fraction = castProgress;
                transition = 0.13f;
            }
            else if (move != WarriorMove.None)
            {
                int index = (int)move;
                clip = attacks[index];
                next = index;
                float contact = contactFrames[index];
                transition = Mathf.Clamp(impact * 0.52f, 0.065f, 0.145f);
                float poseTime = time;
                if (move == contactMove)
                {
                    float remaining = duration - contactTime;
                    float hold = Mathf.Min(contactPause, remaining * 0.35f);
                    if (remaining > hold)
                        poseTime = contactTime + Mathf.Clamp01((time - contactTime - hold) / (remaining - hold)) * remaining;
                }
                if (move == WarriorMove.Plunge)
                    fraction = grounded
                        ? Mathf.Lerp(contact, 1f, Mathf.Clamp01(poseTime / duration))
                        : Mathf.Lerp(0.16f, contact, Mathf.InverseLerp(1f, -6.3f, vertical));
                else
                    fraction = AttackFraction(move, poseTime, impact, duration, contact);
            }
            else if (charge >= 0f)
            {
                clip = attacks[(int)WarriorMove.Heavy];
                next = 22;
                fraction = Mathf.Lerp(0.16f, contactFrames[(int)WarriorMove.Heavy] * 0.72f, charge);
                transition = 0.15f;
            }
            else if (reacting && (guarding || Time.time >= impactUntil || frontalImpact))
            {
                clip = guarding ? guardHit : hit;
                next = guarding ? 23 : 24;
                transition = 0.065f;
                rate = 2f;
            }
            else if (guarding) { clip = guard; next = 25; loop = true; }
            else if (!grounded)
            {
                bool ascending = vertical > 2f;
                clip = ascending ? jumpStart : jumpAir;
                next = ascending ? 26 : 27;
                transition = ascending ? 0.10f : 0.14f;
                loop = !ascending;
            }
            else if (landingTime > 0f)
            {
                clip = jumpLand;
                next = 28;
                transition = 0.09f;
                fraction = 1f - landingTime / landingDuration;
            }
            else
            {
                clip = null;
                next = -1;
                transition = state == 28 || state == 21 ? 0.15f : 0.18f;
            }

            // A queued cast can restart the same clip without passing through locomotion.
            bool changed = state != next || (casting != null && fraction < previousFraction);
            if (changed)
            {
                BeginBlend(clip, loop, transition);
                state = next;
            }
            previousFraction = fraction;

            stateAge += dt;
            if (destination != 0)
            {
                ref ActionPose pose = ref poses[destination];
                pose.GaitCarry = grounded ? (casting != null || guarding ? 1f : recoveryGait) : 0f;
                float sample = fraction >= 0f ? Mathf.Clamp01(fraction) * pose.Length : stateAge * rate;
                if (!pose.Loop) sample = Mathf.Min(sample, pose.Length);
                pose.Rate = fraction < 0f ? rate : changed || dt <= 0f ? 0f :
                    Mathf.Clamp((sample - pose.Sample) / dt, -3f, 3f);
                pose.Sample = sample;
            }
            currentClip = clip != null ? clip : gaitClip;
            EvaluateBlend(dt);
            ApplyImpactPose(dt, down);

            // The source dodge includes a root translation. The capsule already moved.
            if (motionRoot != null)
            {
                rootHeightWeight = Mathf.Lerp(rootHeightWeight, grounded && dodgeProgress < 0f ? 1f : 0f, 1f - Mathf.Exp(-26f * dt));
                motionRoot.localPosition = new Vector3(0f, motionRoot.localPosition.y * rootHeightWeight, 0f);
            }
            if (swordTrail != null)
                swordTrail.emitting = move != WarriorMove.None && (move != WarriorMove.Plunge || grounded) &&
                    time >= Mathf.Max(0f, impact - 0.07f) && time <= impact + (move == WarriorMove.Lunge ? 0.30f : 0.10f);
        }

        private AnimationClip UpdateGait(float speed, Vector3 direction, float dt)
        {
            float ease = 1f - Mathf.Exp(-17f * dt);
            gaitSpeed = Mathf.Lerp(gaitSpeed, speed, ease);
            float moving = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.018f, 0.19f, gaitSpeed));
            float running = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.94f, gaitSpeed));
            float left = strafeLeft != null ? Mathf.Max(0f, -direction.x) : 0f;
            float right = strafeRight != null ? Mathf.Max(0f, direction.x) : 0f;
            float backward = backstep != null ? Mathf.Max(0f, -direction.z) : 0f;
            float forward = Mathf.Max(0f, direction.z);
            float total = left + right + backward + forward;
            if (total < 0.02f) total = forward = 1f;
            float share = moving / total;
            desiredGait[0] = 1f - moving;
            desiredGait[1] = forward * share * (1f - running);
            desiredGait[2] = forward * share * running;
            desiredGait[3] = left * share;
            desiredGait[4] = right * share;
            desiredGait[5] = backward * share;

            float walkFrequency = Mathf.Clamp(gaitSpeed / 0.56f, 0.35f, 1.45f) / walk.length;
            float runFrequency = Mathf.Clamp(gaitSpeed, 0.55f, 1.25f) / run.length;
            gaitPhase = Mathf.Repeat(gaitPhase + Mathf.Lerp(walkFrequency, runFrequency, running) * moving * dt, 1f);
            idleTime = Mathf.Repeat(idleTime + dt, idle.length);
            int dominant = 0;
            for (int i = 0; i < gaitPlayers.Length; i++)
            {
                if (!gaitPlayers[i].IsValid()) continue;
                gaitWeights[i] = Mathf.Lerp(gaitWeights[i], desiredGait[i], ease);
                if (gaitWeights[i] > gaitWeights[dominant]) dominant = i;
                gait.SetInputWeight(i, gaitWeights[i]);
                if (lowerGait.IsValid()) lowerGait.SetInputWeight(i, gaitWeights[i]);
                if (gaitWeights[i] > 0.0001f)
                {
                    float sample = i == 0 ? idleTime : gaitPhase * gaitClips[i].length;
                    gaitPlayers[i].SetTime(sample);
                    if (lowerGaitPlayers[i].IsValid()) lowerGaitPlayers[i].SetTime(sample);
                }
            }
            return gaitClips[dominant];
        }

        private static float AttackFraction(WarriorMove move, float time, float impact, float duration, float contact)
        {
            // Keep the lunge extended for its moving hit window.
            if (move == WarriorMove.Lunge && time > impact)
            {
                float recoveryStart = Mathf.Max(impact, duration - 0.3f);
                if (time <= recoveryStart) return contact;
                return Mathf.Lerp(contact, 1f, Mathf.InverseLerp(recoveryStart, duration, time));
            }
            return SampleThroughContact(time, impact, duration, contact);
        }

        internal static float SampleThroughContact(float time, float impact, float duration, float contact)
        {
            float recovery = duration - impact;
            if (impact <= 0.001f || recovery <= 0.001f) return Mathf.InverseLerp(0f, duration, time);
            float windupRate = contact / impact;
            float recoveryRate = (1f - contact) / recovery;
            float contactRate = 2f * windupRate * recoveryRate / (windupRate + recoveryRate);
            if (time <= impact)
                return SampleSegment(0f, contact, 0.3f * contact, contactRate * impact, Mathf.Clamp01(time / impact));
            return SampleSegment(contact, 1f, contactRate * recovery, 0.25f * (1f - contact),
                Mathf.Clamp01((time - impact) / recovery));
        }

        private static float SampleSegment(float start, float end, float startRate, float endRate, float t)
        {
            float eased = t * t * (3f - 2f * t);
            float rest = 1f - t;
            return Mathf.Lerp(start, end, eased) + t * rest * (rest * startRate - t * endRate);
        }

        private AvatarMask CreateLowerBodyMask()
        {
            if (motionRoot == null || motionRoot.Find("hips/upperleg.l") == null) return null;
            var mask = new AvatarMask();
            mask.AddTransformPath(animator.transform, true);
            string rootPath = mask.GetTransformPath(0);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                if (rootPath.Length > 0) path = i == 0 ? "" : path.Substring(rootPath.Length + 1);
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, path.EndsWith("/hips", System.StringComparison.Ordinal) || path.Contains("/hips/upperleg."));
            }
            return mask;
        }

        private void BeginBlend(AnimationClip clip, bool loop, float duration)
        {
            destination = 0;
            if (clip != null)
            {
                destination = FindPoseInput();
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0d);
                graph.Connect(playable, 0, mixer, destination);
                poses[destination] = new ActionPose { Player = playable, Loop = loop, Length = clip.length };
            }
            System.Array.Copy(weights, startWeights, LayerCount);
            stateAge = blendAge = 0f;
            blendDuration = duration;
        }

        private int FindPoseInput()
        {
            int weakest = 1;
            for (int i = 1; i < LayerCount; i++)
            {
                if (!poses[i].Player.IsValid()) return i;
                if (weights[i] < weights[weakest]) weakest = i;
            }

            int strongest = 0;
            for (int i = 1; i < LayerCount; i++)
                if (i != weakest && weights[i] > weights[strongest]) strongest = i;
            weights[strongest] += weights[weakest];
            weights[weakest] = 0f;
            ClearPose(weakest);
            return weakest;
        }

        private void EvaluateBlend(float dt)
        {
            blendAge += dt;
            float amount = blendDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendAge / blendDuration)) : 1f;
            float gaitCarry = 0f;
            for (int i = 0; i < LayerCount; i++)
            {
                weights[i] = Mathf.Lerp(startWeights[i], i == destination ? 1f : 0f, amount);
                mixer.SetInputWeight(i, weights[i]);
                if (i == 0 || !poses[i].Player.IsValid()) continue;
                if (amount >= 1f && i != destination)
                {
                    ClearPose(i);
                    continue;
                }

                ref ActionPose pose = ref poses[i];
                if (i != destination) pose.Sample += pose.Rate * dt;
                pose.Player.SetTime(pose.Loop ? Mathf.Repeat(pose.Sample, pose.Length) : Mathf.Clamp(pose.Sample, 0f, pose.Length));
                gaitCarry += pose.GaitCarry * weights[i];
            }
            if (outputLayers.IsValid())
                outputLayers.SetInputWeight(1, gaitCarry * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.028f, 0.15f, gaitSpeed)));
            graph.Evaluate(0f);
        }

        private void ClearPose(int input)
        {
            if (!poses[input].Player.IsValid()) return;
            graph.Disconnect(mixer, input);
            graph.DestroyPlayable(poses[input].Player);
            poses[input] = default;
        }

        private void ResetPose()
        {
            state = -1;
            destination = 0;
            currentClip = idle;
            previousFraction = -1f;
            stateAge = blendAge = blendDuration = gaitPhase = idleTime = gaitSpeed = airborneTime = landingTime = landingDuration = 0f;
            rootHeightWeight = 1f;
            if (outputLayers.IsValid()) outputLayers.SetInputWeight(1, 0f);
            for (int i = 0; i < LayerCount; i++)
            {
                if (i > 0) ClearPose(i);
                weights[i] = startWeights[i] = i == 0 ? 1f : 0f;
                mixer.SetInputWeight(i, weights[i]);
            }
            for (int i = 0; i < gaitWeights.Length; i++)
            {
                gaitWeights[i] = i == 0 ? 1f : 0f;
                gait.SetInputWeight(i, gaitWeights[i]);
                if (lowerGait.IsValid()) lowerGait.SetInputWeight(i, gaitWeights[i]);
            }
        }
    }
}
