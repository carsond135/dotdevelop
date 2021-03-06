//
// AspNetCoreExecutionHandler.cs
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

using System;
using System.Linq;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Core.Web;

namespace MonoDevelop.AspNetCore
{
	class AspNetCoreExecutionHandler : IExecutionHandler
	{
		public bool CanExecute (ExecutionCommand command) => command is AspNetCoreExecutionCommand;

		public ProcessAsyncOperation Execute (ExecutionCommand command, OperationConsole console)
		{
			var dotNetCoreCommand = (AspNetCoreExecutionCommand)command;

			// ApplicationURL is passed to ASP.NET Core server via ASPNETCORE_URLS enviorment variable
			var envVariables = dotNetCoreCommand.EnvironmentVariables.ToDictionary ((arg) => arg.Key, (arg) => arg.Value);
			if (!envVariables.ContainsKey ("ASPNETCORE_URLS"))
				envVariables ["ASPNETCORE_URLS"] = dotNetCoreCommand.ApplicationURLs;

			var process = Runtime.ProcessService.StartConsoleProcess (
				dotNetCoreCommand.Command,
				dotNetCoreCommand.Arguments,
				dotNetCoreCommand.WorkingDirectory,
				console,
				envVariables);

			if (dotNetCoreCommand.LaunchBrowser) {
				LaunchBrowserAsync (dotNetCoreCommand.ApplicationURL, dotNetCoreCommand.LaunchURL, dotNetCoreCommand.Target, process.Task).Ignore ();
			}

			return process;
		}

		public static async Task LaunchBrowserAsync (string appUrl, string launchUrl, ExecutionTarget target, Task processTask)
		{
			launchUrl = launchUrl ?? "";
			//Check if launchUrl is valid absolute url and use it if it is...
			if (!Uri.TryCreate (launchUrl, UriKind.Absolute, out var launchUri) || launchUri.IsFile) {
				//Otherwise check if appUrl is valid absolute and launchUrl is relative then concat them...
				if (!Uri.TryCreate (appUrl, UriKind.Absolute, out var appUri) || appUri.IsFile) {
					LoggingService.LogWarning ("Failed to launch browser because invalid launch and app urls.");
					return;
				}
				if (!Uri.TryCreate (launchUrl, UriKind.Relative, out launchUri)) {
					LoggingService.LogWarning ("Failed to launch browser because invalid launch url.");
					return;
				}
				launchUri = new Uri (appUri, launchUri);
			}

			//Try to connect every 50ms while process is running
			while (!processTask.IsCompleted) {
				await Task.Delay (50).ConfigureAwait (false);
				using (var httpClient = HttpClientProvider.CreateHttpClient (launchUri.AbsoluteUri)) {
					try {
						using (var response = await httpClient.GetAsync (launchUri.AbsoluteUri, System.Net.Http.HttpCompletionOption.ResponseHeadersRead)) {
							await Task.Delay (1000).ConfigureAwait (false);
							break;
						}
					} catch {
					}
				}
			}

			if (processTask.IsCompleted) {
				LoggingService.LogDebug ("Failed to launch browser because process exited before server started listening.");
				return;
			}

			// Process is still alive hence we succesfully connected inside loop to web server, launch browser
			var aspNetCoreTarget = target as AspNetCoreExecutionTarget;
			if (aspNetCoreTarget != null && !aspNetCoreTarget.DesktopApplication.IsDefault) {
				aspNetCoreTarget.DesktopApplication.Launch (launchUri.AbsoluteUri);
			} else {
				IdeServices.DesktopService.ShowUrl (launchUri.AbsoluteUri);
			}
		}
	}
}