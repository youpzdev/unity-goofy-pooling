using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

public class PoolingTests
{
    private const string Prefix = "PoolingTests_";
    private GameObject prefab;

    [SetUp]
    public void SetUp()
    {
        ResetPool();
        prefab = new GameObject(Prefix + "Prefab");
    }

    [TearDown]
    public void TearDown() {ResetPool();}

    [Test]
    public void ReturnedInstanceLeavesItsPreviousParent()
    {
        var parent = new GameObject(Prefix + "Parent");
        var instance = Pooling.Instantiate(prefab, parent.transform);

        Pooling.Destroy(instance);

        Assert.That(instance.transform.parent, Is.Not.EqualTo(parent.transform), "Returned instance still belongs to its scene parent");
        Object.DestroyImmediate(parent);
        Assert.That(instance != null, Is.True, "Destroying the old parent destroyed a returned instance");
    }

    [Test]
    public void ReusedInstanceWithoutParentIsDetached()
    {
        var parent = new GameObject(Prefix + "Parent");
        var instance = Pooling.Instantiate(prefab, parent.transform);
        Pooling.Destroy(instance);

        var reused = Pooling.Instantiate(prefab);

        Assert.That(reused, Is.SameAs(instance), "Pool did not reuse the returned instance");
        Assert.That(reused.transform.parent, Is.Null, "No parent was requested, but the reused instance retained a parent");
    }

    [Test]
    public void DestroyedInactiveEntryIsSkipped()
    {
        var instance = Pooling.Instantiate(prefab);
        Pooling.Destroy(instance);
        Object.DestroyImmediate(instance);

        GameObject next = null;
        Assert.DoesNotThrow(() => next = Pooling.Instantiate(prefab), "Destroyed pooled entry must be discarded before reuse");
        Assert.That(next != null, Is.True, "Pool returned a destroyed object");
        Assert.That(next.activeSelf, Is.True, "Replacement instance is inactive");
    }

    [Test]
    public void ExternallyDisabledInstanceCanBeReturned()
    {
        var instance = Pooling.Instantiate(prefab);
        instance.SetActive(false);

        Pooling.Destroy(instance);
        var reused = Pooling.Instantiate(prefab);

        Assert.That(reused, Is.SameAs(instance), "activeSelf must not replace pool ownership state");
        Assert.That(reused.activeSelf, Is.True, "Reused instance was not activated");
    }

    [Test]
    public void ReturningAnInstanceTwiceDoesNotDuplicateIt()
    {
        var instance = Pooling.Instantiate(prefab);
        Pooling.Destroy(instance);
        Pooling.Destroy(instance);

        var first = Pooling.Instantiate(prefab);
        var second = Pooling.Instantiate(prefab);

        Assert.That(first, Is.SameAs(instance), "First checkout must reuse the returned instance");
        Assert.That(second, Is.Not.SameAs(first), "One instance was checked out twice");
    }

    [Test]
    public void OmittedRotationUsesIdentity()
    {
        var instance = Pooling.Instantiate(prefab);

        Assert.That(instance.transform.rotation, Is.EqualTo(Quaternion.identity), "Omitted rotation must produce an identity rotation");
    }

