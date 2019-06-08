using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class HarmonySharedState
	{
		private delegate PatchInfo StateGetter(MethodBase method);
		private delegate IEnumerable<MethodBase> StateKeyGetter();

		static readonly string name = "HarmonySharedState";
		internal static readonly int internalVersion = 100;
		internal static int actualVersion = -1;

		/// <summary>
		/// Gets a PatchInfo for the given MethodBase.
		/// </summary>
		private static StateGetter stateGet;
		/// <summary>
		/// Gets (or inserts) a PatchInfo for the given MethodBase.
		/// </summary>
		private static StateGetter stateGetOrInsert;
		/// <summary>
		/// Gets the MethodBases which have patches.
		/// </summary>
		private static StateKeyGetter stateGetKeys;

		/// <summary>
		/// Only one instance of this class will actually be used.
		/// Stores the mapping from MethodBase to PatchInfo.
		/// </summary>
		private class SharedPatchInfosDictionary
		{
			private readonly Dictionary<MethodBase, SharedPatchInfo> state = new Dictionary<MethodBase, SharedPatchInfo>();

			public SharedPatchInfo Get(MethodBase key)
			{
				if(state.TryGetValue(key, out var info))
					return info;
				return null;
			}

			public SharedPatchInfo GetOrInsert(MethodBase key)
			{
				if(state.TryGetValue(key, out var info))
					return info;
				info = new SharedPatchInfo();
				state[key] = info;
				return info;
			}

			public IEnumerable<MethodBase> GetKeys()
			{
				return state.Keys;
			}
		}

		/// <summary>
		/// Only one instance of this class will actually be used.
		/// Concrete implementation of a PatchInfo.
		/// </summary>
		private sealed class SharedPatchInfo : PatchInfo
		{
			/// <summary>The prefixes</summary>
			public List<SharedPatch> prefixes;
			/// <summary>The postfixes</summary>
			public List<SharedPatch> postfixes;
			/// <summary>The transpilers</summary>
			public List<SharedPatch> transpilers;
			/// <summary>The finalizers</summary>
			public List<SharedPatch> finalizers;

			IEnumerable<Patch> PatchInfo.prefixes => prefixes.Cast<Patch>();
			IEnumerable<Patch> PatchInfo.postfixes => postfixes.Cast<Patch>();
			IEnumerable<Patch> PatchInfo.transpilers => transpilers.Cast<Patch>();
			IEnumerable<Patch> PatchInfo.finalizers => finalizers.Cast<Patch>();

			/// <summary>Default constructor</summary>
			public SharedPatchInfo()
			{
				prefixes = new List<SharedPatch>();
				postfixes = new List<SharedPatch>();
				transpilers = new List<SharedPatch>();
				finalizers = new List<SharedPatch>();
			}

			/// <summary>Adds a prefix</summary>
			/// <param name="patch">The patch</param>
			/// <param name="owner">The owner (Harmony ID)</param>
			/// <param name="priority">The priority</param>
			/// <param name="before">The before parameter</param>
			/// <param name="after">The after parameter</param>
			///
			public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after)
			{
				prefixes.Add(new SharedPatch(patch, prefixes.Count + 1, owner, priority, before, after));
			}

			/// <summary>Removes a prefix</summary>
			/// <param name="owner">The owner or (*) for any</param>
			///
			public void RemovePrefix(string owner)
			{
				if (owner == "*")
				{
					prefixes.Clear();
					return;
				}
				prefixes.RemoveAll(patch => patch.owner == owner);
			}

			/// <summary>Adds a postfix</summary>
			/// <param name="patch">The patch</param>
			/// <param name="owner">The owner (Harmony ID)</param>
			/// <param name="priority">The priority</param>
			/// <param name="before">The before parameter</param>
			/// <param name="after">The after parameter</param>
			///
			public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after)
			{
				postfixes.Add(new SharedPatch(patch, postfixes.Count + 1, owner, priority, before, after));
			}

			/// <summary>Removes a postfix</summary>
			/// <param name="owner">The owner or (*) for any</param>
			///
			public void RemovePostfix(string owner)
			{
				if (owner == "*")
				{
					postfixes.Clear();
					return;
				}
				postfixes.RemoveAll(patch => patch.owner == owner);
			}

			/// <summary>Adds a transpiler</summary>
			/// <param name="patch">The patch</param>
			/// <param name="owner">The owner (Harmony ID)</param>
			/// <param name="priority">The priority</param>
			/// <param name="before">The before parameter</param>
			/// <param name="after">The after parameter</param>
			///
			public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after)
			{
				transpilers.Add(new SharedPatch(patch, transpilers.Count + 1, owner, priority, before, after));
			}

			/// <summary>Removes a transpiler</summary>
			/// <param name="owner">The owner or (*) for any</param>
			///
			public void RemoveTranspiler(string owner)
			{
				if (owner == "*")
				{
					transpilers.Clear();
					return;
				}
				transpilers.RemoveAll(patch => patch.owner == owner);
			}

			/// <summary>Adds a finalizer</summary>
			/// <param name="patch">The patch</param>
			/// <param name="owner">The owner (Harmony ID)</param>
			/// <param name="priority">The priority</param>
			/// <param name="before">The before parameter</param>
			/// <param name="after">The after parameter</param>
			///
			public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after)
			{
				finalizers.Add(new SharedPatch(patch, finalizers.Count + 1, owner, priority, before, after));
			}

			/// <summary>Removes a finalizer</summary>
			/// <param name="owner">The owner or (*) for any</param>
			///
			public void RemoveFinalizer(string owner)
			{
				if (owner == "*")
				{
					finalizers.Clear();
					return;
				}
				finalizers.RemoveAll(patch => patch.owner == owner);
			}

			/// <summary>Removes a patch</summary>
			/// <param name="patch">The patch method</param>
			///
			public void RemovePatch(MethodInfo patch)
			{
				prefixes.RemoveAll(patch2 => patch2.patch == patch);
				postfixes.RemoveAll(patch2 => patch2.patch == patch);
				transpilers.RemoveAll(patch2 => patch2.patch == patch);
				finalizers.RemoveAll(patch2 => patch2.patch == patch);
			}
		}

		/// <summary>
		/// Only one instance of this class will actually be used.
		/// Concrete implementation of a Patch.
		/// </summary>
		internal sealed class SharedPatch : Patch
		{
			/// <summary>Zero-based index</summary>
			public readonly int index;
			/// <summary>The owner (Harmony ID)</summary>
			public readonly string owner;
			/// <summary>The priority</summary>
			public readonly int priority;
			/// <summary>The before</summary>
			public readonly string[] before;
			/// <summary>The after</summary>
			public readonly string[] after;

			/// <summary>The patch method</summary>
			public readonly MethodInfo patch;

			int Patch.index => index;
			string Patch.owner => owner;
			int Patch.priority => priority;
			string[] Patch.before => before;
			string[] Patch.after => after;
			MethodInfo Patch.patch => patch;

			/// <summary>Creates a patch</summary>
			/// <param name="patch">The patch</param>
			/// <param name="index">Zero-based index</param>
			/// <param name="owner">The owner (Harmony ID)</param>
			/// <param name="priority">The priority</param>
			/// <param name="before">The before parameter</param>
			/// <param name="after">The after parameter</param>
			///
			public SharedPatch(MethodInfo patch, int index, string owner, int priority, string[] before, string[] after)
			{
				if (patch is DynamicMethod)
					throw new Exception($"Cannot directly reference dynamic method \"{patch.FullDescription()}\" in Harmony. Use a factory method instead that will return the dynamic method.");

				this.index = index;
				this.owner = owner;
				this.priority = priority;
				this.before = before;
				this.after = after;
				this.patch = patch;
			}

			/// <summary>Gets the patch method</summary>
			/// <param name="original">The original method</param>
			/// <returns>The patch method</returns>
			///
			public MethodInfo GetMethod(MethodBase original)
			{
				if (patch.ReturnType != typeof(DynamicMethod))
					return patch;
				if (patch.IsStatic == false)
					return patch;
				var parameters = patch.GetParameters();
				if (parameters.Length != 1)
					return patch;
				if (parameters[0].ParameterType != typeof(MethodBase))
					return patch;

				// we have a DynamicMethod factory, let's use it
				return patch.Invoke(null, new object[] { original }) as DynamicMethod;
			}

			/// <summary>Determines whether patches are equal</summary>
			/// <param name="obj">The other patch</param>
			/// <returns>true if equal</returns>
			///
			public override bool Equals(object obj)
			{
				return obj is Patch other && patch == other.patch;
			}

			/// <summary>Determines how patches sort</summary>
			/// <param name="obj">The other patch</param>
			/// <returns>integer to define sort order (-1, 0, 1)</returns>
			///
			public int CompareTo(object obj)
			{
				if(!(obj is Patch other))
					return -1;
				if(priority != other.priority)
					return -priority.CompareTo(other.priority);
				return index.CompareTo(other.index);
			}

			/// <summary>Hash function</summary>
			/// <returns>A hash code</returns>
			///
			public override int GetHashCode()
			{
				return patch.GetHashCode();
			}
		}

		/// <summary>
		/// Creates the accessors for the shared state.
		/// </summary>
		static void CreateStateAccessors()
		{
			lock (name)
			{
				Type wrapperType = null;
				FieldInfo patchInfosField;

				var assembly = SharedStateAssembly();
				if (assembly == null)
				{
					//we are the first harmony, setup the common state using our shared types
					var patchInfoImplType = typeof(SharedPatchInfo);
					var patchImplType = typeof(SharedPatch);

					var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
					var moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
					var typeAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract;
					var typeBuilder = moduleBuilder.DefineType(name, typeAttributes);

					//a version number for common state
					typeBuilder.DefineField("version", typeof(int), FieldAttributes.Static | FieldAttributes.Public).SetConstant(internalVersion);
					//the concrete type of patch infos
					typeBuilder.DefineField("patchInfoType", typeof(Type), FieldAttributes.Static | FieldAttributes.Public);
					//the concrete type of patches
					typeBuilder.DefineField("patchType", typeof(Type), FieldAttributes.Static | FieldAttributes.Public);
					//the dictionary of patches
					typeBuilder.DefineField("patchInfos", typeof(SharedPatchInfosDictionary), FieldAttributes.Static | FieldAttributes.Public);

					var stateType = typeBuilder.CreateType();

					getField(stateType, "patchInfoType").SetValue(null, patchInfoImplType);
					getField(stateType, "patchType").SetValue(null, patchImplType);
					patchInfosField = getField(stateType, "patchInfos");
					patchInfosField.SetValue(null, new SharedPatchInfosDictionary());

					assembly = SharedStateAssembly();
					if(assembly == null)
						throw new Exception("Cannot find or create harmony shared state");
				}
				else
				{
					var stateType = assembly.GetType(name);
					if(stateType == null)
						throw new Exception($"Cannot find harmony shared state type\n{assembly.GetTypes().Join(type => type.Name)}");

					actualVersion = (int)getField(stateType, "version").GetValue(null);
					var patchInfoType = (Type)getField(stateType, "patchInfoType").GetValue(null);
					var patchType = (Type)getField(stateType, "patchType").GetValue(null);
					patchInfosField = getField(stateType, "patchInfos");

					wrapperType = PatchImpl.CreatePatchInfoImpl(patchInfoType, patchType);
				}

				FieldInfo getField(Type type, string name)
				{
					var field = AccessTools.Field(type, name);
					if(field == null)
						throw new Exception($"Cannot find harmony {name} field");
					return field;
				}

				var patchInfosType = patchInfosField.FieldType;
				var get = AccessTools.DeclaredMethod(patchInfosType, "Get");
				var getOrInsert = AccessTools.DeclaredMethod(patchInfosType, "GetOrInsert");
				var getKeys = AccessTools.DeclaredMethod(patchInfosType, "GetKeys");

				ConstructorInfo wrapConstructor = null;
				if(wrapperType != null)
					wrapConstructor = AccessTools.DeclaredConstructor(wrapperType, new[] { typeof(MethodBase) });

				DynamicMethod getImpl = new DynamicMethod("stateGet", typeof(PatchInfo), new []{ typeof(MethodBase) }, typeof(HarmonySharedState).Module, true);
				var il = getImpl.GetILGenerator();
				il.Emit(OpCodes.Ldsfld, patchInfosField);
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Call, get);
				if(wrapConstructor != null)
				{
					var done = il.DefineLabel();
					il.Emit(OpCodes.Brfalse, done);
					il.Emit(OpCodes.Call, wrapConstructor);
					il.MarkLabel(done);
				}
				il.Emit(OpCodes.Ret);
				stateGet = (StateGetter)getImpl.CreateDelegate(typeof(StateGetter));

				DynamicMethod getOrInsertImpl = new DynamicMethod("stateGetOrInsert", typeof(PatchInfo), new []{ typeof(MethodBase) }, typeof(HarmonySharedState).Module, true);
				il = getOrInsertImpl.GetILGenerator();
				il.Emit(OpCodes.Ldsfld, patchInfosField);
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Call, getOrInsert);
				if(wrapConstructor != null)
					il.Emit(OpCodes.Call, wrapConstructor);
				il.Emit(OpCodes.Ret);
				stateGetOrInsert = (StateGetter)getOrInsertImpl.CreateDelegate(typeof(StateGetter));

				DynamicMethod getKeysImpl = new DynamicMethod("stateGetKeys", typeof(IEnumerable<MethodBase>), Type.EmptyTypes, typeof(HarmonySharedState).Module, true);
				il = getKeysImpl.GetILGenerator();
				il.Emit(OpCodes.Ldsfld, patchInfosField);
				il.Emit(OpCodes.Call, getKeys);
				il.Emit(OpCodes.Ret);
				stateGetKeys = (StateKeyGetter)getKeysImpl.CreateDelegate(typeof(StateKeyGetter));
			}
		}

		static Assembly SharedStateAssembly()
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(a => a.GetName().Name.Contains(name));
		}

		/// <summary>
		/// Gets an IPatchInfo for the given MethodBase. Returns null if there are no patches for the given MethodBase.
		/// </summary>
		/// <param name="method">The method to lookup.</param>
		/// <returns>The IPatchInfo implementation or null.</returns>
		internal static PatchInfo GetPatchInfo(MethodBase method)
		{
			if(stateGet == null)
				CreateStateAccessors();
			return stateGet(method);
		}

		/// <summary>
		/// Gets an IPatchInfo for the given MethodBase. Inserts and returns a new IPatchInfo if there are no patches for the given MethodBase.
		/// </summary>
		/// <param name="method">The method to lookup.</param>
		/// <returns>The IPatchInfo implementation.</returns>
		internal static PatchInfo GetOrCreatePatchInfo(MethodBase method)
		{
			if(stateGetOrInsert == null)
				CreateStateAccessors();
			return stateGetOrInsert(method);
		}

		/// <summary>
		/// Returns the list of MethodBases with patches.
		/// </summary>
		/// <returns>An enumerable of patched MethodBases.</returns>
		internal static IEnumerable<MethodBase> GetPatchedMethods()
		{
			if(stateGetKeys == null)
				CreateStateAccessors();
			return stateGetKeys();
		}
	}
}