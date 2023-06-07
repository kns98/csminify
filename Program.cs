// foo
#define PROGRAM_CS
using System;
using System.IO;
namespace csminify
{
	class Program
	{
		static void _PrintUsage()
		{
			string exe = Path.GetFileNameWithoutExtension(typeof(Program).Assembly.GetModules()[0].Name);
			Console.Error.Write("Usage: ");
			Console.Error.Write(exe);
			Console.Error.WriteLine(" <filename1> [{<filenameN>}]");
		}
		/// <summary>
		/// The entry point
		/// </summary>
		/// <param name="args">The arguments</param>
		static void Main(string[] args)
		{
			if(0==args.Length)
			{
				_PrintUsage();
				return;
			}
			Minifier.MergeMinify(Console.Out,100,args);
			Console.Error.WriteLine();
		}
	}
}
