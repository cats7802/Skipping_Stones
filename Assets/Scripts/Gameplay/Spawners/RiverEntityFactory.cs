using UnityEngine;

namespace SkippingStones.Gameplay.Spawners
{
    /// <summary>
    /// 🏭 수면 엔티티(10종 물고기, 부스트 패드, 랜덤 링, 바위, 과녁, 깃발, 연잎) 프리팹 인스턴스화 팩토리
    /// </summary>
    public class RiverEntityFactory
    {
        public GameObject boostPadPrefab;
        public GameObject randomRingPrefab;
        public GameObject obstacleRockPrefab;
        public GameObject targetZonePrefab;
        public GameObject friendFlagPrefab;
        public GameObject[] fishPrefabs = new GameObject[10];
        public GameObject lilyPadClusterPrefab;

        public void EnsurePrefabsLoaded()
        {
#if UNITY_EDITOR
            if (boostPadPrefab == null) boostPadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/BoostPad.prefab");
            if (randomRingPrefab == null) randomRingPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Ingame_Object/Random_Ring.fbx");
            if (obstacleRockPrefab == null) obstacleRockPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/ObstacleRock.prefab");
            if (targetZonePrefab == null) targetZonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/TargetZone.prefab");
            if (friendFlagPrefab == null) friendFlagPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/FriendFlag.prefab");
            if (lilyPadClusterPrefab == null) lilyPadClusterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/LilyPadCluster.prefab");

            for (int i = 1; i <= 10; i++)
            {
                if (fishPrefabs[i - 1] == null)
                {
                    string p = $"Assets/Resources/FishPrefabs/River_Fish_{i:D2}.prefab";
                    fishPrefabs[i - 1] = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
                }
            }
#endif
            if (boostPadPrefab == null) boostPadPrefab = Resources.Load<GameObject>("BoostPad");
            if (randomRingPrefab == null) randomRingPrefab = Resources.Load<GameObject>("Random_Ring");
            if (obstacleRockPrefab == null) obstacleRockPrefab = Resources.Load<GameObject>("ObstacleRock");
            if (targetZonePrefab == null) targetZonePrefab = Resources.Load<GameObject>("TargetZone");
            if (friendFlagPrefab == null) friendFlagPrefab = Resources.Load<GameObject>("FriendFlag");
            if (lilyPadClusterPrefab == null) lilyPadClusterPrefab = Resources.Load<GameObject>("LilyPadCluster");

            for (int i = 1; i <= 10; i++)
            {
                if (fishPrefabs[i - 1] == null)
                {
                    fishPrefabs[i - 1] = Resources.Load<GameObject>($"FishPrefabs/River_Fish_{i:D2}");
                }
            }
        }

        public GameObject CreateTargetRing(Transform parent, Vector3 pos)
        {
            EnsurePrefabsLoaded();
            if (targetZonePrefab != null)
            {
                GameObject obj = Object.Instantiate(targetZonePrefab, pos, Quaternion.identity, parent);
                obj.name = $"TargetZone_{pos.x:F0}x{pos.z:F0}";
                return obj;
            }
            else
            {
                GameObject ring = new GameObject($"TargetZone_{pos.x:F0}x{pos.z:F0}");
                ring.transform.SetParent(parent);
                ring.transform.position = pos;
                ring.AddComponent<FloatingTargetZone>();
                return ring;
            }
        }

        public GameObject CreateBoostPad(Transform parent, Vector3 pos, Quaternion rot)
        {
            EnsurePrefabsLoaded();
            if (boostPadPrefab != null)
            {
                GameObject obj = Object.Instantiate(boostPadPrefab, pos, rot, parent);
                obj.name = $"BoostPad_{pos.x:F0}x{pos.z:F0}";
                return obj;
            }
            else
            {
                GameObject pad = new GameObject($"BoostPad_{pos.x:F0}x{pos.z:F0}");
                pad.transform.SetParent(parent);
                pad.transform.position = pos;
                pad.transform.rotation = rot;
                pad.AddComponent<BoostPad>();
                return pad;
            }
        }

        public GameObject CreateRandomRing(Transform parent, Vector3 pos, Quaternion rot)
        {
            EnsurePrefabsLoaded();
            GameObject ringObj = new GameObject($"RandomRing_{pos.x:F0}x{pos.z:F0}");
            ringObj.transform.SetParent(parent);
            ringObj.transform.position = pos;
            ringObj.transform.rotation = rot;
            ringObj.AddComponent<SkippingStones.Arcade.RandomRing>();
            return ringObj;
        }

