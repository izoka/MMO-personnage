using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MMO.Prototype.Editor
{
    public static class PrototypeSetup
    {
        public const string ScenePath = "Assets/Prototype/Scenes/PuppetSandbox.unity";
        private const string MaterialsPath = "Assets/Prototype/Materials";

        public const string WarriorScenePath = "Assets/Prototype/Scenes/WarriorSandbox.unity";

        [MenuItem("Prototype/Intégrer le guerrier dans une nouvelle scène")]
        public static void CreateWarriorSandbox()
        {
            if (File.Exists(WarriorScenePath))
            {
                EditorSceneManager.OpenScene(WarriorScenePath);
                return;
            }
            var source = EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.SaveScene(source, WarriorScenePath, true);
            var scene = EditorSceneManager.OpenScene(WarriorScenePath);
            Material targetMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/Target.mat");
            Material joint = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/Joints.mat");
            Material sword = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/Sword.mat");
            Material opponentMaterial = MaterialAsset("Opponent", new Color(0.64f, 0.38f, 0.34f));
            for (int i = 0; i < 3; i++)
            {
                var groupTarget = CreateTarget(new Vector3(-4f + (i - 1) * 0.9f, 0f, 10f + (i == 1 ? 0.3f : 0f)), targetMaterial, joint);
                groupTarget.gameObject.name = "Group Target " + (i + 1);
                groupTarget.displayName = "GROUPE " + (i + 1);
            }
            var opponent = new GameObject("Sparring Opponent");
            opponent.transform.position = new Vector3(4f, 0f, 9f);
            opponent.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var collider = opponent.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.94f, 0f);
            collider.size = new Vector3(0.6f, 1.86f, 0.5f);
            CreateRig(opponent.transform, opponentMaterial, joint, sword);
            var target = opponent.AddComponent<TrainingTarget>();
            target.maxHealth = 250;
            target.displayName = "DUEL";
            target.visual = opponent.transform.Find("Articulated Puppet");
            target.healthText = Text("Target Health", opponent.transform, new Vector3(0f, 2.35f, 0f), "DUEL", Color.white);
            target.damageText = Text("Damage", opponent.transform, new Vector3(0f, 2.15f, 0f), "", new Color(1f, 0.8f, 0.3f));
            opponent.AddComponent<SparringOpponent>();
            foreach (var rig in Object.FindObjectsByType<PuppetRig>(FindObjectsSortMode.None)) rig.BindWeapon();
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(WarriorScenePath, true) };
            PlayerSettings.productName = "MMO - Guerrier";
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            Debug.Log("Kit du guerrier intégré dans " + WarriorScenePath);
        }

        [MenuItem("Prototype/Créer la scène de test")]
        public static void CreateAndOpen()
        {
            Directory.CreateDirectory("Assets/Prototype/Scenes");
            Directory.CreateDirectory(MaterialsPath);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floor = MaterialAsset("Floor", new Color(0.39f, 0.42f, 0.45f));
            Material block = MaterialAsset("Obstacles", new Color(0.54f, 0.57f, 0.60f));
            Material line = MaterialAsset("Grid", new Color(0.31f, 0.34f, 0.37f));
            Material body = MaterialAsset("Puppet", new Color(0.80f, 0.82f, 0.83f));
            Material joint = MaterialAsset("Joints", new Color(0.19f, 0.22f, 0.25f));
            Material sword = MaterialAsset("Sword", new Color(0.74f, 0.77f, 0.80f), 0.45f);
            Material targetMaterial = MaterialAsset("Target", new Color(0.78f, 0.43f, 0.20f));

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.65f, 0.70f);
            RenderSettings.fog = false;
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            RenderSettings.sun = light;

            var arena = new GameObject("Test Arena").transform;
            Primitive("Ground", PrimitiveType.Cube, arena, new Vector3(0, -0.15f, 4), new Vector3(20, 0.3f, 22), floor, true);
            for (int x = -8; x <= 8; x += 2)
                Primitive("Grid X " + x, PrimitiveType.Cube, arena, new Vector3(x, 0.006f, 4), new Vector3(0.015f, 0.008f, 22), line);
            for (int z = -6; z <= 14; z += 2)
                Primitive("Grid Z " + z, PrimitiveType.Cube, arena, new Vector3(0, 0.006f, z), new Vector3(20, 0.008f, 0.015f), line);
            Primitive("Boundary Left", PrimitiveType.Cube, arena, new Vector3(-10, 0.35f, 4), new Vector3(0.3f, 0.7f, 22), block, true);
            Primitive("Boundary Right", PrimitiveType.Cube, arena, new Vector3(10, 0.35f, 4), new Vector3(0.3f, 0.7f, 22), block, true);
            Primitive("Boundary Back", PrimitiveType.Cube, arena, new Vector3(0, 0.35f, -7), new Vector3(20, 0.7f, 0.3f), block, true);
            Primitive("Boundary Front", PrimitiveType.Cube, arena, new Vector3(0, 0.35f, 15), new Vector3(20, 0.7f, 0.3f), block, true);
            Primitive("Jump Block", PrimitiveType.Cube, arena, new Vector3(-4, 0.4f, 3), new Vector3(2.5f, 0.8f, 2.5f), block, true);
            for (int i = 0; i < 3; i++)
                Primitive("Step " + (i + 1), PrimitiveType.Cube, arena, new Vector3(4, (i + 1) * 0.15f, 2.5f + i * 1.25f), new Vector3(2.5f, (i + 1) * 0.3f, 1.25f), block, true);

            var player = new GameObject("Puppet");
            player.layer = 2; // Ignore Raycast: camera collision and weapon queries ignore the player.
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.86f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0, 0.94f, 0);
            controller.stepOffset = 0.32f;
            controller.slopeLimit = 48f;
            controller.skinWidth = 0.035f;
            controller.minMoveDistance = 0f;
            CreateRig(player.transform, body, joint, sword);
            var motor = player.AddComponent<PuppetMotor>();
            CreateTarget(new Vector3(0f, 0f, 3.5f), targetMaterial, joint);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.23f, 0.26f, 0.30f);
            camera.transform.SetPositionAndRotation(new Vector3(0, 3.23f, -4.79f), Quaternion.Euler(23f, 0f, 0f));
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<OrbitCamera>().target = player.transform;
            new GameObject("Controls").AddComponent<PrototypeHelp>().player = motor;

            PlayerSettings.companyName = "Izoka";
            PlayerSettings.productName = "MMO - Pantin";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 40f;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1, 2), Quaternion.Euler(30, 0, 0), 10f);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            Debug.Log("Scène pantin prête : " + ScenePath);
        }

        private static void CreateRig(Transform parent, Material body, Material joint, Material sword)
        {
            var root = Pivot("Articulated Puppet", parent, Vector3.zero);
            var rig = root.gameObject.AddComponent<PuppetRig>();
            Primitive("Pelvis", PrimitiveType.Sphere, root, new Vector3(0, 0.94f, 0), new Vector3(0.38f, 0.25f, 0.27f), body);
            rig.torso = Pivot("Torso Pivot", root, new Vector3(0, 1.20f, 0));
            Primitive("Chest", PrimitiveType.Capsule, rig.torso, Vector3.zero, new Vector3(0.43f, 0.29f, 0.27f), body);
            Primitive("Neck", PrimitiveType.Sphere, rig.torso, new Vector3(0, 0.34f, 0), Vector3.one * 0.14f, joint);
            Primitive("Head", PrimitiveType.Sphere, rig.torso, new Vector3(0, 0.52f, 0), new Vector3(0.29f, 0.32f, 0.28f), body);
            Primitive("Facing Marker", PrimitiveType.Cube, rig.torso, new Vector3(0, 0.53f, 0.14f), new Vector3(0.14f, 0.045f, 0.025f), joint);
            rig.leftArm = CreateArm("Left", -1f, rig.torso, body, joint, out Transform leftElbow, out _);
            rig.rightArm = CreateArm("Right", 1f, rig.torso, body, joint, out Transform rightElbow, out Transform hand);
            rig.leftElbow = leftElbow;
            rig.rightElbow = rightElbow;
            rig.leftLeg = CreateLeg("Left", -1f, root, body, joint, out Transform leftKnee);
            rig.rightLeg = CreateLeg("Right", 1f, root, body, joint, out Transform rightKnee);
            rig.leftKnee = leftKnee;
            rig.rightKnee = rightKnee;
            var weapon = Pivot("Sword", hand, Vector3.zero);
            rig.weapon = weapon;
            Primitive("Grip", PrimitiveType.Cube, weapon, new Vector3(0, 0, 0.005f), new Vector3(0.07f, 0.06f, 0.20f), joint);
            Primitive("Guard", PrimitiveType.Cube, weapon, new Vector3(0, 0, 0.12f), new Vector3(0.23f, 0.05f, 0.045f), sword);
            Primitive("Blade", PrimitiveType.Cube, weapon, new Vector3(0, 0, 0.47f), new Vector3(0.055f, 0.027f, 0.67f), sword);
            var tip = Pivot("Sword Trail", weapon, new Vector3(0, 0, 0.76f));
            rig.swordTrail = tip.gameObject.AddComponent<TrailRenderer>();
            rig.swordTrail.sharedMaterial = TrailMaterial();
            rig.swordTrail.time = 0.13f;
            rig.swordTrail.startWidth = 0.12f;
            rig.swordTrail.endWidth = 0f;
            rig.swordTrail.startColor = new Color(1f, 0.88f, 0.55f, 0.8f);
            rig.swordTrail.endColor = new Color(1f, 0.88f, 0.55f, 0f);
            rig.swordTrail.emitting = false;
            rig.swordTrail.shadowCastingMode = ShadowCastingMode.Off;
            rig.Pose(0f, true, 0f, -1f, -1f, 0f);
        }

        private static Transform CreateArm(string name, float side, Transform parent, Material body, Material joint, out Transform elbow, out Transform hand)
        {
            var shoulder = Pivot(name + " Shoulder", parent, new Vector3(side * 0.30f, 0.22f, 0));
            Primitive("Shoulder Joint", PrimitiveType.Sphere, shoulder, Vector3.zero, Vector3.one * 0.18f, joint);
            Primitive("Upper Arm", PrimitiveType.Capsule, shoulder, new Vector3(0, -0.16f, 0), new Vector3(0.16f, 0.16f, 0.16f), body);
            elbow = Pivot(name + " Elbow", shoulder, new Vector3(0, -0.32f, 0));
            Primitive("Elbow Joint", PrimitiveType.Sphere, elbow, Vector3.zero, Vector3.one * 0.13f, joint);
            Primitive("Forearm", PrimitiveType.Capsule, elbow, new Vector3(0, -0.14f, 0), new Vector3(0.13f, 0.14f, 0.13f), body);
            hand = Pivot(name + " Hand", elbow, new Vector3(0, -0.30f, 0));
            Primitive("Hand", PrimitiveType.Sphere, hand, Vector3.zero, new Vector3(0.13f, 0.16f, 0.12f), joint);
            return shoulder;
        }

        private static Transform CreateLeg(string name, float side, Transform root, Material body, Material joint, out Transform knee)
        {
            var hip = Pivot(name + " Hip", root, new Vector3(side * 0.13f, 0.93f, 0));
            Primitive("Hip Joint", PrimitiveType.Sphere, hip, Vector3.zero, Vector3.one * 0.18f, joint);
            Primitive("Thigh", PrimitiveType.Capsule, hip, new Vector3(0, -0.205f, 0), new Vector3(0.21f, 0.205f, 0.21f), body);
            knee = Pivot(name + " Knee", hip, new Vector3(0, -0.43f, 0));
            Primitive("Knee Joint", PrimitiveType.Sphere, knee, Vector3.zero, Vector3.one * 0.16f, joint);
            Primitive("Shin", PrimitiveType.Capsule, knee, new Vector3(0, -0.19f, 0), new Vector3(0.16f, 0.19f, 0.16f), body);
            Primitive("Foot", PrimitiveType.Cube, knee, new Vector3(0, -0.435f, 0.07f), new Vector3(0.18f, 0.11f, 0.31f), joint);
            return hip;
        }

        private static TrainingTarget CreateTarget(Vector3 position, Material body, Material joint)
        {
            var root = new GameObject("Training Target");
            root.transform.position = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1f, 0);
            collider.size = new Vector3(0.8f, 2f, 0.55f);
            var target = root.AddComponent<TrainingTarget>();
            target.visual = Pivot("Target Visual", root.transform, Vector3.zero);
            Primitive("Base", PrimitiveType.Cylinder, target.visual, new Vector3(0, 0.1f, 0), new Vector3(0.8f, 0.1f, 0.8f), joint);
            Primitive("Post", PrimitiveType.Cylinder, target.visual, new Vector3(0, 0.8f, 0), new Vector3(0.13f, 0.7f, 0.13f), joint);
            Primitive("Body", PrimitiveType.Capsule, target.visual, new Vector3(0, 1.2f, 0), new Vector3(0.53f, 0.4f, 0.38f), body);
            Primitive("Arms", PrimitiveType.Cube, target.visual, new Vector3(0, 1.37f, 0), new Vector3(1.15f, 0.16f, 0.18f), body);
            Primitive("Head", PrimitiveType.Sphere, target.visual, new Vector3(0, 1.83f, 0), Vector3.one * 0.29f, body);
            target.healthText = Text("Target Health", root.transform, new Vector3(0, 2.35f, 0), "CIBLE\n100 / 100", Color.white);
            target.damageText = Text("Damage", root.transform, new Vector3(0, 2.15f, 0), "", new Color(1f, 0.8f, 0.3f));
            return target;
        }

        internal static TextMesh Text(string name, Transform parent, Vector3 position, string text, Color color)
        {
            var item = Pivot(name, parent, position).gameObject.AddComponent<TextMesh>();
            item.text = text;
            item.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            item.GetComponent<MeshRenderer>().sharedMaterial = item.font.material;
            item.fontSize = 64;
            item.characterSize = 0.014f;
            item.anchor = TextAnchor.MiddleCenter;
            item.alignment = TextAlignment.Center;
            item.color = color;
            return item;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            item.localPosition = position;
            return item;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool collides = false)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collides) Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        private static Material MaterialAsset(string name, Color color, float smoothness = 0.1f)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static Material TrailMaterial()
        {
            string path = MaterialsPath + "/SwordTrail.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}
