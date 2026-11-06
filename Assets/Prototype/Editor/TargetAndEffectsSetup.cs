using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MMO.Prototype.Editor
{
    public static class TargetAndEffectsSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxFX.unity";
        private const string MaterialsPath = "Assets/Prototype/Materials/SpellFX";
        private const string TexturesPath = "Assets/ThirdParty/Kenney/ParticlePack";

        [MenuItem("MMO/Configurer l’arène avec ciblage et effets")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play before configuring the arena.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var shader = Shader.Find("MMO/Spell Particle");
            if (shader == null || !shader.isSupported) throw new System.InvalidOperationException("The spell particle shader is unavailable.");
            if (!AssetDatabase.IsValidFolder(MaterialsPath))
                AssetDatabase.CreateFolder("Assets/Prototype/Materials", "SpellFX");

            var glow = MakeMaterial(shader, "Glow", "light_01");
            var flame = MakeMaterial(shader, "Flame", "flame_02");
            var smoke = MakeMaterial(shader, "Smoke", "smoke_04", BlendMode.OneMinusSrcAlpha);
            var sigil = MakeMaterial(shader, "Sigil", "magic_03");
            var sparks = MakeMaterial(shader, "Sparks", "spark_05");
            var star = MakeMaterial(shader, "Star", "star_06");
            var ring = MakeMaterial(shader, "Ring", "circle_02");
            var swirl = MakeMaterial(shader, "Swirl", "twirl_02");

            if (!File.Exists(ScenePath))
            {
                var original = EditorSceneManager.OpenScene(ClassArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(original, ScenePath, true))
                    throw new IOException("The class arena could not be copied.");
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var classes = Object.FindFirstObjectByType<ClassSwitcher>();
            var targeting = Object.FindFirstObjectByType<TargetLock>();
            if (targeting == null) targeting = new GameObject("Gaze Targeting").AddComponent<TargetLock>();
            targeting.orbit = classes.orbit;
            targeting.view = classes.orbit.GetComponent<Camera>();
            targeting.markerMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/SwordTrail.mat");
            classes.orbit.targeting = targeting;
            classes.mage.targeting = targeting;
            classes.warrior.targeting = targeting;
            WireLockMovement(classes.warrior, "Knight");
            WireLockMovement(classes.mage, "Mage");

            var vfx = classes.mage.GetComponent<MageVfx>();
            if (vfx == null) vfx = classes.mage.gameObject.AddComponent<MageVfx>();
            vfx.glow = glow;
            vfx.flame = flame;
            vfx.smoke = smoke;
            vfx.sigil = sigil;
            vfx.sparks = sparks;
            vfx.star = star;
            vfx.ring = ring;
            vfx.swirl = swirl;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The FX arena could not be saved.");
            AssetDatabase.SaveAssets();
        }

        internal static void WireLockMovement(PuppetMotor player, string model) =>
            WireLockMovement(player.GetComponentInChildren<KayKitCharacterVisual>(true), model);

        internal static void WireLockMovement(KayKitCharacterVisual visual, string model)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"Assets/ThirdParty/KayKit/Adventurers/{model}.fbx"))
            {
                if (!(asset is AnimationClip clip)) continue;
                if (clip.name == "Running_Strafe_Left") visual.strafeLeft = clip;
                if (clip.name == "Running_Strafe_Right") visual.strafeRight = clip;
                if (clip.name == "Walking_Backwards") visual.backstep = clip;
                if (clip.name == "Dodge_Left") visual.dodgeLeft = clip;
                if (clip.name == "Dodge_Right") visual.dodgeRight = clip;
                if (clip.name == "Dodge_Backward") visual.dodgeBack = clip;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            EditorUtility.SetDirty(visual);
        }

        private static Material MakeMaterial(Shader shader, string name, string textureName, BlendMode destination = BlendMode.One)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesPath}/{textureName}.png");
            if (texture == null) throw new FileNotFoundException($"Missing Kenney texture {textureName}");
            string path = $"{MaterialsPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.mainTexture = texture;
            material.SetFloat("_DstBlend", (float)destination);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
