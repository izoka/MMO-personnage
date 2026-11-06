using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class SweepArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxSweep.unity";

        [MenuItem("MMO/Configurer l’escarmouche avec balayage")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/SwordTrail.mat");
            if (material == null) throw new InvalidOperationException("The sweep marker material is unavailable.");

            if (!File.Exists(ScenePath))
            {
                var original = EditorSceneManager.OpenScene(MobileSkirmishSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(original, ScenePath, true))
                    throw new IOException("The sweep arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var opponent = Object.FindFirstObjectByType<SparringOpponent>();
            if (opponent.sweepSector == null)
                opponent.sweepSector = new GameObject("Avertissement du balayage").AddComponent<ArenaAttackSector>();
            opponent.sweepSector.material = material;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The sweep arena could not be saved.");
        }
    }
}
