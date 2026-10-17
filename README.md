# Goofy Pooling

A static GameObject pool for Unity. Copy Goofy-Pooling/Scripts into Assets.
No runtime package dependencies. Calls belong on the Unity main thread.

```csharp
Pooling.SetCapacity(bulletPrefab, 100);
Pooling.Prewarm(bulletPrefab, 20);
var bullet = Pooling.Instantiate(bulletPrefab, position, rotation);
Pooling.Destroy(bullet);
```

## Checkout and return

```csharp
Pooling.Instantiate(prefab);
Pooling.Instantiate(prefab, position);
Pooling.Instantiate(prefab, position, rotation, parent);
Pooling.Instantiate(prefab, parent);
```

Omitted position is zero and omitted rotation is identity. A reused object
gets the requested world position, rotation and parent. Omitting parent
detaches it from the pool root or its previous parent.

Destroy returns an instance issued by this pool. It disables the object and
moves it under the persistent [Pool] root. An object that disabled itself can
still be returned. Repeated returns do not add duplicate entries. A foreign,
null or already destroyed object produces an argument exception.

The pool does not reset component state. Reset gameplay state before reuse.
Objects destroyed through Unity are discarded when encountered during reuse
or counting. Instantiate activates the object, so normal Unity lifecycle
callbacks still run.

## Capacity and counts

```csharp
Pooling.SetCapacity(prefab, maxInactive: 50);
Pooling.Prewarm(prefab, 20);
int available = Pooling.CountInactive(prefab);
int checkedOut = Pooling.CountActive(prefab);
```

Capacity limits retained inactive objects per prefab. The default is
int.MaxValue. It does not limit simultaneous checkouts. Lowering capacity
trims excess free objects immediately; zero disables retention. Excess
objects are destroyed when returned. Negative counts or capacity throw.

Prewarm tops up the number of free objects to count, bounded by capacity.
Calling it again with the same or smaller target creates nothing. Active
checkouts do not count toward this target. Counts exclude destroyed objects;
CountActive includes issued objects even if they disabled themselves.

## Cleanup

```csharp
Pooling.Clear(prefab);
Pooling.ClearAll();
Pooling.Clear(prefab, includeActive: true);
Pooling.ClearAll(includeActive: true);
```

Default cleanup destroys free instances and preserves outstanding checkouts.
They can still be returned afterward. includeActive also destroys checked
out instances. Both forms preserve configured capacities. Starting a new
play session resets instances, ownership and capacity, including when domain
reload is disabled. Destruction during play uses Unity's deferred Destroy;
outside play it uses DestroyImmediate.

Clear can run from lifecycle callbacks. Do not request new checkouts of an
object while its lifecycle callback is returning or destroying that object.

## Tests

Goofy-Pooling/Tests/Editor contains NUnit tests using real Unity GameObjects.
Run them as EditMode tests in the development stand with Unity Test Framework.
Copy only Scripts into projects that do not need the tests or Demo.

## License

MIT. See LICENSE.
