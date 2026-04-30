# 🐟 Goofy Pooling

> Zero-config object pooling for Unity. Drop in, swap calls, done.

**by [youpzdev](https://github.com/youpzdev)**

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

## Install

Import `Goofy-Pooling.unitypackage` into your project or copy the contents anywhere into your `Assets/` folder.

No dependencies. No packages. No bullshit.

---

## Usage

```csharp
// ── All params ─────────────────────────────────────────────
Pooling.Instantiate(prefab, position, rotation, parent);

// ── Without rotation ───────────────────────────────────────
Pooling.Instantiate(prefab, position);

// ── Just prefab ────────────────────────────────────────────
Pooling.Instantiate(prefab);

// ── With parent ────────────────────────────────────────────
Pooling.Instantiate(prefab, parent: transform);

// ── Return to pool ─────────────────────────────────────────
Pooling.Destroy(bullet);

// ── Prewarm (optional) ─────────────────────────────────────
Pooling.Prewarm(prefab, 100);

// ── Cleanup ────────────────────────────────────────────────
Pooling.Clear(prefab);
Pooling.ClearAll();
```

Objects are automatically tracked by prefab reference. First call creates the pool, subsequent calls reuse existing instances.

---

## Benchmarks

Tested on **10 000 objects**, 5 runs average:

| | Spawn | Destroy | Total | GC |
|---|---|---|---|---|
| Default | 64ms | 3ms | 67ms | 4kb |
| **Goofy Pooling** | **33ms** | **19ms** | **52ms** | **0kb** |

**~2x faster spawn, zero GC allocations.**

> Destroy is slower than Unity's — that's the cost of `SetActive(false)` + pool bookkeeping.
> In practice: you save more on spawn than you spend on destroy, and you never trigger the GC.

<details>
<summary>1 000 objects</summary>

| | Spawn | Destroy | Total | GC |
|---|---|---|---|---|
| Default | 7ms | 0ms | 7ms | 0kb |
| **Goofy Pooling** | **3ms** | **1ms** | **4ms** | **0kb** |

</details>

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

At extreme counts, `SetActive` overhead dominates — that's Unity, not the pool.

</details>

---

## How it works

| | |
|---|---|
| 📦 **pools** | Maps prefab → `Stack` of inactive instances |
| 🗺️ **prefabMap** | Maps `instance.GetInstanceID()` (int) → source prefab |
| ⚡ **Instantiate** | Pops from stack or creates a new instance |
| 🗑️ **Destroy** | `SetActive(false)` and pushes back to stack |
| 🔥 **Prewarm** | Pre-fills the pool before gameplay — eliminates first-run spike |
| 🚀 **Lookup** | `int` key dictionary — no `GetComponent`, no reflection, O(1) |
| 🌍 **PoolRoot** | All pooled objects live under a `[Pool]` GameObject, `DontDestroyOnLoad` |

---

## Limitations

- Objects must be returned via `Pooling.Destroy()` — calling Unity's `Object.Destroy()` directly will leak the instance
- Pool never shrinks automatically — use `Clear()` / `ClearAll()` when you need to free memory (e.g. on scene unload)
- `transform.parent` is not reset on reuse — handle that yourself if needed
- First run without `Prewarm` will be slower — the pool allocates on demand

---

## Changelog

### v1.2.0
- `Queue` replaced with `Stack` — LIFO reuse keeps recently-used objects in CPU cache
- `prefabMap` key changed from `GameObject` to `int` (`GetInstanceID()`) — faster hash, cheaper equality
- `ContainsKey` + indexer replaced with `TryGetValue` — single dictionary lookup instead of two
- Added `Prewarm(prefab, count)` — pre-fill pool before gameplay to avoid first-run spike
- Added `Clear(prefab)` and `ClearAll()` — explicit pool cleanup
- Added `PoolRoot` — pooled objects are parented to a `[Pool]` object, survives scene loads

### v1.1.0
- `Instantiate` now accepts optional `position`, `rotation` and `parent` — all arguments are optional
- Removed `PooledObject` MonoBehaviour — replaced with `prefabMap` dictionary, no more `AddComponent` overhead

### v1.0.0
- Initial release

---

## Part of the Goofy Tools collection

| | |
|---|---|
| **goofy-pooling** | 🐟 You are here |
| [**goofy-timers**](https://github.com/youpzdev/unity-goofy-timers) | ⏱️ No-coroutine timer system |
| [**goofy-eventbus**](https://github.com/youpzdev/unity-goofy-eventbus) | 📡 Type-safe event bus |
| [**goofy-save**](https://github.com/youpzdev/unity-goofy-saves) | 💾 AES-256 encrypted save system |

---

## License

MIT — do whatever you want.