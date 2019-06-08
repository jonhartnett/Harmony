using System.Collections.Generic;
using System.Reflection;

namespace Harmony
{
	/// <summary>An interface for patch information. Actual PatchInfos are implemented dynamically at runtime.</summary>
	public interface PatchInfo
	{
		/// <summary>The prefixes</summary>
		IEnumerable<Patch> prefixes{ get; }

		/// <summary>The postfixes</summary>
		IEnumerable<Patch> postfixes{ get; }

		/// <summary>The transpilers</summary>
		IEnumerable<Patch> transpilers{ get; }

		/// <summary>The finalizers</summary>
		IEnumerable<Patch> finalizers{ get; }

		/// <summary>Adds a prefix</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after);

		/// <summary>Removes a prefix</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		void RemovePrefix(string owner);

		/// <summary>Adds a postfix</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after);

		/// <summary>Removes a postfix</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		void RemovePostfix(string owner);

		/// <summary>Adds a transpiler</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after);

		/// <summary>Removes a transpiler</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		void RemoveTranspiler(string owner);

		/// <summary>Adds a finalizer</summary>
		/// <param name="patch">The patch</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="priority">The priority</param>
		/// <param name="before">The before parameter</param>
		/// <param name="after">The after parameter</param>
		///
		void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after);

		/// <summary>Removes a finalizer</summary>
		/// <param name="owner">The owner or (*) for any</param>
		///
		void RemoveFinalizer(string owner);

		/// <summary>Removes a patch</summary>
		/// <param name="patch">The patch method</param>
		///
		void RemovePatch(MethodInfo patch);
	}
}