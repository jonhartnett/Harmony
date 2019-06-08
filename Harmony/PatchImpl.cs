using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	///<summary>Creates IPatch and IPatchInfo implementations.</summary>
	public static class PatchImpl
	{
		/// <summary>
		/// Creates a wrapper implementation of IPatchInfo using another assembly's HarmonySharedState.PatchInfo.
		/// </summary>
		/// <param name="patchInfoType">The concrete PatchInfo type.</param>
		/// <param name="patchType">The concrete Patch type.</param>
		/// <returns>A type the wraps patchInfoType to implement IPatchInfo.</returns>
		public static Type CreatePatchInfoImpl(Type patchInfoType, Type patchType)
		{
			//make dynamic module
			string name = $"{typeof(PatchImpl).Module.Assembly.FullName}PatchImplementation";
			var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule(name);

			return CreatePatchInfoImpl(moduleBuilder, patchInfoType, patchType);
		}

		private static Type CreatePatchInfoImpl(ModuleBuilder moduleBuilder, Type patchInfoType, Type patchType)
		{
			var typeBuilder = moduleBuilder.DefineType("PatchInfo",
				TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Sealed,
				null,
				new []{typeof(PatchInfo)}
			);

			var instance = typeBuilder.DefineField("instance", patchInfoType, FieldAttributes.Public | FieldAttributes.InitOnly);

			var patchWrapperType = CreatePatchImpl(moduleBuilder, patchType);
			//make factory for wrappers
			var factory = CreateFactory(patchWrapperType, new []{ patchType });

			DefineImplConstructor(patchInfoType, typeBuilder, instance);

			DefineImplPatchEnumerableProperty(patchInfoType, typeBuilder, instance, "prefixes", factory, patchType);
			DefineImplPatchEnumerableProperty(patchInfoType, typeBuilder, instance, "postfixes", factory, patchType);
			DefineImplPatchEnumerableProperty(patchInfoType, typeBuilder, instance, "transpilers", factory, patchType);
			DefineImplPatchEnumerableProperty(patchInfoType, typeBuilder, instance, "finalizers", factory, patchType);

			DefineImplMethod(patchInfoType, typeBuilder, instance, "AddPrefix", typeof(void), new []
			{
				typeof(MethodInfo), typeof(string), typeof(int), typeof(string[]), typeof(string[])
			});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "RemovePrefix", typeof(void), new []{typeof(string)});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "AddPostfix", typeof(void), new []
			{
				typeof(MethodInfo), typeof(string), typeof(int), typeof(string[]), typeof(string[])
			});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "RemovePostfix", typeof(void), new []{typeof(string)});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "AddTranspiler", typeof(void), new []
			{
				typeof(MethodInfo), typeof(string), typeof(int), typeof(string[]), typeof(string[])
			});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "RemoveTranspiler", typeof(void), new []{typeof(string)});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "AddFinalizer", typeof(void), new []
			{
				typeof(MethodInfo), typeof(string), typeof(int), typeof(string[]), typeof(string[])
			});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "RemoveFinalizer", typeof(void), new []{typeof(string)});
			DefineImplMethod(patchInfoType, typeBuilder, instance, "RemovePatch", typeof(void), new []{ typeof(MethodInfo) });

			return typeBuilder.CreateType();
		}

		private static Type CreatePatchImpl(ModuleBuilder moduleBuilder, Type patchType)
		{
			var typeBuilder = moduleBuilder.DefineType("Patch",
				TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Sealed,
				null,
				new []{typeof(Patch)}
			);

			var instance = typeBuilder.DefineField("instance", patchType, FieldAttributes.Public | FieldAttributes.InitOnly);

			DefineImplConstructor(patchType, typeBuilder, instance);

			DefineImplProperty(patchType, typeBuilder, instance, "index", typeof(int));
			DefineImplProperty(patchType, typeBuilder, instance, "owner", typeof(string));
			DefineImplProperty(patchType, typeBuilder, instance, "priority", typeof(int));
			DefineImplProperty(patchType, typeBuilder, instance, "before", typeof(string[]));
			DefineImplProperty(patchType, typeBuilder, instance, "after", typeof(string[]));
			DefineImplProperty(patchType, typeBuilder, instance, "patch", typeof(MethodInfo));

			DefineImplMethod(patchType, typeBuilder, instance, "GetMethod", typeof(MemberInfo), new []{typeof(MethodBase)});
			DefineImplMethod(patchType, typeBuilder, instance, "Equals", typeof(bool), new []{typeof(object)});
			DefineImplMethod(patchType, typeBuilder, instance, "CompareTo", typeof(int), new []{typeof(object)});
			DefineImplMethod(patchType, typeBuilder, instance, "GetHashCode", typeof(int), Type.EmptyTypes);

			return typeBuilder.CreateType();
		}

		private static void DefineImplConstructor(Type instanceType, TypeBuilder typeBuilder, FieldInfo instance)
		{
			//implement method
			var methodBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new []{instanceType});
			var il = methodBuilder.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Stfld, instance);
			il.Emit(OpCodes.Ret);
		}

		private static void DefineImplProperty(Type instanceType, TypeBuilder typeBuilder, FieldInfo instance, string name, Type type)
		{
			//get underlying field
			var field = AccessTools.Field(instanceType, name);
			if(field == null)
				throw new Exception($"{instanceType.FullName} field ${name} is missing.");
			if(!field.FieldType.Equals(type))
				throw new Exception($"{instanceType.FullName} field ${name} is type {field.FieldType.FullName}. Expected {type.FullName}.");

			//implement property
			typeBuilder.DefineProperty(name, PropertyAttributes.None, type, Type.EmptyTypes);

			//implement getter
			var getterBuilder = typeBuilder.DefineMethod($"get_{name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, type, Type.EmptyTypes);
			var il = getterBuilder.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, instance);
			il.Emit(OpCodes.Ldfld, field);
			il.Emit(OpCodes.Ret);
		}

		private static void DefineImplPatchEnumerableProperty(Type instanceType, TypeBuilder typeBuilder, FieldInfo instance, string name, MethodInfo factory, Type patchType)
		{
			//get underlying field
			var field = AccessTools.Field(instanceType, name);
			if(field == null)
				throw new Exception($"{instanceType.FullName} field ${name} is missing.");

			//get enumerable of patches
			var patchEnumerableType = typeof(IEnumerable<>).MakeGenericType(patchType);

			//get func of patch to ipatch
			var wrapperFunc = typeof(Func<>).MakeGenericType(patchType, typeof(Patch));

			//get Select
			var select = AccessTools.DeclaredMethod(typeof(Enumerable), "Select", new[]{ patchEnumerableType, wrapperFunc }, new []{ patchType, typeof(Patch) });

			//implement property
			typeBuilder.DefineProperty(name, PropertyAttributes.None, typeof(IEnumerable<Patch>), Type.EmptyTypes);

			//implement getter
			var getterBuilder = typeBuilder.DefineMethod($"get_{name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(Patch), Type.EmptyTypes);
			var il = getterBuilder.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, instance);
			il.Emit(OpCodes.Ldfld, field);
			il.Emit(OpCodes.Ldftn, factory);
			il.Emit(OpCodes.Call, select);
			il.Emit(OpCodes.Ret);
		}

		private static void DefineImplMethod(Type instanceType, TypeBuilder typeBuilder, FieldInfo instance, string name, Type retType, Type[] args)
		{
			//get underlying field
			var method = AccessTools.DeclaredMethod(instanceType, name, args);
			if(method == null)
				throw new Exception($"{instanceType.FullName} method ${name} is missing.");
			if(!method.ReturnType.Equals(retType))
				throw new Exception($"{instanceType.FullName} method ${name} returns type {method.ReturnType.FullName}. Expected {retType.FullName}.");

			//implement method
			var methodBuilder = typeBuilder.DefineMethod(name, MethodAttributes.Public, retType, args);
			var il = methodBuilder.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, instance);
			for(int i = 0; i < args.Length; i++)
				EmitFastLdArg(il, i);
			il.Emit(OpCodes.Call, method);
			il.Emit(OpCodes.Ret);
		}

		private static DynamicMethod CreateFactory(Type type, Type[] args=null)
		{
			args = args ?? Type.EmptyTypes;
			var constructor = AccessTools.DeclaredConstructor(type, args);
			if(constructor == null)
				throw new Exception($"{type.FullName} constructor (${args.Join(arg => arg.FullName)}) is missing.");
			var dynamicMethod = new DynamicMethod($"create{type.Name}", type, args, typeof(PatchImpl).Module, true);
			var il = dynamicMethod.GetILGenerator();
			for(int i = 0; i < args.Length; i++)
				EmitFastLdArg(il, i);
			il.Emit(OpCodes.Newobj, constructor);
			il.Emit(OpCodes.Ret);
			return dynamicMethod;
		}

		private static void EmitFastLdArg(ILGenerator il, int index)
		{
			switch(index)
			{
				case 0:
					il.Emit(OpCodes.Ldarg_0);
					return;
				case 1:
					il.Emit(OpCodes.Ldarg_1);
					return;
				case 2:
					il.Emit(OpCodes.Ldarg_2);
					return;
				case 3:
					il.Emit(OpCodes.Ldarg_3);
					return;
				default:
					il.Emit(OpCodes.Ldarg, index);
					return;
			}
		}
	}
}