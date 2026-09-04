using System.Reflection;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.ClientRuntime;

/// <summary>
/// Phase 43.21 Task 1 — a Project Mission Run has no local tool authority, and cannot acquire any.
///
/// This exists because the rule was broken once, in a way no test caught: a live default Janus run
/// was handed the session's real capabilities and executed <c>ls /</c> on the machine, simply
/// because someone pressed Run. Every assertion below is therefore about ABSENCE — what this path
/// cannot reach — rather than about a branch choosing not to. A rule that says "don't execute" can
/// be regressed by one edit; machinery that is not there cannot be called.
/// </summary>
public class ProjectMissionToolAuthorityTests
{
    /// <summary>The session cannot be given the means to execute anything, because it does not
    /// accept them. Compare with the legacy Janus session, which takes both — that contrast is the
    /// point, and a regression that "helpfully" restores either parameter fails here.</summary>
    [Fact]
    public void TheProjectMissionSession_TakesNoCapabilityRegistryOrDispatcher()
    {
        var parameters = ConstructorParameterTypes(typeof(ProjectMissionRuntimeSession));

        Assert.DoesNotContain(typeof(CapabilityRegistry), parameters);
        Assert.DoesNotContain(typeof(ICapabilityDispatcher), parameters);

        // The comparison that gives the assertion above its meaning: the legacy path DOES take
        // them, so this is a deliberate difference rather than an accident of the type's shape.
        var legacy = ConstructorParameterTypes(typeof(ConversationRuntimeSession));
        Assert.Contains(typeof(CapabilityRegistry), legacy);
        Assert.Contains(typeof(ICapabilityDispatcher), legacy);
    }

    /// <summary>It also does not hold them privately, having obtained them some other way.</summary>
    [Fact]
    public void TheProjectMissionSession_HoldsNoExecutionMachinery()
    {
        foreach (var type in FieldTypes(typeof(ProjectMissionRuntimeSession)))
        {
            Assert.NotEqual(typeof(CapabilityRegistry), type);
            Assert.NotEqual(typeof(ICapabilityDispatcher), type);
            Assert.NotEqual(typeof(ToolExecutorRegistry), type);
            // Not even the authorized hand-off the legacy path uses: having it would put a local
            // executor one call away.
            Assert.NotEqual(typeof(ConversationToolHandOff), type);
        }
    }

    /// <summary>The refusal that answers a stray tool request is built the same way — it reports,
    /// it never executes.</summary>
    [Fact]
    public void TheToolRefusal_HoldsAndTakesNoExecutionMachinery()
    {
        var parameters = ConstructorParameterTypes(typeof(ProjectMissionToolRefusal));
        var fields = FieldTypes(typeof(ProjectMissionToolRefusal)).ToArray();

        foreach (var forbidden in new[]
                 {
                     typeof(CapabilityRegistry), typeof(ICapabilityDispatcher),
                     typeof(ToolExecutorRegistry), typeof(ConversationToolHandOff),
                 })
        {
            Assert.DoesNotContain(forbidden, parameters);
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    /// <summary>The transport contract has no capability member either, so a surface has nothing
    /// to send even if it wanted to.</summary>
    [Fact]
    public void TheClientRuntimeRunContract_CarriesNoCapability()
    {
        Assert.Equal(
            ["SessionId", "CommandId", "Input"],
            typeof(ForgeMission.ClientRuntime.Transport.StartProjectMissionRunRequest)
                .GetProperties().Select(property => property.Name));
    }

    private static Type[] ConstructorParameterTypes(Type type) =>
        [.. type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)];

    private static IEnumerable<Type> FieldTypes(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(field => field.FieldType);
}