    [Test]
    public void PrewarmTopsUpAvailableCount()
    {
        Pooling.Prewarm(prefab, 3);
        Pooling.Prewarm(prefab, 2);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(3), "Prewarm must not add to an already sufficient pool");
        var active = Pooling.Instantiate(prefab);
        Pooling.Prewarm(prefab, 4);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(4));
        Assert.That(Pooling.CountActive(prefab), Is.EqualTo(1));
        Assert.That(active.activeSelf, Is.True);
    }

    [Test]
    public void CapacityLimitsInactiveWithoutLimitingCheckout()
    {
        Pooling.SetCapacity(prefab, 1);
        Pooling.Prewarm(prefab, 5);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(1));
        var first = Pooling.Instantiate(prefab);
        var second = Pooling.Instantiate(prefab);
        Assert.That(Pooling.CountActive(prefab), Is.EqualTo(2));
        Pooling.Destroy(first);
        Pooling.Destroy(second);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(1));
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(second == null, Is.True, "Excess instance must be destroyed on return");
    }

    [Test]
    public void ReducingCapacityTrimsFreeObjectsImmediately()
    {
        Pooling.Prewarm(prefab, 4);
        var active = Pooling.Instantiate(prefab);
        Pooling.SetCapacity(prefab, 0);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Assert.That(Pooling.CountActive(prefab), Is.EqualTo(1));
        Pooling.Destroy(active);
        Assert.That(active == null, Is.True);
    }

    [Test]
    public void CountsDiscardDestroyedObjects()
    {
        Pooling.Prewarm(prefab, 2);
        var active = Pooling.Instantiate(prefab);
        Object.DestroyImmediate(active);
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        var inactive = Pooling.Instantiate(prefab);
        Pooling.Destroy(inactive);
        Object.DestroyImmediate(inactive);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
    }

    [Test]
    public void ClearPreservesCheckoutAndCapacity()
    {
        Pooling.SetCapacity(prefab, 1);
        Pooling.Prewarm(prefab, 1);
        var active = Pooling.Instantiate(prefab);
        Pooling.Prewarm(prefab, 1);
        Pooling.Clear(prefab);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Assert.That(Pooling.CountActive(prefab), Is.EqualTo(1));
        Assert.That(active != null, Is.True);
        Pooling.Destroy(active);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(1), "Clear must preserve outstanding ownership");
        Pooling.Prewarm(prefab, 5);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(1), "Clear must preserve configured capacity");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClearIncludingActiveDestroysAllInstances(bool allPools)
    {
        var otherPrefab = new GameObject(Prefix + "OtherPrefab");
        var active = Pooling.Instantiate(prefab);
        var other = Pooling.Instantiate(otherPrefab);
        Pooling.Prewarm(prefab, 2);
        if (allPools) Pooling.ClearAll(true);
        else Pooling.Clear(prefab, true);
        Assert.That(active == null, Is.True);
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Assert.That(other == null, Is.EqualTo(allPools));
    }

    [Test]
    public void SessionResetDropsInstancesAndConfiguration()
    {
        Pooling.SetCapacity(prefab, 0);
        var active = Pooling.Instantiate(prefab);
        typeof(Pooling).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Assert.That(active == null, Is.True);
        Pooling.Prewarm(prefab, 3);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(3), "Previous play session capacity survived reset");
    }

    [Test]
    public void InvalidArgumentsAndForeignObjectsAreReported()
    {
        Assert.Throws<System.ArgumentNullException>(() => Pooling.Instantiate(null));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => Pooling.Prewarm(prefab, -1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => Pooling.SetCapacity(prefab, -1));
        Assert.Throws<System.ArgumentException>(() => Pooling.Destroy(prefab));
        Assert.That(prefab.activeSelf, Is.True, "Foreign object was changed after ownership failure");
    }

    [Test]
    public void ReuseAppliesRequestedTransform()
    {
        var firstParent = new GameObject(Prefix + "FirstParent");
        var secondParent = new GameObject(Prefix + "SecondParent");
        secondParent.transform.position = new Vector3(10, 5, -2);
        var instance = Pooling.Instantiate(prefab, firstParent.transform);
        Pooling.Destroy(instance);
        var position = new Vector3(2, 3, 4);
        var rotation = Quaternion.Euler(15, 70, 25);

        var reused = Pooling.Instantiate(prefab, position, rotation, secondParent.transform);

        Assert.That(reused, Is.SameAs(instance));
        Assert.That(reused.transform.parent, Is.SameAs(secondParent.transform));
        Assert.That(Vector3.Distance(reused.transform.position, position), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(reused.transform.rotation, rotation), Is.LessThan(0.01f));
    }

    [Test]
    public void ClearAllPreservesOutstandingObjectsInEveryPool()
    {
        var otherPrefab = new GameObject(Prefix + "OtherPrefab");
        var first = Pooling.Instantiate(prefab);
        var second = Pooling.Instantiate(otherPrefab);
        Pooling.Prewarm(prefab, 2);
        Pooling.Prewarm(otherPrefab, 3);

        Pooling.ClearAll();
        Pooling.Destroy(first);
        Pooling.Destroy(second);

        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(1));
        Assert.That(Pooling.CountInactive(otherPrefab), Is.EqualTo(1));
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountActive(otherPrefab), Is.Zero);
    }

    [Test]
    public void ClearDuringReturnDoesNotRetainTheDestroyedInstance()
    {
        var instance = Pooling.Instantiate(prefab);
        var callback = instance.AddComponent<PoolingClearOnDisable>();
        callback.Prefab = prefab;

        Pooling.Destroy(instance);

        Assert.That(instance == null, Is.True);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.Instantiate(prefab) != null, Is.True);
    }

    [Test]
    public void OriginalClearMethodGroupsRemainAssignable()
    {
        System.Action clearAll = Pooling.ClearAll;
        System.Action<GameObject> clearPrefab = Pooling.Clear;
        Pooling.Prewarm(prefab, 2);

        clearPrefab(prefab);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Pooling.Prewarm(prefab, 1);
        clearAll();
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
    }

    [Test]
    public void FirstActivationHasOwnershipAndRequestedTransform()
    {
        var parent = new GameObject(Prefix + "ActivationParent");
        var callback = prefab.AddComponent<PoolingReturnOnEnable>();
        callback.ReturnOnEnable = true;
        var position = new Vector3(9, -3, 7);
        var rotation = Quaternion.Euler(20, 40, 60);

        var instance = Pooling.Instantiate(prefab, position, rotation, parent.transform);
        var observed = instance.GetComponent<PoolingReturnOnEnable>();

        Assert.That(instance.activeSelf, Is.False, "First OnEnable could not return its owned instance");
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(1));
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Vector3.Distance(observed.ObservedPosition, position), Is.LessThan(0.0001f), "OnEnable observed the template position");
        Assert.That(Quaternion.Angle(observed.ObservedRotation, rotation), Is.LessThan(0.01f), "OnEnable observed the template rotation");
        Assert.That(observed.ObservedParent, Is.SameAs(parent.transform));
    }

    [Test]
    public void CountingAWarmedPoolDoesNotAllocate()
    {
        using (var calibration = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 16, ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            var knownAllocation = new byte[1024];
            calibration.Stop();
            System.GC.KeepAlive(knownAllocation);
            Assert.That(calibration.Count, Is.GreaterThan(0), "GC.Alloc recorder cannot detect a known live allocation on this runtime");
        }
        Pooling.Prewarm(prefab, 8);
        for (int i = 0; i < 10; i++) Pooling.CountInactive(prefab);
        int count = 0;
        int allocations;
        using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 2048, ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            for (int i = 0; i < 1000; i++) count += Pooling.CountInactive(prefab);
            recorder.Stop();
            allocations = recorder.Count;
        }
        Assert.That(count, Is.EqualTo(8000));
        Assert.That(allocations, Is.Zero, $"CountInactive produced {allocations} GC.Alloc samples over 1000 calls");
    }

    [Test]
    public void ClearDuringFirstActivationDestroysTheCheckout()
    {
        var callback = prefab.AddComponent<PoolingClearDuringCheckout>();
        PoolingClearDuringCheckout.Prefab = prefab;
        callback.ClearOnEnable = true;

        var instance = Pooling.Instantiate(prefab);

        Assert.That(instance == null, Is.True, "Clear from OnEnable must discard the checkout after activation completes");
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
    }

    [Test]
    public void ClearDuringCheckoutParentChangeDestroysTheCheckout()
    {
        var parent = new GameObject(Prefix + "ClearParent");
        var callback = prefab.AddComponent<PoolingClearDuringCheckout>();
        PoolingClearDuringCheckout.Prefab = prefab;
        callback.ClearParent = parent.transform;

        var instance = Pooling.Instantiate(prefab, parent.transform);

        Assert.That(instance == null, Is.True, "Clear from OnTransformParentChanged must discard the checkout after parent change completes");
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
    }

    [Test]
    public void ReturningFromOnDisableDuringClearDoesNotReportForeignOwnership()
    {
        var callback = prefab.AddComponent<PoolingReturnOnDisable>();
        callback.ReturnOnDisable = true;
        var instance = Pooling.Instantiate(prefab);
        callback.ReturnOnDisable = false;

        Assert.DoesNotThrow(() => Pooling.Clear(prefab, true));

        Assert.That(instance == null, Is.True);
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PrepareRunsBeforeActivationWithAssignedTransformAndOwnership(bool reused)
    {
        var parent = new GameObject(Prefix + "PrepareParent");
        prefab.AddComponent<PoolingPreparedState>();
        GameObject previous = null;
        if (reused) {previous = Pooling.Instantiate(prefab); Pooling.Destroy(previous);}
        var position = new Vector3(3, 4, 5);
        var rotation = Quaternion.Euler(10, 20, 30);
        int calls = 0;

        var instance = Pooling.Instantiate(prefab, position, rotation, parent.transform, prepare: candidate =>
        {
            calls++;
            Assert.That(candidate.activeSelf, Is.False);
            Assert.That(Pooling.CountActive(prefab), Is.EqualTo(1));
            Assert.That(candidate.transform.parent, Is.SameAs(parent.transform));
            Assert.That(Vector3.Distance(candidate.transform.position, position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(candidate.transform.rotation, rotation), Is.LessThan(0.01f));
            candidate.GetComponent<PoolingPreparedState>().Value = 42;
        });

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(instance.GetComponent<PoolingPreparedState>().ObservedValue, Is.EqualTo(42));
        Assert.That(instance.activeSelf, Is.True);
        if (reused) Assert.That(instance, Is.SameAs(previous));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PrepareFailureDiscardsItsCheckoutAndKeepsPoolUsable(bool reused)
    {
        if (reused) {var previous = Pooling.Instantiate(prefab); Pooling.Destroy(previous);}
        GameObject rejected = null;
        var cause = new System.ArgumentException("invalid projectile data");

        var error = Assert.Throws<System.InvalidOperationException>(() => Pooling.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent: null, prepare: candidate =>
        {
            rejected = candidate;
            throw cause;
        }));

        Assert.That(error.InnerException, Is.SameAs(cause));
        Assert.That(error.Message, Does.Contain("prepare").And.Contain(prefab.name));
        Assert.That(rejected == null, Is.True, "Partially prepared instance must be discarded");
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Assert.That(Pooling.Instantiate(prefab) != null, Is.True);
    }

    [Test]
    public void ReturningDuringPrepareCancelsActivationWithoutLosingOwnership()
    {
        GameObject returned = null;
        var result = Pooling.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent: null, prepare: candidate =>
        {
            returned = candidate;
            Pooling.Destroy(candidate);
            Pooling.Destroy(candidate);
        });

        Assert.That(result, Is.Null);
        Assert.That(returned.activeSelf, Is.False);
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.EqualTo(1));
        Assert.That(Pooling.Instantiate(prefab), Is.SameAs(returned));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClearingDuringPrepareCancelsAndDestroysTheCheckout(bool allPools)
    {
        GameObject cleared = null;
        var result = Pooling.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent: null, prepare: candidate =>
        {
            cleared = candidate;
            if (allPools) Pooling.ClearAll(true);
            else Pooling.Clear(prefab, true);
        });

        Assert.That(result == null, Is.True);
        Assert.That(cleared == null, Is.True);
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Assert.That(Pooling.Instantiate(prefab) != null, Is.True);
    }

    [Test]
    public void OriginalInstantiateNullParentAndMethodGroupsRemainCompatible()
    {
        System.Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiate = Pooling.Instantiate;
        System.Func<GameObject, Transform, GameObject> instantiateWithParent = Pooling.Instantiate;
        var positionalNull = Pooling.Instantiate(prefab, Vector3.zero, Quaternion.identity, null);
        var viaDelegate = instantiate(prefab, Vector3.one, Quaternion.identity, null);
        var parentDelegate = instantiateWithParent(prefab, null);

        Assert.That(positionalNull.activeSelf, Is.True);
        Assert.That(viaDelegate.transform.position, Is.EqualTo(Vector3.one));
        Assert.That(parentDelegate.transform.parent, Is.Null);
    }

    [Test]
    public void PrepareFailureAfterReturnDoesNotPutRejectedInstanceBackInPool()
    {
        GameObject rejected = null;
        Assert.Throws<System.InvalidOperationException>(() => Pooling.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent: null, prepare: candidate =>
        {
            rejected = candidate;
            Pooling.Destroy(candidate);
            throw new System.InvalidOperationException("preparation cancelled");
        }));

        Assert.That(rejected == null, Is.True);
        Assert.That(Pooling.CountActive(prefab), Is.Zero);
        Assert.That(Pooling.CountInactive(prefab), Is.Zero);
        Assert.That(Pooling.Instantiate(prefab) != null, Is.True);
    }

    private static void ResetPool()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        foreach (var field in typeof(Pooling).GetFields(flags))
        {
            if (field.GetValue(null) is IDictionary dictionary) dictionary.Clear();
            if (field.GetValue(null) is Transform root && root != null)
            {
                Object.DestroyImmediate(root.gameObject);
                field.SetValue(null, null);
            }
        }

        foreach (var instance in Resources.FindObjectsOfTypeAll<GameObject>())
            if (instance != null && instance.name.StartsWith(Prefix)) Object.DestroyImmediate(instance);
    }
}

