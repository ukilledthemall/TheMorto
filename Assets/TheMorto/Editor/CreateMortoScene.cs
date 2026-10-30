#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheMorto.Editor
{
    public static class CreateMortoScene
    {
        const string Root = "Assets/TheMorto";
        [MenuItem("The Morto/Create Playable Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode before creating the scene."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh();
            string[] art = { "living.jpg", "hallway.jpg", "kitchen.jpg", "fence.jpg", "sideyard.jpg", "porch.jpg", "morto.png", "portrait.jpg", "scare.jpg", "morto_living.png", "morto_hallway.png", "morto_kitchen.png", "morto_fence.png", "morto_sideyard.png", "morto_porch.png" };
            string[] audio = { "hum", "hoof", "camera", "flash", "move", "scare", "win", "knock", "breathe" };
            foreach (string name in art)
            {
                string artPath = Root + "/Art/" + name;
                TextureImporter importer = AssetImporter.GetAtPath(artPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing image file: " + artPath);
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.alphaIsTransparency = name.EndsWith(".png");
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Require<Texture2D>("Art/" + name);
            }
            foreach (string name in audio) Require<AudioClip>("Audio/" + name + ".wav");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            cameraObject.transform.position = new Vector3(0, 0, -10); cameraObject.AddComponent<AudioListener>();
            var root = new GameObject("The Morto - Select to tune gameplay and replace sounds");
            var controller = root.AddComponent<MortoController>();
            var view = root.AddComponent<MortoView>();
            view.living = Load<Texture2D>("Art/living.jpg");
            view.hallway = Load<Texture2D>("Art/hallway.jpg");
            view.kitchen = Load<Texture2D>("Art/kitchen.jpg");
            view.fence = Load<Texture2D>("Art/fence.jpg");
            view.sideyard = Load<Texture2D>("Art/sideyard.jpg");
            view.porch = Load<Texture2D>("Art/porch.jpg");
            view.morto = Load<Texture2D>("Art/morto.png");
            view.portrait = Load<Texture2D>("Art/portrait.jpg");
            view.scare = Load<Texture2D>("Art/scare.jpg");
            view.mortoLiving = Load<Texture2D>("Art/morto_living.png");
            view.mortoHallway = Load<Texture2D>("Art/morto_hallway.png");
            view.mortoKitchen = Load<Texture2D>("Art/morto_kitchen.png");
            view.mortoFence = Load<Texture2D>("Art/morto_fence.png");
            view.mortoSideyard = Load<Texture2D>("Art/morto_sideyard.png");
            view.mortoPorch = Load<Texture2D>("Art/morto_porch.png");
            controller.hum = Load<AudioClip>("Audio/hum.wav");
            controller.hoof = Load<AudioClip>("Audio/hoof.wav");
            controller.cameraSound = Load<AudioClip>("Audio/camera.wav");
            controller.flashSound = Load<AudioClip>("Audio/flash.wav");
            controller.moveSound = Load<AudioClip>("Audio/move.wav");
            controller.scare = Load<AudioClip>("Audio/scare.wav");
            controller.win = Load<AudioClip>("Audio/win.wav");
            controller.knock = Load<AudioClip>("Audio/knock.wav");
            controller.breathe = Load<AudioClip>("Audio/breathe.wav");
            if (!AssetDatabase.IsValidFolder(Root + "/Scenes")) AssetDatabase.CreateFolder(Root, "Scenes");
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Scenes/TheMorto.unity");
            EditorSceneManager.SaveScene(scene, path); Selection.activeGameObject = root;
            Debug.Log("The Morto scene is ready. Open Game view in 16:9 and press Play. " + path);
        }
        static T Load<T>(string path) where T : UnityEngine.Object { return AssetDatabase.LoadAssetAtPath<T>(Root + "/" + path); }
        static void Require<T>(string path) where T : UnityEngine.Object
        {
            if (Load<T>(path) == null) throw new InvalidOperationException("Missing asset: " + Root + "/" + path + ". Import the complete package first.");
        }
        [MenuItem("The Morto/Apply Balance To Current Scene")]
        public static void ApplyBalance()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode before applying balance."); return; }
            var controller = UnityEngine.Object.FindObjectOfType<MortoController>();
            if (controller == null) { Debug.LogWarning("Open your Morto game scene first."); return; }
            Undo.RecordObject(controller, "Apply Morto awareness balance");
            controller.tuning.cameraDrain = 1.8f;
            controller.tuning.lureSuccessChance = .7f;
            controller.tuning.lureCost = 6; controller.tuning.lureCooldown = 10; controller.tuning.lureHold = 5;
            controller.tuning.awarenessGain = 33.6f;
            controller.tuning.awarenessDecay = 12;
            controller.tuning.hidingAwarenessDrop = 35;
            controller.tuning.roomChangeAwarenessDrop = 50;
            controller.tuning.awarenessThreshold = 100;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log("Morto awareness balanced. Save this scene. Your assigned sounds and progress are preserved.");
        }
        [MenuItem("The Morto/Reset Saved Progress")]
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("TheMorto.CompletedNights"); PlayerPrefs.DeleteKey("TheMorto.UnlockedNight"); PlayerPrefs.DeleteKey("TheMorto.Completed"); PlayerPrefs.Save();
            Debug.Log("The Morto progress reset. Restart Play mode to refresh the menu.");
        }
    }
}
#endif
