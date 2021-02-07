using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace HierachySection.Editor
{
    [InitializeOnLoad]
    public static class HierachySection
    {
        const char PREFIX_SYMBOL = '-';

        const string PREFIX = "---";
        const string DEFAULT_LABEL = "Section";
        const string DEFAULT_FULL_LABEL = "--- Section ---";

        static int totalObject;
        static int renamingInstanceID;
        static int currentSectionIndex;
        static int currentSectionID;
        static int currentPinSectionInstanceID;

        static int previousTotalSection;
        static int currentTotalSection;

        static int previousTotalSelection;
        static int currentTotalSelection;

        static bool autoSetEditorOnlyTag = false;
        static bool isBeginRename = false;
        static bool isPinSection = false;

        static Scene openScene;

        static Dictionary<int, SectionInfo> sectionInstanceID;
        static HashSet<int> selectedInstanceID;

        static Color normalContentColor = Color.white;
        static Color foregroundColor = Color.white;
        static Color backgroundColor = Color.black;
        static Color hilightForegroundColor = Color.white;
        static Color hilightBackgroundColor = Color.yellow;

        public class SectionInfo
        {
            public bool IsDirty => (previousSiblingIndex != currentSiblingIndex);

            int previousSiblingIndex;
            int currentSiblingIndex;

            public int order;
            public bool isSelected;
            public string title;

            public SectionInfo(int order)
            {
                this.order = order;
                isSelected = false;
            }

            public void UpdateTransformSiblingIndex(int index)
            {
                previousSiblingIndex = currentSiblingIndex;
                currentSiblingIndex = index;
            }

            public void ResetTransformSiblingIndex(int index)
            {
                previousSiblingIndex = index;
                currentSiblingIndex = index;
            }
        }

        static HierachySection()
        {
            Initialize();
        }

        static void Initialize()
        {
            normalContentColor = GUI.contentColor;
            sectionInstanceID = new Dictionary<int, SectionInfo>();
            selectedInstanceID = new HashSet<int>();
            SubscribeEvents();
            RefreshSectionInstanceID(SceneManager.GetActiveScene());
        }

        static void SubscribeEvents()
        {
            EditorApplication.hierarchyChanged += OnHierachyChanged;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierachyWindowItemGUI;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChange;
            Selection.selectionChanged += OnSelectionChanged;
        }

        static void RefreshSectionInstanceID(Scene scene)
        {
            LoadSettings();
            sectionInstanceID.Clear();

            currentSectionIndex = 0;
            currentSectionID = 0;
            currentPinSectionInstanceID = 0;

            previousTotalSection = 0;
            currentTotalSection = 0;

            previousTotalSelection = 0;
            currentTotalSelection = 0;

            selectedInstanceID.Clear();

            openScene = scene;
            totalObject = scene.rootCount;

            isPinSection = false;
            var allObject = openScene.GetRootGameObjects();

            for (int i = 0; i < allObject.Length; ++i)
            {
                var obj = allObject[i];

                if (obj.name.Contains(PREFIX))
                {
                    if (autoSetEditorOnlyTag)
                    {
                        Undo.RecordObject(obj, "Set section tag to \"EditorOnly\"");
                        obj.tag = "EditorOnly";
                    }

                    AddSectionID(obj.transform);
                }
            }

            currentTotalSection = sectionInstanceID.Keys.Count;
        }

        static void UpdateSelectionCache()
        {
            previousTotalSelection = currentTotalSelection;
            currentTotalSelection = Selection.instanceIDs.Length;

            bool isSelecting = (previousTotalSelection != currentTotalSelection) && currentTotalSelection > 0;
            bool isSelected = false;

            if (isSelecting)
            {
                selectedInstanceID.Clear();
                selectedInstanceID.UnionWith(sectionInstanceID.Keys);
                selectedInstanceID.IntersectWith(Selection.instanceIDs);
            }
            else
            {
                selectedInstanceID.Clear();
            }

            foreach (var pair in sectionInstanceID)
            {
                if (selectedInstanceID.Count > 0) {
                    isSelected = selectedInstanceID.Contains(pair.Key);
                }

                pair.Value.isSelected = isSelected;
            }
        }

        static void LoadSettings()
        {
            var settings = HierachySectionSetting.GetOrCreateSettings();
            autoSetEditorOnlyTag = settings.AutoSetEditorOnlyTag;

            foregroundColor = settings.ForegroundColor;
            backgroundColor = settings.BackgroundColor;

            hilightForegroundColor = settings.HilightForegroundColor;
            hilightBackgroundColor = settings.HilightBackgroundColor;
        }

        static void OnHierachyChanged()
        {
            SectionHandler();
        }

        static void OnHierachyWindowItemGUI(int instanceID, Rect selectionRect)
        {
            DrawSectionGUI(instanceID, selectionRect);
        }

        static void DrawSectionGUI(int instanceID, Rect selectionRect)
        {
            if (!sectionInstanceID.ContainsKey(instanceID))
                return;

            Object obj = EditorUtility.InstanceIDToObject(instanceID);

            if (obj ==  null)
                return;

            bool isSelectOneObject = (Selection.activeObject == obj && Selection.instanceIDs.Length == 1);

            if (isSelectOneObject)
            {
                currentSectionID = instanceID;
                currentSectionIndex = sectionInstanceID[instanceID].order;

                Event evt = Event.current;

                bool isKeyUpEvent = (evt.type == EventType.KeyUp && evt.isKey);
                bool isMouseDownEvent = (evt.type == EventType.MouseDown && evt.isMouse);

                bool isPressRename = isKeyUpEvent && evt.keyCode == KeyCode.F2;
                bool isPressEnter = isKeyUpEvent && evt.keyCode == KeyCode.Return;
                bool isPressEscape = isKeyUpEvent && evt.keyCode == KeyCode.Escape;

                bool isClickOutsideSelectionRect = (isMouseDownEvent && !selectionRect.Contains(evt.mousePosition));
                bool isConfirmRename = isBeginRename && (isPressEnter || isPressEscape || isClickOutsideSelectionRect);

                if (isPressRename)
                {
                    isBeginRename = true;
                    renamingInstanceID = instanceID;
                    evt.Use();
                }
                else if (isConfirmRename)
                {
                    isBeginRename = false;
                    renamingInstanceID = 0;
                    evt.Use();
                }
                else
                {
                    if (isBeginRename && Selection.activeInstanceID == renamingInstanceID)
                    {
                        return;
                    }
                }
            }

            var info = sectionInstanceID[instanceID];

            string postFix = "";

            if (instanceID == currentSectionID)
            {
                string labelStatus = "x";

                if (isPinSection && (instanceID == currentPinSectionInstanceID))
                    labelStatus += "P";

                postFix = $"[ {labelStatus} ]";
            }
            else
            {
                postFix = (isPinSection) && (currentPinSectionInstanceID == instanceID) ? $"[ P ]" : PREFIX;
            }

            string objectName = $"{PREFIX} {info.title} {postFix}";
            bool isInSelect = sectionInstanceID[instanceID].isSelected;

            EditorGUI.DrawRect(selectionRect, (isSelectOneObject || isInSelect) ? hilightBackgroundColor : backgroundColor);
            GUI.contentColor = (isSelectOneObject || isInSelect) ? hilightForegroundColor : foregroundColor;

            EditorGUI.DropShadowLabel(selectionRect, objectName);
            GUI.contentColor = normalContentColor;
        }

        static void OnActiveSceneChange(Scene current, Scene next)
        {
            RefreshSectionInstanceID(next);
        }

        static void OnSelectionChanged()
        {
            UpdateSelectionCache();
        }

        static void SectionHandler()
        {
            int totalSceneRoot = openScene.rootCount;

            bool isSomeObjectDeleted = (totalObject > totalSceneRoot);
            bool isSomeObjectChanged = (totalObject == totalSceneRoot);
            bool isSomeObjectAddIn = (totalObject < totalSceneRoot);

            if (isSomeObjectDeleted)
            {
                ClearNonExistSectionID();
                CheckForOrderHierachyChangeWhenDeleted();
            }
            else if (isSomeObjectChanged)
            {
                UpdateChanged(Selection.transforms);
                CheckForOrderHierachyChanged();
            }
            else if (isSomeObjectAddIn)
            {
                AddSectionID(Selection.transforms);
            }

            totalObject = totalSceneRoot;
        }

        static void ClearNonExistSectionID()
        {
            var keyTable = sectionInstanceID.Keys.ToArray();

            for (int i = 0; i < keyTable.Length; ++i)
            {
                var instanceID = keyTable[i];
                var obj = EditorUtility.InstanceIDToObject(instanceID);

                if (obj == null)
                {
                    sectionInstanceID.Remove(instanceID);
                }
            }
        }

        static void UpdateSectionOrder()
        {
            if (sectionInstanceID.Keys.Count <= 0)
                return;

            var sortID = new SortedDictionary<int, int>();
            var removeList = new List<int>();

            foreach (var id in sectionInstanceID.Keys)
            {
                var targetObj = EditorUtility.InstanceIDToObject(id) as GameObject;

                if (targetObj == null) {
                    removeList.Add(id);
                    continue;
                }


                bool shouldPreventFromBeingChild = (targetObj.transform.parent != null);

                if (shouldPreventFromBeingChild) {
                    targetObj.transform.SetParent(null);
                    Undo.SetTransformParent(targetObj.transform, null, "Prevent section to be a child...");
                }

                int siblingIndex = targetObj.transform.GetSiblingIndex();

                if (sortID.ContainsKey(siblingIndex))
                    continue;

                sortID.Add(siblingIndex, id);
            }

            var indice = 0;

            foreach (var item in sortID)
            {
                int siblingIndex = item.Key;
                int id = item.Value;
                int order = indice;

                sectionInstanceID[id].order = indice;
                sectionInstanceID[id].ResetTransformSiblingIndex(siblingIndex);

                indice += 1;
            }

            foreach (var id in removeList)
            {
                sectionInstanceID.Remove(id);
            }
        }

        static void UpdateChanged(Transform[] targetObjects)
        {
            foreach (Transform obj in targetObjects)
            {
                UpdateChanged(obj);
            }
        }

        static void CheckForOrderHierachyChanged()
        {
            if (Selection.activeTransform != null)
            {
                int id = Selection.activeInstanceID;

                if (sectionInstanceID.ContainsKey(id))
                {
                    bool shouldPreventFromBeingChild = (Selection.activeTransform.parent != null);

                    if (shouldPreventFromBeingChild) {
                        Undo.SetTransformParent(Selection.activeTransform, null, "Prevent section to be a child...");
                    }

                    int siblingIndex = Selection.activeTransform.GetSiblingIndex();
                    sectionInstanceID[id].UpdateTransformSiblingIndex(siblingIndex);

                    if (sectionInstanceID[id].IsDirty)
                        UpdateSectionOrder();
                }
            }
        }

        static void CheckForOrderHierachyChangeWhenDeleted()
        {
            previousTotalSection = currentTotalSection;
            currentTotalSection = sectionInstanceID.Keys.Count;

            bool isSectionAmountChanged = (previousTotalSection != currentTotalSection);

            if (isSectionAmountChanged)
                UpdateSectionOrder();
        }

        static void UpdateChanged(Transform targetObject)
        {
            if (targetObject == null)
                return;

            int id = targetObject.gameObject.GetInstanceID();

            if (!sectionInstanceID.ContainsKey(id))
                return;
            
            bool canRename = !targetObject.gameObject.name.Contains(PREFIX);
            bool isSectionInstance = sectionInstanceID.ContainsKey(id);
            bool shouldAddSectionPrefix = (isSectionInstance && canRename);

            if (shouldAddSectionPrefix)
                Rename(targetObject.gameObject, id);
        }

        static void Rename(GameObject obj, int id, string postFix = "---")
        {
            string objectName = obj.name.Trim(PREFIX_SYMBOL, ' ');

            if (string.IsNullOrEmpty(objectName))
            {
                objectName = DEFAULT_LABEL;
            }

            sectionInstanceID[id].title = objectName;
            obj.name = $"{PREFIX} {objectName} {postFix}";
        }

        static void AddSectionID(Transform[] targetObjects)
        {
            foreach (Transform obj in targetObjects)
            {
                AddSectionID(obj);
            }
        }

        static void AddSectionID(Transform targetObject)
        {
            if (targetObject == null)
                return;

            int id = targetObject.gameObject.GetInstanceID();

            bool canAddSectionID = targetObject.gameObject.name.Contains(PREFIX);
            bool isAlreadyExist = sectionInstanceID.ContainsKey(id);

            if (canAddSectionID && !isAlreadyExist)
            {
                var expectIndex = sectionInstanceID.Keys.Count;

                sectionInstanceID.Add(id, new SectionInfo(expectIndex));
                Rename(targetObject.gameObject, id);

                int siblingIndex = targetObject.GetSiblingIndex();
                sectionInstanceID[id].ResetTransformSiblingIndex(siblingIndex);

                currentSectionID = id;
                currentSectionIndex = expectIndex;
            }
        }

        [MenuItem("GameObject/Section/Create Section %t", false, 0)]
        public static void CreateSection()
        {
            GameObject obj = new GameObject(DEFAULT_FULL_LABEL);

            if (autoSetEditorOnlyTag)
            {
                obj.tag = "EditorOnly";
            }

            obj.transform.position = Vector3.zero;
            obj.SetActive(false);

            int id = (isPinSection) ? currentPinSectionInstanceID : Selection.activeInstanceID;

            if (isPinSection || sectionInstanceID.ContainsKey(id))
            {
                GameObject sectionObj = EditorUtility.InstanceIDToObject(id) as GameObject;
                int offset = sectionObj.transform.GetSiblingIndex() + 1;
                obj.transform.SetSiblingIndex(offset);
            }

            Undo.RegisterCreatedObjectUndo(obj, "Add Section");
            Selection.SetActiveObjectWithContext(obj, null);

            AddSectionID(obj.transform);
        }

        [MenuItem("GameObject/Move GameObject Up &#r", false, 0)]
        public static void MoveGameObjectUp()
        {
            Transform target = Selection.activeTransform;

            if (target == null)
                return;

            int currentIndex = target.GetSiblingIndex();

            currentIndex = (currentIndex - 1) < 0 ? 0 : (currentIndex - 1);
            target.SetSiblingIndex(currentIndex);
        }

        [MenuItem("GameObject/Move GameObject Down &r", false, 0)]
        public static void MoveGameObjectDown()
        {
            Transform target = Selection.activeTransform;

            if (target == null)
                return;

            int currentIndex = target.GetSiblingIndex();

            currentIndex = (currentIndex + 1) > (openScene.rootCount - 1) ? (openScene.rootCount - 1) : (currentIndex + 1);
            target.SetSiblingIndex(currentIndex);
        }

        [MenuItem("GameObject/Section/Select Next Section _s", false, 0)]
        public static void SelectNextSection()
        {
            if (EditorApplication.isPlaying)
                return;

            int maxIndex = (sectionInstanceID.Keys.Count - 1);

            if (maxIndex < 0)
                return;

            if (!sectionInstanceID.ContainsKey(currentSectionID)) {
                try
                {
                    var item = sectionInstanceID.First(x => x.Value.order == maxIndex);
                    currentSectionID = item.Key;
                }
                catch (Exception e) { throw; }
            }

            int currentIndex = sectionInstanceID[currentSectionID].order;
            currentIndex = (currentIndex + 1) > maxIndex ? 0 : (currentIndex + 1);

            try
            {
                var item = sectionInstanceID.First(x => x.Value.order == currentIndex);
                var obj = EditorUtility.InstanceIDToObject(item.Key);

                if (obj != null)
                {
                    currentSectionIndex = currentIndex;
                    Selection.SetActiveObjectWithContext(obj, null);
                }
            }
            catch (Exception e) { throw; }
        }

        [MenuItem("GameObject/Section/Select Previous Section _S", false, 0)]
        public static void SelectPreviousSection()
        {
            if (EditorApplication.isPlaying)
                return;

            int maxIndex = (sectionInstanceID.Keys.Count - 1);

            if (maxIndex < 0)
                return;

            if (!sectionInstanceID.ContainsKey(currentSectionID)) {
                try
                {
                    var item = sectionInstanceID.First(x => x.Value.order == 0);
                    currentSectionID = item.Key;
                }
                catch (Exception e) { throw; }
            }

            int currentIndex = sectionInstanceID[currentSectionID].order;
            currentIndex = (currentIndex - 1) < 0 ? maxIndex : (currentIndex - 1);

            try
            {
                var item = sectionInstanceID.First(x => x.Value.order == currentIndex);
                var obj = EditorUtility.InstanceIDToObject(item.Key);

                if (obj != null)
                {
                    currentSectionIndex = currentIndex;
                    Selection.SetActiveObjectWithContext(obj, null);
                }
            }
            catch (Exception e) { throw; }
        }

        [MenuItem("GameObject/Section/Move to Current Section _g", false, 0)]
        public static void MoveToCurrentSection()
        {
            if (Selection.transforms.Length <= 0)
                return;

            if (sectionInstanceID.ContainsKey(Selection.activeInstanceID))
                return;

            if (!sectionInstanceID.ContainsKey(currentSectionID))
                return;

            int id = (isPinSection) ? currentPinSectionInstanceID : currentSectionID;
            GameObject sectionObj = EditorUtility.InstanceIDToObject(id) as GameObject;

            for (int i = 0; i < Selection.transforms.Length; ++i)
            {
                int offset = 0;
                bool isTargetBelowSection = Selection.transforms[i].GetSiblingIndex() > sectionObj.transform.GetSiblingIndex();

                if (isTargetBelowSection)
                {
                    offset = sectionObj.transform.GetSiblingIndex() + 1;
                }
                else
                {
                    offset = sectionObj.transform.GetSiblingIndex();
                }

                var obj = Selection.transforms[i];
                Undo.SetTransformParent(obj.transform, obj.transform.parent, "Move to section : " + sectionObj.name);

                obj.SetSiblingIndex(offset);
            }
        }

        [MenuItem("GameObject/Section/Move to Current Section (Upper) #G", false, 0)]
        public static void MoveToCurrentSectionUpperWays()
        {
            if (Selection.transforms.Length <= 0)
                return;

            if (sectionInstanceID.ContainsKey(Selection.activeInstanceID))
                return;

            int id = (isPinSection) ? currentPinSectionInstanceID : currentSectionID;
            GameObject sectionObj = EditorUtility.InstanceIDToObject(id) as GameObject;

            for (int i = 0; i < Selection.transforms.Length; ++i)
            {
                int offset = 0;
                bool isTargetBelowSection = Selection.transforms[i].GetSiblingIndex() > sectionObj.transform.GetSiblingIndex();

                if (isTargetBelowSection)
                {
                    offset = sectionObj.transform.GetSiblingIndex();
                }
                else
                {
                    offset = sectionObj.transform.GetSiblingIndex() - 1;
                }

                var obj = Selection.transforms[i];
                Undo.SetTransformParent(obj.transform, obj.transform.parent, "Move to section (upper) : " + sectionObj.name);

                obj.SetSiblingIndex(offset);
            }
        }

        [MenuItem("GameObject/Section/Refresh Order &#s", false, 0)]
        public static void RefreshSectionOrder()
        {
            UpdateSectionOrder();

            foreach (SceneView scene in SceneView.sceneViews)
            {
                scene.ShowNotification(new GUIContent("Section has re-ordered..."));
            }
        }

        [MenuItem("GameObject/Section/Refresh Section %#F12", false, 0)]
        public static void RefreshSectionPrompt()
        {
            bool confirmRefresh = EditorUtility.DisplayDialog("Warning", "Are you sure to refresh section?\n(Large scene might takes sometime)", "OK", "Cancel");

            if (confirmRefresh)
            {
                RefreshSectionInstanceID(openScene);
                foreach (SceneView scene in SceneView.sceneViews)
                {
                    scene.ShowNotification(new GUIContent("Refreshed Hierachy Section"));
                }
            }
        }

        [MenuItem("GameObject/Section/Pin Section &s", false, 0)]
        public static void PinSection()
        {
            var isSelectSomething = (Selection.activeTransform != null);
            var id = (isSelectSomething) ? Selection.activeInstanceID : currentSectionID;
            var isSelectSection = sectionInstanceID.ContainsKey(id);

            if (!isSelectSection)
                return;

            var shouldChangePinImmediately = isSelectSection && (isPinSection && currentPinSectionInstanceID != id);
            isPinSection = (shouldChangePinImmediately) ? true : !isPinSection;

            if (isPinSection)
            {
                currentPinSectionInstanceID = id;
                currentSectionID = id;
                currentSectionIndex = sectionInstanceID[id].order;
            }
            else
            {
                currentPinSectionInstanceID = 0;
            }
        }

        [MenuItem("GameObject/Section/Remove all section", false, 0)]
        public static void RemoveAllSection()
        {
            bool confirmRefresh = EditorUtility.DisplayDialog("Warning", "Are you sure to remove all section?", "OK", "Cancel");

            if (!confirmRefresh)
                return;

            foreach (var item in sectionInstanceID)
            {
                int id = item.Key;
                var obj = EditorUtility.InstanceIDToObject(id);

                if (obj == null)
                    continue;

                Undo.DestroyObjectImmediate(obj);
            }

            sectionInstanceID.Clear();
            EditorUtility.DisplayDialog("Done", "All section has been removed", "OK");
        }
    }
}

