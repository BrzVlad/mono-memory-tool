using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Benchmarker.Common.Models
{
	public class Benchmark
	{
		public string Name { get; set; }
		public string TestDirectory { get; set; }
		public string[] CommandLine { get; set; }

		public Benchmark ()
		{
		}

		public static Benchmark LoadFrom (string filename)
		{
			using (var reader = new StreamReader (new FileStream (filename, FileMode.Open))) {
				var benchmark = JsonConvert.DeserializeObject<Benchmark> (reader.ReadToEnd ());

				if (String.IsNullOrEmpty (benchmark.TestDirectory))
					throw new InvalidDataException ("TestDirectory");
				if (benchmark.CommandLine == null || benchmark.CommandLine.Length == 0)
					throw new InvalidDataException ("CommandLine");

				return benchmark;
			}
		}

		public static List<Benchmark> LoadAllFrom (string directory)
		{
			return LoadAllFrom (directory, null);
		}

		public static List<Benchmark> LoadAllFrom (string directory, string[] names)
		{
			var allPaths = Directory.EnumerateFiles (directory)
				.Where (f => f.EndsWith (".benchmark"));
			if (names != null) {
				foreach (var name in names) {
					if (!allPaths.Any (p => Path.GetFileNameWithoutExtension (p) == name))
						return null;
				}
				allPaths = allPaths
					.Where (f => names.Any (n => Path.GetFileNameWithoutExtension (f) == n));
			}
			return allPaths
				.Select (f => Benchmark.LoadFrom (f))
				.OrderBy (b => b.Name)
				.ToList ();
		}

		public override bool Equals (object other)
		{
			if (other == null)
				return false;

			var benchmark = other as Benchmark;
			if (benchmark == null)
				return false;

			return Name.Equals (benchmark.Name);
		}

		public override int GetHashCode ()
		{
			return Name.GetHashCode ();
		}
	}
}
