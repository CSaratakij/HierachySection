using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoadAttribute]
public static class HierachySection
{
    const char PREFIX_SYMBOL = '-';
    const string PREFIX = "---";
    const string DEFAULT_LABEL = "Section";
    const string DEFAULT_FULL_LABEL = "--- Section ---";

    static int totalObject;
    static List<int> sectionInstanceID;


    static HierachySection()
    {
        Initialize();
    }

    static void Initialize()
    {
        EditorApplication.hierarchyChanged += OnHierachyChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;

        sectionInstanceID = new List<int>();
        UpdateSectionInstanceID();

    }

    static void UpdateSectionInstanceID()
    {
        sectionInstanceID.Clear();

        var allObject = Resources.FindObjectsOfTypeAll<GameObject>();
        totalObject = allObject.Length;

        foreach (GameObject obj in allObject)
        {
            if (obj.name.Contains(PREFIX))
            {
                sectionInstanceID.Add(obj.GetInstanceID());
            }
        }
    }

    static void OnHierachyChanged()
    {
        SectionHandler();
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        UpdateSectionInstanceID();
    }

    static void SectionHandler()
    {
        var allObject = Resources.FindObjectsOfTypeAll<GameObject>();

        bool isSomeObjectDeleted = (totalObject > allObject.Length);
        bool isSomeObjectRename = (totalObject == allObject.Length);
        bool isSomeObjectAddIn = (totalObject < allObject.Length);

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

        totalObject = allObject.Length;
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
            sectionInstanceID.Add(targetObject.gameObject.GetInstanceID());
        }
    }

    [MenuItem("GameObject/Create Section %t", false, 0)]
    public static void CreateSection()
    {
        GameObject obj = new GameObject(DEFAULT_FULL_LABEL);

        obj.transform.position = Vector3.zero;
        obj.SetActive(false);

        Undo.RegisterCreatedObjectUndo(obj, "Add Section");
        AddSectionID(obj.transform);
    }
}

