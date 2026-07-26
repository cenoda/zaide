using System;
using System.Reflection;
using Xunit.Sdk;

namespace Zaide.Tests.Infrastructure;

/// <summary>
/// Resets mutable ReactiveUI/Splat state before each test in serialized UI
/// collections so routed-event and locator registrations do not leak.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ReactiveUiMutableStateResetAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest)
    {
        ReactiveUiTestBootstrap.ResetMutableState();
    }
}
