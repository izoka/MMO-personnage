using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class HealerArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxHealer.unity";

        [MenuItem("MMO/Configurer l’arène du soigneur")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            var prefab = KayKitSetup.CreateHealerPrefab();
            if (prefab == null) throw new InvalidOperationException("The healer prefab is unavailable.");
            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKit/Adventurers/Mage.fbx")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var castClips = new AnimationClip[Enum.GetValues(typeof(MageSpell)).Length];
            castClips[(int)MageSpell.Light] = clips["Spellcast_Shoot"];
            castClips[(int)MageSpell.Mend] = clips["Spellcast_Long"];
            castClips[(int)MageSpell.Renew] = clips["Spellcast_Raise"];
            castClips[(int)MageSpell.Aegis] = clips["Spellcast_Raise"];
            castClips[(int)MageSpell.Sanctuary] = clips["Spellcasting"];

            if (!File.Exists(ScenePath))
            {
                var source = EditorSceneManager.OpenScene(RangerArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                    throw new IOException("The healer arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var classes = Object.FindFirstObjectByType<ClassSwitcher>();
            if (classes.healer != null) return;

            var healer = ClassArenaSetup.CreateActor("Soigneur", classes.warrior);
            healer.targeting = classes.warrior.targeting;
            var magic = healer.gameObject.AddComponent<MageCombat>();
            var mage = classes.mage.GetComponent<MageCombat>();
            magic.isHealer = true;
            magic.castClips = castClips;
            magic.effectMaterial = mage.effectMaterial;
            magic.trailMaterial = mage.trailMaterial;
            EditorUtility.CopySerialized(classes.mage.GetComponent<MageVfx>(), healer.gameObject.AddComponent<MageVfx>());

            PrefabUtility.InstantiatePrefab(prefab, healer.transform);
            ClassArenaSetup.AttachFocus(magic, "1H_Wand");
            TargetAndEffectsSetup.WireLockMovement(healer, "Mage");
            healer.gameObject.SetActive(false);
            classes.healer = healer;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The healer arena could not be saved.");
            AssetDatabase.SaveAssets();
        }
    }
}
