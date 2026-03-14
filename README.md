# 🐟 Goofy Pooling

> Zero-config object pooling for Unity. Drop in, swap calls, done.

**by [youpzdev](https://github.com/youpzz)**

---

## What is this

A static object pool that uses the same API as Unity's built-in `Instantiate` / `Destroy`. No setup, no PoolManager prefab, no configuration files. Just replace two method calls and your objects stop getting garbage collected.

```csharp
// Before
Instantiate(prefab, pos, rot);
Destroy(obj);

// After
Pooling.Instantiate(prefab, pos, rot);
Pooling.Destroy(obj);
```

That's it.

---

## Benchmarks

Tested on **1000 objects**, 7 runs average:

| | Spawn | Destroy | Total | GC |
|---|---|---|---|---|
| Default | 7ms | 0ms | 7ms | 0kb |
| **Goofy Pooling** | **3ms** | **1ms** | **4ms** | **0kb** |

**~2x faster** on real-world spawn counts. Zero allocations.

<details>
<summary>500 objects</summary>

| | Spawn | Destroy | Total | GC |
|---|---|---|---|---|
| Default | 3ms | 0ms | 3ms | 0kb |
| **Goofy Pooling** | **1ms** | **0ms** | **1ms** | **0kb** |

</details>

<details>
<summary>15 000 objects (stress test)</summary>

| | Spawn | Destroy | Total | GC |
|---|---|---|---|---|
| Default | 108ms | 6ms | 114ms | 0kb |
| **Goofy Pooling** | **55ms** | **40ms** | **95ms** | **0kb** |

At extreme counts, SetActive overhead dominates — that's Unity, not the pool.

</details>

---

## Install

Import `Goofy-Pooling.unitypackage` in your project or just drop content anywhere into your `Assets/` folder.

No dependencies. No packages. No bullshit.

---

## Usage

```csharp
// Spawn
GameObject bullet = Pooling.Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation);

// Return to pool
Pooling.Destroy(bullet);
```

Objects are automatically tracked by prefab reference. First call creates the pool, subsequent calls reuse existing instances.

---

## How it works

Two dictionaries:

- `pools` — maps prefab → queue of inactive instances
- `prefabMap` — maps instance → its source prefab

On `Instantiate`: dequeue an existing object or create a new one.  
On `Destroy`: `SetActive(false)` and enqueue back.

No `GetComponent`, no MonoBehaviour marker, no reflection. O(1) lookup.

---

## Limitations

- Objects must be returned via `Pooling.Destroy()` — calling Unity's `Object.Destroy()` directly will leak the instance
- Pool never shrinks — if you spawn 1000 bullets and never need that many again, they stay in memory
- `transform.parent` is not reset on reuse — handle that yourself if needed

---

## License

MIT — do whatever you want.
