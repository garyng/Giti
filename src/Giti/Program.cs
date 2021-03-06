﻿using LibGit2Sharp;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Scriban;

namespace Giti
{
	public enum SourceTypes
	{
		GitHeadFriendlyName,
		GitHeadCanonicalName
	}

	public class Options
	{
		[Value(0, HelpText = "Path to the COMMIT_EDITMSG file (provided by git).")]
		public string CommitMessageFile { get; set; }

		[Option('s', "sourceType", Default = SourceTypes.GitHeadFriendlyName, HelpText = "The source for extraction.")]
		public SourceTypes SourceType { get; set; }

		[Option('p', "pattern", Required = true, HelpText = "The regex used for extraction from the source.")]
		public string Pattern { get; set; }

		[Option('t', "template", Required = true,
			HelpText = "The template used for constructing the commit message. Uses scriban templating engine.")]
		public string Template { get; set; }

		[Option('k', "skipPattern", Default = null, HelpText = "Skip replacement if this regex pattern matches the original commit message. Default to use 'pattern'.")]
		public string SkipPattern { get; set; }
	}

	class Program
	{
		static async Task Main(string[] args)
		{
			var result = await new Parser(config => config.HelpWriter = null)
				.ParseArguments<Options>(args)
				.WithParsedAsync(options => Run(options));
			result.WithNotParsed(errors => Console.WriteLine(
				HelpText.AutoBuild(result, help =>
				{
					help.AddEnumValuesToHelpText = true;
					return help;
				})));
		}

		public static async Task Run(Options option)
		{
			using var repo = TryCreateRepo();
			if (repo is null)
			{
				Error("Not a valid git repo");
				return;
			}

			var source = option.SourceType switch
			{
				SourceTypes.GitHeadFriendlyName => repo.Head.FriendlyName,
				SourceTypes.GitHeadCanonicalName => repo.Head.CanonicalName,
				_ => throw new ArgumentOutOfRangeException()
			};

			var match = Regex.Match(source, option.Pattern).Value;
			if (string.IsNullOrEmpty(match))
			{
				Warn($"Pattern '{option.Pattern}' matched nothing");
				return;
			}

			Ok($"Matched '{match}' from '{source}'");

			var originalMessage = await File.ReadAllTextAsync(option.CommitMessageFile);

			var skipPattern = string.IsNullOrEmpty(option.SkipPattern) ? option.Pattern : option.SkipPattern;
			var matchInMessage = Regex.Match(originalMessage, skipPattern).Value;
			if (!string.IsNullOrEmpty(matchInMessage))
			{
				Warn($"Pattern '{skipPattern}' matches the original message. Skipping.");
				return;
			}

			var template = Template.Parse(option.Template);
			var finalMessage = template.Render(new
			{
				match,
				message = originalMessage,
			});
			await File.WriteAllTextAsync(option.CommitMessageFile, finalMessage);


			Repository? TryCreateRepo()
			{
				var curDir = Directory.GetCurrentDirectory();
				try
				{
					return new Repository(curDir);
				}
				catch (Exception)
				{
					return null;
				}
			}

			void Warn(string text) => Console.WriteLine($"[WARN] {text}");
			void Ok(string text) => Console.WriteLine($"[OK] {text}");
			void Error(string text) => Console.WriteLine($"[ERROR] {text}");
		}
	}
}