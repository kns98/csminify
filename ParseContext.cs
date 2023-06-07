namespace Grimoire
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	#region ExpectingException
	/// <summary>
	/// An exception encountered during parsing where the stream contains an unexpected character
	/// </summary>
	[Serializable]
	internal sealed class ExpectingException : Exception
	{
		/// <summary>
		/// Initialize the exception with the specified message.
		/// </summary>
		/// <param name="message">The message</param>
		public ExpectingException(string message) : base(message) { }
		/// <summary>
		/// The list of expected strings.
		/// </summary>
		public string[] Expecting { get; internal set; }
		/// <summary>
		/// The position when the error was realized.
		/// </summary>
		public long Position { get; internal set; }
		/// <summary>
		/// The line of the error
		/// </summary>
		public int Line { get; internal set; }
		/// <summary>
		/// The column of the error
		/// </summary>
		public int Column { get; internal set; }
		
	}
	#endregion ExpectingException

	#region ParseContext
	internal abstract partial class ParseContext : IEnumerator<char>, IDisposable
	{
		const string _HexDigits = "0123456789ABCDEF";
		static byte _FromHexChar(char hex)
		{
			if (':' > hex && '/' < hex)
				return (byte)(hex - '0');
			if ('G' > hex && '@' < hex)
				return (byte)(hex - '7'); // 'A'-10
			if ('g' > hex && '`' < hex)
				return (byte)(hex - 'W'); // 'a'-10
			throw new ArgumentException("The value was not hex.", "hex");
		}
		static bool _IsHexChar(char hex)
		{
			return (
				(':' > hex && '/' < hex) ||
				('G' > hex && '@' < hex) ||
				('g' > hex && '`' < hex)
			);
		}
		public bool TryReadWhiteSpace()
		{
			EnsureStarted();
			if (-1 == Current || !char.IsWhiteSpace((char)Current))
				return false;
			CaptureCurrent();
			while (-1 != Advance() && char.IsWhiteSpace((char)Current))
				CaptureCurrent();
			return true;
		}
		public bool TrySkipWhiteSpace()
		{
			EnsureStarted();
			if (-1 == Current || !char.IsWhiteSpace((char)Current))
				return false;
			while (-1 != Advance() && char.IsWhiteSpace((char)Current)) ;
			return true;
		}
		public bool TryReadUntil(int character, bool readCharacter = true)
		{
			EnsureStarted();
			if (0 > character) character = -1;
			CaptureCurrent();
			if (Current == character)
			{
				return true;
			}
			while (-1 != Advance() && Current != character)
				CaptureCurrent();
			CaptureCurrent();
			if (Current == character)
			{
				if (readCharacter)
					Advance();
				return true;
			}
			return false;
		}
		public bool TrySkipUntil(int character, bool skipCharacter = true)
		{
			EnsureStarted();
			if (0 > character) character = -1;
			if (Current == character)
				return true;
			while (-1 != Advance() && Current != character) ;
			if (Current == character)
			{
				if (skipCharacter)
					Advance();
				return true;
			}
			return false;
		}
		public bool TryReadUntil(int character, int escapeChar, bool readCharacter = true)
		{
			EnsureStarted();
			if (0 > character) character = -1;
			CaptureCurrent();
			if (Current == character)
			{
				return true;
			}
			while (-1 != Advance() && Current != character)
			{
				if (character == escapeChar)
				{
					CaptureCurrent();
					if (-1 == Advance())
						break;
				}
				CaptureCurrent();
			}
			CaptureCurrent();
			if (Current == character)
			{
				if (readCharacter)
					Advance();
				return true;
			}
			return false;
		}
		public bool TrySkipUntil(int character, int escapeChar, bool skipCharacter = true)
		{
			EnsureStarted();
			if (0 > character) character = -1;
			if (Current == character)
				return true;
			while (-1 != Advance() && Current != character)
			{
				if (character == escapeChar)
					if (-1 == Advance())
						break;
			}
			if (Current == character)
			{
				if (skipCharacter)
					Advance();
				return true;
			}
			return false;
		}
		private static bool _ContainsChar(char[] chars, char ch)
		{
			foreach (char cmp in chars)
				if (cmp == ch)
					return true;
			return false;
		}
		public bool TryReadLiteral(string literal,bool checkTerminated=true)
		{
			foreach(char ch in literal)
			{
				if(Current==ch)
				{
					CaptureCurrent();
					if (-1 == Advance())
						break;
				}
			}
			if (checkTerminated)
				return -1 == Current || !char.IsLetterOrDigit((char)Current);
			return true;
		}
		public bool TrySkipLiteral(string literal, bool checkTerminated = true)
		{
			foreach (char ch in literal)
			{
				if (Current == ch)
				{
					if (-1 == Advance())
						break;
				}
			}
			if (checkTerminated)
				return -1 == Current || !char.IsLetterOrDigit((char)Current);
			return true;
		}
		public bool TryReadUntil(bool readCharacter = true, params char[] anyOf)
		{
			EnsureStarted();
			if (null == anyOf)
				anyOf = Array.Empty<char>();
			CaptureCurrent();
			if (-1 != Current && _ContainsChar(anyOf, (char)Current))
			{
				if (readCharacter)
				{
					CaptureCurrent();
					Advance();
				}
				return true;
			}
			while (-1 != Advance() && !_ContainsChar(anyOf, (char)Current))
				CaptureCurrent();
			if (-1 != Current && _ContainsChar(anyOf, (char)Current))
			{
				if (readCharacter)
				{
					CaptureCurrent();
					Advance();
				}
				return true;
			}
			return false;
		}
		public bool TrySkipUntil(bool skipCharacter = true, params char[] anyOf)
		{
			EnsureStarted();
			if (null == anyOf)
				anyOf = Array.Empty<char>();
			if (-1 != Current && _ContainsChar(anyOf, (char)Current))
			{
				if (skipCharacter)
					Advance();
				return true;
			}
			while (-1 != Advance() && !_ContainsChar(anyOf, (char)Current)) ;
			if (-1 != Current && _ContainsChar(anyOf, (char)Current))
			{
				if (skipCharacter)
					Advance();
				return true;
			}
			return false;
		}
		public bool TryReadUntil(string text)
		{
			EnsureStarted();
			if (string.IsNullOrEmpty(text))
				return false;
			while (-1 != Current && TryReadUntil(text[0], false))
			{
				bool found = true;
				for (int i = 1; i < text.Length; ++i)
				{
					if (Advance() != text[i])
					{
						found = false;
						break;
					}
					CaptureCurrent();
				}
				if (found)
				{
					Advance();
					return true;
				}
			}

			return false;
		}
		public bool TrySkipUntil(string text)
		{
			EnsureStarted();
			if (string.IsNullOrEmpty(text))
				return false;
			while (-1 != Current && TrySkipUntil(text[0], false))
			{
				bool found = true;
				for (int i = 1; i < text.Length; ++i)
				{
					if (Advance() != text[i])
					{
						found = false;
						break;
					}
				}
				if (found)
				{
					Advance();
					return true;
				}
			}
			return false;
		}
		
		public bool TryReadCString()
		{
			EnsureStarted();
			if ('\"' != Current)
				return false;
			CaptureCurrent();
			while (-1 != Advance() && '\r' != Current && '\n' != Current && '\"' != Current)
			{
				CaptureCurrent();
				if ('\\' == Current)
				{
					if (-1 == Advance() || '\r' == Current || '\n' == Current)
						return false;
					CaptureCurrent();

				}
			}
			if ('\"' == Current)
			{
				CaptureCurrent();
				Advance(); // move past the string
				return true;
			}
			return false;
		}
		
		public bool TrySkipCString()
		{
			EnsureStarted();
			if ('\"' != Current)
				return false;
			while (-1 != Advance() && '\r' != Current && '\n' != Current && '\"' != Current)
				if ('\\' == Current)
					if (-1 == Advance() || '\r' == Current || '\n' == Current)
						return false;

			if ('\"' == Current)
			{
				Advance(); // move past the string
				return true;
			}
			return false;
		}
		
		public bool TryReadCLineComment()
		{
			EnsureStarted();
			if ('/' != Current)
				return false;
			CaptureCurrent();
			if ('/' != Advance())
				return false;
			CaptureCurrent();
			while (-1 != Advance() && '\r' != Current && '\n' != Current)
				CaptureCurrent();
			return true;
		}
		public bool TrySkipCLineComment()
		{
			EnsureStarted();
			if ('/' != Current)
				return false;
			if ('/' != Advance())
				return false;
			while (-1 != Advance() && '\r' != Current && '\n' != Current) ;
			return true;
		}
		public bool TryReadCBlockComment()
		{
			EnsureStarted();
			if ('/' != Current)
				return false;
			CaptureCurrent();
			if ('*' != Advance())
				return false;
			CaptureCurrent();
			if (-1 == Advance())
				return false;
			return TryReadUntil("*/");
		}
		public bool TrySkipCBlockComment()
		{
			EnsureStarted();
			if ('/' != Current)
				return false;
			if ('*' != Advance())
				return false;
			if (-1 == Advance())
				return false;
			return TrySkipUntil("*/");
		}
		public bool TryReadCComment()
		{
			EnsureStarted();
			if ('/' != Current)
				return false;
			CaptureCurrent();
			if ('*' == Advance())
			{
				CaptureCurrent();
				if (-1 == Advance())
					return false;
				return TryReadUntil("*/");
			}
			if ('/' == Current)
			{
				CaptureCurrent();
				while (-1 != Advance() && '\r' != Current && '\n' != Current)
					CaptureCurrent();
				return true;
			}
			return false;
		}
		public bool TrySkipCComment()
		{
			EnsureStarted();
			if ('/' != Current)
				return false;
			if ('*' == Advance())
			{
				if (-1 == Advance())
					return false;
				return TrySkipUntil("*/");
			}
			if ('/' == Current)
			{
				while (-1 != Advance() && '\r' != Current && '\n' != Current) ;
				return true;
			}
			return false;
		}
		public bool TryReadCCommentsAndWhitespace()
		{
			bool result = false;
			while (-1 != Current)
			{
				if (!TryReadWhiteSpace() && !TryReadCComment())
					break;
				result = true;
			}
			if (TryReadWhiteSpace())
				result = true;
			return result;
		}
		/// <summary>
		/// Skips a C style comment or whitespace
		/// </summary>
		/// <returns>True if successful, otherwise false.</returns>
		public bool TrySkipCCommentsAndWhitespace()
		{
			bool result = false;
			while (-1 != Current)
			{
				if (!TrySkipWhiteSpace() && !TrySkipCComment())
					break;
				result = true;
			}
			if (TrySkipWhiteSpace())
				result = true;
			return result;
		}
		public bool TryReadIdentifier()
		{
			EnsureStarted();
			if (-1 == Current || !('_' == Current || char.IsLetter((char)Current)))
				return false;
			CaptureCurrent();
			while (-1 != Advance() && ('_' == Current || char.IsLetterOrDigit((char)Current)))
				CaptureCurrent();
			return true;
		}
		public bool TrySkipIdentifier()
		{
			EnsureStarted();
			if (-1 == Current || !('_' == Current || char.IsLetter((char)Current)))
				return false;
			while (-1 != Advance() && ('_' == Current || char.IsLetterOrDigit((char)Current))) ;
			return true;
		}
		string _GetExpectingMessage(int[] expecting)
		{
			StringBuilder sb = null;
			switch (expecting.Length)
			{
				case 0:
					break;
				case 1:
					sb = new StringBuilder();
					if (-1 == expecting[0]) // shouldn't really run this condition but handle it anyway
						sb.Append("end of input");
					else
					{
						sb.Append("\"");
						sb.Append((char)expecting[0]);
						sb.Append("\"");
					}
					break;
				case 2:
					sb = new StringBuilder();
					if (-1 == expecting[0])
						sb.Append("end of input");
					else
					{
						sb.Append("\"");
						sb.Append((char)expecting[0]);
						sb.Append("\"");
					}
					sb.Append(" or ");
					if (-1 == expecting[1])
						sb.Append("end of input");
					else
					{
						sb.Append("\"");
						sb.Append((char)expecting[1]);
						sb.Append("\"");
					}
					break;
				default: // length > 2
					sb = new StringBuilder();
					if (-1 == expecting[0])
						sb.Append("end of input");
					else
					{
						sb.Append("\"");
						sb.Append((char)expecting[0]);
						sb.Append("\"");
					}
					int l = expecting.Length - 1;
					int i = 1;
					for (; i < l; ++i)
					{
						sb.Append(", ");
						if (-1 == expecting[i])
							sb.Append("end of input");
						else
						{
							sb.Append("\"");
							sb.Append((char)expecting[i]);
							sb.Append("\"");
						}
					}
					sb.Append(", or ");
					if (-1 == expecting[i])
						sb.Append("end of input");
					else
					{
						sb.Append("\"");
						sb.Append((char)expecting[i]);
						sb.Append("\"");
					}
					break;
			}
			string at = string.Concat(" at line ", Line, ", column ", Column, ", position ", Position);
			if (-1 == Current)
			{
				if (0 == expecting.Length)
					return string.Concat("Unexpected end of input", at, ".");
				return string.Concat("Unexpected end of input. Expecting ", sb.ToString(), at, ".");
			}
			if (0 == expecting.Length)
				return string.Concat("Unexpected character \"", (char)Current, "\" in input", at, ".");
			return string.Concat("Unexpected character \"", (char)Current, "\" in input. Expecting ", sb.ToString(), at, ".");

		}
		/// <summary>
		/// Checks that the next character is one of the input characters, otherwise throws an expecting-exception.
		/// </summary>
		/// <param name="expecting">An array of inputs, or -1 for end of stream. If this is empty, anything except the end of the stream will be accepted.</param>
		public void Expecting(params int[] expecting)
		{
			switch (expecting.Length)
			{
				case 0:
					if (-1 == Current)
						throw new ExpectingException(_GetExpectingMessage(expecting));
					return;
				case 1:
					if (expecting[0] != Current)
						throw new ExpectingException(_GetExpectingMessage(expecting));
					return;
				default:
					if (0 > Array.IndexOf<int>(expecting, Current))
						throw new ExpectingException(_GetExpectingMessage(expecting));
					return;
			}
		}
		StringBuilder _captureBuffer;
		/// <summary>
		/// Reports the line the parser is on
		/// </summary>
		/// <remarks>The line starts at one.</remarks>
		public int Line { get; protected set; }
		/// <summary>
		/// Reports the column the parser is on
		/// </summary>
		/// <remarks>The column starts at one.</remarks>
		public int Column { get; protected set; }
		/// <summary>
		/// Reports the position the parser is on
		/// </summary>
		/// <remarks>The position starts at zero.</remarks>
		public long Position { get; protected set; }
		/// <summary>
		/// Reports the current character, or -1 if past end of stream
		/// </summary>
		public int Current { get; protected set; }
		protected ParseContext()
		{
			_captureBuffer = new StringBuilder();
			Position = 0;
			Line = 1;
			Column = 1;
			Current = -2;
		}
		/// <summary>
		/// Advances the cursor if it is before the start of the input stream
		/// </summary>
		public void EnsureStarted()
		{
			if (-2 == Current)
				Advance();
		}
		/// <summary>
		/// Advances the cursor and returns the next character
		/// </summary>
		/// <returns>The next character, or -1 if after the end of the stream, or -2 if it is before the start of the stream.</returns>
		public abstract int Advance();
		/// <summary>
		/// Closes the input stream and releases any associated resources.
		/// </summary>
		public abstract void Close();
		void IDisposable.Dispose()
		{
			Close();
		}
		/// <summary>
		/// The buffer used to capture the input
		/// </summary>
		public StringBuilder CaptureBuffer {
			get { return _captureBuffer; }
		}
		/// <summary>
		/// Reports the Capture buffer as a string
		/// </summary>
		public string Capture
		{
			get { return _captureBuffer.ToString(); }
		}

		char IEnumerator<char>.Current { get { if (0 > Current) throw new InvalidOperationException(); return (char)Current; } }
		object IEnumerator.Current { get { return ((IEnumerator<char>)this).Current; } }

		/// <summary>
		/// Gets a string for part of the capture buffer
		/// </summary>
		/// <param name="startIndex">the start index</param>
		/// <param name="count">the count of characters to return</param>
		/// <returns>The specified substring</returns>
		public string GetCapture(int startIndex,int count)
		{
			return _captureBuffer.ToString(startIndex,count);
		}
		/// <summary>
		/// Gets a string for part of the capture buffer
		/// </summary>
		/// <param name="startIndex">the start index</param>
		/// <returns>The specified substring</returns>
		public string GetCapture(int startIndex)
		{
			return _captureBuffer.ToString(startIndex, _captureBuffer.Length-startIndex);
		}
		/// <summary>
		/// If the cursor is over an input character then add that character to the capture buffer
		/// </summary>
		public void CaptureCurrent()
		{
			if (-1 < Current)
			{
				CaptureBuffer.Append((char)Current);
			}
		}
		/// <summary>
		/// Clear the capture buffer
		/// </summary>
		public void ClearCapture()
		{
			CaptureBuffer.Clear();
		}
		/// <summary>
		/// Create a parse context over the specified input stream
		/// </summary>
		/// <param name="input">The input stream</param>
		/// <returns>A parse context over the input stream</returns>
		public static ParseContext Create(IEnumerable<char> input)
		{
			return new CharEnumeratorParseContext(input.GetEnumerator());
		}
		/// <summary>
		/// Create a parse context over the specified input stream
		/// </summary>
		/// <param name="input">The input stream</param>
		/// <returns>A parse context over the input stream</returns>
		public static ParseContext Create(TextReader input)
		{
			return new TextReaderParseContext(input);
		}

		bool IEnumerator.MoveNext()
		{
			return -1 < Advance();
		}

		void IEnumerator.Reset()
		{
			throw new NotImplementedException();
		}
		#region CharEnumeratorParseContext
		internal partial class CharEnumeratorParseContext : ParseContext
		{
			IEnumerator<char> _enumerator;
			internal CharEnumeratorParseContext(IEnumerator<char> enumerator)
			{
				_enumerator = enumerator;
			}
			public override int Advance()
			{
				if (_enumerator.MoveNext())
				{
					Current = _enumerator.Current;
					++Position;
					++Column;
					switch (Current)
					{
						case '\r':
							Column = 1;
							break;
						case '\n':
							Column = 1; ++Line;
							break;
					}
				}
				else
				{
					if (-1 != Current)
					{ // last read moves us past the end. subsequent reads don't move anything
						++Position;
						++Column;
					}
					Current = -1;
				}
				return Current;
			}
			public override void Close()
			{
				if (null != _enumerator)
					_enumerator.Dispose();
				_enumerator = null;
			}
		}
		#endregion CharEnumeratorParseContext
		#region TextReaderParseContext
		internal partial class TextReaderParseContext : ParseContext
		{
			TextReader _reader;
			internal TextReaderParseContext(TextReader reader)
			{
				_reader = reader;
			}
			public override int Advance()
			{
				int och = Current;
				if (-1 != (Current = _reader.Read()))
				{
					++Position;
					++Column;
					switch (Current)
					{
						case '\r':
							Column = 1;
							break;
						case '\n':
							Column = 1; ++Line;
							break;
					}
				}
				else
				{
					if (-1 != och) // last read moves us past the end. subsequent reads don't move anything
					{
						++Column;
						++Position;
					}
				}
				return Current;
			}
			public override void Close()
			{
				if (null != _reader)
					_reader.Dispose();
				_reader = null;
			}
		}
		#endregion TextReaderParseContext
	}
	#endregion ParseContext
}
