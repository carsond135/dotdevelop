//
// BreakpointsPropertiesDialog.cs
//
// Author:
//       Therzok <teromario@yahoo.com>
//
// Copyright (c) 2013 Therzok
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using MetadataReferenceProperties = Microsoft.CodeAnalysis.MetadataReferenceProperties;
using Microsoft.VisualStudio.Text.Editor;

using Mono.Debugging.Client;

using Xwt.Drawing;
using Xwt;

using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

namespace MonoDevelop.Debugger
{
	enum ConditionalHitWhen
	{
		ResetCondition,
		ConditionIsTrue,
		ExpressionChanges
	}

	public enum BreakpointType
	{
		Location,
		Function,
		Catchpoint
	}

	sealed class BreakpointPropertiesDialog : Xwt.Dialog
	{
		// For button sensitivity.
		DialogButton buttonOk;
		bool editing;

		// Groupings for sensitivity
		HBox hboxFunction = new HBox () { MarginLeft = 18 };
		HBox hboxLocation = new HBox ();
		HBox hboxException = new HBox ();
		HBox hboxCondition = new HBox ();
		VBox vboxException = new VBox () { MarginLeft = 18 };
		VBox vboxLocation = new VBox () { MarginLeft = 18 };
		// Breakpoint Action radios.
		readonly RadioButton breakpointActionPause = new RadioButton (GettextCatalog.GetString ("Pause the program"));
		readonly RadioButton breakpointActionPrint = new RadioButton (GettextCatalog.GetString ("Print a message and continue"));

		// Stop-type radios.
		readonly RadioButton stopOnFunction = new RadioButton (GettextCatalog.GetString ("When a function is entered"));
		readonly RadioButton stopOnLocation = new RadioButton (GettextCatalog.GetString ("When a location is reached"));
		readonly RadioButton stopOnException = new RadioButton (GettextCatalog.GetString ("When an exception is thrown"));

		// Text entries
		readonly TextEntry entryFunctionName = new TextEntry () { PlaceholderText = GettextCatalog.GetString ("e.g. System.Object.ToString") };
		readonly TextEntry entryLocationFile = new TextEntry () { PlaceholderText = GettextCatalog.GetString ("e.g. Program.cs:15:5") };
		readonly TextEntryWithCodeCompletion entryExceptionType = new TextEntryWithCodeCompletion () { PlaceholderText = GettextCatalog.GetString ("e.g. System.InvalidOperationException") };
		readonly TextEntry entryConditionalExpression = new TextEntry () { PlaceholderText = GettextCatalog.GetString ("e.g. colorName == \"Red\"") };
		readonly TextEntry entryPrintExpression = new TextEntry () { PlaceholderText = GettextCatalog.GetString ("e.g. Value of 'name' is {name}") };

		// Warning icon
		readonly Components.InformationPopoverWidget warningFunction = new Components.InformationPopoverWidget () { Severity = Ide.Tasks.TaskSeverity.Warning };
		readonly Components.InformationPopoverWidget warningLocation = new Components.InformationPopoverWidget () { Severity = Ide.Tasks.TaskSeverity.Warning };
		readonly Components.InformationPopoverWidget warningException = new Components.InformationPopoverWidget () { Severity = Ide.Tasks.TaskSeverity.Warning };
		readonly Components.InformationPopoverWidget warningCondition = new Components.InformationPopoverWidget () { Severity = Ide.Tasks.TaskSeverity.Warning };
		readonly Components.InformationPopoverWidget warningPrintExpression = new Components.InformationPopoverWidget () { Severity = Ide.Tasks.TaskSeverity.Warning };

		// Combobox + Pager
		readonly SpinButton ignoreHitCount = new SpinButton ();
		readonly ComboBox ignoreHitType = new ComboBox ();
		readonly ComboBox conditionalHitType = new ComboBox ();

		// Optional checkboxes.
		readonly CheckBox checkIncludeSubclass = new CheckBox (GettextCatalog.GetString ("Include subclasses"));

		// Tips labels.
		readonly Label printMessageTip = new Label (GettextCatalog.GetString ("Place simple C# expressions within {} to interpolate them.")) {
			TextColor = Styles.BreakpointPropertiesSecondaryTextColor,
			TextAlignment = Alignment.End
		};
		readonly Label conditionalExpressionTip = new Label (GettextCatalog.GetString ("A C# boolean expression. Scope is local to the breakpoint.")) {
			TextColor = Styles.BreakpointPropertiesSecondaryTextColor,
			TextAlignment = Alignment.End
		};

