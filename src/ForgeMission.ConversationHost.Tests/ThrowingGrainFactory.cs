using Orleans;
using Orleans.Runtime;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>Fails any call — proves structurally that a code path never reached a grain call
/// (Phase 43.16 Task 8c poison-input tests). Every member throws; there is no "real" overload to
/// special-case, since no member of <see cref="IGrainFactory"/> should ever be invoked on
/// unaddressable input regardless of which overload the caller happens to use.</summary>
internal sealed class ThrowingGrainFactory : IGrainFactory
{
    private static InvalidOperationException Fail() =>
        new("IGrainFactory must not be called for unaddressable input.");

    public TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string? grainClassNamePrefix = null) where TGrainInterface : IGrainWithGuidKey => throw Fail();
    public TGrainInterface GetGrain<TGrainInterface>(long primaryKey, string? grainClassNamePrefix = null) where TGrainInterface : IGrainWithIntegerKey => throw Fail();
    public TGrainInterface GetGrain<TGrainInterface>(string primaryKey, string? grainClassNamePrefix = null) where TGrainInterface : IGrainWithStringKey => throw Fail();
    public TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string keyExtension, string? grainClassNamePrefix = null) where TGrainInterface : IGrainWithGuidCompoundKey => throw Fail();
    public TGrainInterface GetGrain<TGrainInterface>(long primaryKey, string keyExtension, string? grainClassNamePrefix = null) where TGrainInterface : IGrainWithIntegerCompoundKey => throw Fail();
    public TGrainObserverInterface CreateObjectReference<TGrainObserverInterface>(IGrainObserver obj) where TGrainObserverInterface : IGrainObserver => throw Fail();
    public void DeleteObjectReference<TGrainObserverInterface>(IGrainObserver obj) where TGrainObserverInterface : IGrainObserver => throw Fail();
    public IGrain GetGrain(Type grainInterfaceType, Guid grainPrimaryKey) => throw Fail();
    public IGrain GetGrain(Type grainInterfaceType, long grainPrimaryKey) => throw Fail();
    public IGrain GetGrain(Type grainInterfaceType, string grainPrimaryKey) => throw Fail();
    public IGrain GetGrain(Type grainInterfaceType, Guid grainPrimaryKey, string keyExtension) => throw Fail();
    public IGrain GetGrain(Type grainInterfaceType, long grainPrimaryKey, string keyExtension) => throw Fail();
    TGrainInterface IGrainFactory.GetGrain<TGrainInterface>(GrainId grainId) => throw Fail();
    public IAddressable GetGrain(GrainId grainId) => throw Fail();
    public IAddressable GetGrain(GrainId grainId, GrainInterfaceType interfaceType) => throw Fail();
    public IAddressable GetGrain(Type grainInterfaceType, IdSpan grainPrimaryKey, string? keyExtension) => throw Fail();
    public IAddressable GetGrain(Type grainInterfaceType, IdSpan grainPrimaryKey) => throw Fail();
}
