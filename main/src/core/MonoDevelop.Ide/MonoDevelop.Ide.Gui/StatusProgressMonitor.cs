//
// StatusProgressMonitor.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System.Collections.Generic;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Ide.ProgressMonitoring;
using MonoDevelop.Core;

namespace MonoDevelop.Ide.Gui
{
	internal class StatusProgressMonitor: ProgressMonitor
	{
		string icon;
		bool showErrorDialogs;
		bool showTaskTitles;
		bool lockGui;
		bool showCancelButton;
		string title;
		StatusBarContext statusBar;
		Pad statusSourcePad;
		
		public StatusProgressMonitor (string title, string iconName, bool showErrorDialogs, bool showTaskTitles, bool lockGui, Pad statusSourcePad, bool showCancelButton): base (Runtime.MainSynchronizationContext)
		{

			this.lockGui = lockGui;
			this.showErrorDialogs = showErrorDialogs;
			this.showTaskTitles = showTaskTitles;
			this.title = title;
			this.statusSourcePad = statusSourcePad;
			this.showCancelButton = showCancelButton;
			icon = iconName;
			statusBar = IdeApp.Workbench.StatusBar.CreateContext ();
			statusBar.StatusSourcePad = statusSourcePad;
			if (showCancelButton)
				statusBar.CancellationTokenSource = CancellationTokenSource;
			statusBar.BeginProgress (iconName, title);
			if (lockGui)
				IdeApp.Workbench.LockGui ();
		}
		
		protected override void OnProgressChanged ()
		{
			if (showTaskTitles)
				statusBar.ShowMessage (icon, CurrentTaskName);
			if (!ProgressIsUnknown) {
				statusBar.SetProgressFraction (Progress);
				IdeServices.DesktopService.SetGlobalProgress (Progress);
			} else
				IdeServices.DesktopService.ShowGlobalProgressIndeterminate ();
		}
		
		public void UpdateStatusBar ()
		{
			if (showTaskTitles)
				statusBar.ShowMessage (icon, CurrentTaskName);
			else
				statusBar.ShowMessage (icon, title);
			if (!ProgressIsUnknown)
				statusBar.SetProgressFraction (Progress);
			else
				statusBar.SetProgressFraction (0);
		}
		
		protected override void OnCompleted ()
		{
			if (lockGui)
				IdeApp.Workbench.UnlockGui ();

			statusBar.EndProgress ();

			try {
				if (Errors.Length > 0 || Warnings.Length > 0) {
					if (Errors.Length > 0) {
						statusBar.ShowError (Errors [Errors.Length - 1].DisplayMessage);
					} else if (SuccessMessages.Length == 0) {
						statusBar.ShowWarning (Warnings [Warnings.Length - 1]);
					}

					IdeServices.DesktopService.ShowGlobalProgressError ();

					base.OnCompleted ();

					if (!CancellationToken.IsCancellationRequested && showErrorDialogs)
						this.ShowResultDialog ();
					return;
				}

				if (SuccessMessages.Length > 0)
					statusBar.ShowMessage (MonoDevelop.Ide.Gui.Stock.StatusSuccess, SuccessMessages [SuccessMessages.Length - 1]);

			} finally {
				statusBar.StatusSourcePad = statusSourcePad;
				statusBar.Dispose ();
			}

			IdeServices.DesktopService.SetGlobalProgress (Progress);

			base.OnCompleted ();
		}
	}
}
