using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil.Cil;

namespace HarmonyLib;

/// <summary>A file log for debugging</summary>
public static class FileLog
{
	private static readonly object fileLock = new object();

	private static bool _logPathInited;

	private static string _logPath;

	/// <summary>The indent character. The default is <c>tab</c></summary>
	public static char indentChar = '\t';

	/// <summary>The current indent level</summary>
	public static int indentLevel = 0;

	private static List<string> buffer = new List<string>();

	/// <summary>Set this to make Harmony write its log content to this stream</summary>
	public static StreamWriter LogWriter { get; set; }

	/// <summary>Full pathname of the log file, defaults to a file called <c>harmony.log.txt</c> on your Desktop</summary>
	public static string LogPath
	{
		get
		{
			lock (fileLock)
			{
				if (!_logPathInited)
				{
					_logPathInited = true;
					string environmentVariable = Environment.GetEnvironmentVariable("HARMONY_NO_LOG");
					if (!string.IsNullOrEmpty(environmentVariable))
					{
						return null;
					}
					_logPath = Environment.GetEnvironmentVariable("HARMONY_LOG_FILE");
					if (string.IsNullOrEmpty(_logPath))
					{
						string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
						Directory.CreateDirectory(folderPath);
						_logPath = Path.Combine(folderPath, "harmony.log.txt");
					}
				}
				return _logPath;
			}
		}
	}

	private static string IndentString()
	{
		return new string(indentChar, indentLevel);
	}

	private static string CodePos(int offset)
	{
		return $"IL_{offset:X4}: ";
	}

	/// <summary>Changes the indentation level</summary>
	/// <param name="delta">The value to add to the indentation level</param>
	public static void ChangeIndent(int delta)
	{
		lock (fileLock)
		{
			indentLevel = Math.Max(0, indentLevel + delta);
		}
	}

	/// <summary>Log a string in a buffered way. Use this method only if you are sure that FlushBuffer will be called
	///  or else logging information is incomplete in case of a crash</summary>
	/// <param name="str">The string to log</param>
	public static void LogBuffered(string str)
	{
		lock (fileLock)
		{
			buffer.Add(IndentString() + str);
		}
	}

	/// <summary>Logs a list of string in a buffered way. Use this method only if you are sure that FlushBuffer will be called
	///  or else logging information is incomplete in case of a crash</summary>
	/// <param name="strings">A list of strings to log (they will not be re-indented)</param>
	public static void LogBuffered(List<string> strings)
	{
		lock (fileLock)
		{
			buffer.AddRange(strings);
		}
	}

	/// <summary>Returns the log buffer and optionally empties it</summary>
	/// <param name="clear">True to empty the buffer</param>
	/// <returns>The buffer.</returns>
	public static List<string> GetBuffer(bool clear)
	{
		lock (fileLock)
		{
			List<string> result = buffer;
			if (clear)
			{
				buffer = new List<string>();
			}
			return result;
		}
	}

	/// <summary>Replaces the buffer with new lines</summary>
	/// <param name="buffer">The lines to store</param>
	public static void SetBuffer(List<string> buffer)
	{
		lock (fileLock)
		{
			FileLog.buffer = buffer;
		}
	}

	/// <summary>Flushes the log buffer to disk (use in combination with LogBuffered)</summary>
	public static void FlushBuffer()
	{
		lock (fileLock)
		{
			if (LogWriter != null)
			{
				foreach (string item in buffer)
				{
					LogWriter.WriteLine(item);
				}
				buffer.Clear();
			}
			else
			{
				if (LogPath == null || buffer.Count <= 0)
				{
					return;
				}
				using FileStream stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
				using StreamWriter streamWriter = new StreamWriter(stream);
				foreach (string item2 in buffer)
				{
					streamWriter.WriteLine(item2);
				}
				buffer.Clear();
				return;
			}
		}
	}

