using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class KayKitSetup
    {
        private const string Source = "Assets/ThirdParty/KayKit/Adventurers/";
        private const string Prefabs = "Assets/Prototype/Characters/";
        public const string ScenePath = "Assets/Prototype/Scenes/WarriorCharacters.unity";

        private enum Variant { Default, Duel, Ranger, Healer }

        public static GameObject CreateMagePrefab() => CreateCharacter("Mage", "mage_texture");
        public static GameObject CreateRangerPrefab() => CreateCharacter("RogueHooded", "rogue_texture", Variant.Ranger);
        public static GameObject CreateHealerPrefab() => CreateCharacter("Mage", "mage_texture", Variant.Healer);

        [MenuItem("Prototype/Intégrer les personnages KayKit")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");
            Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh();
            var knight = CreateCharacter("Knight", "knight_texture");
            var rogue = CreateCharacter("RogueHooded", "rogue_texture", Variant.Duel);

            if (!File.Exists(ScenePath))
            {
                var original = EditorSceneManager.OpenScene(PrototypeSetup.WarriorScenePath);
                if (!EditorSceneManager.SaveScene(original, ScenePath, true))
                    throw new IOException("Unable to create the character scene.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var player = Object.FindFirstObjectByType<PuppetMotor>();
            var sparring = Object.FindFirstObjectByType<SparringOpponent>();
            Attach(player.gameObject, knight);
            var opponentVisual = Attach(sparring.gameObject, rogue);
            sparring.GetComponent<TrainingTarget>().visual = opponentVisual.transform;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        private static GameObject Attach(GameObject actor, GameObject prefab)
        {
            var existing = actor.GetComponentInChildren<KayKitCharacterVisual>(true);
            if (existing != null) return existing.gameObject;
            var puppet = actor.GetComponentInChildren<PuppetRig>(true);
            if (puppet != null) puppet.gameObject.SetActive(false);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, actor.transform);
        }

        private static GameObject CreateCharacter(string modelName, string texture, Variant variant = Variant.Default)
        {
            bool duel = variant == Variant.Duel;
            bool ranger = variant == Variant.Ranger;
            bool healer = variant == Variant.Healer;
            string target = Prefabs + (ranger || healer ? variant.ToString() : modelName) + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
            if (existing != null) return existing;
            string modelPath = Source + modelName + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().ToDictionary(c => c.name);
            if (asset == null) throw new FileNotFoundException(modelPath);

            var wrapper = new GameObject(modelName + " Visual");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, wrapper.transform);
                bool mage = modelName == "Mage";
                string equipped = healer ? "1H_Wand" : mage ? "2H_Staff" : ranger ? "2H_Crossbow" : "1H_Sword";
                Material material = ranger ? MaterialFor("ranger_texture", texture, new Color(0.60f, 0.76f, 1f)) :
                    healer ? MaterialFor("healer_texture", texture, new Color(1f, 0.91f, 0.60f)) : MaterialFor(texture);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    bool inHand = renderer.transform.parent.name.StartsWith("handslot.", StringComparison.Ordinal);
                    bool keep = inHand ? (!duel && renderer.name == equipped) || (healer && renderer.name == "Spellbook")
                        : (duel || renderer.name != "Knight_Cape") && (!healer || renderer.name != "Mage_Hat");
                    renderer.enabled = keep;
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }

                var visual = AddVisual(wrapper, model, sourceClips);
                if (mage || ranger)
                {
                    visual.idle.SampleAnimation(model, 0f);
                    FitToActor(model, wrapper.transform);
                    return PrefabUtility.SaveAsPrefabAsset(wrapper, target);
                }

                Transform weapon;
                if (duel)
                {
                    var knight = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "Knight.fbx");
                    weapon = Object.Instantiate(Find(knight, "1H_Sword").gameObject, Find(model, "handslot.r"), false).transform;
                    weapon.name = "Duel Sword";
                    weapon.GetComponent<Renderer>().sharedMaterial = MaterialFor("knight_texture");
                }
                else weapon = Find(model, "1H_Sword");

                var tip = new GameObject("Blade Tip").transform;
                tip.SetParent(weapon, false);
                Bounds blade = weapon.GetComponent<MeshFilter>().sharedMesh.bounds;
                tip.localPosition = new Vector3(blade.center.x,
                    Mathf.Abs(blade.max.y) > Mathf.Abs(blade.min.y) ? blade.max.y : blade.min.y, blade.center.z);

                visual.attacks = new AnimationClip[Enum.GetValues(typeof(WarriorMove)).Length];
                visual.contactFrames = new float[visual.attacks.Length];
                SetAttack(visual, sourceClips["1H_Melee_Attack_Slice_Horizontal"], WarriorMove.Light1, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Slice_Diagonal"], WarriorMove.Light2, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Chop"], WarriorMove.Light3, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Chop"], WarriorMove.Heavy, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Stab"], WarriorMove.Thrust, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Stab"], WarriorMove.Lunge, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Slice_Horizontal"], WarriorMove.Sweep, tip, model);
                SetAttack(visual, sourceClips["Block_Attack"], WarriorMove.GuardBreaker, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Slice_Diagonal"], WarriorMove.Retreat, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Stab"], WarriorMove.Riposte, tip, model);
                SetAttack(visual, sourceClips["1H_Melee_Attack_Chop"], WarriorMove.Plunge, tip, model);

                visual.idle.SampleAnimation(model, 0f);
                FitToActor(model, wrapper.transform);
                var trail = tip.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/SwordTrail.mat");
                trail.startWidth = 0.07f;
                trail.endWidth = 0f;
                trail.time = 0.11f;
                trail.emitting = false;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.startColor = new Color(1f, 0.88f, 0.58f, 0.72f);
                trail.endColor = new Color(1f, 0.88f, 0.58f, 0f);
                visual.swordTrail = trail;
                return PrefabUtility.SaveAsPrefabAsset(wrapper, target);
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
            }
        }

        private static KayKitCharacterVisual AddVisual(GameObject wrapper, GameObject model,
            System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var visual = wrapper.AddComponent<KayKitCharacterVisual>();
            visual.animator = model.GetComponent<Animator>();
            if (visual.animator == null) visual.animator = model.AddComponent<Animator>();
            visual.animator.applyRootMotion = false;
            visual.motionRoot = Find(model, "root");
            visual.idle = clips["Idle"];
            visual.walk = clips["Walking_A"];
            visual.run = clips["Running_A"];
            visual.guard = clips["Blocking"];
            visual.guardHit = clips["Block_Hit"];
            visual.hit = clips["Hit_A"];
            visual.dodge = clips["Dodge_Forward"];
            visual.jumpStart = clips["Jump_Start"];
            visual.jumpAir = clips["Jump_Idle"];
            visual.jumpLand = clips["Jump_Land"];
            visual.death = clips["Death_A"];
            return visual;
        }

        private static void SetAttack(KayKitCharacterVisual visual, AnimationClip clip, WarriorMove move, Transform tip, GameObject model)
        {
            int index = (int)move;
            visual.attacks[index] = clip;
            float contact = 0.45f;
            float reach = float.NegativeInfinity;
            // Use the blade's farthest forward sample as the visual contact.
            for (int i = 8; i <= 33; i++)
            {
                float fraction = i / 45f;
                clip.SampleAnimation(model, fraction * clip.length);
                float extension = model.transform.InverseTransformPoint(tip.position).z;
                if (extension <= reach) continue;
                contact = fraction;
                reach = extension;
            }
            visual.contactFrames[index] = contact;
        }

        private static void FitToActor(GameObject model, Transform reference)
        {
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked);
                    foreach (var vertex in baked.vertices)
                    {
                        float y = reference.InverseTransformPoint(skin.transform.TransformPoint(vertex)).y;
                        low = Mathf.Min(low, y);
                        high = Mathf.Max(high, y);
                    }
                }
                finally { Object.DestroyImmediate(baked); }
            }
            if (!float.IsFinite(low) || high - low < 0.1f)
                throw new InvalidOperationException("Unable to measure the character mesh.");
            float scale = 1.80f / (high - low);
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(0f, -low * scale + 0.008f, 0f);
        }

        private static Transform Find(GameObject model, string bone) =>
            model.GetComponentsInChildren<Transform>(true).First(t => t.name == bone);

        private static Material MaterialFor(string texture, string sourceTexture = null, Color? tint = null)
        {
            string path = Prefabs + texture + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard"))
            {
                mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + (sourceTexture ?? texture) + ".png"),
                color = tint ?? Color.white
            };
            material.SetFloat("_Glossiness", 0.14f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
