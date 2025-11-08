using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Cobra.Interpreter
{
    public static class CobraDelegateFactory
    {
        private static readonly ConcurrentDictionary<string, Type> Cache = new();
        private static readonly AssemblyBuilder AsmBuilder;
        private static readonly ModuleBuilder ModBuilder;

        static CobraDelegateFactory()
        {
            var asmName = new AssemblyName("CobraDynamicDelegateAssembly");
            AsmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            ModBuilder = AsmBuilder.DefineDynamicModule("CobraDynamicDelegateModule");
        }

        public static Type Create(Type returnType, params Type[] parameterTypes)
        {
            // Create a unique key for this delegate signature to cache it
            var key = $"{returnType.FullName}:{string.Join(",", Array.ConvertAll(parameterTypes, t => t.FullName))}";

            return Cache.GetOrAdd(key, _ =>
            {
                // Create a new delegate type with a unique name
                var typeBuilder = ModBuilder.DefineType(
                    $"CobraDynamicDelegate_{Guid.NewGuid():N}",
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
                    typeof(MulticastDelegate));

                // Define the constructor
                typeBuilder.DefineConstructor(
                        MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                        CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) })
                    .SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

                // Define the "Invoke" method, which is the core of the delegate
                var methodBuilder = typeBuilder.DefineMethod(
                    "Invoke",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                    returnType,
                    parameterTypes);
                
                methodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

                // Bake the new type
                return typeBuilder.CreateType();
            });
        }
    }
}
