using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.U2D.Sprites;

public static class FishSetupTool
{
    [MenuItem("Tools/Fish/1. Slice 2D River Fishes Sprite")]
    public static void SliceRiverFishesSprite()
    {
        string path = "Assets/2D/River_Fishes.png";
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        UnityEngine.Object targetObj = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (targetObj == null) targetObj = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(targetObj);
        
        if (dataProvider != null)
        {
            dataProvider.InitSpriteEditorDataProvider();
            var spriteRects = new List<SpriteRect>();

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float fw = tex != null ? tex.width : 1024;
            float fh = tex != null ? tex.height : 1024;

            // 10 fish slices
            spriteRects.Add(new SpriteRect { name = "Fish_01_ChineseMinnow", spriteID = GUID.Generate(), rect = new Rect(0.08f * fw, 0.88f * fh, 0.28f * fw, 0.10f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_02_PaleChub", spriteID = GUID.Generate(), rect = new Rect(0.62f * fw, 0.86f * fh, 0.30f * fw, 0.11f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_03_DarkChub", spriteID = GUID.Generate(), rect = new Rect(0.09f * fw, 0.75f * fh, 0.32f * fw, 0.12f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_04_Ayu", spriteID = GUID.Generate(), rect = new Rect(0.07f * fw, 0.63f * fh, 0.36f * fw, 0.11f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_05_MasuTrout", spriteID = GUID.Generate(), rect = new Rect(0.55f * fw, 0.67f * fh, 0.42f * fw, 0.13f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_06_KoreanChub", spriteID = GUID.Generate(), rect = new Rect(0.07f * fw, 0.48f * fh, 0.38f * fw, 0.12f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_07_MandarinFish", spriteID = GUID.Generate(), rect = new Rect(0.52f * fw, 0.49f * fh, 0.44f * fw, 0.13f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_08_LargemouthBass", spriteID = GUID.Generate(), rect = new Rect(0.02f * fw, 0.25f * fh, 0.48f * fw, 0.18f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_09_RainbowTrout", spriteID = GUID.Generate(), rect = new Rect(0.51f * fw, 0.28f * fh, 0.46f * fw, 0.16f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
            spriteRects.Add(new SpriteRect { name = "Fish_10_PredaceousCarp", spriteID = GUID.Generate(), rect = new Rect(0.20f * fw, 0.03f * fh, 0.60f * fw, 0.18f * fh), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });

            dataProvider.SetSpriteRects(spriteRects.ToArray());
            dataProvider.Apply();

            var assetImporter = dataProvider.targetObject as AssetImporter;
            if (assetImporter != null)
            {
                assetImporter.SaveAndReimport();
            }
            Debug.Log("[FishSetupTool] Sliced River_Fishes.png using modern ISpriteEditorDataProvider!");
        }
    }

    [MenuItem("Tools/Fish/2. Build 10 Fish Prefabs & Animator Controllers")]
    public static void BuildFishPrefabs()
    {
        string prefabsDir = "Assets/Resources/FishPrefabs";
        if (!Directory.Exists(prefabsDir)) Directory.CreateDirectory(prefabsDir);

        string animCtrlDir = "Assets/3D/Ingame_Object/River_Fish/Animators";
        if (!Directory.Exists(animCtrlDir)) Directory.CreateDirectory(animCtrlDir);

        // Load 2D sprites
        Sprite[] sprites = Resources.LoadAll<Sprite>("River_Fishes");
        if (sprites == null || sprites.Length == 0)
        {
            Object[] allObjs = AssetDatabase.LoadAllAssetsAtPath("Assets/2D/River_Fishes.png");
            List<Sprite> sList = new List<Sprite>();
            foreach (var o in allObjs) if (o is Sprite s) sList.Add(s);
            sprites = sList.ToArray();
        }

        for (int i = 1; i <= 10; i++)
        {
            string idxStr = i.ToString("D2");
            string idlePath = $"Assets/3D/Ingame_Object/River_Fish/River_Fish_{idxStr}_Idle.fbx";
            string runPath = $"Assets/3D/Ingame_Object/River_Fish/River_Fish_{idxStr}_Run.fbx";

            GameObject idleFBX = AssetDatabase.LoadAssetAtPath<GameObject>(idlePath);

            if (idleFBX == null)
            {
                Debug.LogWarning($"Missing idle FBX for fish {i} at {idlePath}");
                continue;
            }

            // Create or get Runtime Animator Controller
            string ctrlPath = $"{animCtrlDir}/Fish_{idxStr}_CTRL.controller";
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            var idleState = rootStateMachine.AddState("Idle");
            var runState = rootStateMachine.AddState("Run");

            // Extract clips from FBX
            Object[] idleAssets = AssetDatabase.LoadAllAssetsAtPath(idlePath);
            foreach (var a in idleAssets)
            {
                if (a is AnimationClip c && !c.name.Contains("__preview__"))
                {
                    idleState.motion = c;
                    break;
                }
            }

            Object[] runAssets = AssetDatabase.LoadAllAssetsAtPath(runPath);
            foreach (var a in runAssets)
            {
                if (a is AnimationClip c && !c.name.Contains("__preview__"))
                {
                    runState.motion = c;
                    break;
                }
            }

            // Parameters & Transitions
            controller.AddParameter("isSwimming", AnimatorControllerParameterType.Bool);
            var toRun = idleState.AddTransition(runState);
            toRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "isSwimming");
            toRun.duration = 0.25f;
            toRun.hasExitTime = false;

            var toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "isSwimming");
            toIdle.duration = 0.25f;
            toIdle.hasExitTime = false;

            // Instantiate root gameobject for Prefab
            GameObject rootObj = new GameObject($"River_Fish_{idxStr}");
            GameObject modelInst = Object.Instantiate(idleFBX, rootObj.transform);
            modelInst.name = "Model";
            modelInst.transform.localPosition = Vector3.zero;
            modelInst.transform.localRotation = Quaternion.identity;

            Animator anim = modelInst.GetComponent<Animator>();
            if (anim == null) anim = modelInst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            JumpingFish jf = rootObj.AddComponent<JumpingFish>();
            FishSpeciesData preset = FishPresetDatabase.GetPreset(i);
            jf.fishIndex = i;
            jf.speciesId = preset.id;
            jf.speciesName = preset.nameKor;
            jf.jumpHeight = (preset.minJumpHeight + preset.maxJumpHeight) * 0.5f;
            jf.jumpDuration = preset.jumpDuration;
            jf.scaleFactor = preset.scaleFactor;
            jf.rewardCoins = preset.rewardCoins;
            jf.animator = anim;

            if (sprites != null && i - 1 < sprites.Length)
            {
                jf.bookSprite = sprites[i - 1];
            }

            // Save Prefab
            string prefabPath = $"{prefabsDir}/River_Fish_{idxStr}.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootObj, prefabPath);
            Object.DestroyImmediate(rootObj);

            Debug.Log($"[FishSetupTool] Created Prefab at {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FishSetupTool] Successfully built 10 River Fish Prefabs and Animators!");
    }
}
