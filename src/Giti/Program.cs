using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using DryIoc;
using ImTools;
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

		[Option('t', "sourceType", Default = SourceTypes.GitHeadFriendlyName, HelpText = "The source for extraction.")]
		public SourceTypes SourceType { get; set; }

		[Option('p', "pattern", Required = true, HelpText = "The regex used for extraction from the source.")]
		public string Pattern { get; set; }

		[Option('r', "template", Required = true,
			HelpText = "The template used for constructing the commit message. Uses scriban templating engine.")]
		public string Template { get; set; }
	}

	class Program
	{
		static async Task Main(string[] args)
		{
			args = new[]
			{
				"test-repo",
				"-t",
				"GitHeadFriendlyName",
				"-p",
				@"[a-zA-Z]+-\d+",
				"-r",
				"{{ message }}"
			};
			var result = await new Parser(config => config.HelpWriter = null)
				.ParseArguments<Options>(args)
				.WithParsedAsync(async options => await (await App.New(options)).Run());
			result.WithNotParsed(errors => Console.WriteLine(
				HelpText.AutoBuild(result, help =>
				{
					help.AddEnumValuesToHelpText = true;
					return help;
				})));

			Console.ReadKey();
		}
	}

	public class App
	{
		public static async Task<App> New(Options options)
		{
			var container = new Container();

			container.RegisterInstance(options);
			container.RegisterDelegate(() => new Repository(Directory.GetCurrentDirectory()), reuse: Reuse.Singleton);

			new[]
				{
					typeof(GitHeadCanonicalNameSource), typeof(GitHeadFriendlyNameSource),
					typeof(RegexExtractor), 
					typeof(ScribanReplacer)
				}
				.ForEach(t => container.Register(t));

			container.Register<App>();

			return container.Resolve<App>();
		}

		private readonly ISource _source;
		private readonly IExtractor _extractor;
		private readonly IReplacer _replacer;
		private readonly Repository _repo;

		public App(ISource source, IExtractor extractor, IReplacer replacer, Lazy<Repository> repo)
		{
			_source = source;
			_extractor = extractor;
			_replacer = replacer;
			_repo = repo.Value;
		}

		

		public async Task Run()
		{
			var match = await _extractor.Extract(await _source.Get());
			_replacer.Replace(new
			{
				match,
			});

		}
	}

	public class GitHeadFriendlyNameSource : ISource
	{
		private readonly Repository _repo;

		public GitHeadFriendlyNameSource(Lazy<Repository> repo)
		{
			_repo = repo.Value;
		}

		public async Task<string> Get()
		{
			return _repo.Head.FriendlyName;
		}
	}

	public class GitHeadCanonicalNameSource : ISource
	{
		private readonly Repository _repo;

		public GitHeadCanonicalNameSource(Lazy<Repository> repo)
		{
			_repo = repo.Value;
		}

		public async Task<string> Get()
		{
			return _repo.Head.CanonicalName;
		}
	}

	public interface ISource
	{
		Task<string> Get();
	}

	public interface IExtractor
	{
		Task<string> Extract(string source);
	}

	public class RegexExtractor : IExtractor
	{
		private Regex _regex;

		public RegexExtractor(string pattern)
		{
			_regex = new Regex(pattern, RegexOptions.Compiled);
		}

		public async Task<string> Extract(string source)
		{
			return _regex.Match(source).Value;
		}
	}

	public interface IReplacer
	{
		Task<string> Replace(object model);
	}

	public class ScribanReplacer : IReplacer
	{
		private readonly string _template;

		public ScribanReplacer(string template)
		{
			_template = template;
		}

		public async Task<string> Replace(object model)
		{
			var parsed = Template.Parse(_template);
			return await parsed.RenderAsync(model);
		}
	}
}