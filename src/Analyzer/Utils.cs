using System;
using System.Runtime.CompilerServices;

public static class Utils {
	public static T[] SubArray<T>(this T[] data, int index)
	{
		T[] result = new T[data.Length - index];
		Array.Copy(data, index, result, 0, data.Length - index);
		return result;
	}

	public static void Assert (bool condition, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null)
	{
		if (!condition)
			throw new Exception (string.Format ("Assertion at {0}:{1} not met", file, lineNumber));
	}

	public static void AssertEqual<T> (T t1, T t2, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null) where T : IEquatable<T>
	{
		if (!t1.Equals (t2))
			throw new Exception (string.Format ("Assertion at {0}:{1} not met. {2} != {3}", file, lineNumber, t1, t2));
	}

	public static void AssertNotEqual<T> (T t1, T t2, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null) where T : IEquatable<T>
	{
		if (t1.Equals (t2))
			throw new Exception (string.Format ("Assertion at {0}:{1} not met. {2} == {3}", file, lineNumber, t1, t2));
	}
}
