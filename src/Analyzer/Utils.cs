using System;
using System.Text;
using System.Runtime.CompilerServices;

public static class Utils {
	public static T[] SubArray<T>(this T[] data, int index)
	{
		T[] result = new T[data.Length - index];
		Array.Copy(data, index, result, 0, data.Length - index);
		return result;
	}

	public static String Center (this String s, int size)
	{
		if (s == null)
			s = "";
		AssertGreaterThanEqual<int> (size, s.Length, s, null);

		StringBuilder builder = new StringBuilder (size);
		builder.Append (' ', (size - s.Length) / 2);
		builder.Append (s);
		builder.Append (' ', size - (size - s.Length) / 2 - s.Length);
		return builder.ToString();
	}

	public static void Assert (bool condition, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null)
	{
		if (!condition)
			throw new Exception (string.Format ("Assertion at {0}:{1} not met", file, lineNumber));
	}

	public static void AssertEqualRef (object t1, object t2, object o1, object o2, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null)
	{
		if (t1 != t2) {
			if (o1 != null)
				Console.WriteLine (o1);
			if (o2 != null)
				Console.WriteLine (o2);
			throw new Exception (string.Format ("Assertion at {0}:{1} not met. {2} != {3}", file, lineNumber, t1, t2));
		}
	}

	public static void AssertEqual<T> (T t1, T t2, object o1, object o2, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null) where T : IEquatable<T>
	{
		if (!t1.Equals (t2)) {
			if (o1 != null)
				Console.WriteLine (o1);
			if (o2 != null)
				Console.WriteLine (o2);
			throw new Exception (string.Format ("Assertion at {0}:{1} not met. {2} != {3}", file, lineNumber, t1, t2));
		}
	}

	public static void AssertNotEqual<T> (T t1, T t2, object o1, object o2, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null) where T : IEquatable<T>
	{
		if (t1.Equals (t2)) {
			if (o1 != null)
				Console.WriteLine (o1);
			if (o2 != null)
				Console.WriteLine (o2);
			throw new Exception (string.Format ("Assertion at {0}:{1} not met. {2} == {3}", file, lineNumber, t1, t2));
		}
	}

	public static void AssertGreaterThanEqual<T> (T t1, T t2, object o1, object o2, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string file = null) where T : IComparable<T>
	{
		if (t1.CompareTo (t2) < 0) {
			if (o1 != null)
				Console.WriteLine (o1);
			if (o2 != null)
				Console.WriteLine (o2);
			throw new Exception (string.Format ("Assertion at {0}:{1} not met. {2} < {3}", file, lineNumber, t1, t2));
		}
	}
}