	/// <summary>Logs a string directly to disk to avoid losing information in case of a crash</summary>
	/// <param name="str">The string to log.</param>
	public static void Log(string str)
	{
		lock (fileLock)
		{
			if (LogWriter != null)
			{
				LogWriter.WriteLine(IndentString() + str);
			}
			else
			{
				if (LogPath == null)
				{
					return;
				}
				using FileStream stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
				using StreamWriter streamWriter = new StreamWriter(stream);
				streamWriter.WriteLine(IndentString() + str);
				return;
			}
		}
	}

	/// <summary>Logs an inline comment at the specified code position</summary>
	/// <remarks>This method formats the comment with the code position and logs it.</remarks>
	/// <param name="codePos">The position in the code where the comment should be logged.</param>
	/// <param name="comment">The comment text to log. Cannot be null or empty.</param>
	public static void LogILComment(int codePos, string comment)
	{
		LogBuffered($"{CodePos(codePos)}// {comment}");
	}

	/// <summary>Logs the specified Intermediate Language (IL) operation code and its position in the code stream</summary>
	/// <remarks>This method formats the IL operation code and its position into a string and logs it.</remarks>
	/// <param name="codePos">The position of the IL operation code in the code stream.</param>
	/// <param name="opcode">The IL operation code to log.</param>
	public static void LogIL(int codePos, System.Reflection.Emit.OpCode opcode)
	{
		LogBuffered($"{CodePos(codePos)}{opcode}");
	}

	/// <summary>Logs information about an Intermediate Language (IL) instruction, including its position, opcode, and operand</summary>
	/// <remarks>This method formats and logs details about an IL instruction for debugging or analysis purposes. 
	/// The logged output includes the instruction's position, opcode, and operand (if any).</remarks>
	/// <param name="codePos">The position of the IL instruction within the method body.</param>
	/// <param name="opcode">The <see cref="T:System.Reflection.Emit.OpCode" /> representing the operation to be performed.</param>
	/// <param name="arg">The operand associated with the IL instruction, or <see langword="null" /> if the instruction has no operand.</param>
	public static void LogIL(int codePos, System.Reflection.Emit.OpCode opcode, object arg)
	{
		string text = Emitter.FormatOperand(arg);
		string text2 = ((text.Length > 0) ? " " : "");
		string text3 = opcode.ToString();
		if (opcode.FlowControl == System.Reflection.Emit.FlowControl.Branch || opcode.FlowControl == System.Reflection.Emit.FlowControl.Cond_Branch)
		{
			text3 += " =>";
		}
		text3 = text3.PadRight(10);
		LogBuffered($"{CodePos(codePos)}{text3}{text2}{text}");
	}

	/// <summary>Logs information about a local variable in Intermediate Language (IL) code</summary>
	/// <remarks>The logged information includes the variable's index, type, and whether it is pinned.</remarks>
	/// <param name="variable">The <see cref="T:Mono.Cecil.Cil.VariableDefinition" /> representing the local variable to log. Must not be <see langword="null" />.</param>
	internal static void LogIL(VariableDefinition variable)
	{
		LogBuffered(string.Format("{0}Local var {1}: {2}{3}", CodePos(0), variable.Index, variable.VariableType.FullName, variable.IsPinned ? "(pinned)" : ""));
	}

	/// <summary>Logs the intermediate language (IL) code at the specified position with the given label operand</summary>
	/// <remarks>Formats and logs the IL code position and label operand for detailed IL tracking or debugging.</remarks>
	/// <param name="codePos">The position in the IL code to log.</param>
	/// <param name="label">The label operand associated with the IL code to log.</param>
	public static void LogIL(int codePos, Label label)
	{
		LogBuffered(CodePos(codePos) + Emitter.FormatOperand(label));
	}

