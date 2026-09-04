using UnityEngine;
using UnityEditor;
using System.IO;

public static class ResourceSyncTool
{
    [MenuItem("Stone Skipping/🔄 Sync Resources for Build")]
    public static void SyncResources()
    {
        string resFolder = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // 1. In-game Objects / Decor Prefabs
        SyncAsset("Assets/prefab/BG_Deco/BoostPad.prefab", "Assets/Resources/BoostPad.prefab");
        SyncAsset("Assets/prefab/BG_Deco/ObstacleRock.prefab", "Assets/Resources/ObstacleRock.prefab");
        SyncAsset("Assets/prefab/BG_Deco/TargetZone.prefab", "Assets/Resources/TargetZone.prefab");
        SyncAsset("Assets/prefab/BG_Deco/FriendFlag.prefab", "Assets/Resources/FriendFlag.prefab");
        SyncAsset("Assets/prefab/BG_Deco/LilyPadCluster.prefab", "Assets/Resources/LilyPadCluster.prefab");
        SyncAsset("Assets/prefab/Lakeside_WoodenPier.prefab", "Assets/Resources/Lakeside_WoodenPier.prefab");

        // 2. Lily Pads / Lotus Foliage (5 Variations)
        SyncAsset("Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_LilyPad1.prefab", "Assets/Resources/LilyPad_1.prefab");
        SyncAsset("Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_LilyPad2.prefab", "Assets/Resources/LilyPad_2.prefab");
        SyncAsset("Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_LilyPad3.prefab", "Assets/Resources/LilyPad_3.prefab");
        SyncAsset("Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_LilyPadCluster1.prefab", "Assets/Resources/LilyPad_4.prefab");
        SyncAsset("Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_LilyPadCluster2.prefab", "Assets/Resources/LilyPad_5.prefab");

        // 3. Random Ring
        SyncAsset("Assets/3D/Ingame_Object/Random_Ring.fbx", "Assets/Resources/Random_Ring.fbx");

        // 4. Character Animator Controller
        SyncAsset("Assets/3D/Character/Test_Chr_CTRL.controller", "Assets/Resources/Test_Chr_CTRL.controller");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ [ResourceSyncTool] Resources 동기화가 성공적으로 완료되었습니다!");
    }

    private static void SyncAsset(string srcPath, string destPath)
    {
        if (!File.Exists(srcPath))
        {
            Debug.LogWarning($"[ResourceSyncTool] 소스 파일이 존재하지 않습니다: {srcPath}");
            return;
        }

        if (File.Exists(destPath))
        {
            AssetDatabase.DeleteAsset(destPath);
        }

        AssetDatabase.CopyAsset(srcPath, destPath);
    }
}
