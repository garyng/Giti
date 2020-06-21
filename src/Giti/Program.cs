using LibGit2Sharp;
using System;
using System.IO;

namespace Giti
{
	public enum Sources
	{
		CurrentBranch
	}

	public class Options
	{
		/// <summary>
		/// File path that contains the commit message. Provided by git.
		/// </summary>
		public string CommitMessageFile { get; set; }
		/// <summary>
		/// The source for extraction.
		/// </summary>
		public Sources Source { get; set; }
		/// <summary>
		/// The regex used for extraction from the <see cref="Source"/>.
		/// </summary>
		public string ExtractionRegex { get; set; }
		/// <summary>
		/// The template used for replacing the commit message.
		/// </summary>
		public string ReplacementTemplate { get; set; }

	}

	class Program
	{
		static void Main(string[] args)
		{
			var currentDir = Directory.GetCurrentDirectory();
			using var repo = new Repository(@"D:\projects\giti");
		}
	}
}
