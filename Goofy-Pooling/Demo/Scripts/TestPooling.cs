using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TestPooling : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int spawnAmount = 100;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button runDefaultButton;
    [SerializeField] private Button runPoolButton;
    [Space(10), Header("Spawn Pos Range Min/Max")]
    [SerializeField] private Vector2 xPos;
    [SerializeField] private Vector2 yPos;
    [SerializeField] private Vector2 zPos;

    private long defaultSpawnGC, defaultDestroyGC;
    private long poolingSpawnGC, poolingDestroyGC;
    private long defaultSpawnTime, defaultDestroyTime;
    private long poolingSpawnTime, poolingDestroyTime;
    private int defaultCycle, poolingCycle;

    private void Start()
    {
        runDefaultButton?.onClick.AddListener(DefaultTest);
        runPoolButton?.onClick.AddListener(PoolingTest);
    }
    private void DefaultTest()
    {
        var spawned = new GameObject[spawnAmount];
        var sw = Stopwatch.StartNew();
        long gcBefore = GC.GetTotalMemory(false);

        for (int i = 0; i < spawnAmount; i++) spawned[i] = Instantiate(prefab, RandomPos(), Quaternion.identity);

        defaultSpawnTime = sw.ElapsedMilliseconds;
        defaultSpawnGC = GC.GetTotalMemory(false) - gcBefore;

        sw.Restart();
        gcBefore = GC.GetTotalMemory(false);

        for (int i = 0; i < spawnAmount; i++) Destroy(spawned[i]);

        defaultDestroyTime = sw.ElapsedMilliseconds;
        defaultDestroyGC = GC.GetTotalMemory(false) - gcBefore;

        defaultCycle++;

        UpdateUI();
    }
    private void PoolingTest()
    {
        var spawned = new GameObject[spawnAmount];
        var sw = Stopwatch.StartNew();
        long gcBefore = GC.GetTotalMemory(false);

        for (int i = 0; i < spawnAmount; i++) spawned[i] = Pooling.Instantiate(prefab, RandomPos(), Quaternion.identity);

        poolingSpawnTime = sw.ElapsedMilliseconds;
        poolingSpawnGC = GC.GetTotalMemory(false) - gcBefore;

        sw.Restart();
        gcBefore = GC.GetTotalMemory(false);

        for (int i = 0; i < spawnAmount; i++) Pooling.Destroy(spawned[i]);

        poolingDestroyTime = sw.ElapsedMilliseconds;
        poolingDestroyGC = GC.GetTotalMemory(false) - gcBefore;

        poolingCycle++;

        UpdateUI();
    }
    private void UpdateUI()
    {
        statsText.text =
            $"Spawn count: {spawnAmount}\n\n" +
            $"Default  (run: {defaultCycle})\n" +
            $"  spawn:   {defaultSpawnTime}ms  | gc: {defaultSpawnGC / 1024}kb\n" +
            $"  destroy: {defaultDestroyTime}ms  | gc: {defaultDestroyGC / 1024}kb\n\n" +
            $"  summary: {defaultDestroyTime + defaultSpawnTime}ms  | gc: {(defaultSpawnGC / 1024) + (defaultDestroyGC / 1024)}kb\n" +
            $"Pooling  (run: {poolingCycle})\n" +
            $"  spawn:   {poolingSpawnTime}ms  | gc: {poolingSpawnGC / 1024}kb\n" +
            $"  destroy: {poolingDestroyTime}ms  | gc: {poolingDestroyGC / 1024}kb" +
            $"  summary: {poolingDestroyTime + poolingSpawnTime}ms  | gc: {(poolingSpawnGC / 1024) + (poolingDestroyGC / 1024)}kb\n\n" +
            $"Result: {defaultDestroyTime + defaultSpawnTime - (poolingDestroyTime + poolingSpawnTime)}ms";
    }
    private Vector3 RandomPos() => new(UnityEngine.Random.Range(xPos.x, xPos.y), UnityEngine.Random.Range(yPos.x, yPos.y), UnityEngine.Random.Range(zPos.x, zPos.y));
}