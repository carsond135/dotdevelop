//
// DotNetCoreExecutionCommand.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using MonoDevelop.Core.Execution;

namespace MonoDevelop.DotNetCore
{
	public class DotNetCoreBaseExecutionCommand : ProcessExecutionCommand
	{
		public DotNetCoreBaseExecutionCommand (string directory, string outputPath, string arguments)
		{
			WorkingDirectory = directory;
			OutputPath = outputPath;
			DotNetArguments = arguments;

			Command = DotNetCoreRuntime.FileName;
			Arguments = string.Format ("\"{0}\" {1}", outputPath, arguments);
		}

		public string OutputPath { get; private set; }
		public string DotNetArguments { get; private set; }

		public bool PauseConsoleOutput { get; set; }
		public bool ExternalConsole { get; set; }
		public bool LaunchBrowser { get; set; }
		public string LaunchURL { get; set; }
		public string ApplicationURL { get; set; }
		public PipeTransportSettings PipeTransport { get; set; }
	}

	public class DotNetCoreExecutionCommand : DotNetCoreBaseExecutionCommand
	{
		public DotNetCoreExecutionCommand (string directory, string outputPath, string arguments)
			: base (directory, outputPath, arguments)
		{
		}
	}
}