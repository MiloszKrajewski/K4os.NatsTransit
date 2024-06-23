using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace K4os.NatsTransit.Extensions;

internal static class TypeExtensions
{
	private const string NullText = "<null>";

	/// <summary>
	/// Type distance cache. It should Concurrent dictionary but it is not available
	/// on all flavors of Portable Class Library.
	/// </summary>
	private static readonly ConcurrentDictionary<(Type, Type), int> 
		TypeDistanceMap = new();

	/// <summary>Checks if child type inherits (or implements) from parent.</summary>
	/// <param name="child">The child.</param>
	/// <param name="parent">The parent.</param>
	/// <returns><c>true</c> if child type inherits (or implements) from parent; <c>false</c> otherwise</returns>
	public static bool InheritsFrom(this Type child, Type parent) =>
		parent.IsAssignableFrom(child);
	
	/// <summary>Checks if child type inherits (or implements) from parent.</summary>
	/// <param name="child">The child.</param>
	/// <typeparam name="TParent">Potential parent class.</typeparam>
	/// <returns><c>true</c> if child type inherits (or implements) from parent; <c>false</c> otherwise</returns>
	public static bool InheritsFrom<TParent>(this Type child) =>
		typeof(TParent).IsAssignableFrom(child);

	/// <summary>Calculates distance between child and parent type.</summary>
	/// <param name="child">The child.</param>
	/// <param name="grandparent">The parent.</param>
	/// <returns>Inheritance distance between child and parent.</returns>
	/// <exception cref="System.ArgumentException">Thrown when child does not inherit from parent at all.</exception>
	public static int DistanceFrom(this Type child, Type grandparent)
	{
		ArgumentNullException.ThrowIfNull(child);
		ArgumentNullException.ThrowIfNull(grandparent);

		return child != grandparent
			? TypeDistanceMap.GetOrAdd((child, grandparent), ResolveDistance)
			: 0; 
	}

	private static T MinOrDefault<T>(this IEnumerable<T> values, T defaultValue = default)
		where T: struct
	{
		var empty = true;
		var comparer = Comparer<T>.Default;
		var minimum = defaultValue;
			
		foreach (var value in values)
		{
			minimum = empty ? value : comparer.Compare(value, minimum) < 0 ? value : minimum;
			empty = false;
		}
			
		return empty ? defaultValue : minimum;
	}

	private static int ResolveDistance((Type, Type) types)
	{
		var (child, grandparent) = types;
		if (child == grandparent)
			return 0;

		if (!child.InheritsFrom(grandparent))
			throw new ArgumentException(
				$"Type '{child.Name}' does not inherit nor implements '{grandparent.Name}'");

		static int Inc(int value) => value == int.MaxValue ? value : value + 1;

		// this may happen with covariant interfaces
		// they are "assignable from" but not "inheriting" from each other
		return GetIntermediateParents(child, grandparent)
			.Select(t => Inc(DistanceFrom(t, grandparent)))
			.MinOrDefault(int.MaxValue);
	}

	/// <summary>Gets the list of parent types which also inherit for grandparent.</summary>
	/// <param name="child">The child.</param>
	/// <param name="grandparent">The parent.</param>
	/// <returns>Collection of types.</returns>
	private static IEnumerable<Type> GetIntermediateParents(Type child, Type grandparent)
	{
		var baseType = child.BaseType;

		if (grandparent.IsInterface)
		{
			// determines if given interface "leads" to grandparent
			// and if child is first implementor of given interface
			// note: this is special case for interfaces as they are reported on every child
			// along the way, and we want the most distant one (when it was implemented
			// for the first time in hierarchy)
			bool IsFirstImplementation(Type interfaceType) =>
				interfaceType.InheritsFrom(grandparent) && // right path 
				(baseType is null || !baseType.InheritsFrom(interfaceType)); // first time

			var baseInterfaces = child.GetInterfaces().Where(IsFirstImplementation);
			foreach (var i in baseInterfaces)
				yield return i;
		}

		if (baseType is null) 
			yield break;

		if (baseType.InheritsFrom(grandparent))
			yield return baseType;
	}

	private static readonly ConcurrentDictionary<Type, string> FriendlyNames = new();

	public static string GetFriendlyName(this Type type) => 
		FriendlyNames.GetOrAdd(type, NewFriendlyName);

	private static string NewFriendlyName(Type? type)
	{
		if (type is null) 
			return NullText;

		var typeName = type.Name;
		if (!type.IsGenericType)
			return typeName;

		var length = typeName.IndexOf('`');
		if (length < 0) length = typeName.Length;

		return new StringBuilder()
			.Append(typeName, 0, length)
			.Append('<')
			.Append(string.Join(",", type.GetGenericArguments().Select(GetFriendlyName)))
			.Append('>')
			.ToString();
	}
	
	public static string GetObjectFriendlyName(this object? subject) =>
		subject is not null
			? $"{subject.GetType().GetFriendlyName()}@{RuntimeHelpers.GetHashCode(subject):x}"
			: NullText;
}