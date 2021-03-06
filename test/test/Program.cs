﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using il2cpp;

namespace test
{
	internal class Program
	{
		private const string TestCaseDir = "../../../testcases/";
		private static int TotalTests;
		private static int PassedTests;

		private static string GetRelativePath(string path, string relativeTo)
		{
			string fullPath = Path.GetFullPath(path + '/');
			string fullRelative = Path.GetFullPath(relativeTo);
			return fullPath.Substring(fullRelative.Length);
		}

		private static MethodDef IsTestClass(TypeDef typeDef)
		{
			if (typeDef.HasCustomAttributes)
			{
				var attr = typeDef.CustomAttributes[0];
				if (attr.AttributeType.Name == "TestAttribute")
				{
					return typeDef.FindMethod("Entry");
				}
			}
			else
			{
				MethodDef mainDef = typeDef.FindMethod("Main");
				if (mainDef != null &&
					mainDef.HasBody &&
					mainDef.Body.Instructions.Count > 2)
					return mainDef;
			}
			return null;
		}

		private static byte[] ReplaceNewLines(byte[] data)
		{
			int len = data.Length;
			int writePtr = 0;
			int readPtr = 0;
			for (; readPtr < len; ++writePtr, ++readPtr)
			{
				byte curr = data[writePtr] = data[readPtr];
				if (curr == '\r')
				{
					int nextPtr = readPtr + 1;
					if (nextPtr < len)
					{
						byte next = data[nextPtr];
						if (next == '\n')
						{
							data[writePtr] = (byte)'\n';
							++readPtr;
						}
					}
					else
						break;
				}
			}

			int offset = readPtr - writePtr;
			if (offset > 0)
			{
				byte[] result = new byte[writePtr];
				Array.Copy(data, result, writePtr);
				return result;
			}
			else
				return data;
		}

		private static void TestType(
			Il2cppContext context, TypeDef typeDef,
			string imageDir, string imageName, string subDir)
		{
			MethodDef metDef = IsTestClass(typeDef);
			if (metDef == null)
				return;

			string testName = string.Format("[{0}]{1}", imageName, typeDef.FullName);
			var oldColor = Console.ForegroundColor;
			Console.Write("{0} {1}: ", subDir, testName);

			context.AddEntry(metDef);

			var sw = new Stopwatch();
			sw.Start();
			string exceptionMsg = null;
			try
			{
				context.Process();
			}
			catch (TypeLoadException ex)
			{
				exceptionMsg = ex.Message;
			}
			sw.Stop();
			long elapsedMS = sw.ElapsedMilliseconds;
			Console.Write("{0}ms, ", elapsedMS);

			StringBuilder sb = new StringBuilder();
			if (exceptionMsg != null)
				sb.Append(exceptionMsg);
			else
			{
				HierarchyDump dumper = new HierarchyDump(context);

				sb.Append("* MethodTables:\n");
				dumper.DumpMethodTables(sb);
				sb.Append("* Types:\n");
				dumper.DumpTypes(sb);
			}

			var dumpData = Encoding.UTF8.GetBytes(sb.ToString());
			File.WriteAllBytes(
				Path.Combine(imageDir, testName + ".dump"),
				dumpData);

			byte[] cmpData = null;
			try
			{
				cmpData = File.ReadAllBytes(Path.Combine(imageDir, testName + ".txt"));
				cmpData = ReplaceNewLines(cmpData);
			}
			catch
			{
			}

			if (cmpData != null && dumpData.SequenceEqual(cmpData))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("PASS");
				Console.ForegroundColor = oldColor;

				++PassedTests;
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("FAIL");
				Console.ForegroundColor = oldColor;
			}

			++TotalTests;

			context.Reset();
		}

		private static void TestAssembly(string imageDir, string imagePath)
		{
			string imageName = Path.GetFileName(imagePath);
			string subDir = GetRelativePath(imageDir, TestCaseDir);

			Il2cppContext context = new Il2cppContext(imagePath);
			foreach (TypeDef typeDef in context.Module.Types)
			{
				TestType(context, typeDef, imageDir, imageName, subDir);
			}
		}

		private static void Main(string[] args)
		{
			var files = Directory.GetFiles(TestCaseDir, "*.exe", SearchOption.AllDirectories);
			foreach (string file in files)
			{
				string dir = Path.GetDirectoryName(file);
				TestAssembly(dir, file);
			}

			Console.WriteLine("\nPassed: {0}/{1}", PassedTests, TotalTests);
		}
	}
}
