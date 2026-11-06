using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace BennerSmartInstaller.DynamicProxy
{
    public static class DynamicTypeGenerator
    {
        public static Type CreateDummyTypeForInterface(Type interfaceType)
        {
            try
            {
                var assemblyName = new AssemblyName("BennerDynamicDummyAssembly_" + interfaceType.Name);
                var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule("BennerDynamicDummyModule");
                var typeBuilder = moduleBuilder.DefineType("Dummy" + interfaceType.Name, TypeAttributes.Public | TypeAttributes.Class);

                typeBuilder.AddInterfaceImplementation(interfaceType);

                foreach (var method in interfaceType.GetMethods())
                {
                    var methodBuilder = typeBuilder.DefineMethod(
                        method.Name,
                        MethodAttributes.Public | MethodAttributes.Virtual,
                        method.ReturnType,
                        method.GetParameters().Select(p => p.ParameterType).ToArray());

                    var il = methodBuilder.GetILGenerator();
                    if (method.ReturnType != typeof(void))
                    {
                        if (method.ReturnType.IsValueType)
                        {
                            il.Emit(OpCodes.Ldc_I4_0);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldnull);
                        }
                    }
                    il.Emit(OpCodes.Ret);

                    typeBuilder.DefineMethodOverride(methodBuilder, method);
                }

                return typeBuilder.CreateType();
            }
            catch
            {
                return null;
            }
        }
    }
}