		ParsedLocation breakpointLocation = new ParsedLocation ();

		BreakEvent be;
		string [] parsedParamTypes;
		string parsedFunction;
		readonly HashSet<string> classes = new HashSet<string> ();

		public BreakpointPropertiesDialog (BreakEvent be, BreakpointType breakpointType)
		{
			this.be = be;
			Task.Run (LoadExceptionList);
			Initialize ();
			SetInitialData ();
			SetLayout ();
			SetAccessibility ();
			if (be == null) {
				switch (breakpointType) {
				case BreakpointType.Location:
					stopOnLocation.Active = true;
					entryLocationFile.SetFocus ();
					break;
				case BreakpointType.Function:
					stopOnFunction.Active = true;
					entryFunctionName.SetFocus ();
					break;
				case BreakpointType.Catchpoint:
					stopOnException.Active = true;
					entryExceptionType.SetFocus ();
					entryExceptionType.Text = "System.Exception";
					entryExceptionType.SelectionStart = 0;
					entryExceptionType.SelectionLength = entryExceptionType.TextLength;
					break;
				}
			}
		}

		void Initialize ()
		{
			string buttonLabel;
			if (be == null) {
				Title = GettextCatalog.GetString ("Create a Breakpoint");
				buttonLabel = GettextCatalog.GetString ("Create");
			} else {
				Title = GettextCatalog.GetString ("Edit Breakpoint");
				buttonLabel = GettextCatalog.GetString ("Apply");
			}
			var actionGroup = new RadioButtonGroup ();
			breakpointActionPause.Group = actionGroup;
			breakpointActionPrint.Group = actionGroup;

			var stopGroup = new RadioButtonGroup ();
			stopOnFunction.Group = stopGroup;
			stopOnLocation.Group = stopGroup;
			stopOnException.Group = stopGroup;

			ignoreHitType.Items.Add (HitCountMode.None, GettextCatalog.GetString ("Reset condition"));
			ignoreHitType.Items.Add (HitCountMode.LessThan, GettextCatalog.GetString ("When hit count is less than"));
			ignoreHitType.Items.Add (HitCountMode.LessThanOrEqualTo, GettextCatalog.GetString ("When hit count is less than or equal to"));
			ignoreHitType.Items.Add (HitCountMode.EqualTo, GettextCatalog.GetString ("When hit count is equal to"));
			ignoreHitType.Items.Add (HitCountMode.GreaterThan, GettextCatalog.GetString ("When hit count is greater than"));
			ignoreHitType.Items.Add (HitCountMode.GreaterThanOrEqualTo, GettextCatalog.GetString ("When hit count is greater than or equal to"));
			ignoreHitType.Items.Add (HitCountMode.MultipleOf, GettextCatalog.GetString ("When hit count is a multiple of"));

			ignoreHitCount.IncrementValue = 1;
			ignoreHitCount.Digits = 0;
			ignoreHitCount.ClimbRate = 1;
			ignoreHitCount.MinimumValue = 0;
			ignoreHitCount.MaximumValue = Int32.MaxValue;


			conditionalHitType.Items.Add (ConditionalHitWhen.ResetCondition, GettextCatalog.GetString ("Reset condition"));
			conditionalHitType.Items.Add (ConditionalHitWhen.ConditionIsTrue, GettextCatalog.GetString ("And the following condition is true"));
			conditionalHitType.Items.Add (ConditionalHitWhen.ExpressionChanges, GettextCatalog.GetString ("And the following expression changes"));

			buttonOk = new DialogButton (buttonLabel, Command.Ok) {
				Sensitive = false
			};

			// Register events.
			stopGroup.ActiveRadioButtonChanged += OnUpdateControls;
			entryFunctionName.Changed += OnUpdateControls;
			entryLocationFile.Changed += OnUpdateControls;

			entryConditionalExpression.Changed += OnUpdateControls;
			ignoreHitType.SelectionChanged += OnUpdateControls;
			conditionalHitType.SelectionChanged += OnUpdateControls;
			breakpointActionPause.ActiveChanged += OnUpdateControls;
			breakpointActionPrint.ActiveChanged += OnUpdateControls;

			entryFunctionName.Changed += OnUpdateText;
			entryLocationFile.Changed += OnUpdateText;
			entryExceptionType.Changed += OnUpdateText;
			entryPrintExpression.Changed += OnUpdateText;

			buttonOk.Clicked += OnSave;

			CompletionWindowManager.WindowShown += HandleCompletionWindowShown;
			CompletionWindowManager.WindowClosed += HandleCompletionWindowClosed;
		}

