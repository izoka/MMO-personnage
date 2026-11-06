using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class RangerArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxRanger.unity";
        private const string Model = "RogueHooded";

        [MenuItem("MMO/Configurer l’arène du rôdeur")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            var prefab = KayKitSetup.CreateRangerPrefab();
            var clips = AssetDatabase.LoadAllAssetsAtPath($"Assets/ThirdParty/KayKit/Adventurers/{Model}.fbx")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var boltMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/SwordTrail.mat");
            if (prefab == null || boltMaterial == null) throw new InvalidOperationException("The ranger assets are unavailable.");

            if (!File.Exists(ScenePath))
            {
                var source = EditorSceneManager.OpenScene(TargetAndEffectsSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                    throw new IOException("The ranger arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var classes = Object.FindFirstObjectByType<ClassSwitcher>();
            if (classes.ranger != null) return;

            var ranger = ClassArenaSetup.CreateActor("Rôdeur", classes.warrior);
            ranger.targeting = classes.warrior.targeting;
            var combat = ranger.gameObject.AddComponent<RangerCombat>();
            combat.shootClip = clips["2H_Ranged_Shoot"];
            combat.reloadClip = clips["2H_Ranged_Reload"];
            combat.boltMaterial = boltMaterial;

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ranger.transform);
            var crossbow = visual.GetComponentsInChildren<MeshFilter>(true).First(mesh => mesh.name == "2H_Crossbow");
            var bounds = crossbow.sharedMesh.bounds;
            var muzzle = new GameObject("Sortie du carreau").transform;
            muzzle.SetParent(crossbow.transform, false);
            muzzle.localPosition = new Vector3(bounds.min.x + 0.1f, bounds.center.y + 0.08f, bounds.center.z);
            combat.muzzle = muzzle;
            TargetAndEffectsSetup.WireLockMovement(ranger, Model);

            ranger.gameObject.SetActive(false);
            classes.ranger = ranger;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The ranger arena could not be saved.");
            AssetDatabase.SaveAssets();
        }
    }
}
