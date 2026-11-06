using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class MobileSkirmishSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxMobile.unity";

        [MenuItem("MMO/Configurer l’escarmouche mobile")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            if (!File.Exists(ScenePath))
            {
                var original = EditorSceneManager.OpenScene(SkirmishArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(original, ScenePath, true))
                    throw new IOException("The mobile arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var arbalester = Object.FindFirstObjectByType<SparringArbalester>();
            var melee = arbalester.melee;
            var ally = melee.practiceAlly;

            WireFootwork(ally.gameObject, new Vector3(1.95f, 0f, 8.20f), 2.75f, ally.visual, "Knight");
            WireFootwork(melee.gameObject, new Vector3(4.85f, 0f, 9.35f), 2.80f,
                melee.GetComponentInChildren<KayKitCharacterVisual>(), "RogueHooded");
            WireFootwork(arbalester.gameObject, new Vector3(-2.20f, 0f, 11.35f), 2.4f,
                arbalester.character, "RogueHooded");
            ally.secondaryEnemy = arbalester.GetComponent<TrainingTarget>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The mobile arena could not be saved.");
        }

        private static void WireFootwork(GameObject actor, Vector3 position, float speed, KayKitCharacterVisual visual, string model)
        {
            actor.transform.position = position;
            var footwork = actor.GetComponent<ArenaFootwork>();
            if (footwork == null)
            {
                Object.DestroyImmediate(actor.GetComponent<Collider>());
                var body = actor.AddComponent<CharacterController>();
                body.center = Vector3.up * 0.91f;
                body.height = 1.82f;
                body.radius = 0.32f;
                body.stepOffset = 0.22f;
                body.skinWidth = 0.034f;
                body.minMoveDistance = 0f;
                footwork = actor.AddComponent<ArenaFootwork>();
            }
            footwork.speed = speed;
            TargetAndEffectsSetup.WireLockMovement(visual, model);
        }
    }
}
