using System.Reflection;
using ECMAScript;
using Microsoft.AspNetCore.Components;

namespace Jazor.RazorVue.Sg.Test;

[TestClass]
public sealed class RouterComponentBindingTests
{
    [TestMethod]
    public void VueRoute_RouterLinkProxy_ExposesStronglyTypedRazorContract()
    {
        var componentType = typeof(ECMAScript.VueRouterLink);
        Assert.IsTrue(typeof(ComponentBase).IsAssignableFrom(componentType));

        var component = componentType.GetCustomAttribute<ECMAScriptAttribute>();
        Assert.IsNotNull(component);
        Assert.AreEqual("vue-router", component!.Import);
        Assert.AreEqual("RouterLink", componentType.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);

        Assert.AreEqual(
            "class",
            componentType.GetProperty(nameof(ECMAScript.VueRouterLink.CssClass))!
                .GetCustomAttribute<ECMAScript.ECMAScriptNameAttribute>()?.Name);
        Assert.AreEqual(
            "style",
            componentType.GetProperty(nameof(ECMAScript.VueRouterLink.CssStyle))!
                .GetCustomAttribute<ECMAScript.ECMAScriptNameAttribute>()?.Name);

        var to = componentType.GetProperty(nameof(ECMAScript.VueRouterLink.To));
        Assert.IsNotNull(to);
        Assert.AreEqual(typeof(ECMAScript.RouteLocationRaw), to!.PropertyType);
        Assert.IsNotNull(to.GetCustomAttribute<ParameterAttribute>());
        Assert.IsNotNull(to.GetCustomAttribute<EditorRequiredAttribute>());

        Assert.AreEqual(
            typeof(EventCallback<ECMAScript.MouseEvent>),
            componentType.GetProperty(nameof(ECMAScript.VueRouterLink.OnClick))!.PropertyType);
        Assert.AreEqual(
            typeof(RenderFragment<ECMAScript.RouterLinkSlotScope>),
            componentType.GetProperty(nameof(ECMAScript.VueRouterLink.ChildContent))!.PropertyType);

        var additionalAttributes = componentType.GetProperty(nameof(ECMAScript.VueRouterLink.AdditionalAttributes));
        Assert.IsNotNull(additionalAttributes);
        Assert.AreEqual(
            typeof(IReadOnlyDictionary<string, object?>),
            additionalAttributes!.PropertyType);
        Assert.IsTrue(additionalAttributes.GetCustomAttribute<ParameterAttribute>()!.CaptureUnmatchedValues);
    }

    [TestMethod]
    public void VueRoute_RouterViewProxy_ExposesTypedPropsAndScopedSlot()
    {
        var componentType = typeof(ECMAScript.VueRouterView);
        Assert.IsTrue(typeof(ComponentBase).IsAssignableFrom(componentType));

        var component = componentType.GetCustomAttribute<ECMAScriptAttribute>();
        Assert.IsNotNull(component);
        Assert.AreEqual("vue-router", component!.Import);
        Assert.AreEqual("RouterView", componentType.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);
        Assert.AreEqual(typeof(string), componentType.GetProperty(nameof(ECMAScript.VueRouterView.Name))!.PropertyType);
        Assert.AreEqual(typeof(ECMAScript.RouteLocationNormalized), componentType.GetProperty(nameof(ECMAScript.VueRouterView.Route))!.PropertyType);
        Assert.AreEqual(typeof(RenderFragment<ECMAScript.RouterViewSlotScope>), componentType.GetProperty(nameof(ECMAScript.VueRouterView.ChildContent))!.PropertyType);
        Assert.IsTrue(componentType.GetProperty(nameof(ECMAScript.VueRouterView.AdditionalAttributes))!.GetCustomAttribute<ParameterAttribute>()!.CaptureUnmatchedValues);
    }
}