		#region Modal and Dialog.Run workaround
		/*
		 * If Dialog is ran with Dialog.Run and Modal=true it takes all events like mouse, keyboard... from other windows.
		 * So when CodeCompletionList window appears mouse events don't work on it(except if CodeCompletionList.Modal=true, but then
		 * events don't work on BreakpointPropertiesDialog(can't type rest of exception type name)).
		 * So what this workaround does is disables Modal on BreakpointProperties so CodeCompletionList mouse events work fine. But if user
		 * tries to access anything outside this two windows(e.g. MainWindow). CodeCompletionList loses focus and closes itself. Resulting
		 * in BreakpointProperties.Modal = true and user can't do anything on MainWindow.
		 * All this is done so fast(or in correct order) that user can't notice this Modal switching.
		 */

		void HandleCompletionWindowClosed (object sender, EventArgs e)
		{
			var gtkWidget = Xwt.Toolkit.CurrentEngine.GetNativeWidget (vboxLocation) as Gtk.Widget;//Any widget is fine
			if (gtkWidget != null) {
				var topWindow = gtkWidget.Toplevel as Gtk.Window;
				if (topWindow != null) {
					topWindow.Modal = true;
				}
			}
		}

		void HandleCompletionWindowShown (object sender, EventArgs e)
		{
			var gtkWidget = Xwt.Toolkit.CurrentEngine.GetNativeWidget (vboxLocation) as Gtk.Widget;//Any widget is fine
			if (gtkWidget != null) {
				var topWindow = gtkWidget.Toplevel as Gtk.Window;
				if (topWindow != null) {
					topWindow.Modal = false;
				}
			}
		}

		#endregion

		void SetInitialFunctionBreakpointData (FunctionBreakpoint fb)
		{
			stopOnLocation.Visible = false;
			vboxLocation.Visible = false;
			stopOnException.Visible = false;
			vboxException.Visible = false;

			stopOnFunction.Active = true;
			if (fb.ParamTypes != null) {
				// FIXME: support non-C# syntax based on fb.Language
				entryFunctionName.Text = fb.FunctionName + " (" + String.Join (", ", fb.ParamTypes) + ")";
			} else
				entryFunctionName.Text = fb.FunctionName;
		}

		void SetInitialBreakpointData (Breakpoint bp)
		{
			stopOnFunction.Visible = false;
			hboxFunction.Visible = false;
			stopOnException.Visible = false;
			vboxException.Visible = false;

			stopOnLocation.Active = true;
			breakpointLocation.Update (bp);

			entryLocationFile.Text = breakpointLocation.ToString ();
			Project project = null;
			if (!string.IsNullOrEmpty (bp.FileName))
				project = IdeApp.Workspace.GetProjectsContainingFile (bp.FileName).FirstOrDefault ();

			if (project != null) {
				// Check the startup project of the solution too, since the current project may be a library
				SolutionItem startup = project.ParentSolution.StartupItem;
				entryConditionalExpression.Sensitive = DebuggingService.IsFeatureSupported (project, DebuggerFeatures.ConditionalBreakpoints) ||
				DebuggingService.IsFeatureSupported (startup, DebuggerFeatures.ConditionalBreakpoints);

				bool canTrace = DebuggingService.IsFeatureSupported (project, DebuggerFeatures.Tracepoints) ||
								DebuggingService.IsFeatureSupported (startup, DebuggerFeatures.Tracepoints);

				breakpointActionPause.Sensitive = canTrace;
				entryPrintExpression.Sensitive = canTrace;
			}
		}

		void SetInitialCatchpointData (Catchpoint cp)
		{
			stopOnFunction.Visible = false;
			hboxFunction.Visible = false;
			stopOnLocation.Visible = false;
			vboxLocation.Visible = false;

			stopOnException.Active = true;
			entryExceptionType.Text = cp.ExceptionName;
			checkIncludeSubclass.Active = cp.IncludeSubclasses;
		}