	/// <summary>Logs the beginning of an intermediate language (IL) exception handling block</summary>
	/// <remarks>Logs the start of an exception handling block (e.g., <c>.try</c>, <c>.catch</c>, <c>.finally</c>, <c>.fault</c>),
	/// adjusts indentation, and simulates a <c>LEAVE</c> opcode for consistency.</remarks>
	/// <param name="codePos">The position of the IL code where the block begins.</param>
	/// <param name="block">The <see cref="T:HarmonyLib.ExceptionBlock" /> representing the type of exception handling block to log. This includes
	/// information about the block type (e.g., try, catch, finally) and any associated metadata.</param>
	public static void LogILBlockBegin(int codePos, ExceptionBlock block)
	{
		switch (block.blockType)
		{
		case ExceptionBlockType.BeginExceptionBlock:
			LogBuffered(".try");
			LogBuffered("{");
			ChangeIndent(1);
			break;
		case ExceptionBlockType.BeginCatchBlock:
			LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
			ChangeIndent(-1);
			LogBuffered("} // end try");
			LogBuffered($".catch {block.catchType}");
			LogBuffered("{");
			ChangeIndent(1);
			break;
		case ExceptionBlockType.BeginExceptFilterBlock:
			LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
			ChangeIndent(-1);
			LogBuffered("} // end try");
			LogBuffered(".filter");
			LogBuffered("{");
			ChangeIndent(1);
			break;
		case ExceptionBlockType.BeginFaultBlock:
			LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
			ChangeIndent(-1);
			LogBuffered("} // end try");
			LogBuffered(".fault");
			LogBuffered("{");
			ChangeIndent(1);
			break;
		case ExceptionBlockType.BeginFinallyBlock:
			LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
			ChangeIndent(-1);
			LogBuffered("} // end try");
			LogBuffered(".finally");
			LogBuffered("{");
			ChangeIndent(1);
			break;
		}
	}

	/// <summary>Logs the end of an intermediate language (IL) exception block</summary>
	/// <remarks>This method handles the logging of specific types of exception blocks, such as the end of a try-catch or 
	/// similar constructs. It adjusts the indentation level and outputs relevant information about the block's conclusion.</remarks>
	/// <param name="codePos">The position in the IL code where the block ends.</param>
	/// <param name="block">The exception block to log. Must have a valid block type.</param>
	public static void LogILBlockEnd(int codePos, ExceptionBlock block)
	{
		ExceptionBlockType blockType = block.blockType;
		if (blockType == ExceptionBlockType.EndExceptionBlock)
		{
			LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
			ChangeIndent(-1);
			LogBuffered("} // end handler");
		}
	}

	/// <summary>Log a string directly to disk if Harmony.DEBUG is true. Slower method that prevents missing information in case of a crash</summary>
	/// <param name="str">The string to log.</param>
	public static void Debug(string str)
	{
		if (Harmony.DEBUG)
		{
			Log(str);
		}
	}

	/// <summary>Resets and deletes the log</summary>
	public static void Reset()
	{
		lock (fileLock)
		{
			string path = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}{Path.DirectorySeparatorChar}harmony.log.txt";
			File.Delete(path);
		}
	}

	/// <summary>Logs some bytes as hex values</summary>
	/// <param name="ptr">The pointer to some memory</param>
	/// <param name="len">The length of bytes to log</param>
	public unsafe static void LogBytes(long ptr, int len)
	{
		lock (fileLock)
		{
			byte* ptr2 = (byte*)ptr;
			string text = "";
			for (int i = 1; i <= len; i++)
			{
				if (text.Length == 0)
				{
					text = "#  ";
				}
				text += $"{*ptr2:X2} ";
				if (i > 1 || len == 1)
				{
					if (i % 8 == 0 || i == len)
					{
						Log(text);
						text = "";
					}
					else if (i % 4 == 0)
					{
						text += " ";
					}
				}
				ptr2++;
			}
			byte[] destination = new byte[len];
			Marshal.Copy((IntPtr)ptr, destination, 0, len);
			MD5 mD = MD5.Create();
			byte[] array = mD.ComputeHash(destination);
			StringBuilder stringBuilder = new StringBuilder();
			for (int j = 0; j < array.Length; j++)
			{
				stringBuilder.Append(array[j].ToString("X2"));
			}
			Log($"HASH: {stringBuilder}");
		}
	}
}
