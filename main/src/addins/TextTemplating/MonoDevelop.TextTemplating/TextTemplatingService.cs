// 
// TextTemplatingService.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2009 Novell, Inc. (http://www.novell.com)
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

using Mono.TextTemplating;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Tasks;
using System.CodeDom.Compiler;

namespace MonoDevelop.TextTemplating
{
	public static class TextTemplatingService
	{
		public static void ShowTemplateHostErrors (CompilerErrorCollection errors)
		{
			if (errors.Count == 0)
				return;
			
			IdeServices.TaskService.Errors.Clear ();
			foreach (CompilerError err in errors) {
					IdeServices.TaskService.Errors.Add (new TaskListEntry (err.FileName, err.ErrorText, err.Column, err.Line,
					                                  err.IsWarning? TaskSeverity.Warning : TaskSeverity.Error));
			}
			IdeServices.TaskService.ShowErrors ();
		}
		
		static TemplatingAppDomainRecycler recycler;
		
		public static TemplatingAppDomainRecycler.Handle GetTemplatingDomain ()
		{
			if (recycler == null) {
				recycler = new TemplatingAppDomainRecycler ("T4Domain");
			}
			var handle = recycler.GetHandle ();
			handle.AddAssembly (typeof (TextTemplatingService).Assembly);
			return handle;
		}
	}
}