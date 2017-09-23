using System;
using System.IO;
using System.Reflection;

namespace SDDL
{
	class Program
	{
		static int Main(string[] args)
		{
			Options options = new Options();
			if (!options.Parse(args))
				return -1;
			if (options.Files.Count == 0)
			{
				Console.WriteLine(options.GetUsage());
				return -1;
			}
			if (options.Namespace == null)
				options.Namespace = Path.GetFileNameWithoutExtension(options.Output);
			Compiler compiler = new Compiler();
			foreach (string file in options.Files)
			{
				compiler.AddInput(file);
			}
			string errmsg = null;
			Target target = null;
			StreamWriter output;
			try
			{
				Assembly assembly = Assembly.LoadFrom(options.Target + ".dll");
				foreach (var type in assembly.GetExportedTypes())
				{
					if (typeof(Target).IsAssignableFrom(type))
					{
						try
						{
							target = (Target)Activator.CreateInstance(type, options.Namespace);
							break;
						}
						catch
						{
						}
					}
				}
				output = new StreamWriter(new FileStream(options.Output, FileMode.Create, FileAccess.Write, FileShare.None),
										target.Encoding) {NewLine = target.NewLine};
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.Message);
				return -1;
			}
			using (output)
			{
				if (!compiler.Output(target, output, ref errmsg))
				{
					Console.Error.WriteLine(errmsg);
					return -1;
				}
			}
			return 0;
		}
	}
}
