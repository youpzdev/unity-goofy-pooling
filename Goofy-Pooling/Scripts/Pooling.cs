using System.Collections.Generic;
using UnityEngine;

public static class Pooling
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
    private static readonly Dictionary<GameObject, GameObject> prefabMap = new();

    // ====== Methods ====================================================================================
    public static GameObject Instantiate(GameObject prefab, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
    {
        if (!pools.ContainsKey(prefab)) pools[prefab] = new Queue<GameObject>();

        GameObject obj;

        if (pools[prefab].Count > 0)
        {
            obj = pools[prefab].Dequeue();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
        }
        else
        {
            obj = Object.Instantiate(prefab, position, rotation);
            prefabMap[obj] = prefab;
        }

        if (parent != null) obj.transform.SetParent(parent);

        return obj;
    }
    
    public static void Destroy(GameObject obj)
    {
        if (!prefabMap.TryGetValue(obj, out GameObject prefab)) return;

        obj.SetActive(false);

        if (!pools.ContainsKey(prefab)) pools[prefab] = new Queue<GameObject>();

        pools[prefab].Enqueue(obj);
    }
}