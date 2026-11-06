using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class PoisonArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxPoison.unity";

        [MenuItem("MMO/Configurer l’escarmouche avec poison")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            var cast = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKit/Adventurers/Mage.fbx")
                .OfType<AnimationClip>().First(clip => clip.name == "Spellcast_Raise");
            if (!File.Exists(ScenePath))
            {
                var original = EditorSceneManager.OpenScene(SweepArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(original, ScenePath, true))
                    throw new IOException("The poison arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var arbalester = Object.FindFirstObjectByType<SparringArbalester>();
            var ally = arbalester.melee.practiceAlly;
            var healer = ally.support;
            if (healer.GetComponent<PoisonStatus>() == null) healer.gameObject.AddComponent<PoisonStatus>();
            if (ally.GetComponent<PoisonStatus>() == null) ally.gameObject.AddComponent<PoisonStatus>();
            Array.Resize(ref healer.castClips, Enum.GetValues(typeof(MageSpell)).Length);
            healer.castClips[(int)MageSpell.Purify] = cast;
            arbalester.poisonBolts = true;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The poison arena could not be saved.");
        }
    }
}
