//
// MakefileProject.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2014 Xamarin, Inc (http://www.xamarin.com)
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
using MonoDevelop.Projects;
using MonoDevelop.Core;
using System.Collections;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using MonoDevelop.Ide;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Core.Execution;
using System.CodeDom.Compiler;

namespace MonoDeveloper
{
	public class MonoMakefileProjectExtension: DotNetProjectExtension
	{
		string outFile;
		ArrayList refNames = new ArrayList ();
		bool loading;
		string testFileBase;
		object unitTest;

		public MonoMakefileProjectExtension ()
		{
		}

		protected override void Initialize ()
		{
			base.Initialize ();
			Project.FileAddedToProject += OnFileAddedToProject;
			Project.FileRemovedFromProject += OnFileRemovedFromProject;
			Project.FileRenamedInProject += OnFileRenamedInProject;
		}

		public string SourcesFile {
			get { return outFile + ".sources"; }
		}

		protected override Task OnSave (ProgressMonitor monitor)
		{
			// Nothing to do, changes are saved directly to the backing file
			return Task.FromResult (0);
		}

		internal void Read (MonoMakefile mkfile)
		{
			loading = true;

			string basePath = Path.GetDirectoryName (mkfile.FileName);
			string aname;

			string targetAssembly = mkfile.GetVariable ("LIBRARY");
			if (targetAssembly == null) {
				targetAssembly = mkfile.GetVariable ("PROGRAM");
				if (Path.GetDirectoryName (targetAssembly) == "")
					targetAssembly = Path.Combine (basePath, targetAssembly);
				aname = Path.GetFileName (targetAssembly);
			} else {
				aname = Path.GetFileName (targetAssembly);
				string targetName = mkfile.GetVariable ("LIBRARY_NAME");
				if (targetName != null) targetAssembly = targetName;
				targetAssembly = "$(topdir)/class/lib/$(PROFILE)/" + targetAssembly;
			}

			outFile = Path.Combine (basePath, aname);
			Project.FileName = mkfile.FileName;

			ArrayList checkedFolders = new ArrayList ();

			// Parse projects
			string sources = outFile + ".sources";
			StreamReader sr = new StreamReader (sources);
			string line;
			while ((line = sr.ReadLine ()) != null) {
				line = line.Trim (' ','\t');
				if (line != "") {
					string fname = Path.Combine (basePath, line);
					Project.Files.Add (new ProjectFile (fname));

					string dir = Path.GetDirectoryName (fname);
					if (!checkedFolders.Contains (dir)) {
						checkedFolders.Add (dir);
						fname = Path.Combine (dir, "ChangeLog");
						if (File.Exists (fname))
							Project.Files.Add (new ProjectFile (fname, BuildAction.Content));
					}
				}
			}

			sr.Close ();

			// Project references
			string refs = mkfile.GetVariable ("LIB_MCS_FLAGS");
			if (refs == null || refs == "") refs = mkfile.GetVariable ("LOCAL_MCS_FLAGS");

			if (refs != null && refs != "") {
				Regex var = new Regex(@"(.*?/r:(?<ref>.*?)(( |\t)|$).*?)*");
				Match match = var.Match (refs);
				if (match.Success) {
					foreach (Capture c in match.Groups["ref"].Captures)
						refNames.Add (Path.GetFileNameWithoutExtension (c.Value));
				}
			}

			int i = basePath.LastIndexOf ("/mcs/", basePath.Length - 2);
			string topdir = basePath.Substring (0, i + 4);
			targetAssembly = targetAssembly.Replace ("$(topdir)", topdir);

			if (mkfile.GetVariable ("NO_TEST") != "yes") {
				string tname = Path.GetFileNameWithoutExtension (aname) + "_test_";
				testFileBase = Path.Combine (basePath, tname);
			}

			foreach (string sconf in MonoMakefile.MonoConfigurations) {
				var conf = (DotNetProjectConfiguration) Project.CreateConfiguration (sconf);
				conf.OutputDirectory = basePath;
				conf.OutputAssembly = Path.GetFileName (targetAssembly);
				Project.Configurations.Add (conf);
			}

			loading = false;
			IdeApp.Workspace.SolutionLoaded += CombineOpened;
		}

		void CombineOpened (object sender, SolutionEventArgs args)
		{
			if (args.Solution == Project.ParentSolution) {
				foreach (string pref in refNames) {
					Project p = Project.ParentSolution.FindProjectByName (pref);
					if (p != null) Project.References.Add (ProjectReference.CreateProjectReference (p));
				}
			}
		}

