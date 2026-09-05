#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NPCGame.EditorTools
{
    /// <summary>
    /// Generates the sample scene together with its prefabs, placeholder sprites
    /// and dialogue assets. Everything it writes is an ordinary project asset, so
    /// once the scene exists this builder leaves it alone and hand edits survive.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        private const string PrefabDir = "Assets/Prefabs";
        private const string ArtDir = "Assets/Art";
        private const string DialogueDir = "Assets/Dialogue";
        private const string SceneDir = "Assets/Scenes";

        private const int InteractableLayer = 6;
        private const int SpritePixelSize = 32;

        // Room bounds, in world units.
        private const float RoomHalfWidth = 8f;
        private const float RoomHalfHeight = 5f;

        [MenuItem("NPC Game/Rebuild Sample Scene")]
        private static void RebuildSampleSceneMenu()
        {
            bool overwriting = File.Exists(ScenePath);
            if (overwriting && !Application.isBatchMode)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Rebuild Sample Scene",
                    $"This overwrites {ScenePath} and the generated prefabs. Any hand edits there are lost. Continue?",
                    "Rebuild",
                    "Cancel");

                if (!confirmed)
                {
                    return;
                }
            }

            BuildSampleScene();
        }

        /// <summary>Builds the scene only when it is missing. Safe to call repeatedly.</summary>
        public static void EnsureSampleScene()
        {
            if (!File.Exists(ScenePath))
            {
                BuildSampleScene();
            }
        }

        [InitializeOnLoadMethod]
        private static void AutoBuildOnFirstLoad()
        {
            // delayCall keeps this out of the asset import that triggered the domain reload.
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath))
                {
                    Debug.Log("[SceneBuilder] No sample scene found - generating one.");
                    BuildSampleScene();
                }
            };
        }

        public static void BuildSampleScene()
        {
            EnsureDirectories();
            EnsureInteractableLayer();
            EnsureTextMeshProResources();

            Sprite square = CreateSquareSprite("Square");
            Sprite circle = CreateCircleSprite("Circle");

            DialogueData elderDialogue = CreateDialogue(
                "Elder",
                "Elder Mira",
                "Ah, a new face in Willowbrook. You picked a quiet season to arrive.",
                "The well by the square has been running dry. Nobody can agree on why.",
                "Talk to Bram at the stall - he watches the roads, and the roads talk.");

            DialogueData merchantDialogue = CreateDialogue(
                "Merchant",
                "Bram the Trader",
                "Rope, lantern oil, dried figs. All fairly priced, mostly fresh.",
                "You are asking about the well? Caravans stopped coming through the north pass.",
                "No caravans, no water carts. The village has been drinking its reserves.");

            DialogueData guardDialogue = CreateDialogue(
                "Guard",
                "Watchman Odo",
                "Keep clear of the north gate. Orders.",
                "Something came down from the pass last week. We sealed it and said nothing.",
                "If the elder asks, you never heard that from me.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateRoom(square);

            GameObject promptInstance = CreateInteractionPromptPrefab();
            CreateDialogueSystemPrefab();

            GameObject player = CreatePlayerPrefab(square);
            WirePlayerToPrompt(player, promptInstance);

            GameObject npcPrefab = CreateNpcPrefab(circle);

            ConfigureNpcInstance(
                npcPrefab,
                "NPC_Elder",
                new Vector2(-4f, 2f),
                new Color(0.95f, 0.76f, 0.35f),
                elderDialogue,
                new[] { new Vector2(-6f, 2f), new Vector2(-2f, 2f) });

            ConfigureNpcInstance(
                npcPrefab,
                "NPC_Merchant",
                new Vector2(4f, 1.5f),
                new Color(0.45f, 0.78f, 0.55f),
                merchantDialogue,
                null);

            ConfigureNpcInstance(
                npcPrefab,
                "NPC_Guard",
                new Vector2(0f, 3.5f),
                new Color(0.62f, 0.55f, 0.85f),
                guardDialogue,
                new[] { new Vector2(-2f, 3.5f), new Vector2(2f, 3.5f), new Vector2(2f, 0.5f) });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] Generated {ScenePath} with prefabs in {PrefabDir}. Press Play to try it.");
        }

        // ---------------------------------------------------------------- setup

        private static void EnsureDirectories()
        {
            foreach (string dir in new[] { SceneDir, PrefabDir, ArtDir, DialogueDir })
            {
                Directory.CreateDirectory(dir);
            }

            AssetDatabase.Refresh();
        }

        private static void EnsureInteractableLayer()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[SceneBuilder] Could not open TagManager.asset - create an 'Interactable' layer manually.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || layers.arraySize <= InteractableLayer)
            {
                return;
            }

            SerializedProperty slot = layers.GetArrayElementAtIndex(InteractableLayer);
            if (slot.stringValue != "Interactable")
            {
                slot.stringValue = "Interactable";
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// TMP needs its essential resources (the default font asset) before any text
        /// renders. In the Editor that is normally a one-click import prompt, which
        /// never appears in batch mode, so try to trigger it directly.
        /// </summary>
        private static void EnsureTextMeshProResources()
        {
            try
            {
                if (TMP_Settings.defaultFontAsset != null)
                {
                    return;
                }
            }
            catch (System.Exception)
            {
                // TMP_Settings itself is part of the essential resources.
            }

            System.Type importer = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                importer = assembly.GetType("TMPro.TMP_PackageResourceImporter");
                if (importer != null)
                {
                    break;
                }
            }

            var method = importer?.GetMethod(
                "ImportResources",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (method != null && method.GetParameters().Length == 3)
            {
                try
                {
                    method.Invoke(null, new object[] { true, false, false });
                    AssetDatabase.Refresh();
                    Debug.Log("[SceneBuilder] Imported TMP essential resources.");
                    return;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SceneBuilder] Automatic TMP resource import failed: {e.Message}");
                }
            }

            Debug.LogWarning(
                "[SceneBuilder] TextMeshPro essential resources are missing - dialogue text will render blank. " +
                "Import them via Window > TextMeshPro > Import TMP Essential Resources.");
        }

        // --------------------------------------------------------------- sprites

        private static Sprite CreateSquareSprite(string assetName)
        {
            return CreateSprite(assetName, (x, y) => true);
        }

        private static Sprite CreateCircleSprite(string assetName)
        {
            float radius = SpritePixelSize * 0.5f;
            float center = radius - 0.5f;

            return CreateSprite(assetName, (x, y) =>
            {
                float dx = x - center;
                float dy = y - center;
                return (dx * dx) + (dy * dy) <= radius * radius;
            });
        }

        private static Sprite CreateSprite(string assetName, System.Func<int, int, bool> isInside)
        {
            string path = $"{ArtDir}/{assetName}.png";

            if (!File.Exists(path))
            {
                var texture = new Texture2D(SpritePixelSize, SpritePixelSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[SpritePixelSize * SpritePixelSize];

                for (int y = 0; y < SpritePixelSize; y++)
                {
                    for (int x = 0; x < SpritePixelSize; x++)
                    {
                        bool inside = isInside(x, y);
                        pixels[(y * SpritePixelSize) + x] = inside
                            ? new Color32(255, 255, 255, 255)
                            : new Color32(255, 255, 255, 0);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureSpriteImporter(path);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureSpriteImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // One sprite covers exactly one world unit.
            importer.spritePixelsPerUnit = SpritePixelSize;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // -------------------------------------------------------------- dialogue

        private static DialogueData CreateDialogue(string assetName, string npcName, params string[] lines)
        {
            string path = $"{DialogueDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (existing != null)
            {
                return existing;
            }

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.npcName = npcName;
            data.lines = lines;

            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        // ----------------------------------------------------------- scene parts

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            // Shows the whole room at 16:9 without needing a follow camera.
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.12f, 0.16f);
        }

        private static void CreateRoom(Sprite square)
        {
            var room = new GameObject("Room");

            CreateFloor(square, room.transform);

            const float thickness = 1f;
            CreateWall(square, room.transform, "Wall_Top", new Vector2(0f, RoomHalfHeight + (thickness * 0.5f)), new Vector2((RoomHalfWidth * 2f) + (thickness * 2f), thickness));
            CreateWall(square, room.transform, "Wall_Bottom", new Vector2(0f, -RoomHalfHeight - (thickness * 0.5f)), new Vector2((RoomHalfWidth * 2f) + (thickness * 2f), thickness));
            CreateWall(square, room.transform, "Wall_Left", new Vector2(-RoomHalfWidth - (thickness * 0.5f), 0f), new Vector2(thickness, RoomHalfHeight * 2f));
            CreateWall(square, room.transform, "Wall_Right", new Vector2(RoomHalfWidth + (thickness * 0.5f), 0f), new Vector2(thickness, RoomHalfHeight * 2f));
        }

        private static void CreateFloor(Sprite square, Transform parent)
        {
            var floor = new GameObject("Floor", typeof(SpriteRenderer));
            floor.transform.SetParent(parent, false);
            floor.transform.localScale = new Vector3(RoomHalfWidth * 2f, RoomHalfHeight * 2f, 1f);

            var renderer = floor.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(0.20f, 0.22f, 0.27f);
            renderer.sortingOrder = -10;
        }

        private static void CreateWall(Sprite square, Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = wall.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(0.33f, 0.35f, 0.42f);
            renderer.sortingOrder = -5;
        }

        // ------------------------------------------------------------- prefabs

        private static GameObject CreatePlayerPrefab(Sprite square)
        {
            var player = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(BoxCollider2D));
            player.transform.position = new Vector3(0f, -2.5f, 0f);

            var renderer = player.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(0.36f, 0.68f, 0.95f);
            renderer.sortingOrder = 10;

            var body = player.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            player.AddComponent<PlayerController>();

            var interactor = player.AddComponent<PlayerInteractor>();
            SetProperty(interactor, "interactableLayer", p => p.intValue = 1 << InteractableLayer);

            // Connect turns `player` itself into an instance of the saved prefab, so the
            // scene object is what callers need to keep wiring - not the returned asset.
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, $"{PrefabDir}/Player.prefab", InteractionMode.AutomatedAction);
            return player;
        }

        private static GameObject CreateNpcPrefab(Sprite circle)
        {
            var npc = new GameObject("NPC", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D));
            npc.layer = InteractableLayer;

            var renderer = npc.GetComponent<SpriteRenderer>();
            renderer.sprite = circle;
            renderer.sortingOrder = 10;

            var body = npc.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            npc.AddComponent<NPCController>();
            npc.AddComponent<NPCDialogueTrigger>();

            // Dialogue data and waypoints are per-instance, so they stay off the prefab.
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(npc, $"{PrefabDir}/NPC.prefab");
            Object.DestroyImmediate(npc);
            return prefab;
        }

        private static void ConfigureNpcInstance(
            GameObject prefab,
            string name,
            Vector2 position,
            Color color,
            DialogueData dialogue,
            Vector2[] patrolPoints)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.position = position;
            instance.GetComponent<SpriteRenderer>().color = color;

            SetProperty(instance.GetComponent<NPCDialogueTrigger>(), "dialogueData", p => p.objectReferenceValue = dialogue);

            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                return;
            }

            var waypointRoot = new GameObject($"{name}_Waypoints");
            waypointRoot.transform.position = Vector3.zero;

            var transforms = new Transform[patrolPoints.Length];
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                var waypoint = new GameObject($"Waypoint_{i + 1}");
                waypoint.transform.SetParent(waypointRoot.transform, false);
                waypoint.transform.position = patrolPoints[i];
                transforms[i] = waypoint.transform;
            }

            SetProperty(instance.GetComponent<NPCController>(), "waypoints", p =>
            {
                p.arraySize = transforms.Length;
                for (int i = 0; i < transforms.Length; i++)
                {
                    p.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
                }
            });
        }

        private static GameObject CreateInteractionPromptPrefab()
        {
            var promptObject = new GameObject("InteractionPrompt", typeof(Canvas));
            var canvas = promptObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            var canvasRect = promptObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400f, 60f);
            canvasRect.localScale = Vector3.one * 0.01f;

            // Kept as a child so toggling it never disables the component itself.
            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(promptObject.transform, false);
            StretchToParent(root.GetComponent<RectTransform>());

            var label = CreateText(root.transform, "Label", 36f, TextAlignmentOptions.Center);
            StretchToParent(label.rectTransform);
            label.text = "Press E";

            var prompt = promptObject.AddComponent<InteractionPrompt>();
            SetProperty(prompt, "promptRoot", p => p.objectReferenceValue = root);
            SetProperty(prompt, "promptText", p => p.objectReferenceValue = label);

            PrefabUtility.SaveAsPrefabAssetAndConnect(promptObject, $"{PrefabDir}/InteractionPrompt.prefab", InteractionMode.AutomatedAction);
            return promptObject;
        }

        private static void CreateDialogueSystemPrefab()
        {
            var canvasObject = new GameObject("DialogueSystem", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.06f, 0.04f);
            panelRect.anchorMax = new Vector2(0.94f, 0.30f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.92f);

            var speakerName = CreateText(panel.transform, "SpeakerNameText", 40f, TextAlignmentOptions.TopLeft);
            var speakerRect = speakerName.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.sizeDelta = new Vector2(-64f, 56f);
            speakerRect.anchoredPosition = new Vector2(0f, -16f);
            speakerName.color = new Color(0.98f, 0.82f, 0.45f);
            speakerName.text = "Speaker";

            var dialogueText = CreateText(panel.transform, "DialogueText", 34f, TextAlignmentOptions.TopLeft);
            var dialogueRect = dialogueText.rectTransform;
            dialogueRect.anchorMin = Vector2.zero;
            dialogueRect.anchorMax = Vector2.one;
            dialogueRect.offsetMin = new Vector2(32f, 24f);
            dialogueRect.offsetMax = new Vector2(-32f, -80f);
            dialogueText.text = "Dialogue line";

            var hint = CreateText(panel.transform, "AdvanceHint", 24f, TextAlignmentOptions.BottomRight);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(-64f, 32f);
            hintRect.anchoredPosition = new Vector2(0f, 12f);
            hint.color = new Color(0.65f, 0.68f, 0.75f);
            hint.text = "[E] continue";

            var manager = canvasObject.AddComponent<DialogueManager>();
            SetProperty(manager, "dialoguePanel", p => p.objectReferenceValue = panel);
            SetProperty(manager, "speakerNameText", p => p.objectReferenceValue = speakerName);
            SetProperty(manager, "dialogueText", p => p.objectReferenceValue = dialogueText);

            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasObject, $"{PrefabDir}/DialogueSystem.prefab", InteractionMode.AutomatedAction);

            // Present for future UI that needs pointer input; harmless for the key-driven flow.
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void WirePlayerToPrompt(GameObject player, GameObject promptInstance)
        {
            var interactor = player.GetComponent<PlayerInteractor>();
            var prompt = promptInstance.GetComponent<InteractionPrompt>();
            SetProperty(interactor, "prompt", p => p.objectReferenceValue = prompt);
        }

        // --------------------------------------------------------------- helpers

        private static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;

            return text;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Assigns a [SerializeField] private field, which is only reachable through
        /// the serialization API from outside the declaring class.
        /// </summary>
        private static void SetProperty(Object target, string propertyPath, System.Action<SerializedProperty> assign)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyPath);

            if (property == null)
            {
                Debug.LogWarning($"[SceneBuilder] {target.GetType().Name} has no serialized field '{propertyPath}'.");
                return;
            }

            assign(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