		void SetInitialData ()
		{
			if (be != null) {
				editing = true;
				if (be.HitCountMode == HitCountMode.None) {
					ignoreHitType.SelectedItem = HitCountMode.GreaterThanOrEqualTo;
					ignoreHitCount.Value = 0;
				} else {
					ignoreHitType.SelectedItem = be.HitCountMode;
					ignoreHitCount.Value = be.HitCount;
				}

				if ((be.HitAction & HitAction.Break) == HitAction.Break) {
					breakpointActionPause.Active = true;
				} else {
					breakpointActionPrint.Active = true;
					entryPrintExpression.Text = be.TraceExpression;
				}

				entryConditionalExpression.Text = be.ConditionExpression ?? "";
				conditionalHitType.SelectedItem = be.BreakIfConditionChanges ?
					ConditionalHitWhen.ExpressionChanges : ConditionalHitWhen.ConditionIsTrue;


				var fb = be as FunctionBreakpoint;
				if (fb != null) {
					SetInitialFunctionBreakpointData (fb);
				} else {
					var bp = be as Breakpoint;
					if (bp != null)
						SetInitialBreakpointData (bp);
				}
				var cp = be as Catchpoint;
				if (cp != null)
					SetInitialCatchpointData (cp);
			} else {
				ignoreHitType.SelectedItem = HitCountMode.GreaterThanOrEqualTo;
				conditionalHitType.SelectedItem = ConditionalHitWhen.ConditionIsTrue;
				checkIncludeSubclass.Active = true;

				if (IdeApp.Workbench.ActiveDocument != null &&
					IdeApp.Workbench.ActiveDocument.GetContent<ITextView> () is ITextView textView &&
					IdeApp.Workbench.ActiveDocument.FileName != FilePath.Null) {
					var (line, col) = textView.MDCaretLineAndColumn ();
					breakpointLocation.Update (IdeApp.Workbench.ActiveDocument.FileName,
						line,
						col);
					entryLocationFile.Text = breakpointLocation.ToString ();
					stopOnLocation.Active = true;
				}
			}
		}

		void SaveFunctionBreakpoint (FunctionBreakpoint fb)
		{
			fb.FunctionName = parsedFunction;
			fb.ParamTypes = parsedParamTypes;
		}

		void SaveCatchpoint (Catchpoint cp)
		{
			cp.ExceptionName = entryExceptionType.Text;
			cp.IncludeSubclasses = checkIncludeSubclass.Active;
		}

		class ParsedLocation
		{
			int line;
			int column;

			public void Update (string location)
			{
				if (string.IsNullOrWhiteSpace (location)) {
					Warning = GettextCatalog.GetString ("Enter location.");
					return;
				}
				var splitted = location.Split (':');
				if (!File.Exists (splitted [0])) {
					//Maybe it's C:\filepath.ext
					if (splitted.Length > 1 && File.Exists (splitted [0] + ":" + splitted [1])) {
						var newSplitted = new string [splitted.Length - 1];
						newSplitted [0] = splitted [0] + ":" + splitted [1];
						for (int i = 2; i < splitted.Length; i++) {
							newSplitted [i - 1] = splitted [i];
						}
						splitted = newSplitted;
					} else {
						Warning = GettextCatalog.GetString ("File does not exist.");
						return;
					}
				}
				if (splitted.Length < 2) {
					Warning = GettextCatalog.GetString ("Missing ':' for line declaration.");
					return;
				}
				FileName = splitted [0];
				if (!int.TryParse (splitted [1], out line)) {
					Warning = GettextCatalog.GetString ("Line is not a number.");
					return;
				}

				if (splitted.Length > 2 && !int.TryParse (splitted [2], out column)) {
					Warning = GettextCatalog.GetString ("Column is not a number.");
					return;
				} else {
					column = 1;
				}
				Warning = "";
			}

			public void Update (Breakpoint bp)
			{
				Update (bp.FileName, bp.Line, bp.Column);
			}

			public void Update (string filePath, int line, int column)
			{
				if (!System.IO.File.Exists (filePath)) {
					Warning = GettextCatalog.GetString ("File does not exist.");
				} else {
					Warning = "";
				}
				this.FileName = filePath;
				this.line = line;
				this.column = column;
			}

			public Breakpoint ToBreakpoint ()
			{
				if (!IsValid)
					throw new InvalidOperationException ("Location is invalid.");
				return new Breakpoint (FileName, line, column);
			}

			public string Warning { get; private set; }

