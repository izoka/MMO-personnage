using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MMO.Prototype.Editor
{
    public static class TowerFloorSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/TowerFloor01.unity";

        [MenuItem("MMO/Configurer le premier étage")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");

            if (!File.Exists(ScenePath))
            {
                var original = EditorSceneManager.OpenScene(ReviveArenaSetup.ScenePath);
                if (!EditorSceneManager.SaveScene(original, ScenePath, true))
                    throw new IOException("The tower floor could not be copied.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindFirstObjectByType<TowerFloor>() != null) return;
            var classes = Object.FindFirstObjectByType<ClassSwitcher>();
            var arbalester = classes.help.groupArbalester;
            var melee = arbalester.melee;
            var ally = melee.practiceAlly;
            var meleeTarget = melee.GetComponent<TrainingTarget>();
            var rangedTarget = arbalester.GetComponent<TrainingTarget>();

            GameObject.Find("Test Arena").SetActive(false);
            foreach (var target in Object.FindObjectsByType<TrainingTarget>(FindObjectsSortMode.None))
                if (target != meleeTarget && target != rangedTarget) target.gameObject.SetActive(false);

            var guardian = Object.Instantiate(melee.gameObject).GetComponent<SparringOpponent>();
            guardian.gameObject.name = "Gardien du seuil";
            guardian.practiceAlly = null;
            guardian.sweepSector = null;
            guardian.damage = 14;
            guardian.windup = 0.91f;
            var guardianTarget = guardian.GetComponent<TrainingTarget>();
            guardianTarget.maxHealth = 112;
            guardianTarget.displayName = "GARDIEN DU SEUIL";
            guardianTarget.respawnAutomatically = false;
            SetRoomPosition(guardian.transform, new Vector3(0f, 0f, 5.75f), new Vector2(-5.4f, 5.4f), new Vector2(-3.7f, 8.2f));
            guardian.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            guardian.GetComponent<ArenaFootwork>().speed = 2.15f;
            guardian.groupForAnyClass = true;
            guardian.autoAttack = false;

            var roomX = new Vector2(-6.8f, 6.8f);
            var roomZ = new Vector2(14.5f, 29.4f);
            SetRoomPosition(ally.transform, new Vector3(1.95f, 0f, 19.4f), roomX, roomZ);
            SetRoomPosition(melee.transform, new Vector3(4.85f, 0f, 20.55f), roomX, roomZ);
            SetRoomPosition(arbalester.transform, new Vector3(-2.2f, 0f, 22.55f), roomX, roomZ);
            meleeTarget.displayName = "ÉPÉISTE";
            melee.groupForAnyClass = true;
            melee.autoAttack = false;
            foreach (var actor in new[] { classes.warrior, classes.mage, classes.ranger })
                if (actor.GetComponent<PoisonStatus>() == null) actor.gameObject.AddComponent<PoisonStatus>();

            var root = new GameObject("Tour · Étage 01");
            var floor = root.AddComponent<TowerFloor>();
            floor.classes = classes;
            floor.guardian = guardian;
            floor.arbalester = arbalester;
            classes.help.tower = floor;
            BuildRooms(root.transform, floor);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("The tower floor could not be saved.");
        }

        private static void SetRoomPosition(Transform actor, Vector3 position, Vector2 xLimits, Vector2 zLimits)
        {
            actor.position = position;
            var footwork = actor.GetComponent<ArenaFootwork>();
            footwork.xLimits = xLimits;
            footwork.zLimits = zLimits;
        }

        private static void BuildRooms(Transform root, TowerFloor floor)
        {
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Floor.mat");
            var walls = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Obstacles.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Joints.mat");
            var trim = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Grid.mat");

            Slab(root, "Sol · Salle I", 12.4f, -4.5f, 9.0f, stone);
            Slab(root, "Sol · Passage", 3.8f, 8.7f, 14.1f, stone);
            Slab(root, "Sol · Salle II", 15.2f, 13.8f, 30.5f, stone);
            Slab(root, "Sol · Montée", 3.8f, 30.0f, 35.7f, stone);
            Block(root, "Fond de l’entrée", new Vector3(0f, 1.2f, -4.45f), new Vector3(12.4f, 2.4f, .38f), walls);
            Block(root, "Fond du palier", new Vector3(0f, 1.4f, 35.52f), new Vector3(3.8f, 2.8f, .38f), walls);

            foreach (int side in new[] { -1, 1 })
            {
                Side(root, side * 6f, -4.5f, 8.85f, walls);
                Side(root, side * 1.78f, 8.85f, 14.0f, walls);
                Side(root, side * 7.4f, 14.0f, 30.2f, walls);
                Side(root, side * 1.78f, 30.2f, 35.7f, walls);
                Block(root, "Pilier de la salle II", new Vector3(side * 4.55f, .85f, 25.6f), new Vector3(1.12f, 1.7f, 1.25f), walls);
            }
            floor.passageGate = Threshold(root, 8.85f, 6.0f, "I · LE SEUIL", walls, metal, trim);
            Threshold(root, 14.0f, 7.4f, "II · L’ESCARMOUCHE", walls, metal, trim, false);
            floor.stairsGate = Threshold(root, 30.2f, 7.4f, "MONTÉE", walls, metal, trim);

            for (int i = 0; i < 4; i++)
            {
                float rise = .22f * (i + 1);
                Block(root, "Marche " + (i + 1), new Vector3(0f, rise * .5f, 30.78f + i * .66f),
                    new Vector3(3.45f, rise, .72f), walls);
            }
            Block(root, "Palier supérieur", new Vector3(0f, .44f, 34.24f), new Vector3(3.45f, .88f, 2.60f), stone);
        }

        private static void Slab(Transform root, string name, float width, float start, float end, Material material) =>
            Block(root, name, new Vector3(0f, -.16f, (start + end) * .5f), new Vector3(width, .32f, end - start), material);

        private static void Side(Transform root, float x, float start, float end, Material material) =>
            Block(root, "Mur latéral", new Vector3(x, 1.2f, (start + end) * .5f), new Vector3(.36f, 2.4f, end - start), material);

        private static TowerGate Threshold(Transform root, float z, float halfWidth, string title, Material walls, Material metal,
            Material trim, bool withGate = true)
        {
            float wing = halfWidth - 1.6f;
            foreach (int side in new[] { -1, 1 })
                Block(root, "Aile du passage", new Vector3(side * (1.6f + wing * .5f), 1.38f, z), new Vector3(wing, 2.76f, .42f), walls);
            Block(root, "Linteau", new Vector3(0f, 3.03f, z), new Vector3(3.55f, .46f, .5f), metal);
            var sign = PrototypeSetup.Text(title, root, new Vector3(0f, 3.24f, z - .29f), title, new Color(.94f, .88f, .66f));
            sign.characterSize = .020f;
            if (!withGate) return null;

            var frame = new GameObject("Herse · " + title);
            frame.transform.SetParent(root, false);
            frame.transform.localPosition = new Vector3(0f, 1.39f, z);
            var barrier = frame.AddComponent<BoxCollider>();
            barrier.size = new Vector3(3.20f, 2.78f, .25f);
            for (int bar = -3; bar <= 3; bar++)
                Block(frame.transform, "Barreau", new Vector3(bar * .44f, 0f, 0f), new Vector3(.15f, 2.78f, .20f), metal, false);
            Block(frame.transform, "Traverse basse", new Vector3(0f, -.88f, 0f), new Vector3(3.2f, .13f, .21f), trim, false);
            Block(frame.transform, "Traverse haute", new Vector3(0f, .82f, 0f), new Vector3(3.2f, .13f, .21f), trim, false);
            return frame.AddComponent<TowerGate>();
        }

        private static void Block(Transform root, string name, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(root, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(block.GetComponent<BoxCollider>());
        }
    }
}
