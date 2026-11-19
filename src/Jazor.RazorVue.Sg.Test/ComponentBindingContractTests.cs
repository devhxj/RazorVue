using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ECMAScript;
using ECMAScript.Contract;
using ECMAScript.ElementPlus;
using ECMAScript.TDesign;
using ECMAScript.Vuetify;
using Microsoft.AspNetCore.Components;
using static ECMAScript.Vue;

namespace Jazor.RazorVue.Sg.Test;

// Verifies proxy API contracts shared by Vue, component-library, and native-union bindings.
// 验证 Vue、组件库和原生 union 绑定共享的代理 API 合同，防止 authoring 表面回退为弱类型包装。
#pragma warning disable CA1416

[TestClass]
public sealed class ComponentBindingContractTests
{
    [TestMethod]
    public void Vuetify_CreateVuetify_UsesStronglyTypedOptionsAndPlugin()
    {
        var overloads = typeof(Vuetify)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name == nameof(Vuetify.CreateVuetify))
            .OrderBy(static method => method.GetParameters().Length)
            .ToArray();

        Assert.AreEqual(2, overloads.Length);
        Assert.IsTrue(overloads.All(static method => method.ReturnType == typeof(VuetifyPlugin)));

        CollectionAssert.AreEqual(
            Array.Empty<Type>(),
            overloads[0].GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(VuetifyOptions) },
            overloads[1].GetParameters().Select(static parameter => parameter.ParameterType).ToArray());

        Assert.IsTrue(typeof(VuePlugin).IsAssignableFrom(typeof(VuetifyPlugin)));
        Assert.IsTrue(typeof(VuePluginOptions).IsAssignableFrom(typeof(VuetifyOptions)));
    }

    [TestMethod]
    public void Vuetify_ImportHosts_UseEcmaScriptImports_InsteadOfModuleEntryMarkers()
    {
        AssertEcmaScriptImport(typeof(Vuetify), "vuetify");
        AssertEcmaScriptSupport(typeof(VuetifyComponents));
        AssertEcmaScriptSupport(typeof(VuetifyLabsComponents));
        AssertEcmaScriptSupport(typeof(VuetifyDirectives));
        AssertEcmaScriptImport(
            typeof(VuetifyComponents).GetProperty(nameof(VuetifyComponents.VBtn))!,
            "vuetify/components/VBtn");
        // VCalendar 等 9 个组件已从 labs 迁到 stable：只有 stable 路径在当前版本可解析，
        // 声明值必须跟随上游（labs 路径经 exports 通配会解析失败）。
        AssertEcmaScriptImport(
            typeof(VuetifyLabsComponents).GetProperty(nameof(VuetifyLabsComponents.VCalendar))!,
            "vuetify/components/VCalendar");
        AssertEcmaScriptImport(
            typeof(VuetifyDirectives).GetProperty(nameof(VuetifyDirectives.Ripple))!,
            "vuetify/directives/ripple");
    }

    [TestMethod]
    public void Vuetify_RuntimeShapes_UseHostOrStructuralContracts_WithoutModuleGeneration()
    {
        var runtimeShapes = new[]
        {
            typeof(IVuetifyComponent),
            typeof(VuetifyPlugin),
            typeof(VuetifyOptions),
            typeof(VuetifyThemeOptions),
            typeof(VuetifyThemeVariationOptions),
            typeof(VuetifyDisplayOptions),
            typeof(VuetifyDisplayBreakpoint),
            typeof(VuetifyDisplayThresholds),
            typeof(VuetifyIconOptions),
            typeof(VuetifyLocaleOptions),
            typeof(VuetifyDateOptions),
            typeof(VuetifyComponentRegistry),
            typeof(VuetifyLabsComponentRegistry),
            typeof(VuetifyDirectiveRegistry),
            typeof(VuetifyDirective)
        };

        foreach (var type in runtimeShapes)
            AssertEcmaScriptSupport(type);
    }

    [TestMethod]
    public void TDesign_ImportHosts_UseEcmaScriptImports_InsteadOfModuleEntryMarkers()
    {
        // 声明值必须是上游公开入口（package.json 的 module 字段），不是 Jazor 自造简写。
        AssertEcmaScriptImport(typeof(TDesign), "tdesign-vue-next/es/index.mjs");
        AssertEcmaScriptImport(typeof(TComponents), "tdesign-vue-next/es/index.mjs");
    }

    [TestMethod]
    public void TDesign_RuntimeShapes_UseHostOrStructuralContracts_WithoutModuleGeneration()
    {
        var runtimeShapes = new[]
        {
            typeof(ITDesignComponent),
            typeof(TPlugin),
            typeof(TInstallOptions),
            typeof(TGlobalConfig),
            typeof(TComponentRegistry),
            typeof(TStyles),
            typeof(TMenuQueryData),
            typeof(TMenuRoute),
            typeof(TTabsDragSortContext)
        };

        foreach (var type in runtimeShapes)
            AssertEcmaScriptSupport(type);
    }

    [TestMethod]
    public void TDesign_PluginAndInstallSurface_UsesStronglyTypedContracts()
    {
        var defaultExport = typeof(TDesign).GetProperty(nameof(TDesign.Default), BindingFlags.Public | BindingFlags.Static);
        var version = typeof(TDesign).GetProperty(nameof(TDesign.Version), BindingFlags.Public | BindingFlags.Static);
        var installOverloads = typeof(TDesign)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name == nameof(TDesign.Install))
            .OrderBy(static method => method.GetParameters().Length)
            .ToArray();

        Assert.IsNotNull(defaultExport);
        Assert.AreEqual(typeof(TPlugin), defaultExport!.PropertyType);
        Assert.AreEqual("default", defaultExport.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);

        Assert.IsNotNull(version);
        Assert.AreEqual(typeof(string), version!.PropertyType);

        Assert.AreEqual(2, installOverloads.Length);
        CollectionAssert.AreEqual(
            new[] { typeof(VueApp) },
            installOverloads[0].GetParameters().Select(static parameter => parameter.ParameterType).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(VueApp), typeof(TInstallOptions) },
            installOverloads[1].GetParameters().Select(static parameter => parameter.ParameterType).ToArray());

        Assert.IsTrue(typeof(VuePlugin).IsAssignableFrom(typeof(TPlugin)));
        Assert.IsTrue(typeof(VuePluginOptions).IsAssignableFrom(typeof(TInstallOptions)));
        Assert.AreEqual(typeof(string), typeof(TGlobalConfig).GetProperty(nameof(TGlobalConfig.ClassPrefix), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
    }

    [TestMethod]
    public void ElementPlus_UsesSharedVueUnionContracts_ForCommonAuthoringShapes()
    {
        Assert.AreEqual(typeof(VueStyleValue), typeof(ElComponentBase).GetProperty(nameof(ElComponentBase.CssStyle), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueTeleportTarget), typeof(ElLoadingOptions).GetProperty(nameof(ElLoadingOptions.Target), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueBooleanStringValue), typeof(ElLinkConfig).GetProperty(nameof(ElLinkConfig.Underline), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
    }

    [TestMethod]
    public void Vuetify_UsesSharedVueDirectiveAndComponentContracts()
    {
        var vuetifyComponents = typeof(VuetifyOptions).GetProperty(nameof(VuetifyOptions.Components), BindingFlags.Public | BindingFlags.Instance);
        var vuetifyDirectives = typeof(VuetifyOptions).GetProperty(nameof(VuetifyOptions.Directives), BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(vuetifyComponents);
        Assert.IsNotNull(vuetifyDirectives);
        Assert.AreEqual(typeof(VueComponentRegistry), vuetifyComponents.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueDirectiveRegistry), vuetifyDirectives.PropertyType.UnwrapNullable());
        Assert.IsTrue(typeof(VueComponentRegistry).IsAssignableFrom(typeof(VuetifyComponentRegistry)));
        Assert.IsTrue(typeof(VueComponentRegistry).IsAssignableFrom(typeof(VuetifyLabsComponentRegistry)));
        Assert.IsTrue(typeof(VueDirectiveRegistry).IsAssignableFrom(typeof(VuetifyDirectiveRegistry)));
        Assert.IsTrue(typeof(VueDirective).IsAssignableFrom(typeof(VuetifyDirective)));
        Assert.IsTrue(typeof(VuePlugin).IsAssignableFrom(typeof(VuetifyPlugin)));
    }

    [TestMethod]
    public void Vuetify_ComponentExports_AreConcreteComponentTypes()
    {
        AssertComponentExportsMatchRegistry(typeof(VuetifyComponents), typeof(VuetifyComponentRegistry));
        AssertComponentExportsMatchRegistry(typeof(VuetifyLabsComponents), typeof(VuetifyLabsComponentRegistry));
    }

    private static void AssertComponentExportsMatchRegistry(Type exportHost, Type registryHost)
    {
        var exportedComponents = exportHost
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
        var registryProperties = registryHost
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToDictionary(static property => property.Name, StringComparer.Ordinal);

        Assert.IsTrue(exportedComponents.Length > 0);
        foreach (var property in exportedComponents)
        {
            Assert.IsTrue(typeof(ECMAScript.Vue.IVueComponent).IsAssignableFrom(property.PropertyType), property.Name);
            Assert.IsTrue(typeof(IVuetifyComponent).IsAssignableFrom(property.PropertyType), property.Name);
            Assert.AreEqual(property.PropertyType, registryProperties[property.Name].PropertyType.UnwrapNullable(), property.Name);
        }
    }

    [TestMethod]
    public void Vuetify_DirectiveExports_AreConcreteDirectiveTypes()
    {
        var exportedDirectives = typeof(VuetifyDirectives)
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
        var registryProperties = typeof(VuetifyDirectiveRegistry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToDictionary(static property => property.Name, StringComparer.Ordinal);

        Assert.IsTrue(exportedDirectives.Length > 0);
        foreach (var property in exportedDirectives)
        {
            Assert.IsTrue(typeof(VuetifyDirective).IsAssignableFrom(property.PropertyType), property.Name);
            Assert.IsTrue(typeof(VueDirective).IsAssignableFrom(property.PropertyType), property.Name);
            Assert.AreEqual(property.PropertyType, registryProperties[property.Name].PropertyType.UnwrapNullable(), property.Name);
        }
    }

    [TestMethod]
    public void Vuetify_OptionsAndRegistries_DoNotExposeObjectProperties()
    {
        var optionTypes = typeof(VuetifyOptions).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace == "ECMAScript.Vuetify" &&
                (type.Name.EndsWith("Options", StringComparison.Ordinal) ||
                 type.Name.EndsWith("Registry", StringComparison.Ordinal)))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.IsTrue(optionTypes.Length > 0);
        foreach (var property in optionTypes.SelectMany(static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
        {
            if (IsUnionValueProperty(property))
                continue;

            Assert.AreNotEqual(typeof(object), property.PropertyType.UnwrapNullable(), $"{property.DeclaringType?.Name}.{property.Name}");
        }
    }

    [TestMethod]
    public void Vuetify_AuthoringComponents_ExposeOnlyAdditionalAttributesAsObjectSink()
    {
        var componentTypes = typeof(Vuetify).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace == "ECMAScript.Vuetify" &&
                type.IsClass &&
                !type.IsAbstract &&
                typeof(Microsoft.AspNetCore.Components.ComponentBase).IsAssignableFrom(type) &&
                type.GetCustomAttribute<ECMAScriptAttribute>()?.Import is not null)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.IsTrue(componentTypes.Length > 0);
        CollectionAssert.AreEquivalent(
            GetVuetifyRuntimeComponentNames()
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray(),
            componentTypes
                .Select(static type => type.Name)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray());

        foreach (var componentType in componentTypes)
        {
            var additionalAttributes = componentType.GetProperty("AdditionalAttributes", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(additionalAttributes, componentType.FullName);
            Assert.AreEqual(
                typeof(IReadOnlyDictionary<string, object?>),
                additionalAttributes!.PropertyType.UnwrapNullable(),
                componentType.FullName);

            var parameter = additionalAttributes.GetCustomAttribute<Microsoft.AspNetCore.Components.ParameterAttribute>();
            Assert.IsNotNull(parameter, componentType.FullName);
            Assert.IsTrue(parameter!.CaptureUnmatchedValues, componentType.FullName);

            foreach (var property in componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.Name == "AdditionalAttributes")
                    continue;

                AssertNotObject(property.PropertyType, $"{componentType.Name}.{property.Name}");
            }
        }
    }

    [TestMethod]
    public void Vuetify_ComponentNames_UseMemberLevelGeneralMetadata()
    {
        var componentTypes = typeof(Vuetify).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace == "ECMAScript.Vuetify" &&
                typeof(ComponentBase).IsAssignableFrom(type))
            .ToArray();

        var names = new (Type Type, string Property, string RuntimeName)[]
        {
            (typeof(VAlert), nameof(VAlert.CssClass), "class"),
            (typeof(VAlert), nameof(VAlert.CssStyle), "style"),
            (typeof(VAutocomplete), nameof(VAutocomplete.SelectedValue), "modelValue"),
            (typeof(VDatePicker), nameof(VDatePicker.HeaderText), "header"),
            (typeof(VImg), nameof(VImg.CrossOrigin), "crossorigin"),
            (typeof(VImg), nameof(VImg.ReferrerPolicy), "referrerpolicy"),
            (typeof(VDataTable), nameof(VDataTable.HeaderSelect), "header.data-table-select"),
            (typeof(VDataTable), nameof(VDataTable.HeaderExpand), "header.data-table-expand"),
            (typeof(VDataTable), nameof(VDataTable.BodyPrepend), "body.prepend"),
            (typeof(VDataTable), nameof(VDataTable.BodyAppend), "body.append"),
            (typeof(VDataTable), nameof(VDataTable.FooterPrepend), "footer.prepend")
        };

        foreach (var (type, propertyName, runtimeName) in names)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, $"{type.Name}.{propertyName}");
            Assert.AreEqual(
                runtimeName,
                property!.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name,
                $"{type.Name}.{propertyName}");
        }
    }

    [TestMethod]
    public void Vuetify_EventParameters_DeclareExactListenerNames()
    {
        var componentTypes = typeof(Vuetify).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace == "ECMAScript.Vuetify" &&
                typeof(ComponentBase).IsAssignableFrom(type) &&
                type.GetCustomAttribute<ECMAScriptAttribute>()?.Import is not null)
            .ToArray();

        var eventProperties = componentTypes
            .SelectMany(static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static property =>
                property.PropertyType == typeof(EventCallback) ||
                property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() == typeof(EventCallback<>))
            .ToArray();

        Assert.IsTrue(eventProperties.Length > 0);
        foreach (var property in eventProperties)
        {
            var runtimeName = property.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name;
            Assert.IsFalse(string.IsNullOrWhiteSpace(runtimeName), $"{property.DeclaringType?.Name}.{property.Name}");
        }

        Assert.AreEqual(
            "onClick:close",
            typeof(VAlert).GetProperty(nameof(VAlert.OnClickClose))?.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);
        Assert.AreEqual(
            "onUpdate:modelValue",
            typeof(VAlert).GetProperty(nameof(VAlert.ModelValueChanged))?.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);
        Assert.AreEqual(
            "onLoadstart",
            typeof(VImg).GetProperty(nameof(VImg.OnLoadStart))?.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);
        Assert.AreEqual(
            "optionsChanged",
            typeof(VDataIterator).GetProperty(nameof(VDataIterator.OptionsChanged))?.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);
        Assert.IsNull(
            typeof(ECMAScript.VueContract.VueInjectAttribute).Assembly
                .GetType("ECMAScript.VueContract.VueLibraryEmitAttribute"));

        Assert.IsNotNull(typeof(VDialog).GetProperty(nameof(VDialog.OnKeydown)));
        Assert.IsNotNull(typeof(VImg).GetProperty(nameof(VImg.OnError)));
    }

    [TestMethod]
    public void TDesign_CurrentCatalog_MapsEveryRuntimeComponentAndUsesCurrentMemberProtocol()
    {
        var repositoryRoot = FindRepositoryRoot();
        var catalog = ReadTDesignCatalog(repositoryRoot);
        var runtimeExports = ReadTDesignRuntimeExports(repositoryRoot);
        var componentTypes = GetTDesignComponentTypes();
        var componentExports = typeof(TComponents)
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
        var registryProperties = typeof(TComponentRegistry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(120, catalog.SourceEntryCount);
        Assert.AreEqual(118, catalog.Components.Count);
        Assert.AreEqual(118, componentTypes.Length);
        Assert.AreEqual(118, componentExports.Length);
        Assert.AreEqual(118, registryProperties.Length);
        CollectionAssert.AreEqual(
            catalog.Components.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray(),
            componentTypes.Select(static type => type.Name).ToArray());
        CollectionAssert.AreEqual(
            componentTypes.Select(static type => type.Name).ToArray(),
            componentExports.Select(static property => property.Name).ToArray());
        CollectionAssert.AreEqual(
            componentTypes.Select(static type => type.Name).ToArray(),
            registryProperties.Select(static property => property.Name).ToArray());

        foreach (var componentType in componentTypes)
        {
            var contract = catalog.Components[componentType.Name];
            var component = componentType.GetCustomAttribute<ECMAScriptAttribute>();
            Assert.IsNotNull(component, componentType.FullName);
            // 组件入口是上游按目录发布的 ESM 入口，导出名仍由 [ECMAScriptName] 给出。
            Assert.AreEqual(
                $"tdesign-vue-next/es/{contract.Module}/index.mjs",
                component!.Import,
                componentType.FullName);
            Assert.AreEqual(contract.RuntimeExport, componentType.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name, componentType.FullName);
            CollectionAssert.Contains(runtimeExports, componentType.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name, componentType.FullName);

            var componentExport = componentExports.Single(property => property.Name == componentType.Name);
            Assert.AreEqual(typeof(ITDesignComponent), componentExport.PropertyType, componentType.FullName);
            Assert.AreEqual(
                componentType.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name,
                componentExport.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name,
                componentType.FullName);

            var registryProperty = registryProperties.Single(property => property.Name == componentType.Name);
            Assert.AreEqual(typeof(ITDesignComponent), registryProperty.PropertyType.UnwrapNullable(), componentType.FullName);
            Assert.AreEqual(
                $"@#{componentType.GetCustomAttribute<ECMAScriptNameAttribute>()?.Name}",
                registryProperty.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description,
                componentType.FullName);

            var authoringProperties = componentType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static property =>
                    property.DeclaringType is not null &&
                    typeof(TComponentBase).IsAssignableFrom(property.DeclaringType))
                .ToArray();
            foreach (var property in authoringProperties)
                Assert.IsNotNull(property.GetCustomAttribute<ParameterAttribute>(inherit: true), $"{componentType.Name}.{property.Name}");

            var parameterNames = authoringProperties
                .Where(static property => property.Name != nameof(TComponentBase.AdditionalAttributes))
                .Where(static property => !IsEventCallback(property.PropertyType))
                .Select(GetTDesignMemberName)
                .ToHashSet(StringComparer.Ordinal);
            var slotNames = authoringProperties
                .Where(static property => IsRenderFragment(property.PropertyType))
                .Select(GetTDesignMemberName)
                .ToHashSet(StringComparer.Ordinal);
            var eventNames = authoringProperties
                .Where(static property => IsEventCallback(property.PropertyType))
                .Select(GetTDesignMemberName)
                .ToHashSet(StringComparer.Ordinal);

            Assert.IsTrue(contract.Props.IsSubsetOf(parameterNames), $"{componentType.Name} missing props: {string.Join(", ", contract.Props.Except(parameterNames))}");
            Assert.IsTrue(contract.Slots.IsSubsetOf(slotNames), $"{componentType.Name} missing slots: {string.Join(", ", contract.Slots.Except(slotNames))}");
            var expectedEventNames = contract.Events
                .Select(ToTDesignListenerName)
                .ToHashSet(StringComparer.Ordinal);
            Assert.IsTrue(expectedEventNames.SetEquals(eventNames), $"{componentType.Name} events differ: expected [{string.Join(", ", expectedEventNames)}], actual [{string.Join(", ", eventNames)}]");

            var additionalAttributes = componentType.GetProperty(nameof(TComponentBase.AdditionalAttributes), BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(additionalAttributes, componentType.FullName);
            Assert.AreEqual(
                typeof(IReadOnlyDictionary<string, object?>),
                additionalAttributes!.PropertyType.UnwrapNullable(),
                componentType.FullName);

            var parameter = additionalAttributes.GetCustomAttribute<ParameterAttribute>(inherit: true);
            Assert.IsNotNull(parameter, componentType.FullName);
            Assert.IsTrue(parameter!.CaptureUnmatchedValues, componentType.FullName);
        }

        var legacyMetadataTypeNames = new[]
        {
            "ECMAScript.VueContract.VuePropAttribute",
            "ECMAScript.VueContract.VueSlotAttribute",
            "ECMAScript.VueContract.VueLibraryComponentAttribute"
        };
        foreach (var typeName in legacyMetadataTypeNames)
            Assert.IsNull(typeof(ECMAScript.VueContract.VueInjectAttribute).Assembly.GetType(typeName), typeName);
    }

    [TestMethod]
    public void TDesign_ValueEvents_UseFirstRuntimeArgumentAsCallbackPayload()
    {
        AssertTDesignEventPayload<TRadioGroup<string>>(nameof(TRadioGroup<string>.OnChange), typeof(string));
        AssertTDesignEventPayload<TSwitch<bool>>(nameof(TSwitch<bool>.OnChange), typeof(bool));
        AssertTDesignEventPayload<TPopup>(nameof(TPopup.OnVisibleChange), typeof(bool));
        AssertTDesignEventPayload<TColorPickerPanel>(nameof(TColorPickerPanel.OnChange), typeof(string));
    }

    [TestMethod]
    public void ElementPlus_GeneratedComponentNames_UseMemberLevelGeneralMetadata()
    {
        var componentTypes = typeof(ElementPlus).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace == "ECMAScript.ElementPlus" &&
                typeof(ComponentBase).IsAssignableFrom(type))
            .ToArray();

        Assert.AreEqual(
            "class",
            typeof(ElAffix).GetProperty(nameof(ElAffix.CssClass))!
                .GetCustomAttribute<ECMAScriptNameAttribute>(inherit: true)?.Name);
        Assert.AreEqual(
            "style",
            typeof(ElAffix).GetProperty(nameof(ElAffix.CssStyle))!
                .GetCustomAttribute<ECMAScriptNameAttribute>(inherit: true)?.Name);
        Assert.AreEqual(
            "title",
            typeof(ElAlert).GetProperty(nameof(ElAlert.TitleSlot))!
                .GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);
        Assert.AreEqual(
            "offset",
            typeof(ElAffix).GetProperty(nameof(ElAffix.Offset))!
                .GetCustomAttribute<ECMAScriptNameAttribute>()?.Name);
    }

    private static IEnumerable<string> GetVuetifyRuntimeComponentNames()
        => GetVuetifyRuntimeComponentNames(typeof(VuetifyComponents))
            .Concat(GetVuetifyRuntimeComponentNames(typeof(VuetifyLabsComponents)));

    private static IEnumerable<string> GetVuetifyRuntimeComponentNames(Type exportHost)
        => exportHost
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static property => property.PropertyType == typeof(IVuetifyComponent))
            .Select(static property => property.Name);

    [TestMethod]
    public void VuetifyGridSpanValue_UsesNativeUnionWithoutLosingNumericAssignments()
    {
        AssertNet11UnionContract(typeof(VuetifyGridSpanValue), typeof(bool), typeof(Number), typeof(string));

        Assert.IsNotNull(typeof(VuetifyGridSpanValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(decimal)]));
    }

    [TestMethod]
    public void VuetifyCalendarValueUnions_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VuetifyCalendarDateValue),
            typeof(VuetifyCalendarAllowedDatesValue),
            typeof(VuetifyCalendarIntervalFormatValue)
        };

        AssertNet11UnionContract(typeof(VuetifyCalendarDateValue), typeof(Date), typeof(string), typeof(Number));
        AssertNet11UnionContract(
            typeof(VuetifyCalendarAllowedDatesValue),
            typeof(VuetifyCalendarDateValues),
            typeof(VuetifyCalendarAllowedDateResolver));
        AssertNet11UnionContract(
            typeof(VuetifyCalendarIntervalFormatValue),
            typeof(string),
            typeof(VuetifyCalendarIntervalFormatter));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }

        Assert.IsNotNull(typeof(VuetifyCalendarDateValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(decimal)]));
        Assert.IsNotNull(typeof(VuetifyCalendarAllowedDatesValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(Date[])]));
    }

    [TestMethod]
    public void VuetifyColorPickerValues_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VuetifyColorPickerModes),
            typeof(VuetifyColorValue),
            typeof(VuetifyColorPickerSwatch),
            typeof(VuetifyColorPickerSwatches)
        };

        AssertNet11UnionContract(typeof(VuetifyColorPickerModes), typeof(VuetifyColorPickerMode[]));
        AssertNet11UnionContract(
            typeof(VuetifyColorValue),
            typeof(string),
            typeof(Number),
            typeof(VuetifyRgbColor),
            typeof(VuetifyHsvColor),
            typeof(VuetifyHslColor));
        AssertNet11UnionContract(typeof(VuetifyColorPickerSwatch), typeof(VuetifyColorValue[]));
        AssertNet11UnionContract(typeof(VuetifyColorPickerSwatches), typeof(VuetifyColorPickerSwatch[]));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }

        Assert.IsNotNull(typeof(VuetifyColorValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(decimal)]));
        Assert.IsNotNull(typeof(VuetifyColorPickerSwatches).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(double[][])]));
    }

    [TestMethod]
    public void VIconBtnValues_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VIconBtnSizeMap),
            typeof(VIconBtnTextValue)
        };

        AssertNet11UnionContract(typeof(VIconBtnSizeMap), typeof(VIconBtnSizeEntry[]));
        AssertNet11UnionContract(typeof(VIconBtnTextValue), typeof(bool), typeof(Number), typeof(string));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }

        Assert.IsNotNull(typeof(VIconBtnTextValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(decimal)]));
    }

    [TestMethod]
    public void VSnackbarQueueValues_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VuetifySnackbarQueueMessages),
            typeof(VuetifySnackbarQueueMessage)
        };

        AssertNet11UnionContract(typeof(VuetifySnackbarQueueMessages), typeof(VuetifySnackbarQueueMessage[]));
        AssertNet11UnionContract(
            typeof(VuetifySnackbarQueueMessage),
            typeof(string),
            typeof(VuetifySnackbarQueueMessageOptions));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }

        Assert.IsNotNull(typeof(VuetifySnackbarQueueMessages).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(VuetifySnackbarQueueMessageOptions[])]));
    }

    [TestMethod]
    public void VuetifySlotValues_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VuetifyCarouselVerticalDelimiters),
            typeof(VChipSelectedClassValue)
        };

        AssertNet11UnionContract(
            typeof(VuetifyCarouselVerticalDelimiters),
            typeof(bool),
            typeof(VuetifyCarouselVerticalDelimiterPosition));
        AssertNet11UnionContract(typeof(VChipSelectedClassValue), typeof(bool), typeof(string[]));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }
    }

    [TestMethod]
    public void VTimePickerValues_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VuetifyTimePickerModelValue),
            typeof(VuetifyTimePickerAllowedUnits),
            typeof(VuetifyTimePickerAllowedUnitValue)
        };

        AssertNet11UnionContract(typeof(VuetifyTimePickerModelValue), typeof(string), typeof(Date));
        AssertNet11UnionContract(typeof(VuetifyTimePickerAllowedUnits), typeof(Number[]));
        AssertNet11UnionContract(
            typeof(VuetifyTimePickerAllowedUnitValue),
            typeof(VuetifyTimePickerAllowedUnits),
            typeof(VuetifyTimePickerAllowedUnitResolver));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }

        Assert.IsNotNull(typeof(VuetifyTimePickerAllowedUnitValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(double[])]));
    }

    [TestMethod]
    public void VConfirmEditValues_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VuetifyConfirmEditActions),
            typeof(VuetifyConfirmEditDisabled)
        };

        AssertNet11UnionContract(typeof(VuetifyConfirmEditActions), typeof(VuetifyConfirmEditAction[]));
        AssertNet11UnionContract(typeof(VuetifyConfirmEditDisabled), typeof(bool), typeof(VuetifyConfirmEditActions));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }

        Assert.IsNotNull(typeof(VuetifyConfirmEditDisabled).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(VuetifyConfirmEditAction[])]));
    }

    [TestMethod]
    public void VDateInputValues_UseNativeUnionContracts()
    {
        AssertNet11UnionContract(
            typeof(VDateInputDisplayFormatValue),
            typeof(string),
            typeof(VDateInputDisplayFormatCallback));
        Assert.IsNull(typeof(VDateInputDisplayFormatValue).GetMethod("From", BindingFlags.Public | BindingFlags.Static));
    }

    [TestMethod]
    public void VDatePickerValues_UseNativeUnionContracts()
    {
        var unionTypes = new[]
        {
            typeof(VuetifyCalendarWeekdays),
            typeof(VuetifyDatePickerMultipleValue),
            typeof(VuetifyDatePickerModelValues),
            typeof(VuetifyDatePickerModelValue),
            typeof(VuetifyDatePickerAllowedDates),
            typeof(VuetifyDatePickerAllowedDatesValue),
            typeof(VuetifyDatePickerActiveValue)
        };

        AssertNet11UnionContract(typeof(VuetifyCalendarWeekdays), typeof(VuetifyCalendarWeekday[]));
        AssertNet11UnionContract(
            typeof(VuetifyDatePickerMultipleValue),
            typeof(bool),
            typeof(Number),
            typeof(VuetifyDatePickerMultipleMode),
            typeof(string));
        AssertNet11UnionContract(typeof(VuetifyDatePickerModelValues), typeof(VueValue[]));
        AssertNet11UnionContract(
            typeof(VuetifyDatePickerModelValue),
            typeof(Date),
            typeof(string),
            typeof(Number),
            typeof(VuetifyDatePickerModelValues));
        AssertNet11UnionContract(typeof(VuetifyDatePickerAllowedDates), typeof(VueValue[]));
        AssertNet11UnionContract(
            typeof(VuetifyDatePickerAllowedDatesValue),
            typeof(VuetifyDatePickerAllowedDates),
            typeof(VuetifyDatePickerAllowedDateResolver));
        AssertNet11UnionContract(typeof(VuetifyDatePickerActiveValue), typeof(string), typeof(string[]));

        foreach (var unionType in unionTypes)
        {
            Assert.IsNull(unionType.GetMethod("From", BindingFlags.Public | BindingFlags.Static), unionType.FullName);
        }

        Assert.IsNotNull(typeof(VuetifyDatePickerMultipleValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(decimal)]));
        Assert.IsNotNull(typeof(VuetifyDatePickerModelValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(double[])]));
        Assert.IsNotNull(typeof(VuetifyDatePickerAllowedDatesValue).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(double[])]));
    }

    [TestMethod]
    public void TDesign_UnionTypes_UseNativeContracts()
    {
        var unionTypes = typeof(TComponents).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace == "ECMAScript.TDesign" &&
                type.GetCustomAttribute<System.Runtime.CompilerServices.UnionAttribute>() is not null &&
                typeof(System.Runtime.CompilerServices.IUnion).IsAssignableFrom(type))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.IsTrue(unionTypes.Length > 0);
        foreach (var unionType in unionTypes)
        {
            var branchTypes = GetUnionConstructorBranchTypes(unionType);
            Assert.IsTrue(branchTypes.Length > 0, unionType.FullName);
            foreach (var branchType in branchTypes)
                AssertTDesignStrongType(branchType, unionType.FullName!);

            var storageFields = unionType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            var isNative = storageFields.Length == 1 &&
                storageFields[0].FieldType == typeof(object) &&
                storageFields[0].GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is not null;
            if (isNative)
            {
                AssertNoAssignableBranchOverlap(unionType, branchTypes);
            }
            else
            {
                Assert.IsTrue(
                    HasAssignableBranchOverlap(branchTypes),
                    $"{unionType.FullName} should use a native union unless exact tagged projections require a fallback.");
                AssertTaggedUnionBranchesExposeStandardCreation(unionType, branchTypes);
            }
        }
    }

    [TestMethod]
    public void ComponentLibraries_UseShortComponentTypes_AndLibraryQualifiedMarkers()
    {
        AssertRetiredAuthoringPrefixIsAbsent(
            typeof(ElComponents).Assembly,
            typeof(ElComponents).Namespace!,
            "ElementPlus",
            nameof(ElementPlus),
            nameof(IElementPlusComponent));
        AssertRetiredAuthoringPrefixIsAbsent(
            typeof(TComponents).Assembly,
            typeof(TComponents).Namespace!,
            "TDesign",
            nameof(TDesign),
            nameof(ITDesignComponent));
    }

    [TestMethod]
    public void ElementPlus_UnionTypes_UseExpectedContracts()
        => AssertComponentLibraryUnionContracts(
            typeof(ElComponents).Assembly,
            typeof(ElComponents).Namespace!,
            expectedCount: 46,
            typeof(ElUploadBeforeUploadResult),
            typeof(ElTransferTextPair));

    [TestMethod]
    public void ElUploadBeforeUploadResult_TaggedUnion_PreservesExactFileAndBlobBranches()
    {
        var file = (ECMAScript.FileRef)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ECMAScript.FileRef));
        var blob = (Blob)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Blob));

        var boolResult = new ElUploadBeforeUploadResult(true);
        var fileResult = new ElUploadBeforeUploadResult(file);
        var blobResult = new ElUploadBeforeUploadResult(blob);

        Assert.AreEqual(true, boolResult.AsBool);
        Assert.IsNull(boolResult.AsFile);
        Assert.IsNull(boolResult.AsBlob);
        Assert.AreSame(file, fileResult.AsFile);
        Assert.IsNull(fileResult.AsBlob);
        Assert.AreSame(file, fileResult.Value);
        Assert.IsNull(blobResult.AsFile);
        Assert.AreSame(blob, blobResult.AsBlob);
        Assert.AreSame(blob, blobResult.Value);
    }

    [TestMethod]
    public void Vuetify_UnionTypes_UseExpectedContracts()
        => AssertComponentLibraryUnionContracts(
            typeof(VuetifyComponentRegistry).Assembly,
            typeof(VuetifyComponentRegistry).Namespace!,
            expectedCount: 113,
            typeof(VuetifyOverlayCoordinateTarget));

    private static void AssertComponentLibraryUnionContracts(
        Assembly assembly,
        string contractNamespace,
        int expectedCount,
        params Type[] expectedTaggedTypes)
    {
        var unionTypes = assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == contractNamespace
                && type.GetCustomAttribute<System.Runtime.CompilerServices.UnionAttribute>() is not null
                && typeof(System.Runtime.CompilerServices.IUnion).IsAssignableFrom(type))
            .ToArray();

        Assert.AreEqual(expectedCount, unionTypes.Length, contractNamespace);
        var taggedTypes = new List<Type>();
        foreach (var unionType in unionTypes)
        {
            var branchTypes = GetUnionConstructorBranchTypes(unionType);
            Assert.IsTrue(branchTypes.Length > 0, unionType.FullName);

            var privateBranchConstructors = unionType
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(static constructor => constructor.GetParameters().Length > 0)
                .ToArray();
            Assert.AreEqual(0, privateBranchConstructors.Length, unionType.FullName);

            var storageFields = unionType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            var isNative = storageFields.Length == 1
                && storageFields[0].FieldType == typeof(object)
                && storageFields[0].GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is not null;

            if (isNative)
            {
                AssertNoAssignableBranchOverlap(unionType, branchTypes);
            }
            else
            {
                taggedTypes.Add(unionType);
                Assert.IsTrue(
                    HasAssignableBranchOverlap(branchTypes) || expectedTaggedTypes.Contains(unionType),
                    $"{unionType.FullName} should use native union unless its tagged fallback is explicitly required by the component contract.");
                AssertTaggedUnionBranchesExposeStandardCreation(unionType, branchTypes);
            }

            Assert.IsFalse(
                unionType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Any(static method => method.Name == "From"),
                $"{unionType.FullName} should use native union construction instead of From factories.");
        }

        CollectionAssert.AreEquivalent(expectedTaggedTypes, taggedTypes.ToArray(), contractNamespace);
    }

    private static void AssertTaggedUnionBranchesExposeStandardCreation(Type unionType, Type[] branchTypes)
    {
        var implicitOperators = unionType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name == "op_Implicit" && method.ReturnType == method.DeclaringType)
            .ToArray();

        foreach (var branchType in branchTypes)
        {
            if (branchType.IsInterface)
            {
                Assert.IsTrue(
                    unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .Any(constructor => constructor.GetParameters().SingleOrDefault()?.ParameterType == branchType),
                    $"{unionType.FullName} must expose a public constructor for interface branch {branchType.FullName}.");
                continue;
            }

            Assert.IsTrue(
                implicitOperators.Any(method => method.GetParameters().SingleOrDefault()?.ParameterType == branchType),
                $"{unionType.FullName} must preserve implicit assignment from {branchType.FullName}.");
        }
    }

    private static void AssertRetiredAuthoringPrefixIsAbsent(
        Assembly assembly,
        string contractNamespace,
        string retiredPrefix,
        params string[] allowedRootTypes)
    {
        var remainingLongNames = assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == contractNamespace)
            .Select(static type => type.Name)
            .Where(name =>
                name.StartsWith(retiredPrefix, StringComparison.Ordinal) ||
                name.StartsWith($"I{retiredPrefix}", StringComparison.Ordinal))
            .Except(allowedRootTypes, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), remainingLongNames, contractNamespace);
    }

    [TestMethod]
    public void Vuetify_ValueAndUnionTypes_ExposeStronglyTypedContracts()
    {
        Assert.IsNotNull(typeof(VuetifyHideDetailsValue).GetCustomAttribute<ECMAScriptAttribute>());
        Assert.IsNotNull(typeof(VuetifyMessagesValue).GetCustomAttribute<ECMAScriptAttribute>());
        Assert.IsNotNull(typeof(VuetifyScrimValue).GetCustomAttribute<ECMAScriptAttribute>());
        Assert.IsNotNull(typeof(VuetifyDisplayBreakpoint).GetCustomAttribute<ECMAScriptAttribute>());
        CollectionAssert.AreEqual(
            new[] { "elevated", "flat", "outlined", "plain", "text", "tonal" },
            GetStringEnumRuntimeValues(typeof(VuetifyVariant)));
        CollectionAssert.AreEqual(
            new[] { "filled", "outlined", "plain", "solo", "solo-filled", "solo-inverted", "underlined" },
            GetStringEnumRuntimeValues(typeof(VuetifyFieldVariant)));
        CollectionAssert.AreEqual(
            new[] { "color", "date", "datetime-local", "email", "month", "number", "password", "search", "tel", "text", "time", "url", "week" },
            GetStringEnumRuntimeValues(typeof(VuetifyInputType)));
        CollectionAssert.AreEqual(
            new[] { "exact" },
            GetStringEnumRuntimeValues(typeof(VuetifyAutoSelectFirstMode)));
        CollectionAssert.AreEqual(
            new[] { "default", "hidden", "split", "stacked" },
            GetStringEnumRuntimeValues(typeof(VuetifyNumberInputControlVariant)));
        CollectionAssert.AreEqual(
            new[] { "horizontal", "vertical" },
            GetStringEnumRuntimeValues(typeof(VuetifySliderDirection)));
        CollectionAssert.AreEqual(
            new[] { "always" },
            GetStringEnumRuntimeValues(typeof(VuetifyAlwaysMode)));
        CollectionAssert.AreEqual(
            new[] { "disable-shrink" },
            GetStringEnumRuntimeValues(typeof(VuetifyProgressCircularIndeterminateMode)));
        CollectionAssert.AreEqual(
            new[] { "bottom", "top" },
            GetStringEnumRuntimeValues(typeof(VuetifyAppBarLocation)));
        CollectionAssert.AreEqual(
            new[] { "bottom", "end", "left", "right", "start", "top" },
            GetStringEnumRuntimeValues(typeof(VuetifyNavigationDrawerLocation)));

        Assert.AreEqual(typeof(bool?), typeof(VuetifyHideDetailsValue).GetProperty(nameof(VuetifyHideDetailsValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(VuetifyHideDetailsMode?), typeof(VuetifyHideDetailsValue).GetProperty(nameof(VuetifyHideDetailsValue.AsMode), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);

        Assert.AreEqual(typeof(string), typeof(VuetifyMessagesValue).GetProperty(nameof(VuetifyMessagesValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(string[]), typeof(VuetifyMessagesValue).GetProperty(nameof(VuetifyMessagesValue.AsStrings), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(bool), typeof(VuetifyScrimValue).GetProperty(nameof(VuetifyScrimValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifyScrimValue).GetProperty(nameof(VuetifyScrimValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifyDisplayBreakpoint).GetProperty(nameof(VuetifyDisplayBreakpoint.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VuetifyDisplayBreakpoint).GetProperty(nameof(VuetifyDisplayBreakpoint.AsNumber), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        AssertNet11UnionContract(typeof(VuetifyDisplayBreakpoint), typeof(string), typeof(Number));
        Assert.IsNull(typeof(VuetifyDisplayBreakpoint).GetMethod("From", BindingFlags.Public | BindingFlags.Static));

        Assert.AreEqual(typeof(bool), typeof(VuetifyAutoSelectFirstValue).GetProperty(nameof(VuetifyAutoSelectFirstValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyAutoSelectFirstMode), typeof(VuetifyAutoSelectFirstValue).GetProperty(nameof(VuetifyAutoSelectFirstValue.AsMode), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifyFileShowSizeValue).GetProperty(nameof(VuetifyFileShowSizeValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyFileSizeBase), typeof(VuetifyFileShowSizeValue).GetProperty(nameof(VuetifyFileShowSizeValue.AsBase), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifyBooleanAlwaysValue).GetProperty(nameof(VuetifyBooleanAlwaysValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyAlwaysMode), typeof(VuetifyBooleanAlwaysValue).GetProperty(nameof(VuetifyBooleanAlwaysValue.AsMode), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifyBooleanStringValue).GetProperty(nameof(VuetifyBooleanStringValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifyBooleanStringValue).GetProperty(nameof(VuetifyBooleanStringValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifyCounterValue).GetProperty(nameof(VuetifyCounterValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VuetifyCounterValue).GetProperty(nameof(VuetifyCounterValue.AsNumber), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifyCounterValue).GetProperty(nameof(VuetifyCounterValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifyTextValue).GetProperty(nameof(VuetifyTextValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VuetifyTextValue).GetProperty(nameof(VuetifyTextValue.AsNumber), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifyTextValue).GetProperty(nameof(VuetifyTextValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifyRoundedValue).GetProperty(nameof(VuetifyRoundedValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VuetifyRoundedValue).GetProperty(nameof(VuetifyRoundedValue.AsNumber), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifyRoundedValue).GetProperty(nameof(VuetifyRoundedValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifyProgressCircularIndeterminateValue).GetProperty(nameof(VuetifyProgressCircularIndeterminateValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyProgressCircularIndeterminateMode), typeof(VuetifyProgressCircularIndeterminateValue).GetProperty(nameof(VuetifyProgressCircularIndeterminateValue.AsMode), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(ECMAScript.FileRef), typeof(VuetifyFileModelValue).GetProperty(nameof(VuetifyFileModelValue.AsFile), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(ECMAScript.FileRef[]), typeof(VuetifyFileModelValue).GetProperty(nameof(VuetifyFileModelValue.AsFiles), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(Number[]), typeof(VuetifyRangeSliderModelValue).GetProperty(nameof(VuetifyRangeSliderModelValue.AsArray), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(string[]), typeof(VuetifyRangeSliderModelValue).GetProperty(nameof(VuetifyRangeSliderModelValue.AsStrings), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        AssertNet11UnionContract(typeof(VuetifyRangeSliderModelValue), typeof(Number[]), typeof(string[]));

        var selectModelValuesCollectionBuilder = typeof(VuetifySelectModelValues).GetCustomAttribute<System.Runtime.CompilerServices.CollectionBuilderAttribute>();
        Assert.IsNotNull(selectModelValuesCollectionBuilder);
        Assert.AreEqual(typeof(VuetifySelectModelValuesCollectionBuilder), selectModelValuesCollectionBuilder!.BuilderType);
        Assert.AreEqual(nameof(VuetifySelectModelValuesCollectionBuilder.Create), selectModelValuesCollectionBuilder.MethodName);
        Assert.AreEqual(typeof(VuetifySelectModelValue[]), typeof(VuetifySelectModelValues).GetProperty(nameof(VuetifySelectModelValues.AsArray), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifySelectModelValue).GetProperty(nameof(VuetifySelectModelValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VuetifySelectModelValue).GetProperty(nameof(VuetifySelectModelValue.AsNumber), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifySelectModelValue).GetProperty(nameof(VuetifySelectModelValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Symbol), typeof(VuetifySelectModelValue).GetProperty(nameof(VuetifySelectModelValue.AsSymbol), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueProps), typeof(VuetifySelectModelValue).GetProperty(nameof(VuetifySelectModelValue.AsObject), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectModelValues), typeof(VuetifySelectModelValue).GetProperty(nameof(VuetifySelectModelValue.AsValues), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifySelectItemValue).GetProperty(nameof(VuetifySelectItemValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItem), typeof(VuetifySelectItemValue).GetProperty(nameof(VuetifySelectItemValue.AsItem), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VuetifySelectItemValue).GetProperty(nameof(VuetifySelectItemValue.AsNumber), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(bool), typeof(VuetifySelectItemValue).GetProperty(nameof(VuetifySelectItemValue.AsBool), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VuetifyBreadcrumbItemValue).GetProperty(nameof(VuetifyBreadcrumbItemValue.AsString), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyBreadcrumbItem), typeof(VuetifyBreadcrumbItemValue).GetProperty(nameof(VuetifyBreadcrumbItemValue.AsItem), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VuetifyBreadcrumbItemValue).GetProperty(nameof(VuetifyBreadcrumbItemValue.AsNumber), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItems), typeof(VSelect).GetProperty(nameof(VSelect.Items), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemKey), typeof(VSelect).GetProperty(nameof(VSelect.ItemTitle), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemKey), typeof(VSelect).GetProperty(nameof(VSelect.ItemValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemPropsSelector), typeof(VSelect).GetProperty(nameof(VSelect.ItemProps), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectModelValue), typeof(VSelect).GetProperty(nameof(VSelect.SelectedValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(EventCallback<VuetifySelectModelValue?>), typeof(VSelect).GetProperty(nameof(VSelect.SelectedValueChanged), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(VuetifySelectItems), typeof(VAutocomplete).GetProperty(nameof(VAutocomplete.Items), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemKey), typeof(VAutocomplete).GetProperty(nameof(VAutocomplete.ItemTitle), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemKey), typeof(VAutocomplete).GetProperty(nameof(VAutocomplete.ItemValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemPropsSelector), typeof(VAutocomplete).GetProperty(nameof(VAutocomplete.ItemProps), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectModelValue), typeof(VAutocomplete).GetProperty(nameof(VAutocomplete.SelectedValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(EventCallback<VuetifySelectModelValue?>), typeof(VAutocomplete).GetProperty(nameof(VAutocomplete.SelectedValueChanged), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(VuetifyBooleanStringValue), typeof(VBtn).GetProperty(nameof(VBtn.Loading), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyTextValue), typeof(VBtn).GetProperty(nameof(VBtn.Text), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VBtn).GetProperty(nameof(VBtn.Size), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VBtn).GetProperty(nameof(VBtn.Height), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyRoundedValue), typeof(VBtn).GetProperty(nameof(VBtn.Rounded), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyCounterValue), typeof(VTextField).GetProperty(nameof(VTextField.Counter), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyCounterValue), typeof(VTextarea).GetProperty(nameof(VTextarea.Counter), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyBooleanStringValue), typeof(VSwitch).GetProperty(nameof(VSwitch.Loading), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VAvatar).GetProperty(nameof(VAvatar.Size), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyRoundedValue), typeof(VAvatar).GetProperty(nameof(VAvatar.Rounded), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VImg).GetProperty(nameof(VImg.Width), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyRoundedValue), typeof(VImg).GetProperty(nameof(VImg.Rounded), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VSheet).GetProperty(nameof(VSheet.Elevation), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyRoundedValue), typeof(VSheet).GetProperty(nameof(VSheet.Rounded), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VProgressLinear).GetProperty(nameof(VProgressLinear.ModelValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyRoundedValue), typeof(VProgressLinear).GetProperty(nameof(VProgressLinear.Rounded), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VProgressCircular).GetProperty(nameof(VProgressCircular.ModelValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyProgressCircularIndeterminateValue), typeof(VProgressCircular).GetProperty(nameof(VProgressCircular.Indeterminate), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VOtpInput).GetProperty(nameof(VOtpInput.Length), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyBooleanStringValue), typeof(VOtpInput).GetProperty(nameof(VOtpInput.Loading), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VPagination).GetProperty(nameof(VPagination.Length), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VPagination).GetProperty(nameof(VPagination.TotalVisible), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VTextarea).GetProperty(nameof(VTextarea.Rows), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VTextarea).GetProperty(nameof(VTextarea.MaxRows), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VBadge).GetProperty(nameof(VBadge.Content), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyTextValue), typeof(VChip).GetProperty(nameof(VChip.Text), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VChip).GetProperty(nameof(VChip.Size), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyBooleanStringValue), typeof(VSnackbar).GetProperty(nameof(VSnackbar.Timer), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VueStringNumberValue), typeof(VSnackbar).GetProperty(nameof(VSnackbar.Timeout), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyRoundedValue), typeof(VSnackbar).GetProperty(nameof(VSnackbar.Rounded), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItems), typeof(VCombobox).GetProperty(nameof(VCombobox.Items), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemKey), typeof(VCombobox).GetProperty(nameof(VCombobox.ItemTitle), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemKey), typeof(VCombobox).GetProperty(nameof(VCombobox.ItemValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectItemPropsSelector), typeof(VCombobox).GetProperty(nameof(VCombobox.ItemProps), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyAutoSelectFirstValue), typeof(VCombobox).GetProperty(nameof(VCombobox.AutoSelectFirst), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySelectModelValue), typeof(VCombobox).GetProperty(nameof(VCombobox.SelectedValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(EventCallback<VuetifySelectModelValue?>), typeof(VCombobox).GetProperty(nameof(VCombobox.SelectedValueChanged), BindingFlags.Public | BindingFlags.Instance)!.PropertyType);
        Assert.AreEqual(typeof(bool), typeof(VFileInput).GetProperty(nameof(VFileInput.Counter), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyFileShowSizeValue), typeof(VFileInput).GetProperty(nameof(VFileInput.ShowSize), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyFileModelValue), typeof(VFileInput).GetProperty(nameof(VFileInput.ModelValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.IsNull(typeof(VNumberInput).GetProperty("ControlVariantHidden", BindingFlags.Public | BindingFlags.Instance));
        Assert.AreEqual(typeof(VuetifyNumberInputControlVariant), typeof(VNumberInput).GetProperty(nameof(VNumberInput.ControlVariant), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VNumberInput).GetProperty(nameof(VNumberInput.Min), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VNumberInput).GetProperty(nameof(VNumberInput.ModelValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VOtpInput).GetProperty(nameof(VOtpInput.Divider), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyInputType), typeof(VOtpInput).GetProperty(nameof(VOtpInput.Type), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VRadio).GetProperty(nameof(VRadio.FalseIcon), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(string), typeof(VRadio).GetProperty(nameof(VRadio.TrueIcon), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyRangeSliderModelValue), typeof(VRangeSlider).GetProperty(nameof(VRangeSlider.ModelValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VRangeSlider).GetProperty(nameof(VRangeSlider.Step), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyBooleanAlwaysValue), typeof(VRangeSlider).GetProperty(nameof(VRangeSlider.ThumbLabel), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySliderDirection), typeof(VRangeSlider).GetProperty(nameof(VRangeSlider.Direction), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VSlider).GetProperty(nameof(VSlider.ModelValue), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(Number), typeof(VSlider).GetProperty(nameof(VSlider.Step), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyBooleanAlwaysValue), typeof(VSlider).GetProperty(nameof(VSlider.ShowTicks), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifySliderDirection), typeof(VSlider).GetProperty(nameof(VSlider.Direction), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyBreadcrumbItems), typeof(VBreadcrumbs).GetProperty(nameof(VBreadcrumbs.Items), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyDataTableHeaders), typeof(VDataTable).GetProperty(nameof(VDataTable.Headers), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        Assert.AreEqual(typeof(VuetifyDataTableItems), typeof(VDataTable).GetProperty(nameof(VDataTable.Items), BindingFlags.Public | BindingFlags.Instance)!.PropertyType.UnwrapNullable());
        AssertNoPublicMemberUsesObject(
            typeof(VuetifyAutoSelectFirstValue),
            typeof(VuetifyDisplayBreakpoint),
            typeof(VuetifyFileShowSizeValue),
            typeof(VuetifyBooleanAlwaysValue),
            typeof(VuetifyBooleanStringValue),
            typeof(VuetifyCounterValue),
            typeof(VuetifyTextValue),
            typeof(VuetifyRoundedValue),
            typeof(VuetifyProgressCircularIndeterminateValue),
            typeof(VuetifyFileModelValue),
            typeof(VuetifyRangeSliderModelValue),
            typeof(VuetifySelectItems),
            typeof(VuetifySelectModelValues),
            typeof(VuetifySelectModelValue),
            typeof(VuetifySelectItemValue),
            typeof(VuetifySelectItem),
            typeof(VuetifySelectItemKey),
            typeof(VuetifySelectItemPropsSelector),
            typeof(VuetifyItemProps),
            typeof(VuetifyBreadcrumbItems),
            typeof(VuetifyBreadcrumbItemValue),
            typeof(VuetifyBreadcrumbItem),
            typeof(VuetifyDataTableHeaders),
            typeof(VuetifyDataTableHeader),
            typeof(VuetifyDataTableItems),
            typeof(VuetifyDataTableItem));
    }

    [TestMethod]
    public void TDesign_PublicContracts_AreStronglyTyped_AndPickOmitRemainExact()
    {
        var publicTypes = typeof(TDesign).Assembly
            .GetExportedTypes()
            .Where(static type => type.Namespace == "ECMAScript.TDesign")
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        foreach (var type in publicTypes)
        {
            AssertTDesignStrongType(type, type.FullName!);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (IsUnionValueProperty(property))
                    continue;

                if (property.Name == nameof(TComponentBase.AdditionalAttributes))
                {
                    Assert.AreEqual(typeof(TComponentBase), property.DeclaringType);
                    continue;
                }

                AssertTDesignStrongType(property.PropertyType, $"{type.Name}.{property.Name}");
            }

            if (!typeof(Delegate).IsAssignableFrom(type))
            {
                foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    foreach (var parameter in constructor.GetParameters())
                        AssertTDesignStrongType(parameter.ParameterType, $"{type.Name}.ctor({parameter.Name})");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(method =>
                             !typeof(Delegate).IsAssignableFrom(type) &&
                             !method.IsSpecialName &&
                             !IsRecordRuntimeMethod(method) &&
                             method.Name is not nameof(object.Equals) and not nameof(object.GetHashCode) and not nameof(ToString)))
            {
                AssertTDesignStrongType(method.ReturnType, $"{type.Name}.{method.Name} return");
                foreach (var parameter in method.GetParameters())
                    AssertTDesignStrongType(parameter.ParameterType, $"{type.Name}.{method.Name}({parameter.Name})");
            }
        }

        CollectionAssert.AreEquivalent(
            new[] { "Fields", "FirstError", "ValidateResult" },
            typeof(TValidateResultContext<>).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static property => property.Name)
                .ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "E", "File", "Response", "XMLHttpRequest" },
            typeof(TUploadOneFileSuccessEventContext<>).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static property => property.Name)
                .ToArray());

        CollectionAssert.AreEquivalent(
            new[] { "Col", "ColIndex", "Row", "RowIndex", "Type" },
            typeof(TCellData<>).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static property => property.Name)
                .ToArray());
        CollectionAssert.IsSubsetOf(
            new[] { "FilterDate", "FormattedFilterDate", "Mode" },
            typeof(TCalendarCell).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static property => property.Name)
                .ToArray());
        CollectionAssert.IsSubsetOf(
            new[] { "Actived", "Checked", "Disabled", "Expanded", "Indeterminate", "Loading" },
            typeof(TTreeNodeModel<>).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static property => property.Name)
                .ToArray());
    }

    private static Type[] GetTDesignComponentTypes()
        => typeof(TDesign).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace == "ECMAScript.TDesign" &&
                type.IsClass &&
                !type.IsAbstract &&
                !type.ContainsGenericParameters &&
                typeof(ComponentBase).IsAssignableFrom(type) &&
                type.GetCustomAttribute<ECMAScriptAttribute>()?.Import is not null)
            .OrderBy(static type => type.Name, StringComparer.Ordinal)
            .ToArray();

    private static TDesignCatalog ReadTDesignCatalog(string repositoryRoot)
    {
        var path = Path.Combine(
            repositoryRoot,
            "src",
            "ECMAScript.Vue.Generator",
            "upstream",
            "tdesign-vue-next",
            "1.20.7",
            "contracts.json");
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        var bindingsPath = Path.Combine(
            repositoryRoot,
            "src",
            "ECMAScript.Vue.Generator",
            "upstream",
            "tdesign-vue-next",
            "1.20.7",
            "bindings.json");
        using var bindings = JsonDocument.Parse(System.IO.File.ReadAllText(bindingsPath));
        var runtimeExports = bindings.RootElement.GetProperty("components").EnumerateArray()
            .GroupBy(static element => element.GetProperty("authoringType").GetString()!, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static element => (
                        Module: element.GetProperty("module").GetString()!,
                        RuntimeExport: element.GetProperty("runtimeExport").GetString()!))
                    .Distinct()
                    .Single(),
                StringComparer.Ordinal);
        var sourceEntries = document.RootElement.GetProperty("components").EnumerateArray().ToArray();
        var components = sourceEntries
            .GroupBy(static element => element.GetProperty("authoringType").GetString()!, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                group => new TDesignCatalogComponent(
                    runtimeExports[group.Key].Module,
                    runtimeExports[group.Key].RuntimeExport,
                    group.SelectMany(static element => GetNames(element, "props")).ToHashSet(StringComparer.Ordinal),
                    group.SelectMany(static element => GetNames(element, "slots")).ToHashSet(StringComparer.Ordinal),
                    group.SelectMany(static element => GetNames(element, "events")).ToHashSet(StringComparer.Ordinal)),
                StringComparer.Ordinal);

        return new TDesignCatalog(sourceEntries.Length, components);
    }

    private static IEnumerable<string> GetNames(JsonElement component, string member)
        => component.TryGetProperty(member, out var items)
            ? items.EnumerateArray().Select(static item => item.GetProperty("name").GetString()!)
            : Array.Empty<string>();

    /// <summary>
    /// 每个组件在上游入口里的导出名，取自committed 的 bindings 快照。
    ///
    /// 运行时代码由 Deno 按 npm identity 恢复，仓库不 vendor 上游 <c>.mjs</c>，
    /// 因此这里不能去读包内源码；导出名来自快照，声明值由 <c>manifest.json</c> 的
    /// imports 键表达，两者都已是受控工件。
    /// </summary>
    private static string[] ReadTDesignRuntimeExports(string repositoryRoot)
    {
        using var bindings = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "ECMAScript.Vue.Generator",
            "upstream",
            "tdesign-vue-next",
            "1.20.7",
            "bindings.json")));

        return bindings.RootElement.GetProperty("components").EnumerateArray()
            .Select(static element => element.GetProperty("runtimeExport").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static member => member, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsEventCallback(Type type)
        => type == typeof(EventCallback) ||
           type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>);

    private static void AssertTDesignEventPayload<TComponent>(string propertyName, Type payloadType)
    {
        var property = typeof(TComponent).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(property, $"{typeof(TComponent).Name}.{propertyName}");
        Assert.AreEqual(
            typeof(EventCallback<>).MakeGenericType(payloadType),
            property!.PropertyType,
            $"{typeof(TComponent).Name}.{propertyName}");
    }

    private static bool IsRenderFragment(Type type)
        => type == typeof(RenderFragment) ||
           type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RenderFragment<>);

    private static string GetTDesignMemberName(PropertyInfo property)
        => property.GetCustomAttribute<ECMAScriptNameAttribute>(inherit: true)?.Name ?? property.Name;

    private static string ToTDesignListenerName(string eventName)
    {
        if (eventName.Length == 0 ||
            eventName.StartsWith("on", StringComparison.Ordinal) &&
            eventName.Length > 2 &&
            char.IsUpper(eventName[2]))
        {
            return eventName;
        }

        var builder = new StringBuilder(eventName.Length + 2);
        builder.Append("on");
        var capitalizeNext = true;
        foreach (var character in eventName)
        {
            if (character == '-')
            {
                capitalizeNext = true;
                continue;
            }

            builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
            capitalizeNext = false;
        }

        return builder.ToString();
    }

    private static void AssertTDesignStrongType(Type type, string message)
    {
        if (type.IsGenericParameter)
            return;

        var unwrapped = type.UnwrapNullable();
        Assert.AreNotEqual(typeof(object), unwrapped, message);
        Assert.AreNotEqual(typeof(VueValue), unwrapped, message);

        if (unwrapped.IsArray)
        {
            AssertTDesignStrongType(unwrapped.GetElementType()!, message);
            return;
        }

        if (!unwrapped.IsGenericType)
            return;

        foreach (var argument in unwrapped.GetGenericArguments())
            AssertTDesignStrongType(argument, message);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "RazorVue.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

    private sealed record TDesignCatalog(
        int SourceEntryCount,
        IReadOnlyDictionary<string, TDesignCatalogComponent> Components);

    private sealed record TDesignCatalogComponent(
        string Module,
        string RuntimeExport,
        HashSet<string> Props,
        HashSet<string> Slots,
        HashSet<string> Events);

    private static string[] GetStringEnumRuntimeValues(Type enumType)
        => enumType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(static field => field.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description)
            .Select(static description => description?.TrimStart('@', '#'))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray()!;

    private static void AssertNotObject(Type type, string message)
    {
        Assert.AreNotEqual(typeof(object), type.UnwrapNullable(), message);

        if (!type.IsGenericType)
            return;

        foreach (var argument in type.GetGenericArguments())
            AssertNotObject(argument, message);
    }

    private static void AssertNoPublicMemberUsesObject(params Type[] types)
    {
        foreach (var type in types)
        {
            AssertNotObject(type, type.FullName ?? type.Name);

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (IsUnionValueProperty(property))
                    continue;

                AssertNotObject(property.PropertyType, $"{type.Name}.{property.Name}");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(static method => !method.IsSpecialName && !IsRecordRuntimeMethod(method)))
            {
                AssertNotObject(method.ReturnType, $"{type.Name}.{method.Name} return");
                foreach (var parameter in method.GetParameters())
                    AssertNotObject(parameter.ParameterType, $"{type.Name}.{method.Name}({parameter.Name})");
            }
        }
    }

    private static void AssertNet11UnionContract(Type unionType, params Type[] constructorBranchTypes)
    {
        Assert.IsNotNull(unionType.GetCustomAttribute<System.Runtime.CompilerServices.UnionAttribute>(), unionType.FullName);
        Assert.IsTrue(typeof(System.Runtime.CompilerServices.IUnion).IsAssignableFrom(unionType), unionType.FullName);

        var value = unionType.GetProperty(nameof(System.Runtime.CompilerServices.IUnion.Value), BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(value, unionType.FullName);
        Assert.AreEqual(typeof(object), value!.PropertyType);

        CollectionAssert.AreEquivalent(
            constructorBranchTypes,
            unionType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(static constructor => constructor.GetParameters().SingleOrDefault()?.ParameterType)
                .Where(static type => type is not null)
                .ToArray(),
            unionType.FullName);

        AssertNoAssignableBranchOverlap(unionType, constructorBranchTypes);
    }

    private static void AssertNoAssignableBranchOverlap(Type unionType, Type[] constructorBranchTypes)
    {
        foreach (var left in constructorBranchTypes)
            foreach (var right in constructorBranchTypes)
            {
                if (left == right)
                    continue;

                Assert.IsFalse(
                    left.IsAssignableFrom(right),
                    $"{unionType.FullName} cannot use native union because branch {right.FullName} is assignable to {left.FullName}; keep a tagged [Union] + IUnion wrapper to preserve exact AsX projections.");
            }
    }

    private static bool HasAssignableBranchOverlap(Type[] constructorBranchTypes)
        => constructorBranchTypes.Any(left => constructorBranchTypes.Any(right =>
            left != right && left.IsAssignableFrom(right)));

    private static bool IsRecordRuntimeMethod(MethodInfo method)
        => method.Name is nameof(object.Equals) or nameof(object.GetHashCode) or nameof(ToString) &&
           method.DeclaringType?.IsAssignableTo(typeof(VueProps)) == true;

    private static bool IsUnionValueProperty(PropertyInfo property)
        => property.Name == nameof(System.Runtime.CompilerServices.IUnion.Value) &&
           typeof(System.Runtime.CompilerServices.IUnion).IsAssignableFrom(property.DeclaringType);

    private static Type GetPropertyType(PropertyInfo? property)
    {
        Assert.IsNotNull(property);
        return property!.PropertyType.UnwrapNullable();
    }

    private static Type[] GetUnionConstructorBranchTypes(Type unionType)
        => unionType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(static constructor => constructor.GetParameters().SingleOrDefault()?.ParameterType)
            .Where(static type => type is not null)
            .ToArray()!;

    private static void AssertEcmaScriptImport(Type type, string expectedImport)
    {
        var runtime = type.GetCustomAttribute<ECMAScriptAttribute>();
        var module = type.GetCustomAttribute<ECMAScriptModuleAttribute>();

        Assert.IsNotNull(runtime, type.FullName);
        Assert.IsNull(module, type.FullName);
        Assert.AreEqual(expectedImport, runtime!.Import, type.FullName);
    }

    private static void AssertEcmaScriptImport(MemberInfo member, string expectedImport)
    {
        var runtime = member.GetCustomAttribute<ECMAScriptAttribute>();

        Assert.IsNotNull(runtime, member.Name);
        Assert.AreEqual(expectedImport, runtime!.Import, member.Name);

    }

    private static void AssertEcmaScriptSupport(Type type)
    {
        var runtime = type.GetCustomAttribute<ECMAScriptAttribute>();
        var module = type.GetCustomAttribute<ECMAScriptModuleAttribute>();

        Assert.IsNull(module, type.FullName);
        if (runtime is not null)
            Assert.IsNull(runtime.Import, type.FullName);
        else
            Assert.IsTrue(type.IsEnum || type.GetMethods().Any(static method => method.Name == "<Clone>$"),
                $"{type.FullName} must declare a host contract or be a structural record/enum.");
    }

    private static bool HasAttribute(MemberInfo member, string attributeTypeName)
        => member.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name == attributeTypeName);

    private static void AssertNoHostInferenceAttribute(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(property, $"{type.FullName}.{propertyName}");

        var attributeTypeNames = property!.GetCustomAttributesData()
            .Select(static attribute => attribute.AttributeType.FullName)
            .ToArray();
        CollectionAssert.DoesNotContain(attributeTypeNames, typeof(PropsAttribute).FullName, $"{type.FullName}.{propertyName}");
        CollectionAssert.DoesNotContain(attributeTypeNames, typeof(EmitsAttribute).FullName, $"{type.FullName}.{propertyName}");
    }
}

internal static class TypeTestExtensions
{
    public static Type UnwrapNullable(this Type type)
        => Nullable.GetUnderlyingType(type) ?? type;
}

public sealed record TestVueProps : VueProps;

public sealed record TestVueSlots : VueSlots;


#pragma warning restore CA1416
