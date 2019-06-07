using System;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>An interface for a patch. Actual patches are implemented dynamically at runtime.</summary>
	public interface IPatch : IComparable
	{
		/// <summary>Zero-based index</summary>
		int index{ get; }

		/// <summary>The owner (Harmony ID)</summary>
		string owner{ get; }

		/// <summary>The priority</summary>
		int priority{ get; }

		/// <summary>The before</summary>
		string[] before{ get; }

		/// <summary>The after</summary>
		string[] after{ get; }

		/// <summary>The patch method</summary>
		MethodInfo patch{ get; }

		/// <summary>Gets the patch method</summary>
		/// <param name="original">The original method</param>
		/// <returns>The patch method</returns>
		///
		MethodInfo GetMethod(MethodBase original);

		/// <summary>Determines whether patches are equal</summary>
		/// <param name="obj">The other patch</param>
		/// <returns>true if equal</returns>
		///
		bool Equals(object obj);

		// <summary>Determines how patches sort</summary>
		// <param name="obj">The other patch</param>
		// <returns>integer to define sort order (-1, 0, 1)</returns>
		//
		//int CompareTo(object obj);

		/// <summary>Hash function</summary>
		/// <returns>A hash code</returns>
		///
		int GetHashCode();
	}
}