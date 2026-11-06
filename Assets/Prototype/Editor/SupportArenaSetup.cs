using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class SupportArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxSupport.unity";

        [MenuItem("MMO/Configurer l’entraînement aux soins")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/Characters/Knight.prefab");
            var marker = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/SwordTrail.mat");
            if (prefab == null || marker == null) throw new InvalidOperationException("The ally assets are unavailable.");

            if (!File.Exists(ScenePath))
            {
                var source = EditorSceneManager.OpenScene(HealerArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                    throw new IOException("The support arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var classes = Object.FindFirstObjectByType<ClassSwitcher>();
            var healer = classes.healer.GetComponent<MageCombat>();
            if (healer.practiceAlly != null) return;

            var actor = new GameObject("Allié d’entraînement") { layer = 2 };
            Vector3 opponentPosition = classes.opponent.transform.position;
            Vector3 position = opponentPosition + new Vector3(-1.60f, 0f, -0.38f);
            actor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(opponentPosition - position));
            var body = actor.AddComponent<CapsuleCollider>();
            body.center = Vector3.up * 0.90f;
            body.height = 1.8f;
            body.radius = 0.32f;

            var ally = actor.AddComponent<PracticeAlly>();
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, actor.transform);
            ally.visual = visual.GetComponent<KayKitCharacterVisual>();
            ally.markerMaterial = marker;
            ally.healthText = PrototypeSetup.Text("Vie de l’allié", actor.transform, Vector3.up * 2.31f,
                "ALLIÉ\n100 / 100", new Color(0.47f, 0.96f, 1f));
            ally.changeText = PrototypeSetup.Text("Variation de vie", actor.transform, Vector3.up * 2.9f, "", Color.white);
            ally.support = healer;
            healer.practiceAlly = ally;
            classes.opponent.practiceAlly = ally;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The support arena could not be saved.");
            AssetDatabase.SaveAssets();
        }
    }
}