[ExecuteAlways]
public class PoolingClearOnDisable : MonoBehaviour
{
    public GameObject Prefab;

    private void OnDisable()
    {
        if (Prefab != null) Pooling.Clear(Prefab, true);
    }
}

[ExecuteAlways]
public class PoolingReturnOnEnable : MonoBehaviour
{
    public bool ReturnOnEnable;
    public Vector3 ObservedPosition;
    public Quaternion ObservedRotation;
    public Transform ObservedParent;

    private void OnEnable()
    {
        if (!ReturnOnEnable) return;
        ObservedPosition = transform.position;
        ObservedRotation = transform.rotation;
        ObservedParent = transform.parent;
        Pooling.Destroy(gameObject);
    }
}

[ExecuteAlways]
public class PoolingClearDuringCheckout : MonoBehaviour
{
    public static GameObject Prefab;

    public bool ClearOnEnable;
    public Transform ClearParent;

    private void OnEnable()
    {
        if (ClearOnEnable && Prefab != null) Pooling.Clear(Prefab, true);
    }

    private void OnTransformParentChanged()
    {
        if (Prefab != null && ClearParent != null && transform.parent == ClearParent) Pooling.Clear(Prefab, true);
    }
}

[ExecuteAlways]
public class PoolingReturnOnDisable : MonoBehaviour
{
    public bool ReturnOnDisable;

    private void OnDisable()
    {
        if (ReturnOnDisable) Pooling.Destroy(gameObject);
    }
}

[ExecuteAlways]
public class PoolingPreparedState : MonoBehaviour
{
    public int Value;
    public int ObservedValue;

    private void OnEnable()
    {
        ObservedValue = Value;
    }
}