			public bool IsValid {
				get {
					return Warning == "";
				}
			}

			public string FileName { get; private set; }

			public int Line { get { return line; } }

			public int Column { get { return column; } }

			public override string ToString ()
			{
				return FileName + ":" + line + ":" + column;
			}
		}

		void SaveBreakpoint (Breakpoint bp)
		{
			bp.SetColumn (breakpointLocation.Column);
			bp.SetLine (breakpointLocation.Line);
		}

		void OnSave (object sender, EventArgs e)
		{
			bool catchpointSaved = false;

			if (be == null) {
				if (stopOnFunction.Active)
					be = new FunctionBreakpoint ("", "C#");
				else if (stopOnLocation.Active)
					be = breakpointLocation.ToBreakpoint ();
				else if (stopOnException.Active) {
					be = new Catchpoint (entryExceptionType.Text, checkIncludeSubclass.Active);
					catchpointSaved = true;
				} else
					return;
			}

			var fb = be as FunctionBreakpoint;
			if (fb != null)
				SaveFunctionBreakpoint (fb);

			var bp = be as Breakpoint;
			if (bp != null)
				SaveBreakpoint (bp);

			if (!catchpointSaved) {
				var cp = be as Catchpoint;
				if (cp != null) {
					SaveCatchpoint (cp);
				}
			}

			if ((HitCountMode)ignoreHitType.SelectedItem == HitCountMode.GreaterThanOrEqualTo && (int)ignoreHitCount.Value == 0) {
				be.HitCountMode = HitCountMode.None;
			} else {
				be.HitCountMode = (HitCountMode)ignoreHitType.SelectedItem;
			}
			be.HitCount = be.HitCountMode != HitCountMode.None ? (int)ignoreHitCount.Value : 0;


			if (!string.IsNullOrWhiteSpace (entryConditionalExpression.Text)) {
				be.ConditionExpression = entryConditionalExpression.Text;
				be.BreakIfConditionChanges = conditionalHitType.SelectedItem.Equals (ConditionalHitWhen.ExpressionChanges);
			} else {
				be.ConditionExpression = null;
			}

			if (breakpointActionPrint.Active) {
				be.HitAction = HitAction.PrintExpression;
				be.TraceExpression = entryPrintExpression.Text;
			} else {
				be.HitAction = HitAction.Break;
			}
			be.CommitChanges ();
		}

		void OnUpdateControls (object sender, EventArgs e)
		{
			//Selection of None actually means ResetCondition
			if (ignoreHitType.SelectedItem != null && (HitCountMode)ignoreHitType.SelectedItem == HitCountMode.None) {
				ignoreHitType.SelectedItem = HitCountMode.GreaterThanOrEqualTo;
				ignoreHitCount.Value = 0;
			}

			if (conditionalHitType.SelectedItem != null && (ConditionalHitWhen)conditionalHitType.SelectedItem == ConditionalHitWhen.ResetCondition) {
				conditionalHitType.SelectedItem = ConditionalHitWhen.ConditionIsTrue;
				entryConditionalExpression.Text = "";
			}

			// Check which radio is selected.
			var connected = DebuggingService.DebuggerSession != null ? DebuggingService.DebuggerSession.IsConnected : false;
			hboxFunction.Sensitive = stopOnFunction.Active && DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints) && !connected;
			hboxLocation.Sensitive = stopOnLocation.Active && DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints) && !connected;
			hboxException.Sensitive = stopOnException.Active && DebuggingService.IsFeatureSupported (DebuggerFeatures.Catchpoints) && !connected;
			checkIncludeSubclass.Sensitive = stopOnException.Active;
			hboxCondition.Sensitive = DebuggingService.IsFeatureSupported (DebuggerFeatures.ConditionalBreakpoints);

			// Check printing an expression.
			entryPrintExpression.Sensitive = breakpointActionPrint.Active && DebuggingService.IsFeatureSupported (DebuggerFeatures.Tracepoints);

