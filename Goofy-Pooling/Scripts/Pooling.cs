using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Pooling
{
    private class Pool
    {
        public readonly Stack<Entry> Inactive = new();
        public readonly HashSet<Entry> Active = new();
        public readonly HashSet<Entry> Returning = new();
        public readonly List<Entry> Scratch = new();

        public int Capacity = int.MaxValue;
    }

    private class Entry
    {
        public int Id;
        public GameObject Instance;
        public Pool Pool;

        public bool DiscardRequested;
        public bool ReturnRequested;

        public int Busy;
    }

    private static readonly Dictionary<int, Entry> prefabMap = new(1024);
    private static readonly Dictionary<GameObject, Pool> pools = new();

    private static Transform _poolRoot;

    private static Transform PoolRoot
    {
        get
        {
            if (_poolRoot != null) return _poolRoot;
            _poolRoot = new GameObject("[Pool]").transform;
            _poolRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Object.DontDestroyOnLoad(_poolRoot.gameObject);

            return _poolRoot;
        }
    }

    public static void Prewarm(GameObject prefab, int count)
    {
        ValidatePrefab(prefab);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Prewarm: count меньше нуля");
        var pool = GetOrCreate(prefab);
        PruneInactive(pool);
        int target = Math.Min(count, pool.Capacity);
        while (pool.Inactive.Count < target)
        {
            var entry = CreateInstance(prefab, pool);
            entry.Instance.SetActive(false);
            if (prefabMap.ContainsKey(entry.Id)) pool.Inactive.Push(entry);
        }
    }

    public static GameObject Instantiate(GameObject prefab, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
    {return InstantiatePrepared(prefab, position, rotation, parent, null);}

    public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, Action<GameObject> prepare)
    {return InstantiatePrepared(prefab, position, rotation, parent, prepare);}

    private static GameObject InstantiatePrepared(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, Action<GameObject> prepare)
    {
        ValidatePrefab(prefab);
        var pool = GetOrCreate(prefab);
        Entry entry = null;
        while (pool.Inactive.Count > 0)
        {
            var candidate = pool.Inactive.Pop();
            if (candidate.Instance != null) {entry = candidate; break;}
            prefabMap.Remove(candidate.Id);
        }

        if (entry == null) entry = CreateInstance(prefab, pool);
        pool.Active.Add(entry);
        bool cancelled = false;
        entry.Busy++;
        try
        {
            entry.Instance.transform.SetParent(parent, true);
            if (!entry.DiscardRequested)
            {
                entry.Instance.transform.SetPositionAndRotation(position, rotation == default ? Quaternion.identity : rotation);
                try
                {
                    prepare?.Invoke(entry.Instance);
                }
                catch (Exception error)
                {
                    entry.DiscardRequested = true;
                    throw new InvalidOperationException($"Pooling.Instantiate: ошибка prepare у prefab {prefab.name}, выдача отменена, экземпляр отправлен на удаление", error);
                }
                cancelled = entry.DiscardRequested || entry.ReturnRequested || entry.Instance == null;
                if (entry.Instance == null) entry.DiscardRequested = true;
                if (!cancelled) entry.Instance.SetActive(true);
            }
        }
        finally
        {
            entry.Busy--;
            if (entry.DiscardRequested) Dispose(entry);
            else if (entry.ReturnRequested) ReturnEntry(entry);
        }

        return cancelled ? null : entry.Instance;
    }

    public static GameObject Instantiate(GameObject prefab, Transform parent) {return Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);}

    public static void Destroy(GameObject instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance), "Pooling.Destroy: объект отсутствует или уничтожен");
        if (!prefabMap.TryGetValue(instance.GetInstanceID(), out var entry) || !ReferenceEquals(entry.Instance, instance))
            throw new ArgumentException($"Pooling.Destroy: объект {instance.name} не выдан пулом", nameof(instance));
        var pool = entry.Pool;
        if (!pool.Active.Remove(entry)) return;
        pool.Returning.Add(entry);
        entry.ReturnRequested = true;
        if (entry.Busy == 0) ReturnEntry(entry);
    }

    private static void ReturnEntry(Entry entry)
    {
        var pool = entry.Pool;
        var instance = entry.Instance;
        entry.ReturnRequested = false;
        entry.Busy++;
        try
        {
            if (instance != null) instance.SetActive(false);
            if (!entry.DiscardRequested && instance != null) instance.transform.SetParent(PoolRoot, false);
        }
        finally
        {
            entry.Busy--;
        }
        pool.Returning.Remove(entry);
        if (entry.DiscardRequested || instance == null) {Dispose(entry); return;}
        if (!prefabMap.ContainsKey(entry.Id)) return;
        if (pool.Inactive.Count >= pool.Capacity) PruneInactive(pool);
        if (pool.Inactive.Count >= pool.Capacity) Dispose(entry);
        else pool.Inactive.Push(entry);
    }

    public static int CountInactive(GameObject prefab)
    {
        ValidatePrefab(prefab);
        if (!pools.TryGetValue(prefab, out var pool)) return 0;
        PruneInactive(pool);

        return pool.Inactive.Count;
    }

    public static int CountActive(GameObject prefab)
    {
        ValidatePrefab(prefab);
        if (!pools.TryGetValue(prefab, out var pool)) return 0;
        pool.Active.RemoveWhere(entry =>
        {
            if (entry.Instance != null) return false;
            prefabMap.Remove(entry.Id);

            return true;
        });

        return pool.Active.Count;
    }

    public static void SetCapacity(GameObject prefab, int maxInactive)
    {
        ValidatePrefab(prefab);
        if (maxInactive < 0) throw new ArgumentOutOfRangeException(nameof(maxInactive), maxInactive, "SetCapacity: maxInactive меньше нуля");
        var pool = GetOrCreate(prefab);
        pool.Capacity = maxInactive;
        PruneInactive(pool);
        while (pool.Inactive.Count > maxInactive) Dispose(pool.Inactive.Pop());
    }

    public static void Clear(GameObject prefab) {Clear(prefab, false);}

    public static void Clear(GameObject prefab, bool includeActive)
    {
        ValidatePrefab(prefab);
        if (!pools.TryGetValue(prefab, out var pool)) return;
        ClearPool(pool, includeActive);
    }

    public static void ClearAll() {ClearAll(false);}

    public static void ClearAll(bool includeActive) {foreach (var pool in new List<Pool>(pools.Values)) ClearPool(pool, includeActive);}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        ClearAll(true);
        pools.Clear();
        prefabMap.Clear();
        if (_poolRoot != null) DestroyObject(_poolRoot.gameObject);
        _poolRoot = null;
    }

    private static void ClearPool(Pool pool, bool includeActive)
    {
        var entries = new List<Entry>(pool.Inactive);
        pool.Inactive.Clear();
        if (includeActive)
        {
            entries.AddRange(pool.Active);
            entries.AddRange(pool.Returning);
            pool.Active.Clear();
            pool.Returning.Clear();
        }
        foreach (var entry in entries) entry.DiscardRequested = true;
        foreach (var entry in entries)
            if (entry.Busy == 0) Dispose(entry);
    }

    private static Pool GetOrCreate(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var pool)) {pool = new Pool(); pools[prefab] = pool;}

        return pool;
    }

    private static Entry CreateInstance(GameObject prefab, Pool pool)
    {
        var instance = Object.Instantiate(prefab, PoolRoot);
        instance.SetActive(false);
        var entry = new Entry {Id = instance.GetInstanceID(), Instance = instance, Pool = pool};
        prefabMap[entry.Id] = entry;

        return entry;
    }

    private static void PruneInactive(Pool pool)
    {
        if (pool.Inactive.Count == 0) return;
        var entries = pool.Scratch;
        entries.Clear();
        foreach (var entry in pool.Inactive) entries.Add(entry);
        pool.Inactive.Clear();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Instance != null) pool.Inactive.Push(entries[i]);
            else prefabMap.Remove(entries[i].Id);
        }
        entries.Clear();
    }

    private static void Dispose(Entry entry)
    {
        entry.DiscardRequested = true;
        entry.Pool.Returning.Remove(entry);
        entry.Pool.Active.Remove(entry);
        entry.Busy++;
        try
        {
            if (entry.Instance != null && entry.Instance.activeSelf) entry.Instance.SetActive(false);
        }
        finally
        {
            entry.Busy--;
        }
        prefabMap.Remove(entry.Id);
        DestroyObject(entry.Instance);
    }

    private static void DestroyObject(GameObject instance)
    {
        if (instance == null) return;
        if (Application.isPlaying) Object.Destroy(instance);
        else Object.DestroyImmediate(instance);
    }

    private static void ValidatePrefab(GameObject prefab) {if (prefab == null) throw new ArgumentNullException(nameof(prefab), "Pooling: prefab отсутствует или уничтожен");}
}