		static Regex regexError = new Regex (@"^(\s*(?<file>.*)\((?<line>\d*)(,(?<column>\d*[\+]*))?\)(:|)\s+)*(?<level>\w+)\s*(?<number>.*):\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		protected override Task<TargetEvaluationResult> OnRunTarget (ProgressMonitor monitor, string target, ConfigurationSelector configuration, TargetEvaluationContext context)
		{
			if (target == ProjectService.BuildTarget)
				target = "all";
			else if (target == ProjectService.CleanTarget)
				target = "clean";

			DotNetProjectConfiguration conf = (DotNetProjectConfiguration) Project.GetConfiguration (configuration);

			return Task<TargetEvaluationResult>.Factory.StartNew (delegate {
				using (var output = new StringWriter ()) {
					using (var tw = new LogTextWriter ()) {
						tw.ChainWriter (output);
						tw.ChainWriter (monitor.Log);

						using (ProcessWrapper proc = Runtime.ProcessService.StartProcess ("make", "PROFILE=" + conf.Id + " " + target, conf.OutputDirectory, monitor.Log, tw, null))
							proc.WaitForOutput ();

						tw.UnchainWriter (output);
						tw.UnchainWriter (monitor.Log);

						CompilerResults cr = new CompilerResults (null);
						string[] lines = output.ToString ().Split ('\n');
						foreach (string line in lines) {
							CompilerError err = CreateErrorFromString (line);
							if (err != null)
								cr.Errors.Add (err);
						}

						return new TargetEvaluationResult (new BuildResult (cr, output.ToString ()));
					}
				}
			});
		}

		private CompilerError CreateErrorFromString (string error_string)
		{
			// When IncludeDebugInformation is true, prevents the debug symbols stats from braeking this.
			if (error_string.StartsWith ("WROTE SYMFILE") ||
				error_string.StartsWith ("make[") ||
				error_string.StartsWith ("OffsetTable") ||
				error_string.StartsWith ("Compilation succeeded") ||
				error_string.StartsWith ("Compilation failed"))
				return null;

			CompilerError error = new CompilerError();

			Match match=regexError.Match(error_string);
			if (!match.Success)
				return null;

			string level = match.Result("${level}");
			if (level == "warning")
				error.IsWarning = true;
			else if (level != "error")
				return null;

			if (String.Empty != match.Result("${file}"))
				error.FileName = Path.Combine (Project.BaseDirectory, match.Result("${file}"));
			if (String.Empty != match.Result("${line}"))
				error.Line=Int32.Parse(match.Result("${line}"));
			if (String.Empty != match.Result("${column}"))
				error.Column = Int32.Parse(match.Result("${column}"));
			error.ErrorNumber = match.Result ("${number}");
			error.ErrorText = match.Result ("${message}");
			return error;
		}

		void OnFileAddedToProject (object s, ProjectFileEventArgs args)
		{
			if (loading) return;

			foreach (ProjectFileEventInfo e in args) {
				if (e.ProjectFile.BuildAction != BuildAction.Compile)
					continue;
				AddSourceFile (e.ProjectFile.Name);
			}
		}

		void OnFileRemovedFromProject (object s, ProjectFileEventArgs args)
		{
			if (loading) return;

			foreach (ProjectFileEventInfo e in args) {
				if (e.ProjectFile.BuildAction != BuildAction.Compile)
					continue;

				RemoveSourceFile (e.ProjectFile.Name);
			}
		}

		void OnFileRenamedInProject (object s, ProjectFileRenamedEventArgs args)
		{
			if (loading) return;

			foreach (ProjectFileRenamedEventInfo e in args) {
				if (e.ProjectFile.BuildAction != BuildAction.Compile)
					continue;

				if (RemoveSourceFile (e.OldName))
					AddSourceFile (e.NewName);
			}
		}

		void AddSourceFile (string sourceFile)
		{
			StreamReader sr = null;
			StreamWriter sw = null;

			try {
				sr = new StreamReader (outFile + ".sources");
				sw = new StreamWriter (outFile + ".sources.new");

				string newFile = Project.GetRelativeChildPath (sourceFile);
				if (newFile.StartsWith ("./")) newFile = newFile.Substring (2);

				string line;
				while ((line = sr.ReadLine ()) != null) {
					string file = line.Trim (' ','\t');
					if (newFile != null && (file == "" || string.Compare (file, newFile) > 0)) {
						sw.WriteLine (newFile);
						newFile = null;
					}
					sw.WriteLine (line);
				}
				if (newFile != null)
					sw.WriteLine (newFile);
			} finally {
				if (sr != null) sr.Close ();
				if (sw != null) sw.Close ();
			}
			File.Delete (outFile + ".sources");
			File.Move (outFile + ".sources.new", outFile + ".sources");
		}

		bool RemoveSourceFile (string sourceFile)
		{
			StreamReader sr = null;
			StreamWriter sw = null;
			bool found = false;

			try {
				sr = new StreamReader (outFile + ".sources");
				sw = new StreamWriter (outFile + ".sources.new");

				string oldFile = Project.GetRelativeChildPath (sourceFile);
				if (oldFile.StartsWith ("./")) oldFile = oldFile.Substring (2);

				string line;
				while ((line = sr.ReadLine ()) != null) {
					string file = line.Trim (' ','\t');
					if (oldFile != file)
						sw.WriteLine (line);
					else
						found = true;
				}
			} finally {
				if (sr != null) sr.Close ();
				if (sw != null) sw.Close ();
			}
			if (found) {
				File.Delete (outFile + ".sources");
				File.Move (outFile + ".sources.new", outFile + ".sources");
			}
			return found;
		}

		public override void Dispose ()
		{
			Project.FileAddedToProject -= OnFileAddedToProject;
			Project.FileRemovedFromProject -= OnFileRemovedFromProject;
			Project.FileRenamedInProject -= OnFileRenamedInProject;
			IdeApp.Workspace.SolutionLoaded -= CombineOpened;
			base.Dispose ();
		}

		public string GetTestFileBase ()
		{
			return testFileBase;
		}

		public object UnitTest {
			get { return unitTest; }
			set { unitTest = value; }
		}
	}
}