        public GameObject CreateObstacleRock(Transform parent, Vector3 pos)
        {
            EnsurePrefabsLoaded();
            if (obstacleRockPrefab != null)
            {
                GameObject obj = Object.Instantiate(obstacleRockPrefab, pos, Quaternion.identity, parent);
                obj.name = $"ObstacleRock_{pos.x:F0}x{pos.z:F0}";
                return obj;
            }
            else
            {
                GameObject rock = new GameObject($"ObstacleRock_{pos.x:F0}x{pos.z:F0}");
                rock.transform.SetParent(parent);
                rock.transform.position = pos;
                rock.AddComponent<ObstacleRock>();
                return rock;
            }
        }

        public GameObject CreateFriendFlag(Transform parent, Vector3 pos, string name, string rank, float targetDist)
        {
            EnsurePrefabsLoaded();
            GameObject flagObj;
            if (friendFlagPrefab != null)
            {
                flagObj = Object.Instantiate(friendFlagPrefab, pos, Quaternion.identity, parent);
                flagObj.name = $"FriendFlag_{name}_{pos.z:F0}";
            }
            else
            {
                flagObj = new GameObject($"FriendFlag_{name}_{pos.z:F0}");
                flagObj.transform.SetParent(parent);
                flagObj.transform.position = pos;
                flagObj.AddComponent<FriendFlag>();
            }

            FriendFlag ff = flagObj.GetComponent<FriendFlag>();
            if (ff != null)
            {
                ff.friendName = name;
                ff.rankText = rank;
                ff.targetDistance = targetDist;
            }
            return flagObj;
        }

        public GameObject SpawnSingleFish(Transform parent, Vector3 pos, float dist)
        {
            EnsurePrefabsLoaded();

            int minFishIdx = 0;
            int maxFishIdx = 9;

            if (dist < 150f)
            {
                minFishIdx = 0; // 버들치
                maxFishIdx = 3; // 은어
            }
            else if (dist < 400f)
            {
                minFishIdx = 1; // 피라미
                maxFishIdx = 6; // 쏘가리
            }
            else if (dist < 800f)
            {
                minFishIdx = 3; // 은어
                maxFishIdx = 8; // 무지개송어
            }
            else
            {
                minFishIdx = 4; // 산천어
                maxFishIdx = 9; // 강준치
            }

            int chosenIdx = Random.Range(minFishIdx, maxFishIdx + 1);
            GameObject chosenPrefab = (fishPrefabs != null && chosenIdx < fishPrefabs.Length) 
                ? fishPrefabs[chosenIdx] 
                : null;

            GameObject fishObj;
            if (chosenPrefab != null)
            {
                fishObj = Object.Instantiate(chosenPrefab, pos, Quaternion.identity, parent);
                fishObj.name = $"Fish_{chosenIdx + 1:D2}_{pos.x:F0}x{pos.z:F0}";
            }
            else
            {
                fishObj = new GameObject($"Fish_{chosenIdx + 1:D2}_{pos.x:F0}x{pos.z:F0}");
                fishObj.transform.SetParent(parent);
                fishObj.transform.position = pos;
                fishObj.AddComponent<JumpingFish>();
            }

            JumpingFish jf = fishObj.GetComponent<JumpingFish>();
            if (jf != null)
            {
                FishSpeciesData preset = FishPresetDatabase.GetPreset(chosenIdx + 1);
                jf.fishIndex = preset.index;
                jf.speciesId = preset.id;
                jf.speciesName = preset.nameKor;
                jf.jumpHeight = Random.Range(preset.minJumpHeight, preset.maxJumpHeight);
                jf.jumpDuration = preset.jumpDuration;
                float randomVariation = Random.Range(0.95f, 1.1f);
                jf.scaleFactor = Mathf.Clamp(preset.scaleFactor * randomVariation, 1.0f, 2.6f);
                jf.rewardCoins = preset.rewardCoins;
            }
            return fishObj;
        }

        public GameObject SpawnSingleLilyCluster(Transform parent, Vector3 centerPos)
        {
            EnsurePrefabsLoaded();
            if (lilyPadClusterPrefab != null)
            {
                GameObject cluster = Object.Instantiate(lilyPadClusterPrefab, centerPos, Quaternion.identity, parent);
                cluster.name = $"LilyCluster_{centerPos.x:F0}x{centerPos.z:F0}";
                return cluster;
            }
            return null;
        }
    }
}
