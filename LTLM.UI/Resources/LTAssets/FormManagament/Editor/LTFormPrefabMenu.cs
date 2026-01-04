using UnityEditor;
using UnityEngine;

public static class LTFormPrefabMenu
{
    private const string ROOT_MENU = "GameObject/LTFormManagement/Prefabs/";
    private const string PREFAB_PATH = "LTPrefabs";

    [MenuItem(ROOT_MENU + "Input Field", false, 11)]
    private static void AddLTForm()
    {
        SpawnPrefab("InputField-FormManagement");
    }

    [MenuItem(ROOT_MENU + "Button", false, 12)]
    private static void AddInputField()
    {
        SpawnPrefab("Button-FormManagement");
    }

    private static void SpawnPrefab(string prefabName)
    {
        GameObject prefab = Resources.Load<GameObject>(
            $"{PREFAB_PATH}/{prefabName}"
        );

        if (prefab == null)
        {
            Debug.LogError($"Prefab not found: Resources/{PREFAB_PATH}/{prefabName}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Selection.activeTransform);

        Undo.RegisterCreatedObjectUndo(instance, $"Create {prefabName}");
        Selection.activeGameObject = instance;
    }
}