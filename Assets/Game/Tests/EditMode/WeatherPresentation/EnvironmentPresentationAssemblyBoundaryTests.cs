using System;
using System.Reflection;
using MSC.Weather.Presentation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WeatherPresentation
{
    public sealed class EnvironmentPresentationAssemblyBoundaryTests
    {
        [Test]
        public void ContractAssembly_DoesNotReferenceEnviroOrRenderPipelineAssemblies()
        {
            Assembly contractAssembly = typeof(IEnvironmentPresentationAdapter).Assembly;
            AssemblyName[] references = contractAssembly.GetReferencedAssemblies();

            for (int index = 0; index < references.Length; index++)
            {
                string name = references[index].Name ?? string.Empty;
                Assert.That(name.StartsWith("Enviro", StringComparison.OrdinalIgnoreCase), Is.False, name);
                Assert.That(name.Contains("RenderPipelines.HighDefinition"), Is.False, name);
                Assert.That(name.Contains("RenderPipelines.Universal"), Is.False, name);
            }
        }

        [Test]
        public void PublicContractSurface_DoesNotExposeVendorOrRenderPipelineTypes()
        {
            Assembly contractAssembly = typeof(IEnvironmentPresentationAdapter).Assembly;
            Type[] exportedTypes = contractAssembly.GetExportedTypes();

            for (int typeIndex = 0; typeIndex < exportedTypes.Length; typeIndex++)
            {
                Type exportedType = exportedTypes[typeIndex];
                AssertAllowed(exportedType, exportedType.FullName);

                PropertyInfo[] properties = exportedType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    AssertAllowed(properties[propertyIndex].PropertyType, properties[propertyIndex].Name);
                }

                FieldInfo[] fields = exportedType.GetFields(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    AssertAllowed(fields[fieldIndex].FieldType, fields[fieldIndex].Name);
                }

                MethodInfo[] methods = exportedType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                {
                    MethodInfo method = methods[methodIndex];
                    AssertAllowed(method.ReturnType, method.Name + " return");
                    ParameterInfo[] parameters = method.GetParameters();
                    for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                    {
                        AssertAllowed(parameters[parameterIndex].ParameterType, method.Name + " parameter");
                    }
                }

                ConstructorInfo[] constructors = exportedType.GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                for (int constructorIndex = 0; constructorIndex < constructors.Length; constructorIndex++)
                {
                    ParameterInfo[] parameters = constructors[constructorIndex].GetParameters();
                    for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                    {
                        AssertAllowed(parameters[parameterIndex].ParameterType, exportedType.Name + " constructor");
                    }
                }
            }
        }

        private static void AssertAllowed(Type type, string context)
        {
            if (type.IsByRef || type.IsArray || type.IsPointer)
            {
                AssertAllowed(type.GetElementType(), context);
                return;
            }

            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments();
                for (int index = 0; index < arguments.Length; index++)
                {
                    AssertAllowed(arguments[index], context);
                }
            }

            string namespaceName = type.Namespace ?? string.Empty;
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            Assert.That(namespaceName.StartsWith("Enviro", StringComparison.OrdinalIgnoreCase), Is.False, context);
            Assert.That(assemblyName.StartsWith("Enviro", StringComparison.OrdinalIgnoreCase), Is.False, context);
            Assert.That(namespaceName.Contains("HighDefinition"), Is.False, context);
            Assert.That(namespaceName.Contains("Universal"), Is.False, context);
        }
    }
}