			// And display warning icons
			buttonOk.Sensitive = CheckValidity ();
		}

		void OnUpdateText (object sender, EventArgs e)
		{
			buttonOk.Sensitive = CheckValidity ();
		}

		bool CheckValidity ()
		{
			warningFunction.Hide ();
			warningLocation.Hide ();
			warningException.Hide ();
			warningCondition.Hide ();
			warningPrintExpression.Hide ();

			bool result = true;

			if (breakpointActionPrint.Active && string.IsNullOrWhiteSpace (entryPrintExpression.Text)) {
				warningPrintExpression.Show ();
				warningPrintExpression.Message = GettextCatalog.GetString ("Enter trace expression.");
				result = false;
			}

			if (stopOnFunction.Active) {
				string text = entryFunctionName.Text.Trim ();

				if (stopOnFunction.Active) {
					if (text.Length == 0) {
						warningFunction.Show ();
						warningFunction.Message = GettextCatalog.GetString ("Enter function name.");
						result = false;
					}

					if (!TryParseFunction (text, out parsedFunction, out parsedParamTypes)) {
						warningFunction.Show ();
						warningFunction.Message = GettextCatalog.GetString ("Invalid function syntax.");
						result = false;
					}
				}
			} else if (stopOnLocation.Active) {
				breakpointLocation.Update (entryLocationFile.Text);
				if (!breakpointLocation.IsValid) {
					warningLocation.Show ();
					warningLocation.Message = breakpointLocation.Warning;
					result = false;
				}
			} else if (stopOnException.Active) {
				if (string.IsNullOrWhiteSpace (entryExceptionType.Text)) {
					warningException.Show ();
					warningException.Message = GettextCatalog.GetString ("Enter exception type.");
					result = false;
				} else if (!classes.Contains (entryExceptionType.Text)) {
					warningException.Show ();
					warningException.Message = GettextCatalog.GetString ("Exception not identified in exception list generated from currently selected project.");
					//We might be missing some exceptions that are loaded at runtime from outside our project
					//or we don't have project at all, hence show warning but still allow user to close window
					result = true;
				}
			}
			return result;
		}

		static bool TryParseFunction (string signature, out string function, out string [] paramTypes)
		{
			int paramListStart = signature.IndexOf ('(');
			int paramListEnd = signature.IndexOf (')');

			if (paramListStart == -1 && paramListEnd == -1) {
				function = signature;
				paramTypes = null;
				return true;
			}

			if (paramListEnd != signature.Length - 1) {
				paramTypes = null;
				function = null;
				return false;
			}

			function = signature.Substring (0, paramListStart).Trim ();

			paramListStart++;

			if (!FunctionBreakpoint.TryParseParameters (signature, paramListStart, paramListEnd, out paramTypes)) {
				paramTypes = null;
				function = null;
				return false;
			}

			return true;
		}

		async Task LoadExceptionList ()
		{
			classes.Add ("System.Exception");
			try {
				Microsoft.CodeAnalysis.Compilation compilation = null;
				MonoDevelopWorkspace workspace = null;

				var project = IdeApp.ProjectOperations.CurrentSelectedProject;
				if (project != null) {
					var roslynProj = IdeApp.TypeSystemService.GetProject (project);
					if (roslynProj != null) {
						workspace = (MonoDevelopWorkspace)roslynProj.Solution.Workspace;
						compilation = await roslynProj.GetCompilationAsync ();
					}
				}

				if (compilation == null) {
					// TypeSystemService.Workspace always returns a workspace,
					// even if it might be empty.
					workspace = workspace ?? (MonoDevelopWorkspace)IdeServices.TypeSystemService.Workspace;
					var service = workspace.MetadataReferenceManager;
					var corlib = service.GetOrCreateMetadataReferenceSnapshot (System.Reflection.Assembly.GetAssembly (typeof (object)).Location, MetadataReferenceProperties.Assembly);
					var system = service.GetOrCreateMetadataReferenceSnapshot (System.Reflection.Assembly.GetAssembly (typeof (Uri)).Location, MetadataReferenceProperties.Assembly);

					//no need to unload this assembly context, it's not cached.
					compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create ("GetExceptions")
					                       .AddReferences (corlib)
					                       .AddReferences (system);
				}
				var exceptionClass = compilation.GetTypeByMetadataName ("System.Exception");
				foreach (var t in compilation.GlobalNamespace.GetAllTypes ().Where ((arg) => arg.IsDerivedFromClass (exceptionClass))) {
					classes.Add (t.GetFullMetadataName ());
				}
			} catch (Exception e) {
				LoggingService.LogError ("Failed to obtain exceptions list in breakpoint dialog.", e);
			}
			await Runtime.RunInMainThread (() => {
				entryExceptionType.SetCodeCompletionList (classes.ToList ());
			});
		}

		public BreakEvent GetBreakEvent ()
		{
			return be;
		}

		void SetAccessibility ()
		{
			var accessible = breakpointActionPause.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.Pause";
			accessible.LabelWidget = actionLabel;
			accessible.Description = GettextCatalog.GetString ("Cause the program to pause when the breakpoint is hit");

			accessible = breakpointActionPrint.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.Print";
			accessible.LabelWidget = actionLabel;
			accessible.Description = GettextCatalog.GetString ("Cause the program to print a message and continue when the breakpoint is hit");

			accessible = entryPrintExpression.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.PrintExpression";
			accessible.LabelWidget = actionLabel;
			accessible.Label = GettextCatalog.GetString ("Breakpoint Expression");
			accessible.Description = GettextCatalog.GetString ("Enter the expression you wish to have printed to the console. Place simple C# expressions within {} to interpolate them.");

			accessible = warningPrintExpression.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.WarningPrintExpression";
			accessible.Description = GettextCatalog.GetString ("There is a warning for the print expression");

			accessible = stopOnFunction.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.StopOnFunction";
			accessible.Description = GettextCatalog.GetString ("Execute the action when a function is entered");

			accessible = entryFunctionName.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.FunctionName";
			accessible.Label = GettextCatalog.GetString ("Breakpoint Function");
			accessible.Description = GettextCatalog.GetString ("Enter the name of the breakpoint function");

			accessible = warningFunction.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.WarningFunction";
			accessible.Description = GettextCatalog.GetString ("There is a warning for the function name");

			accessible = stopOnException.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.StopOnException";
			accessible.Description = GettextCatalog.GetString ("Execute the action when an exception is thrown");

			accessible = entryExceptionType.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.ExceptionType";
			accessible.Label = GettextCatalog.GetString ("Breakpoint Exception");
			accessible.Description = GettextCatalog.GetString ("Enter the type of the breakpoint exception");

			accessible = warningException.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.WarningException";
			accessible.Description = GettextCatalog.GetString ("There is a warning for the exception type");

			accessible = checkIncludeSubclass.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.IncludeSubclasses";
			accessible.Description = GettextCatalog.GetString ("Select whether to also break on exception subclasses");

			accessible = stopOnLocation.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.StopOnLocation";
			accessible.Description = GettextCatalog.GetString ("Execute the action when the program reaches a location in a file");

			accessible = entryLocationFile.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.FileLocation";
			accessible.Label = GettextCatalog.GetString ("Breakpoint Location");
			accessible.Description = GettextCatalog.GetString ("Enter the file and line number of the breakpoint location");

			accessible = warningLocation.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.WarningLocation";
			accessible.Description = GettextCatalog.GetString ("There is a warning for the breakpoint location");

			accessible = ignoreHitType.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.HitType";
			accessible.LabelWidget = advancedLabel;
			accessible.Description = GettextCatalog.GetString ("Select a hit count condition for this breakpoint");

			accessible = ignoreHitCount.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.HitCount";
			accessible.Label = GettextCatalog.GetString ("Condition Hit Count");
			accessible.Description = GettextCatalog.GetString ("Enter the hit count required for the condition");

			accessible = conditionalHitType.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.ConditionalHit";
			accessible.LabelWidget = advancedLabel;
			accessible.Description = GettextCatalog.GetString ("Select an extra condition for this breakpoint");

			accessible = entryConditionalExpression.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.ConditionEntry";
			accessible.LabelWidget = advancedLabel;
			accessible.Label = GettextCatalog.GetString ("Conditional Breakpoint Expression");
			accessible.Description = GettextCatalog.GetString ("Enter a C# boolean expression to act as a condition for this breakpoint. The scope of the expression is local to the breakpoint");

			accessible = warningCondition.Accessible;
			accessible.Identifier = "BreakpointPropertiesDialog.WarningCondition";
			accessible.Description = GettextCatalog.GetString ("There is a warning for the condition expression");
		}

		Label actionLabel, whenLabel, advancedLabel;

		void SetLayout ()
		{
			var vbox = new VBox ();
			vbox.Accessible.Role = Xwt.Accessibility.Role.Filler;
			vbox.MinWidth = 450;

			actionLabel = new Label (GettextCatalog.GetString ("Breakpoint Action")) {
				Font = vbox.Font.WithWeight (FontWeight.Bold)
			};
			vbox.PackStart (actionLabel);

			var breakpointActionGroup = new VBox {
				MarginLeft = 12
			};
			breakpointActionGroup.Accessible.Role = Xwt.Accessibility.Role.Filler;

			breakpointActionGroup.PackStart (breakpointActionPause);
			breakpointActionGroup.PackStart (breakpointActionPrint);

			var printExpressionGroup = new HBox {
				MarginLeft = 18
			};

			printExpressionGroup.Accessible.Role = Xwt.Accessibility.Role.Filler;

			printExpressionGroup.PackStart (entryPrintExpression, true);

			// We'll ignore this label because the content of the label is included in the accessibility Help text of the 
			// entryPrintExpression widget
			printMessageTip.Accessible.Role = Xwt.Accessibility.Role.Filler;

			printExpressionGroup.PackStart (warningPrintExpression);
			breakpointActionGroup.PackStart (printExpressionGroup);

			breakpointActionGroup.PackEnd (printMessageTip);

			vbox.PackStart (breakpointActionGroup);

			whenLabel = new Label (GettextCatalog.GetString ("When to Take Action")) {
				Font = vbox.Font.WithWeight (FontWeight.Bold)
			};
			vbox.PackStart (whenLabel);

			var whenToTakeActionRadioGroup = new VBox {
				MarginLeft = 12
			};
			whenToTakeActionRadioGroup.Accessible.Role = Xwt.Accessibility.Role.Filler;

			// Function group
			{
				whenToTakeActionRadioGroup.PackStart (stopOnFunction);

				hboxFunction.PackStart (entryFunctionName, true);
				hboxFunction.PackEnd (warningFunction);

				whenToTakeActionRadioGroup.PackStart (hboxFunction);
			}

			// Exception group
			{
				whenToTakeActionRadioGroup.PackStart (stopOnException);

				hboxException = new HBox ();
				hboxException.Accessible.Role = Xwt.Accessibility.Role.Filler;

				hboxException.PackStart (entryExceptionType, true);
				hboxException.PackEnd (warningException);

				vboxException.PackStart (hboxException);
				vboxException.PackStart (checkIncludeSubclass);
				whenToTakeActionRadioGroup.PackStart (vboxException);
			}

			// Location group
			{
				whenToTakeActionRadioGroup.PackStart (stopOnLocation);

				hboxLocation.PackStart (entryLocationFile, true);
				hboxLocation.PackStart (warningLocation);
				vboxLocation.PackEnd (hboxLocation);

				whenToTakeActionRadioGroup.PackStart (vboxLocation);
			}
			vbox.PackStart (whenToTakeActionRadioGroup);

			advancedLabel = new Label (GettextCatalog.GetString ("Advanced Conditions")) {
				Font = vbox.Font.WithWeight (FontWeight.Bold)
			};
			vbox.PackStart (advancedLabel);

			var vboxAdvancedConditions = new VBox {
				MarginLeft = 30
			};
			vboxAdvancedConditions.Accessible.Role = Xwt.Accessibility.Role.Filler;

			var hboxHitCount = new HBox ();
			hboxHitCount.Accessible.Role = Xwt.Accessibility.Role.Filler;
			hboxHitCount.PackStart (ignoreHitType, true);
			hboxHitCount.PackStart (ignoreHitCount);
			vboxAdvancedConditions.PackStart (hboxHitCount);

			vboxAdvancedConditions.PackStart (conditionalHitType);
			hboxCondition = new HBox ();
			hboxCondition.Accessible.Role = Xwt.Accessibility.Role.Filler;

			hboxCondition.PackStart (entryConditionalExpression, true);
			hboxCondition.PackStart (warningCondition);
			vboxAdvancedConditions.PackStart (hboxCondition);

			conditionalExpressionTip.Accessible.Role = Xwt.Accessibility.Role.Filler;
			vboxAdvancedConditions.PackEnd (conditionalExpressionTip);

			vbox.PackStart (vboxAdvancedConditions);


			Buttons.Add (new DialogButton (Command.Cancel));
			Buttons.Add (buttonOk);

			Content = vbox;

			OnUpdateControls (null, null);
		}
		protected override void Dispose (bool disposing)
		{
			CompletionWindowManager.WindowShown -= HandleCompletionWindowShown;
			CompletionWindowManager.WindowClosed -= HandleCompletionWindowClosed;
			base.Dispose (disposing);
		}
	}
}
