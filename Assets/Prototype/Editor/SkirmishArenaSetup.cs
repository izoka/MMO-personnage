using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class SkirmishArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxSkirmish.unity";

        [MenuItem("MMO/Configurer l’escarmouche")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/Characters/Ranger.prefab");
            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKit/Adventurers/RogueHooded.fbx")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var boltMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/SwordTrail.mat");
            if (prefab == null || boltMaterial == null) throw new InvalidOperationException("The crossbow assets are unavailable.");

            if (!File.Exists(ScenePath))
            {
                var source = EditorSceneManager.OpenScene(SupportArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                    throw new IOException("The skirmish arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var classes = Object.FindFirstObjectByType<ClassSwitcher>();
            if (classes.help.groupArbalester != null) return;

            var enemy = new GameObject("Arbalétrier ennemi");
            enemy.transform.position = new Vector3(-1.95f, 0f, 10.55f);
            enemy.transform.rotation = Quaternion.LookRotation(classes.opponent.practiceAlly.transform.position - enemy.transform.position);
            var body = enemy.AddComponent<CapsuleCollider>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = Vector3.up * 0.9f;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, enemy.transform);
            var target = enemy.AddComponent<TrainingTarget>();
            target.maxHealth = 60;
            target.respawnAutomatically = false;
            target.displayName = "ARBALÉTRIER";
            target.visual = model.transform;
            target.healthText = PrototypeSetup.Text("Vie de l’arbalétrier", enemy.transform, Vector3.up * 2.35f,
                "ARBALÉTRIER", new Color(1f, 0.75f, 0.56f));
            target.damageText = PrototypeSetup.Text("Dégâts subis", enemy.transform, Vector3.up * 2.15f, "", Color.white);

            var crossbow = model.GetComponentsInChildren<MeshFilter>(true).First(mesh => mesh.name == "2H_Crossbow");
            var bounds = crossbow.sharedMesh.bounds;
            var muzzle = new GameObject("Sortie du carreau").transform;
            muzzle.SetParent(crossbow.transform, false);
            muzzle.localPosition = new Vector3(bounds.min.x + 0.1f, bounds.center.y + 0.08f, bounds.center.z);

            var arbalester = enemy.AddComponent<SparringArbalester>();
            arbalester.melee = classes.opponent;
            arbalester.character = model.GetComponent<KayKitCharacterVisual>();
            arbalester.shootClip = clips["2H_Ranged_Shoot"];
            arbalester.reloadClip = clips["2H_Ranged_Reload"];
            arbalester.muzzle = muzzle;
            arbalester.boltMaterial = boltMaterial;
            classes.opponent.GetComponent<TrainingTarget>().respawnAutomatically = false;
            classes.help.groupArbalester = arbalester;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The skirmish arena could not be saved.");
        }
    }
}
