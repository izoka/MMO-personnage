using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class ReviveArenaSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/ClassSandboxRevive.unity";

        [MenuItem("MMO/Configurer l’escarmouche avec relèvement")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            var cast = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKit/Adventurers/Mage.fbx")
                .OfType<AnimationClip>().First(clip => clip.name == "Spellcasting");
            if (!File.Exists(ScenePath))
            {
                var original = EditorSceneManager.OpenScene(PoisonArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(original, ScenePath, true))
                    throw new IOException("The revive arena could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var ally = Object.FindFirstObjectByType<PracticeAlly>();
            var healer = ally.support;
            ally.requiresRevive = true;
            Array.Resize(ref healer.castClips, Enum.GetValues(typeof(MageSpell)).Length);
            healer.castClips[(int)MageSpell.Revive] = cast;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The revive arena could not be saved.");
        }
    }
}
