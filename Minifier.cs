using Grimoire;
using System.Collections.Generic;
using System.IO;
namespace csminify
{
	class Minifier
	{
		public static void MergeMinify(TextWriter writer, int lineWidth = 0, params string[] sourcePaths)
		{
			MergeMinifyPreamble(writer, sourcePaths);
			MergeMinifyBody(writer, lineWidth, sourcePaths);
		}
		public static void MergeMinifyPreamble(TextWriter writer, params string[] sourcePaths)
		{
			var usings = new HashSet<string>();
			var defines = new HashSet<string>();
			foreach (string fn in sourcePaths)
			{
				using (var pc = ParseContext.Create(new StreamReader(File.Open(fn, FileMode.Open, FileAccess.Read, FileShare.Read))))
				{
					pc.EnsureStarted();
					while (-1 != pc.Current)
					{
						if (!pc.TrySkipWhiteSpace() && !pc.TrySkipCComment())
							break;
					}
					pc.TrySkipWhiteSpace();
					// gather defines
					while ('#'==pc.Current)
					{
						if (-1 == pc.Advance())
							break;
						pc.ClearCapture();
						if (!pc.TryReadIdentifier())
							break;
						if ("define" != pc.Capture.ToString())
							break;
						if (!pc.TrySkipCCommentsAndWhitespace())
							break;
						pc.ClearCapture();
						if (!pc.TryReadIdentifier())
							break;
						defines.Add(pc.Capture.ToString());
						pc.ClearCapture();
						if (!pc.TrySkipCCommentsAndWhitespace())
							break;
					}
					pc.ClearCapture();
					// gather usings
					while('u'==pc.Current)
					{
						pc.TryReadIdentifier();
						if ("using" != pc.Capture.ToString())
							break;
						if (!pc.TrySkipCCommentsAndWhitespace())
							break;
						pc.ClearCapture();
						while (pc.TryReadIdentifier())
						{
							pc.TrySkipCCommentsAndWhitespace();
							if ('.' != pc.Current)
								break;
							pc.CaptureCurrent();
							if (-1 == pc.Advance())
								break;
							pc.TrySkipCCommentsAndWhitespace();
						}
						if (';' != pc.Current)
							break;
						usings.Add(pc.Capture.ToString());
						pc.ClearCapture();
						pc.Advance();
						pc.TrySkipCCommentsAndWhitespace();
					}
				}
			}
			foreach(string def in defines)
			{
				writer.Write("#define ");
				writer.WriteLine(def);
			}
			foreach (string use in usings)
			{
				writer.Write("using ");
				writer.Write(use);
				writer.WriteLine(";");
			}
		}
		
		public static void MergeMinifyBody(TextWriter writer, int lineWidth=0, params string[] sourcePaths)
		{
			int ocol = 0;
			foreach (string fn in sourcePaths)
			{
				using (var pc = ParseContext.Create(new StreamReader(File.Open(fn, FileMode.Open, FileAccess.Read, FileShare.Read))))
				{
					pc.EnsureStarted();
					while (-1 != pc.Current)
					{
						if (!pc.TrySkipWhiteSpace() && !pc.TrySkipCComment())
							break;
					}
					pc.TrySkipWhiteSpace();
					// gather defines
					while ('#' == pc.Current)
					{
						if (-1 == pc.Advance())
							break;
						pc.ClearCapture();
						if (!pc.TryReadIdentifier())
							break;
						if ("define" != pc.Capture.ToString())
							break;
						if (!pc.TrySkipCCommentsAndWhitespace())
							break;
						pc.ClearCapture();
						if (!pc.TryReadIdentifier())
							break;
						pc.ClearCapture();
						if (!pc.TrySkipCCommentsAndWhitespace())
							break;
					}
					pc.ClearCapture();
					// gather usings
					while ('u' == pc.Current)
					{
						pc.TryReadIdentifier();
						if ("using" != pc.Capture.ToString())
							break;
						if (!pc.TrySkipCCommentsAndWhitespace())
							break;
						pc.ClearCapture();
						while (pc.TryReadCSharpIdentifier())
						{
							pc.TrySkipCCommentsAndWhitespace();
							if ('.' != pc.Current)
								break;
							pc.CaptureCurrent();
							if (-1 == pc.Advance())
								break;
							pc.TrySkipCCommentsAndWhitespace();
						}
						if (';' != pc.Current)
							break;
						pc.ClearCapture();
						pc.Advance();
						pc.TrySkipCCommentsAndWhitespace();
					}
					bool isIdentOrNum = false;
					// done skipping preamble
					while (-1 != pc.Current)
					{
						pc.TrySkipWhiteSpace();
						pc.ClearCapture();
						switch (pc.Current)
						{
							case '#':
								isIdentOrNum = false;
								pc.TryReadUntil(false, '\r', '\n');
								if (0 != ocol)
									writer.WriteLine();
								writer.WriteLine(pc.Capture.ToString());
								ocol = 0;
								break;
							case '/':
								isIdentOrNum = false;
								if (!pc.TryReadCComment())
								{
									writer.Write(pc.Capture.ToString());
									ocol += pc.Capture.Length;

									break;
								}
								if (pc.Capture.ToString().StartsWith("///"))
								{ // doc comment
									writer.WriteLine(pc.Capture.ToString());
									ocol = 0;
								}
								break;
							case '@':
								if (!pc.TryReadCSharpString())
								{
									// if it's not a verbatim string then read an identifer (use the generic one to skip keyword checks)
									pc.TryReadIdentifier();
								}
								isIdentOrNum = false;
								writer.Write(pc.Capture.ToString());
								ocol += pc.Capture.Length;
								break;
							case '$':
								isIdentOrNum = false;
								pc.CaptureCurrent();
								if ('\"' == pc.Advance())
									pc.TryReadCString();
								writer.Write(pc.Capture.ToString());
								ocol += pc.Capture.Length;
								break;
							case '\"':
								isIdentOrNum = false;
								pc.TryReadCSharpString();
								writer.Write(pc.Capture.ToString());
								ocol += pc.Capture.Length;
								break;
							case '\'':
								isIdentOrNum = false;
								pc.TryReadCSharpChar();
								writer.Write(pc.Capture.ToString());
								ocol += pc.Capture.Length;
								break;
							case -1:
								isIdentOrNum = false;
								break;
							default:
								pc.ClearCapture();
								if (pc.TryReadIdentifier())
								{
									if (isIdentOrNum)
									{
										writer.Write(' ');
										++ocol;
									}
									isIdentOrNum = true;
									writer.Write(pc.Capture.ToString());
									ocol += pc.Capture.Length;
								}
								else {
									if (0 > pc.Capture.Length && isIdentOrNum && char.IsDigit(pc.Capture[0]))
									{
										writer.Write(' ');
										++ocol;
									}

									writer.Write(pc.Capture.ToString());
									ocol += pc.Capture.Length;
									if (isIdentOrNum && char.IsDigit((char)pc.Current))
									{
										++ocol;
										writer.Write(' ');
									}
									isIdentOrNum = false;
									writer.Write((char)pc.Current);
									++ocol;
									pc.Advance();
								}
								break;
							
						}
						if(-1!=pc.Current && char.IsWhiteSpace((char)pc.Current) && lineWidth>0 && ocol>=lineWidth)
						{
							writer.WriteLine();
							ocol = 0;
						}
					}
				}
			}
		}
	}
}
