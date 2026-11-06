using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MMO.Prototype.Editor
{
    // Frame-based integration checks exercise the real CharacterController and impact timing.
    public static class PrototypeVerification
    {
        [Serializable] private class Check { public string name; public bool passed; public string detail; }
        [Serializable] private class Report { public bool passed; public Check[] checks; }
        private static readonly List<Check> checks = new List<Check>();
        private static IEnumerator routine;
        private static float nextTime;
        private static PuppetMotor player;
        private static TrainingTarget target;
        private static OrbitCamera camera;
        private static Vector3 targetOrigin;
        private static SparringOpponent opponent;
        private static bool previousAutoAttack;

        [MenuItem("Prototype/Vérifier mouvements et combat (Play)")]
        public static void Begin()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            if (routine != null) throw new InvalidOperationException("Verification already running.");
            player = UnityEngine.Object.FindFirstObjectByType<PuppetMotor>();
            target = GameObject.Find("Training Target").GetComponent<TrainingTarget>();
            opponent = UnityEngine.Object.FindFirstObjectByType<SparringOpponent>();
            if (opponent != null)
            {
                previousAutoAttack = opponent.autoAttack;
                opponent.autoAttack = false;
            }
            camera = UnityEngine.Object.FindFirstObjectByType<OrbitCamera>();
            targetOrigin = target.transform.position;
            camera.enabled = false;
            checks.Clear();
            routine = Run();
            nextTime = Time.time;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; routine = null; return; }
            if (Time.time < nextTime) return;
            try
            {
                if (!routine.MoveNext()) { Finish(); return; }
                nextTime = Time.time + Convert.ToSingle(routine.Current);
            }
            catch (Exception exception)
            {
                Record("Verification runner", false, exception.ToString());
                Finish();
            }
        }

        private static IEnumerator Run()
        {
            Prepare(Vector3.zero);
            yield return 0.2f;
            Record("Ground contact", player.Grounded && Mathf.Abs(player.transform.position.y) < 0.08f, player.transform.position.ToString());

            Vector3 start = player.transform.position;
            player.SetExternalInput(Vector2.left);
            yield return 0.55f;
            player.SetExternalInput(Vector2.zero);
            yield return 0.18f;
            float walked = Vector3.Distance(start, player.transform.position);
            Record("Walk", walked > 1.3f && walked < 2.6f && player.transform.position.x < -1f, "distance=" + walked);

            Prepare(Vector3.zero);
            yield return 0.15f;
            start = player.transform.position;
            player.SetExternalInput(Vector2.left, true);
            yield return 0.55f;
            player.SetExternalInput(Vector2.zero);
            yield return 0.18f;
            float ran = Vector3.Distance(start, player.transform.position);
            Record("Sprint", ran > walked * 1.35f && ran < 4.2f, "walk=" + walked + ", run=" + ran);

            Prepare(Vector3.zero);
            yield return 0.15f;
            bool jumped = player.RequestJump();
            yield return 0.2f;
            float jumpY = player.transform.position.y;
            yield return 0.65f;
            Record("Jump and landing", jumped && jumpY > 0.55f && player.Grounded, "height_sample=" + jumpY + ", grounded=" + player.Grounded);

            Prepare(Vector3.zero);
            yield return 0.15f;
            bool dodged = player.RequestDodge();
            float dodgeStarted = Time.time;
            bool attackDuringDodge = player.RequestAttack();
            yield return 0.04f;
            float initialSpeed = player.transform.position.z / (Time.time - dodgeStarted);
            Record("Dodge eases into movement", initialSpeed > 0f && initialSpeed < 5.5f, "initial speed=" + initialSpeed);
            yield return 0.36f;
            float dodgeDistance = player.transform.position.z;
            bool earlyDodge = player.RequestDodge();
            Record("Dodge distance and cooldown", dodged && dodgeDistance > 1.8f && dodgeDistance < 3.3f && !earlyDodge && !attackDuringDodge, "distance=" + dodgeDistance);
            yield return 0.6f;
            Record("Dodge available again", player.RequestDodge(), "cooldown expired");

            Prepare(Vector3.zero);
            yield return 0.15f;
            player.SetExternalInput(Vector2.left, true);
            yield return 2f;
            player.SetExternalInput(Vector2.zero);
            yield return 0.15f;
            Record("Arena wall collision", player.transform.position.x > -9.65f && player.transform.position.x < -8.5f, "x=" + player.transform.position.x);

            Prepare(new Vector3(0, 0, 1.9f));
            yield return 0.15f;
            bool attacked = player.RequestAttack();
            yield return 0.12f;
            Record("Attack windup", attacked && target.Health == 100, "health before impact=" + target.Health);
            yield return 0.22f;
            Record("Front hit applies once", target.Health == 75 && player.LandedHits == 1, "health=" + target.Health + ", hits=" + player.LandedHits);
            yield return 0.4f;
            Record("No repeated damage in recovery", target.Health == 75 && player.LandedHits == 1, "health=" + target.Health);

            Prepare(new Vector3(0, 0, 1.9f), 180f);
            yield return 0.15f;
            player.RequestAttack();
            yield return 0.7f;
            Record("Target behind is not hit", target.Health == 100, "health=" + target.Health);

            Prepare(Vector3.zero);
            yield return 0.15f;
            player.RequestAttack();
            yield return 0.7f;
            Record("Out of range misses", target.Health == 100, "health=" + target.Health);

            Prepare(new Vector3(0, 0, 1.9f));
            yield return 0.15f;
            for (int i = 0; i < 4; i++) { player.RequestAttack(); yield return 0.7f; }
            Record("Target down after four hits", target.Health == 0 && !target.GetComponent<Collider>().enabled, "health=" + target.Health);
            yield return 1.7f;
            Record("Target automatically resets", target.Health == 100 && target.GetComponent<Collider>().enabled, "health=" + target.Health);

            Prepare(new Vector3(4f, 0f, 0f));
            yield return 0.15f;
            player.SetExternalInput(Vector2.up);
            yield return 1.6f;
            player.SetExternalInput(Vector2.zero);
            yield return 0.15f;
            Record("Stairs are climbable", player.transform.position.y > 0.45f && player.transform.position.z > 3f, player.transform.position.ToString());

            Prepare(new Vector3(0f, 0f, 1.9f));
            yield return 0.2f;
            var rig = player.GetComponentInChildren<PuppetRig>();
            var character = player.GetComponentInChildren<KayKitCharacterVisual>();
            Transform arm = rig != null ? rig.rightArm : Array.Find(character.animator.GetComponentsInChildren<Transform>(), t => t.name == "upperarm.r");
            Quaternion restingArm = arm.localRotation;
            player.RequestGuard(true);
            yield return 0.035f;
            float earlyRotation = Quaternion.Angle(restingArm, arm.localRotation);
            yield return 0.16f;
            float settledRotation = Quaternion.Angle(restingArm, arm.localRotation);
            Record("Guard pose blends from the current pose", earlyRotation < settledRotation * 0.85f && settledRotation > 10f,
                "early=" + earlyRotation + ", settled=" + settledRotation);

            Prepare(Vector3.zero);
            yield return 0.2f;
            if (character != null)
            {
                var foot = Array.Find(character.animator.GetComponentsInChildren<Transform>(), t => t.name == "foot.l");
                player.SetExternalInput(Vector2.left);
                yield return 0.3f;
                Quaternion firstStep = foot.localRotation;
                yield return 0.28f;
                Vector3 root = character.motionRoot.localPosition;
                Record("Imported walking clip moves the leg without translating its root", character.CurrentClip == "Walking_A" &&
                    Quaternion.Angle(firstStep, foot.localRotation) > 3f && Mathf.Abs(root.x) + Mathf.Abs(root.z) < 0.001f,
                    "clip=" + character.CurrentClip + ", root=" + root);
                if (opponent != null)
                    foreach (var wait in RunWarriorChecks()) yield return wait;
                yield break;
            }
            var leftFoot = rig.leftKnee.Find("Foot").GetComponent<Renderer>();
            var rightFoot = rig.rightKnee.Find("Foot").GetComponent<Renderer>();
            player.SetExternalInput(Vector2.left);
            yield return 0.22f;
            float highestSupportFoot = float.MinValue;
            float largestSwing = 0f;
            for (int sample = 0; sample < 24; sample++)
            {
                float leftY = leftFoot.bounds.min.y;
                float rightY = rightFoot.bounds.min.y;
                highestSupportFoot = Mathf.Max(highestSupportFoot, Mathf.Min(leftY, rightY));
                largestSwing = Mathf.Max(largestSwing, Mathf.Abs(leftY - rightY));
                yield return 0.025f;
            }
            Record("Walking alternates swing with a supporting foot", highestSupportFoot < 0.09f && largestSwing > 0.05f,
                "highest support=" + highestSupportFoot + ", swing=" + largestSwing);

            if (opponent != null)
                foreach (var wait in RunWarriorChecks()) yield return wait;
        }

        private static IEnumerable<float> RunWarriorChecks()
        {
            Prepare(new Vector3(0f, 0f, 1.9f));
            yield return 0.15f;
            bool charged = player.BeginHeavyCharge();
            yield return 0.85f;
            bool released = player.ReleaseHeavyCharge();
            yield return 0.84f;
            Record("Full charge hits once and spends 30 stamina", charged && released && target.Health == 40 && target.TotalHits == 1 && Mathf.Abs(player.Stamina - 70f) < 0.05f,
                "target=" + target.Health + ", stamina=" + player.Stamina);

            Prepare(new Vector3(0f, 0f, 1.1f));
            yield return 0.15f;
            player.RequestSkill(WarriorMove.Thrust);
            yield return 0.63f;
            Record("Thrust reaches the distant target once", target.Health == 70 && target.TotalHits == 1, "target=" + target.Health);

            Prepare(Vector3.zero);
            yield return 0.15f;
            player.SetExternalInput(Vector2.up, true);
            yield return 0.16f;
            player.RequestAttack();
            bool lunged = player.CurrentMove == WarriorMove.Lunge;
            player.SetExternalInput(Vector2.zero);
            yield return 0.76f;
            Record("Sprint attack lunges, hits once and stops at the target", lunged && target.Health == 75 && player.transform.position.z < 3.2f,
                "target=" + target.Health + ", z=" + player.transform.position.z);

            Prepare(new Vector3(-4f, 0f, 8.7f));
            yield return 0.15f;
            player.RequestSkill(WarriorMove.Sweep);
            yield return 0.82f;
            Record("Sweep reaches all three targets once", player.LandedHits == 3, "hits=" + player.LandedHits);

            Prepare(new Vector3(0f, 0f, 2.4f));
            yield return 0.15f;
            target.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            target.SetGuard(true);
            player.RequestSkill(WarriorMove.GuardBreaker);
            yield return 0.4f;
            Record("Guard breaker opens the defender", target.IsStunned && !target.IsGuarding && target.Health < 100, "target=" + target.Health);
            target.transform.rotation = Quaternion.identity;

            Prepare(new Vector3(0f, 0f, 2.35f));
            yield return 0.15f;
            player.RequestSkill(WarriorMove.Retreat);
            yield return 0.8f;
            Record("Retreat strikes first, then withdraws", target.Health == 80 && Mathf.Abs(player.transform.position.z - 0.75f) < 0.12f,
                "target=" + target.Health + ", z=" + player.transform.position.z);

            Prepare(new Vector3(0f, 0f, 2.4f));
            yield return 0.15f;
            player.RequestJump();
            yield return 0.16f;
            player.RequestAttack();
            yield return 0.13f;
            bool noEarlyImpact = target.Health == 100 && !player.Grounded;
            yield return 0.78f;
            Record("Plunge strikes on landing", noEarlyImpact && target.Health == 62 && target.TotalHits == 1 && player.Grounded, "target=" + target.Health);

            Prepare(new Vector3(4f, 0f, 7.6f));
            opponent.autoAttack = true;
            float deadline = Time.time + 2.5f;
            while (!opponent.WindingUp && Time.time < deadline) yield return 0f;
            yield return 0.72f;
            player.RequestGuard(true);
            yield return 0.21f;
            bool parried = player.Health == 100 && player.RiposteReady;
            player.RequestGuard(false);
            player.RequestAttack();
            bool riposted = player.CurrentMove == WarriorMove.Riposte;
            yield return 0.25f;
            int opponentHealth = opponent.GetComponent<TrainingTarget>().Health;
            Record("Duel attack can be parried and riposted", parried && riposted && opponentHealth == 205,
                "player=" + player.Health + ", opponent=" + opponentHealth);
            opponent.autoAttack = false;
        }

        private static void Prepare(Vector3 position, float yaw = 0f)
        {
            target.transform.position = targetOrigin;
            player.ResetSandbox();
            player.SetExternalInput(Vector2.zero);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Record(string name, bool passed, string detail)
        {
            checks.Add(new Check { name = name, passed = passed, detail = detail });
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            routine = null;
            target.transform.position = targetOrigin;
            player.ReleaseExternalInput();
            player.ResetSandbox();
            if (opponent != null) opponent.autoAttack = previousAutoAttack;
            camera.enabled = true;
            bool passed = checks.TrueForAll(check => check.passed);
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/results.json", JsonUtility.ToJson(new Report { passed = passed, checks = checks.ToArray() }, true));
            Debug.Log("Puppet integration checks: " + (passed ? "PASS" : "FAIL") + " (" + checks.Count + ")");
        }
    }
}
