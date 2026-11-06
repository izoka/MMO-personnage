using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class ClassArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandbox.unity";
        private const string MageSource = "Assets/ThirdParty/KayKit/Adventurers/Mage.fbx";
        private const string EffectPath = "Assets/Prototype/Characters/MagicProjectile.mat";

        [MenuItem("Prototype/Créer l’arène des classes")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");
            var prefab = KayKitSetup.CreateMagePrefab();
            var clips = AssetDatabase.LoadAllAssetsAtPath(MageSource).OfType<AnimationClip>().ToDictionary(c => c.name);
            var castClips = new[]
            {
                null, clips["Spellcast_Shoot"], clips["Spellcast_Long"], clips["Spellcast_Raise"],
                clips["Spellcasting"], clips["Spellcast_Raise"]
            };
            var effect = AssetDatabase.LoadAssetAtPath<Material>(EffectPath);
            if (effect == null)
            {
                effect = new Material(Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(effect, EffectPath);
            }

            if (!File.Exists(ScenePath))
            {
                var source = EditorSceneManager.OpenScene(KayKitSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                    throw new IOException("Unable to create the class sandbox.");
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var existing = Object.FindFirstObjectByType<ClassSwitcher>();
            if (existing != null)
            {
                if (AttachFocus(existing.mage.GetComponent<MageCombat>())) EditorSceneManager.SaveScene(scene);
                return;
            }

            var warrior = Object.FindFirstObjectByType<PuppetMotor>();
            var mage = CreateActor("Mage", warrior);
            var mageObject = mage.gameObject;
            var combat = mageObject.AddComponent<MageCombat>();
            combat.castClips = castClips;
            combat.effectMaterial = effect;
            combat.trailMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/SwordTrail.mat");
            PrefabUtility.InstantiatePrefab(prefab, mageObject.transform);
            AttachFocus(combat);
            mageObject.SetActive(false);

            var switcher = new GameObject("Class Switch").AddComponent<ClassSwitcher>();
            switcher.warrior = warrior;
            switcher.mage = mage;
            switcher.orbit = Object.FindFirstObjectByType<OrbitCamera>();
            switcher.help = Object.FindFirstObjectByType<PrototypeHelp>();
            switcher.opponent = Object.FindFirstObjectByType<SparringOpponent>();
            switcher.help.classSwitcher = switcher;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        internal static PuppetMotor CreateActor(string name, PuppetMotor reference)
        {
            var actor = new GameObject(name) { layer = reference.gameObject.layer };
            actor.transform.SetPositionAndRotation(reference.transform.position, reference.transform.rotation);
            var body = actor.AddComponent<CharacterController>();
            var original = reference.GetComponent<CharacterController>();
            body.center = original.center;
            body.height = original.height;
            body.radius = original.radius;
            body.slopeLimit = original.slopeLimit;
            body.stepOffset = original.stepOffset;
            body.skinWidth = original.skinWidth;
            body.minMoveDistance = original.minMoveDistance;
            return actor.AddComponent<PuppetMotor>();
        }

        internal static bool AttachFocus(MageCombat combat, string itemName = "2H_Staff")
        {
            if (combat.castFocus != null) return false;
            var item = combat.GetComponentsInChildren<MeshFilter>(true).First(mesh => mesh.name == itemName);
            var bounds = item.sharedMesh.bounds;
            var focus = new GameObject("Magic Focus").transform;
            focus.SetParent(item.transform, false);
            focus.localPosition = new Vector3(bounds.center.x, bounds.max.y - Mathf.Min(0.10f, bounds.size.y * 0.16f), bounds.center.z);
            combat.castFocus = focus;
            EditorSceneManager.MarkSceneDirty(combat.gameObject.scene);
            return true;
        }
    }
}
