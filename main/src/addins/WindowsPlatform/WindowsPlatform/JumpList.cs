// 
// JumpList.cs
//  
// Author:
//       Steven Schermerhorn <stevens+monoaddins@ischyrus.com>
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Timers;
using Microsoft.Win32;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Desktop;
using Taskbar = Microsoft.WindowsAPICodePack.Taskbar;

namespace MonoDevelop.Platform
{
	public class JumpList : CommandHandler
	{
		private IList<string> supportedExtensions;
		private RecentFiles recentFiles;
		private Timer updateTimer;

		protected override void Run ()
		{
			if (!Taskbar.TaskbarManager.IsPlatformSupported) {
				return;
			}
			
			bool areFileExtensionsRegistered = this.Initialize ();
			
			if (!areFileExtensionsRegistered) {
				return;
			}
			
			this.updateTimer = new Timer (1000);
			this.updateTimer.Elapsed += this.OnUpdateTimerEllapsed;
			this.updateTimer.AutoReset = false;
			
			this.recentFiles = IdeServices.DesktopService.RecentFiles;
			this.recentFiles.Changed += this.OnRecentFilesChanged;

			try {
				UpdateJumpList();
			} catch (Exception ex) {
				MonoDevelop.Core.LoggingService.LogError ("Could not update jumplists", ex);
			}
		}

		object sync = new object();
		private void OnRecentFilesChanged (object sender, EventArgs args)
		{
			// This event fires several times for a single change. Rather than performing the update
			// several times we will restart the timer which has a 1 second delay on it. 
			// While this means the update won't make it to the JumpList immediately it is significantly
			// better for performance.
			lock (sync) {
				this.updateTimer.Stop ();
				this.updateTimer.Start ();
			}
		}

		private void OnUpdateTimerEllapsed (object sender, EventArgs args)
		{
			try {
				UpdateJumpList();
			} catch (Exception ex) {
				MonoDevelop.Core.LoggingService.LogError ("Could not update jumplists", ex);
			}
		}

		private void UpdateJumpList ()
		{
			Taskbar.JumpList jumplist = Taskbar.JumpList.CreateJumpListForIndividualWindow (
				MonoDevelop.Core.BrandingService.ApplicationName,
				GdkWin32.HgdiobjGet (MessageService.RootWindow)
			);
			jumplist.KnownCategoryToDisplay = Taskbar.JumpListKnownCategoryType.Neither;
			
			Taskbar.JumpListCustomCategory recentProjectsCategory = new Taskbar.JumpListCustomCategory ("Recent Solutions");
			Taskbar.JumpListCustomCategory recentFilesCategory = new Taskbar.JumpListCustomCategory ("Recent Files");
			
			jumplist.AddCustomCategories (recentProjectsCategory, recentFilesCategory);
			jumplist.KnownCategoryOrdinalPosition = 0;
			
			foreach (RecentFile recentProject in recentFiles.GetProjects ()) {
				// Windows is picky about files that are added to the jumplist. Only files that MonoDevelop
				// has been registered as supported in the registry can be added.
				bool isSupportedFileExtension = this.supportedExtensions.Contains (Path.GetExtension (recentProject.FileName));
				if (isSupportedFileExtension) {
					recentProjectsCategory.AddJumpListItems (new Taskbar.JumpListLink (exePath, recentProject.DisplayName) {
						Arguments = MonoDevelop.Core.Execution.ProcessArgumentBuilder.Quote (recentProject.FileName),
						IconReference = new Microsoft.WindowsAPICodePack.Shell.IconReference (exePath, 0),
					});
				}
			}
			
			foreach (RecentFile recentFile in recentFiles.GetFiles ()) {
				if (this.supportedExtensions.Contains (Path.GetExtension (recentFile.FileName)))
					recentFilesCategory.AddJumpListItems (new Taskbar.JumpListLink (exePath, recentFile.DisplayName) {
						Arguments = MonoDevelop.Core.Execution.ProcessArgumentBuilder.Quote (recentFile.FileName),
						IconReference = new Microsoft.WindowsAPICodePack.Shell.IconReference (exePath, 0),
					});
			}
			
			jumplist.Refresh ();
		}

		string exePath;
		private bool Initialize ()
		{
			this.supportedExtensions = new List<string> ();
			
			// Determine the correct value for /HKCR/XamarinStudio/shell/Open/Command
			ProcessModule monoDevelopAssembly = Process.GetCurrentProcess ().MainModule;
			exePath = monoDevelopAssembly.FileName;
			string executeString = exePath + " %1";
			string progId = MonoDevelop.Core.BrandingService.ProfileDirectoryName;

			using (RegistryKey progIdKey = Registry.ClassesRoot.OpenSubKey (progId + @"\shell\Open\Command", false)) {
				if (progIdKey == null) {
					return false;
				}
			
				object path = progIdKey.GetValue (String.Empty);
				bool isProgIdRegistered = String.Equals (executeString, path as string, StringComparison.OrdinalIgnoreCase);
				if (!isProgIdRegistered) {
					return false;
				}
			}
			
			string[] subkeyNames = Registry.ClassesRoot.GetSubKeyNames ();
			foreach (string subkey in subkeyNames) {
				if (subkey[0] != '.') {
					continue;
				}

				using (RegistryKey openWithKey = Registry.ClassesRoot.OpenSubKey (Path.Combine (subkey, "OpenWithProgids"))) {
					if (openWithKey == null) {
						continue;
					}
				
					string progIdValue = openWithKey.GetValue (progId, null) as string;
					if (progIdValue == null) {
						continue;
					}
				}
				
				this.supportedExtensions.Add (subkey);
			}
			
			bool atLeastOneFileTypeRegistered = this.supportedExtensions.Count > 0;
			return atLeastOneFileTypeRegistered;
		}
	}
}

