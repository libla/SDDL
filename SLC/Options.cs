using System;
using System.Reflection;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace SDDL
{
	internal class Options
	{
		[ValueList(typeof(List<string>))]
		public IList<string> Files { get; set; }

		[Option('o', "output", Required = true, HelpText = "Output file name.")]
		public string Output { get; set; }

		[Option('t', "target", Required = true, HelpText = "Output target.")]
		public string Target { get; set; }

		[Option('n', "namespace", HelpText = "Output namespace.")]
		public string Namespace { get; set; }

		[HelpOption]
		public string GetUsage()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			AssemblyProductAttribute product = (AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute));
			HelpText help = new HelpText(string.Format("{0} v{1}", product.Product, assembly.GetName().Version))
			{
				AddDashesToOption = true,
			};
			help.AddPreOptionsLine(string.Format("{0} [options...] <input files...>", product.Product));
			help.AddOptions(this);
			return help.ToString();
		}

		public bool Parse(string[] args)
		{
			Parser parser = new Parser(settings =>
			{
				settings.CaseSensitive = false;
				settings.IgnoreUnknownArguments = false;
				settings.HelpWriter = Console.Out;
			});
			return parser.ParseArguments(args, this);
		}
	}
}
