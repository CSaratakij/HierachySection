using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

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
        static int currentPinSectionInstanceID;

        static bool autoSetEditorOnlyTag = false;
        static bool isBeginRename = false;
        static bool isPinSection = false;

        static Scene openScene;
        static List<int> sectionInstanceID;

        static Color normalContentColor = Color.white;
        static Color foregroundColor = Color.white;
        static Color backgroundColor = Color.black;
        static Color hilightForegroundColor = Color.white;
        static Color hilightBackgroundColor = Color.yellow;


        static HierachySection()
        {
            Initialize();
        }

        static void Initialize()
        {
            normalContentColor = GUI.contentColor;
            sectionInstanceID = new List<int>();
            SubscribeEvents();
            RefreshSectionInstanceID(SceneManager.GetActiveScene());
        }

        static void SubscribeEvents()
        {
            EditorApplication.hierarchyChanged += OnHierachyChanged;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierachyWindowItemGUI;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChange;
        }

        static void RefreshSectionInstanceID(Scene scene)
        {
            LoadSettings();
            sectionInstanceID.Clear();

            currentSectionIndex = 0;
            currentPinSectionInstanceID = 0;

            openScene = scene;
            totalObject = scene.rootCount;

            var allObject = openScene.GetRootGameObjects();

            foreach (GameObject obj in allObject)
            {
                if (obj.name.Contains(PREFIX))
                {
                    if (autoSetEditorOnlyTag)
                    {
                        Undo.RecordObject(obj, "Set section tag to \"EditorOnly\"");
                        obj.tag = "EditorOnly";
                    }

                    sectionInstanceID.Add(obj.GetInstanceID());
                }
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
            if (!sectionInstanceID.Contains(instanceID))
                return;

            Object obj = EditorUtility.InstanceIDToObject(instanceID);

            if (obj ==  null)
                return;

            bool isSelectOneObject = (Selection.activeObject == obj && Selection.instanceIDs.Length == 1);

            if (isSelectOneObject)
            {
                currentSectionIndex = sectionInstanceID.IndexOf(instanceID);
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
                        return;
                }
            }

            bool isInSelelect = false;

            if (Selection.instanceIDs.Length > 1)
                isInSelelect = IsSectionInSelection(Selection.instanceIDs, instanceID);

            EditorGUI.DrawRect(selectionRect, (isSelectOneObject || isInSelelect) ? hilightBackgroundColor : backgroundColor);

            GUI.contentColor = (isSelectOneObject || isInSelelect) ? hilightForegroundColor : foregroundColor;
            EditorGUI.DropShadowLabel(selectionRect, (isPinSection && instanceID == currentPinSectionInstanceID) ? obj.name + " (P)" : obj.name);

            GUI.contentColor = normalContentColor;
        }

        static bool IsSectionInSelection(int[] instanceIDs, int instanceID)
        {
            for (int i = 0; i < instanceIDs.Length; ++i)
            {
                if (instanceID == instanceIDs[i])
                    return true;
            }

            return false;
        }

        static void OnActiveSceneChange(Scene current, Scene next)
        {
            RefreshSectionInstanceID(next);
        }

        static void SectionHandler()
        {
            var totalSceneRoot = openScene.rootCount;

            bool isSomeObjectDeleted = (totalObject > totalSceneRoot);
            bool isSomeObjectRename = (totalObject == totalSceneRoot);
            bool isSomeObjectAddIn = (totalObject < totalSceneRoot);

            if (isSomeObjectDeleted)
            {
                ClearNonExistSectionID();
            }

            if (isSomeObjectRename)
            {
                Rename(Selection.transforms);
            }

            if (isSomeObjectAddIn)
            {
                AddSectionID(Selection.transforms);
            }

            totalObject = totalSceneRoot;
        }

        static void ClearNonExistSectionID()
        {
            List<int> removeList = new List<int>();

            foreach (int id in sectionInstanceID)
            {
                Object obj = EditorUtility.InstanceIDToObject(id);

                if (obj == null)
                {
                    removeList.Add(id);
                }
            }

            foreach (int id in removeList)
            {
                sectionInstanceID.Remove(id);
            }

            removeList.Clear();
        }

        static void Rename(Transform[] targetObjects)
        {
            foreach (Transform obj in targetObjects)
            {
                Rename(obj);
            }
        }

        static void Rename(Transform targetObject)
        {
            if (targetObject == null)
                return;

            int id = targetObject.gameObject.GetInstanceID();
            bool isSectionInstance = sectionInstanceID.Contains(id);

            GameObject obj = EditorUtility.InstanceIDToObject(id) as GameObject;
            bool canRename = false;

            if (obj != null)
            {
                canRename = !obj.name.Contains(PREFIX);
            }

            if (isSectionInstance && canRename)
            {
                string objectName = obj.name.Trim(PREFIX_SYMBOL, ' ');

                if (string.IsNullOrEmpty(objectName))
                {
                    objectName = DEFAULT_LABEL;
                }

                obj.name = string.Format(PREFIX + " {0} " + PREFIX, objectName);
            }
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
            bool isAlreadyExist = sectionInstanceID.Contains(id);

            if (canAddSectionID && !isAlreadyExist)
            {
                sectionInstanceID.Add(id);
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

            if (isPinSection || sectionInstanceID.Contains(id))
            {
                GameObject sectionObj = EditorUtility.InstanceIDToObject(id) as GameObject;
                int offset = sectionObj.transform.GetSiblingIndex() + 1;
                obj.transform.SetSiblingIndex(offset);
            }

            Undo.RegisterCreatedObjectUndo(obj, "Add Section");
            Selection.SetActiveObjectWithContext(obj, obj);

            AddSectionID(obj.transform);
        }

        [MenuItem("GameObject/Move GameObject Up &R", false, 0)]
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

            int maxIndex = sectionInstanceID.Count - 1;

            if (maxIndex < 0)
                return;

            if (currentSectionIndex > maxIndex)
                currentSectionIndex = maxIndex;

            currentSectionIndex = (currentSectionIndex + 1) > maxIndex ? 0 : (currentSectionIndex + 1);

            Object obj = EditorUtility.InstanceIDToObject(sectionInstanceID[currentSectionIndex]);
            Selection.SetActiveObjectWithContext(obj, obj);
        }

        [MenuItem("GameObject/Section/Select Previous Section _S", false, 0)]
        public static void SelectPreviousSection()
        {
            if (EditorApplication.isPlaying)
                return;

            int maxIndex = sectionInstanceID.Count - 1;

            if (maxIndex < 0)
                return;

            if (currentSectionIndex > maxIndex)
                currentSectionIndex = maxIndex;

            currentSectionIndex = (currentSectionIndex - 1) < 0 ? maxIndex : (currentSectionIndex - 1);

            Object obj = EditorUtility.InstanceIDToObject(sectionInstanceID[currentSectionIndex]);
            Selection.SetActiveObjectWithContext(obj, obj);
        }

        [MenuItem("GameObject/Section/Move to Current Section _g", false, 0)]
        public static void MoveToCurrentSection()
        {
            if (Selection.transforms.Length <= 0)
                return;

            if (sectionInstanceID.Contains(Selection.activeInstanceID))
                return;

            int id = (isPinSection) ? currentPinSectionInstanceID : sectionInstanceID[currentSectionIndex];
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

            if (sectionInstanceID.Contains(Selection.activeInstanceID))
                return;

            int id = (isPinSection) ? currentPinSectionInstanceID : sectionInstanceID[currentSectionIndex];
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
            if (sectionInstanceID.Contains(Selection.activeInstanceID))
            {
                isPinSection = true;
                currentPinSectionInstanceID = (isPinSection) ? Selection.activeInstanceID : 0;
            }
            else
            {
                isPinSection = false;
                currentPinSectionInstanceID = 0;
            }
        }
    }

}
