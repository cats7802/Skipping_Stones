using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using SkippingStones.Data;

namespace SkippingStones.Editor
{
    [CustomEditor(typeof(StoneCatalogManager))]
    public class StoneCatalogManagerEditor : UnityEditor.Editor
    {
        private StoneCatalogManager manager;
        private Dictionary<int, bool> editFoldouts = new Dictionary<int, bool>();

        // 신규 돌 추가 폼 필드
        private GameObject newPrefabObj;
        private string newId = "";
        private string newName = "";
        private string newDescription = "";
        private int newUnlockGold = 0;
        private bool newIsUnlocked = true;

        private Vector2 scrollPos;
        private bool showNewForm = true;

        private void OnEnable()
        {
            manager = (StoneCatalogManager)target;
            if (manager != null)
            {
                manager.LoadFromDisk();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. 헤더 스타일
            DrawCustomHeader();

            EditorGUILayout.Space(6);

            // 2. 툴바 버튼 (새로고침 / JSON 저장 / 기본값 복원)
            DrawToolbar();

            EditorGUILayout.Space(8);

            // 3. 등록된 돌 목록 표시
            DrawCatalogList();

            EditorGUILayout.Space(12);

            // 4. 신규 돌 등록 폼
            DrawNewStoneForm();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.75f, 1f) }
            };
            EditorGUILayout.LabelField("🪨 [Skipping Stones] 조약돌 도감 관리자", titleStyle);
            
            GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            EditorGUILayout.LabelField("조약돌 프리팹을 드래그&드롭하여 도감에 등록하거나 기존 돌 데이터를 편집합니다.\n저장된 데이터는 Resources/Data/StoneCatalogData.json에 안전하게 보관됩니다.", descStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🔄 파일에서 새로고침", GUILayout.Height(26)))
            {
                manager.LoadFromDisk();
                EditorUtility.SetDirty(manager);
            }

            if (GUILayout.Button("💾 전체 변경사항 저장", GUILayout.Height(26)))
            {
                manager.SaveToDisk();
                EditorUtility.SetDirty(manager);
            }

            if (GUILayout.Button("⚙️ 기본 4종 복원", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("기본값 복원", "조약돌 도감을 기본 4종(회색, 파랑, 초록, 빨강)으로 리셋하시겠습니까?", "예 (리셋)", "취소"))
                {
                    manager.SeedDefaultCatalog();
                    manager.SaveToDisk();
                    EditorUtility.SetDirty(manager);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCatalogList()
        {
            int count = manager.catalog != null ? manager.catalog.Count : 0;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"📋 등록된 조약돌 목록 (총 {count}종)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (count == 0)
            {
                EditorGUILayout.HelpBox("등록된 조약돌이 없습니다. 아래 [신규 돌 등록하기]에서 돌을 추가해주세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            int itemToDelete = -1;
            int itemToMoveUp = -1;
            int itemToMoveDown = -1;

            for (int i = 0; i < count; i++)
            {
                var stone = manager.catalog[i];
                if (stone == null) continue;

                if (!editFoldouts.ContainsKey(i)) editFoldouts[i] = false;
                bool isEditing = editFoldouts[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 상단 라인: [번호] 이름 (ID) + 프리팹 미리보기 + 버튼들
                EditorGUILayout.BeginHorizontal();

                string foldoutLabel = $"[{i + 1}] {stone.name} (ID: {stone.id})";
                if (stone.isUnlocked) foldoutLabel += " 🔓";
                if (stone.unlockGoldCost > 0) foldoutLabel += $" 💰{stone.unlockGoldCost}G";

                GUIStyle headerLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                if (isEditing) headerLabelStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

                EditorGUILayout.LabelField(foldoutLabel, headerLabelStyle, GUILayout.ExpandWidth(true));

                // Edit 토글 버튼
                string editBtnText = isEditing ? "닫기(Done)" : "✏️ 편집(Edit)";
                if (GUILayout.Button(editBtnText, GUILayout.Width(85), GUILayout.Height(20)))
                {
                    editFoldouts[i] = !editFoldouts[i];
                }

                // 순서 위로 이동
                GUI.enabled = (i > 0);
                if (GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(20)))
                {
                    itemToMoveUp = i;
                }

                // 순서 아래로 이동
                GUI.enabled = (i < count - 1);
                if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(20)))
                {
                    itemToMoveDown = i;
                }
                GUI.enabled = true;

                // 삭제 버튼
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("🗑️", GUILayout.Width(28), GUILayout.Height(20)))
                {
                    itemToDelete = i;
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                // 요약 표시 (편집 모드가 아닐 때)
                if (!isEditing)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"• 프리팹: {stone.prefabPath}", EditorStyles.miniLabel);
                    if (!string.IsNullOrEmpty(stone.description))
                    {
                        EditorGUILayout.LabelField($"• 설명: {stone.description}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
                else
                {
                    // 편집 모드 (항목들 펼침)
                    EditorGUILayout.Space(4);
                    EditorGUI.indentLevel++;

                    // 프리팹 로드 & 교체 슬롯
                    GameObject currentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(stone.prefabPath);
                    GameObject newAssignedPrefab = (GameObject)EditorGUILayout.ObjectField("돌 프리팹 (Prefab)", currentPrefab, typeof(GameObject), false);
                    if (newAssignedPrefab != null && newAssignedPrefab != currentPrefab)
                    {
                        string path = AssetDatabase.GetAssetPath(newAssignedPrefab);
                        if (!string.IsNullOrEmpty(path))
                        {
                            stone.prefabPath = path;
                        }
                    }

                    stone.id = EditorGUILayout.TextField("고유 식별자 (ID)", stone.id);
                    stone.name = EditorGUILayout.TextField("표시 이름 (Name)", stone.name);
                    stone.description = EditorGUILayout.TextField("상세 설명 (Description)", stone.description);
                    stone.unlockGoldCost = EditorGUILayout.IntField("해금 골드 비용", Mathf.Max(0, stone.unlockGoldCost));
                    stone.isUnlocked = EditorGUILayout.Toggle("기본 해금 여부", stone.isUnlocked);

                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("💾 이 항목 저장", GUILayout.Height(22)))
                    {
                        manager.SaveToDisk();
                        EditorUtility.SetDirty(manager);
                        editFoldouts[i] = false;
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();

            // 리스트 조작 처리
            if (itemToMoveUp >= 0)
            {
                var item = manager.catalog[itemToMoveUp];
                manager.catalog.RemoveAt(itemToMoveUp);
                manager.catalog.Insert(itemToMoveUp - 1, item);
                manager.SaveToDisk();
                EditorUtility.SetDirty(manager);
            }
            if (itemToMoveDown >= 0)
            {
                var item = manager.catalog[itemToMoveDown];
                manager.catalog.RemoveAt(itemToMoveDown);
                manager.catalog.Insert(itemToMoveDown + 1, item);
                manager.SaveToDisk();
                EditorUtility.SetDirty(manager);
            }
            if (itemToDelete >= 0)
            {
                if (EditorUtility.DisplayDialog("조약돌 삭제", $"'{manager.catalog[itemToDelete].name}'을(를) 도감에서 삭제하시겠습니까?", "삭제", "취소"))
                {
                    manager.catalog.RemoveAt(itemToDelete);
                    manager.SaveToDisk();
                    EditorUtility.SetDirty(manager);
                }
            }
        }

        private void DrawNewStoneForm()
        {
            EditorGUILayout.BeginVertical("box");

            showNewForm = EditorGUILayout.Foldout(showNewForm, "➕ 신규 조약돌 등록하기 (Add New Stone)", true, EditorStyles.foldoutHeader);

            if (showNewForm)
            {
                EditorGUILayout.Space(4);

                // 프리팹 드래그앤드롭 슬롯
                GameObject prevPrefab = newPrefabObj;
                newPrefabObj = (GameObject)EditorGUILayout.ObjectField("1. 돌 프리팹 슬롯", newPrefabObj, typeof(GameObject), false);

                // 프리팹이 새로 들어왔을 때 ID와 이름을 프리팹 이름으로 자동 채우기
                if (newPrefabObj != null && newPrefabObj != prevPrefab)
                {
                    if (string.IsNullOrEmpty(newId)) newId = newPrefabObj.name.ToLowerInvariant().Replace(" ", "_");
                    if (string.IsNullOrEmpty(newName)) newName = newPrefabObj.name;
                }

                newId = EditorGUILayout.TextField("2. 고유 식별자 (ID)", newId);
                newName = EditorGUILayout.TextField("3. 표시 이름 (Name)", newName);
                newDescription = EditorGUILayout.TextField("4. 상세 설명", newDescription);
                newUnlockGold = EditorGUILayout.IntField("5. 해금 골드 비용", Mathf.Max(0, newUnlockGold));
                newIsUnlocked = EditorGUILayout.Toggle("6. 기본 해금 여부", newIsUnlocked);

                EditorGUILayout.Space(6);

                GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
                if (GUILayout.Button("🌟 카탈로그에 신규 등록하기", GUILayout.Height(32)))
                {
                    RegisterNewStone();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void RegisterNewStone()
        {
            if (newPrefabObj == null)
            {
                EditorUtility.DisplayDialog("오류", "돌 프리팹을 슬롯에 등록해주세요!", "확인");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(newPrefabObj);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("오류", "유효한 프로젝트 내 프리팹 에셋을 등록해주세요!", "확인");
                return;
            }

            if (string.IsNullOrEmpty(newId))
            {
                newId = newPrefabObj.name.ToLowerInvariant().Replace(" ", "_");
            }
            if (string.IsNullOrEmpty(newName))
            {
                newName = newPrefabObj.name;
            }

            // 중복 ID 검사
            if (manager.catalog.Exists(s => s.id.Equals(newId, StringComparison.OrdinalIgnoreCase)))
            {
                EditorUtility.DisplayDialog("중복 ID", $"이미 '{newId}' ID를 가진 돌이 등록되어 있습니다. 다른 ID를 입력해주세요.", "확인");
                return;
            }

            var newStone = new StoneInfoData
            {
                id = newId,
                name = newName,
                description = newDescription,
                prefabPath = assetPath,
                unlockGoldCost = newUnlockGold,
                isUnlocked = newIsUnlocked
            };

            manager.catalog.Add(newStone);
            manager.SaveToDisk();
            EditorUtility.SetDirty(manager);

            EditorUtility.DisplayDialog("등록 완료", $"'{newName}' 조약돌이 도감에 성공적으로 등록되었습니다!\n경로: {assetPath}", "확인");

            // 폼 초기화
            newPrefabObj = null;
            newId = "";
            newName = "";
            newDescription = "";
            newUnlockGold = 0;
            newIsUnlocked = true;
        }
    }
}
