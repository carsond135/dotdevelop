//
// LogWidget.cs
//
// Author:
//       Mike Krüger <mkrueger@novell.com>
//
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
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
using Gtk;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide;
using System.Text;
using System.Threading;
using MonoDevelop.Components;
using Mono.TextEditor;
using System.Linq;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Fonts;
using Humanizer;
using System.Diagnostics;
using System.Threading.Tasks;
using MonoDevelop.Ide.Gui.Documents;

namespace MonoDevelop.VersionControl.Views
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class LogWidget : Gtk.Bin
	{
		Revision[] history;
		public Revision[] History {
			get {
				return history;
			}
			set {
				history = value;
				UpdateHistory ();
			}
		}

		ListStore logstore = new ListStore (typeof (Revision), typeof(string));
		TreeView treeviewFiles;
		TreeStore changedpathstore;
		DocumentToolButton revertButton, revertToButton, refreshButton;
		SearchEntry searchEntry;
		string currentFilter;

		VersionControlDocumentInfo info;
		string preselectFile;
		CellRendererText messageRenderer = new CellRendererText ();
		CellRendererText textRenderer = new CellRendererText ();
		CellRendererImage pixRenderer = new CellRendererImage ();

		bool currentRevisionShortened;

		Xwt.Menu popupMenu;
		DiffRendererWidget diffRenderer;

		class RevisionGraphCellRenderer : Gtk.CellRenderer
		{
			public bool FirstNode {
				get;
				set;
			}

			public bool LastNode {
				get;
				set;
			}

			public override void GetSize (Widget widget, ref Gdk.Rectangle cell_area, out int x_offset, out int y_offset, out int width, out int height)
			{
				x_offset = y_offset = 0;
				width = 16;
				height = cell_area.Height;
			}

			protected override void Render (Gdk.Drawable window, Widget widget, Gdk.Rectangle background_area, Gdk.Rectangle cell_area, Gdk.Rectangle expose_area, CellRendererState flags)
			{
				using (Cairo.Context cr = Gdk.CairoHelper.Create (window)) {
					cr.LineWidth = 2.0;
					double center_x = cell_area.X + Math.Round ((double) (cell_area.Width / 2d));
					double center_y = cell_area.Y + Math.Round ((double) (cell_area.Height / 2d));
					cr.Arc (center_x, center_y, 5, 0, 2 * Math.PI);
					var state = StateType.Normal;
					if (!base.Sensitive)
						state = StateType.Insensitive;
					else if (flags.HasFlag (CellRendererState.Selected)) {
						if (widget.HasFocus)
							state = StateType.Selected;
						else
							state = StateType.Active;
					}
					else if (flags.HasFlag (CellRendererState.Prelit))
						state = StateType.Prelight;
					else if (widget.State == StateType.Insensitive)
						state = StateType.Insensitive;

					cr.SetSourceColor (widget.Style.Text (state).ToCairoColor ());
					cr.Stroke ();
					if (!FirstNode) {
						cr.MoveTo (center_x, cell_area.Y - 2);
						cr.LineTo (center_x, center_y - 5);
						cr.Stroke ();
					}

					if (!LastNode) {
						cr.MoveTo (center_x, cell_area.Y + cell_area.Height + 2);
						cr.LineTo (center_x, center_y + 5);
						cr.Stroke ();
					}
				}
			}
		}

		public LogWidget (VersionControlDocumentInfo info)
		{
			this.Build ();
			this.info = info;
			if (info.Document != null)
				this.preselectFile = info.Item.Path;

			var separator = new HeaderBox ();
			separator.SetMargins (1, 0, 0, 0);
			separator.HeightRequest = 4;
			separator.ShowAll ();

			hpaned1 = hpaned1.ReplaceWithWidget (new HPanedThin (), true);
			vpaned1 = vpaned1.ReplaceWithWidget (new VPanedThin () { HandleWidget = separator }, true);

			revertButton = new DocumentToolButton ("vc-revert-command", GettextCatalog.GetString ("Revert changes from this revision"));
			revertButton.GetNativeWidget<Gtk.Widget> ().Sensitive = false;
			revertButton.Clicked += new EventHandler (RevertRevisionClicked);

			revertToButton = new DocumentToolButton ("vc-revert-command", GettextCatalog.GetString ("Revert to this revision"));
			revertToButton.GetNativeWidget<Gtk.Widget> ().Sensitive = false;
			revertToButton.Clicked += new EventHandler (RevertToRevisionClicked);

			refreshButton = new DocumentToolButton (Gtk.Stock.Refresh, GettextCatalog.GetString ("Refresh"));
			refreshButton.Clicked += new EventHandler (RefreshClicked);

			searchEntry = new SearchEntry ();
			searchEntry.WidthRequest = 200;
			searchEntry.ForceFilterButtonVisible = true;
			searchEntry.EmptyMessage = GettextCatalog.GetString ("Search");
			searchEntry.Changed += HandleSearchEntryFilterChanged;
			searchEntry.Ready = true;
			searchEntry.Show ();

			messageRenderer.Ellipsize = Pango.EllipsizeMode.End;
			TreeViewColumn colRevMessage = new TreeViewColumn ();
			colRevMessage.Title = GettextCatalog.GetString ("Message");
			var graphRenderer = new RevisionGraphCellRenderer ();
			colRevMessage.PackStart (graphRenderer, false);
			colRevMessage.SetCellDataFunc (graphRenderer, GraphFunc);

			colRevMessage.PackStart (messageRenderer, true);
			colRevMessage.SetCellDataFunc (messageRenderer, MessageFunc);
			colRevMessage.Sizing = TreeViewColumnSizing.Autosize;

			treeviewLog.AppendColumn (colRevMessage);
			colRevMessage.MinWidth = 350;
			colRevMessage.Resizable = true;

			TreeViewColumn colRevDate = new TreeViewColumn (GettextCatalog.GetString ("Date"), textRenderer);
			colRevDate.SetCellDataFunc (textRenderer, DateFunc);
			colRevDate.Resizable = true;
			treeviewLog.AppendColumn (colRevDate);

			TreeViewColumn colRevAuthor = new TreeViewColumn ();
			colRevAuthor.Title = GettextCatalog.GetString ("Author");
			colRevAuthor.PackStart (pixRenderer, false);
			colRevAuthor.PackStart (textRenderer, true);
			colRevAuthor.SetCellDataFunc (textRenderer, AuthorFunc);
			colRevAuthor.SetCellDataFunc (pixRenderer, AuthorIconFunc);
			colRevAuthor.Resizable = true;
			treeviewLog.AppendColumn (colRevAuthor);

			TreeViewColumn colRevNum = new TreeViewColumn (GettextCatalog.GetString ("Revision"), textRenderer);
			colRevNum.SetCellDataFunc (textRenderer, RevisionFunc);
			colRevNum.Resizable = true;
			treeviewLog.AppendColumn (colRevNum);

			treeviewLog.Model = logstore;
			treeviewLog.Selection.Changed += TreeSelectionChanged;

			treeviewFiles = new FileTreeView ();

			scrolledwindowFiles.Child = treeviewFiles;
			scrolledwindowFiles.ShowAll ();

			diffRenderer = new DiffRendererWidget ();
			diffRenderer.DiffLineActivated += HandleTreeviewFilesDiffLineActivated;
			scrolledwindowFileContents.AddWithViewport (diffRenderer);
			scrolledwindowFileContents.ShowAll ();

			changedpathstore = new TreeStore (typeof(Xwt.Drawing.Image), typeof (string), // icon/file name
			                                  typeof(Xwt.Drawing.Image), typeof (string), // icon/operation
				typeof (string), // path
				typeof (string), // revision path (invisible)
				typeof (string []) // diff
				);

			TreeViewColumn colChangedFile = new TreeViewColumn ();
			var crp = new CellRendererImage ();
			var crt = new CellRendererText ();
			colChangedFile.Title = GettextCatalog.GetString ("File");
			colChangedFile.PackStart (crp, false);
			colChangedFile.PackStart (crt, true);
			colChangedFile.SetCellDataFunc (crp, HandleNodeCellDataFunc);
			colChangedFile.AddAttribute (crt, "text", 3);
			treeviewFiles.AppendColumn (colChangedFile);

			TreeViewColumn colOperation = new TreeViewColumn ();
			colOperation.Title = GettextCatalog.GetString ("Operation");
			colOperation.PackStart (crp, false);
			colOperation.PackStart (crt, true);
			colOperation.AddAttribute (crp, "image", 0);
			colOperation.AddAttribute (crt, "text", 1);
			treeviewFiles.AppendColumn (colOperation);

			TreeViewColumn colChangedPath = new TreeViewColumn ();
			colChangedPath.Title = GettextCatalog.GetString ("Path");



			treeviewFiles.Model = changedpathstore;
			treeviewFiles.Selection.Changed += SetDiff;

			textviewDetails.WrapMode = Gtk.WrapMode.Word;
			textviewDetails.AddEvents ((int)Gdk.EventMask.ButtonPressMask);
			textviewDetails.ButtonPressEvent += TextviewDetails_ButtonPressEvent;

			labelAuthor.Text = "";
			labelDate.Text = "";
			labelRevision.Text = "";

			labelDate.AddEvents ((int)Gdk.EventMask.ButtonPressMask);
			labelDate.ButtonPressEvent += LabelDate_ButtonPressEvent;

			labelAuthor.AddEvents ((int)Gdk.EventMask.ButtonPressMask);
			labelAuthor.ButtonPressEvent += LabelAuthor_ButtonPressEvent;

			labelRevision.AddEvents ((int)Gdk.EventMask.ButtonPressMask);
			labelRevision.ButtonPressEvent += LabelRevision_ButtonPressEvent;

			vbox2.Remove (scrolledwindow1);
			HeaderBox tb = new HeaderBox ();
			tb.Show ();
			tb.SetMargins (1, 0, 0, 0);
			tb.ShowTopShadow = true;
			tb.ShadowSize = 4;
			tb.SetPadding (8, 8, 8, 8);
			tb.UseChildBackgroundColor = true;
			tb.Add (scrolledwindow1);
			vbox2.PackStart (tb, true, true, 0);

			(Platform.IsMac ? Xwt.Toolkit.NativeEngine : Xwt.Toolkit.CurrentEngine).Invoke (() => {
				popupMenu = new Xwt.Menu ();
				var copyItem = new Xwt.MenuItem (GettextCatalog.GetString ("Copy"));
				popupMenu.Items.Add (copyItem);
				copyItem.Clicked += (sender, e) => {
					var selectedText = GetSelectedText ();
					if (!string.IsNullOrEmpty (selectedText))
						LogView.CopyToClipboard (selectedText);
				};
			});

			UpdateStyle ();
			Ide.Gui.Styles.Changed += HandleStylesChanged;
		}

		static void HandleNodeCellDataFunc (TreeViewColumn tree_column, CellRenderer cell, TreeModel tree_model, TreeIter iter)
		{
			var cri = (CellRendererImage)cell;
			var image = tree_model.GetValue (iter, 2) as Xwt.Drawing.Image;
			cri.Visible = image != null;
			if (image != null)
				cri.Image = image;
		}

		[GLib.ConnectBeforeAttribute]
		void LabelRevision_ButtonPressEvent (object o, ButtonPressEventArgs args)
		{
			if (args.Event.IsContextMenuButton ()) {
				if (currentRevisionShortened) {
					Revision d = SelectedRevision;
					labelRevision.Text = GettextCatalog.GetString ("Revision: {0}", d.Name);
					currentRevisionShortened = false;
				}
				return;
			}
			PopulateLabelMenuAndRaisePopup (labelRevision, args);
		}

		[GLib.ConnectBeforeAttribute]
		void LabelAuthor_ButtonPressEvent (object o, ButtonPressEventArgs args)
		{
			PopulateLabelMenuAndRaisePopup (labelAuthor, args);
		}

		[GLib.ConnectBeforeAttribute]
		void LabelDate_ButtonPressEvent (object o, ButtonPressEventArgs args)
		{
			PopulateLabelMenuAndRaisePopup (labelDate, args);
		}

		void PopulateLabelMenuAndRaisePopup (Label label, ButtonPressEventArgs args)
		{
			if (args.Event.IsContextMenuButton ()) {
				var selectedText = GetSelectedTextFromLabel (label);
				if (string.IsNullOrEmpty (selectedText)) {
					args.RetVal = true;
					return;
				}
				PopulateMenuAndRaisePopup (label, selectedText, args);
			}
		}

		[GLib.ConnectBeforeAttribute]
		void TextviewDetails_ButtonPressEvent (object o, ButtonPressEventArgs args)
		{
			if (args.Event.IsContextMenuButton ()) {
				var selectedText = GetSelectedTextFromTextView (textviewDetails);
				if (string.IsNullOrEmpty (selectedText)) {
					args.RetVal = true;
					return;
				}
				PopulateMenuAndRaisePopup (textviewDetails, selectedText, args);
			}
		}

		void PopulateMenuAndRaisePopup (Gtk.Widget gtkWidget, string selectedText, ButtonPressEventArgs args)
		{
			popupMenu.Popup ();
			args.RetVal = true;
		}

		protected override void OnRealized ()
		{
			base.OnRealized ();
			UpdateStyle ();
		}

		void HandleStylesChanged (object sender, EventArgs e)
		{
			UpdateStyle ();
		}

		void UpdateStyle ()
		{
			var c = Style.Base (StateType.Normal).ToXwtColor ();
			c.Light *= 0.8;
			commitBox.ModifyBg (StateType.Normal, c.ToGdkColor ());

			var tcol = Styles.LogView.CommitDescBackgroundColor.ToGdkColor ();
			textviewDetails.ModifyBase (StateType.Normal, tcol);
			scrolledwindow1.ModifyBase (StateType.Normal, tcol);
		}

		internal void SetToolbar (DocumentToolbar toolbar)
		{
			if (info.Repository.SupportsRevertRevision)
				toolbar.Add (revertButton);

			if (info.Repository.SupportsRevertToRevision)
				toolbar.Add (revertToButton);
			toolbar.Add (refreshButton);

			Gtk.HBox a = new Gtk.HBox ();
			a.PackEnd (searchEntry, false, false, 0);
			toolbar.Add (a, true);

			toolbar.ShowAll ();
		}

		static void SetLogSearchFilter (ListStore store, string filter)
		{
			TreeIter iter;
			if (store.GetIterFirst (out iter))
				store.SetValue (iter, 1, filter);
		}

		bool filtering;
		void HandleSearchEntryFilterChanged (object sender, EventArgs e)
		{
			if (filtering)
				return;
			filtering = true;
			GLib.Timeout.Add (100, delegate {
				filtering = false;
				currentFilter = searchEntry.Entry.Text;
				SetLogSearchFilter (logstore, currentFilter);
				UpdateHistory ();
				return false;
			});
		}

		public void ShowLoading ()
		{
			scrolledLoading.Show ();
			scrolledLog.Hide ();
		}

		async void RevertToRevisionClicked (object src, EventArgs args)
		{
			Revision d = SelectedRevision;
			if (await RevertRevisionsCommands.RevertRevisionAsync (info.Repository, info.Item.Path, d, false))
				VersionControlService.SetCommitComment (info.Item.Path,
				  GettextCatalog.GetString ("(Revert to revision {0})", d.ToString ()), true);
		}

		async void RevertRevisionClicked (object src, EventArgs args)
		{
			try {
				Revision d = SelectedRevision;
				if (await RevertRevisionsCommands.RevertRevisionAsync (info.Repository, info.Item.Path, d, false))
					VersionControlService.SetCommitComment (info.Item.Path,
					  GettextCatalog.GetString ("(Revert revision {0})", d.ToString ()), true);
			} catch (Exception e) {
				LoggingService.LogInternalError (e);
			}
		}

		void RefreshClicked (object src, EventArgs args)
		{
			ShowLoading ();
			info.Start (true);
			revertButton.GetNativeWidget<Gtk.Widget> ().Sensitive = revertToButton.GetNativeWidget<Gtk.Widget> ().Sensitive = false;
		}

		async void HandleTreeviewFilesDiffLineActivated (object sender, int line)
		{
			TreePath[] paths = treeviewFiles.Selection.GetSelectedRows ();

			if (paths.Length != 1)
				return;

			TreeIter iter;
			changedpathstore.GetIter (out iter, paths[0]);

			string fileName = (string)changedpathstore.GetValue (iter, colPath);
			var proj = IdeApp.Workspace.GetProjectsContainingFile (fileName).FirstOrDefault ();
			var doc = await IdeApp.Workbench.OpenDocument (fileName, proj, line, 0, OpenDocumentOptions.Default | OpenDocumentOptions.OnlyInternalViewer);
			doc?.GetContent<VersionControlDocumentController> ()?.ShowDiffView (await SelectedRevision.GetPreviousAsync (), SelectedRevision, line);
		}
		
		const int colFile = 3;
		const int colOperation = 4;
		const int colOperationText = 1;
		const int colPath = 5;
		const int colDiff = 6;


		void SetDiff (object o, EventArgs args)
		{
			this.diffRenderer.Lines = null;
			this.scrolledwindowFileContents.Accessible.Description = GettextCatalog.GetString ("empty");

			if (!this.treeviewFiles.Selection.GetSelected (out var model, out var iter)) {
				labelFilePathName.Text = "";
				return;
			}
			this.diffRenderer.Lines = new string [] { GettextCatalog.GetString ("Loading data…") };
			FilePath path = (string)changedpathstore.GetValue (iter, colPath);
			FilePath personal = Environment.GetFolderPath (Environment.SpecialFolder.Personal);

			labelFilePathName.Text = path.IsChildPathOf (personal) ? "~/" + path.ToRelative (personal) : path.ToString ();
			var rev = SelectedRevision;
			Task.Run (async delegate {
				string text = "";
				try {
					text = await info.Repository.GetTextAtRevisionAsync (path, rev);
				} catch (Exception e) {
					await Runtime.RunInMainThread (delegate {
						LoggingService.LogError ("Error while getting revision text", e);
						MessageService.ShowError (
							GettextCatalog.GetString ("Error while getting revision text."),
							GettextCatalog.GetString ("The file may not be part of the working copy.")
						);
					});
					return;
				}
				Revision prevRev = null;
				try {
					prevRev = await rev.GetPreviousAsync ();
				} catch (Exception e) {
					await Runtime.RunInMainThread (delegate {
						MessageService.ShowError (GettextCatalog.GetString ("Error while getting previous revision."), e);
					});
					return;
				}
				string[] lines;
				// Indicator that the file was binary
				if (text == null) {
					lines = new [] { GettextCatalog.GetString (" Binary files differ") };
				} else {
					var changedDocument = Mono.TextEditor.TextDocument.CreateImmutableDocument (text);
					if (prevRev == null) {
						lines = new string [changedDocument.LineCount];
						for (int i = 0; i < changedDocument.LineCount; i++) {
							lines[i] = "+ " + changedDocument.GetLineText (i + 1).TrimEnd ('\r','\n');
						}
					} else {
						string prevRevisionText = "";
						try {
							prevRevisionText = await info.Repository.GetTextAtRevisionAsync (path, prevRev);
						} catch (Exception e) {
							Application.Invoke ((o2, a2) => {
								LoggingService.LogError ("Error while getting revision text", e);
								MessageService.ShowError (
									GettextCatalog.GetString ("Error while getting revision text."),
									GettextCatalog.GetString ("The file may not be part of the working copy.")
								);
							});
							return;
						}

						if (string.IsNullOrEmpty (text) && !string.IsNullOrEmpty (prevRevisionText)) {
							lines = new string [changedDocument.LineCount];
							for (int i = 0; i < changedDocument.LineCount; i++) {
								lines [i] = "- " + changedDocument.GetLineText (i + 1).TrimEnd ('\r', '\n');
							}
						}

						var originalDocument = Mono.TextEditor.TextDocument.CreateImmutableDocument (prevRevisionText);
						originalDocument.FileName = GettextCatalog.GetString ("Revision {0}", prevRev);
						changedDocument.FileName = GettextCatalog.GetString ("Revision {0}", rev);
						lines = Mono.TextEditor.Utils.Diff.GetDiffString (originalDocument, changedDocument).Split ('\n');
					}
				}
				await Runtime.RunInMainThread (delegate {
					this.diffRenderer.Lines = lines;
					this.scrolledwindowFileContents.Accessible.Description = GettextCatalog.GetString ("file {0}", path);
					changedpathstore.SetValue (iter, colDiff, lines);
				});
			});
		}

		/*		void FileSelectionChanged (object sender, EventArgs e)
				{
					Revision rev = SelectedRevision;
					if (rev == null) {
						diffWidget.ComparisonWidget.OriginalEditor.Text = "";
						diffWidget.ComparisonWidget.DiffEditor.Text = "";
						return;
					}
					TreeIter iter;
					if (!treeviewFiles.Selection.GetSelected (out iter))
						return;
					string path = (string)changedpathstore.GetValue (iter, colPath);
					ThreadPool.QueueUserWorkItem (delegate {
						string text = info.Repository.GetTextAtRevision (path, rev);
						string prevRevision = text; // info.Repository.GetTextAtRevision (path, rev.GetPrevious ());

						Application.Invoke (delegate {
							diffWidget.ComparisonWidget.MimeType = IdeServices.DesktopService.GetMimeTypeForUri (path);
							diffWidget.ComparisonWidget.OriginalEditor.Text = prevRevision;
							diffWidget.ComparisonWidget.DiffEditor.Text = text;
							diffWidget.ComparisonWidget.CreateDiff ();
						});
					});
				}*/

		protected override void OnDestroyed ()
		{
			IsDestroyed = true;
			selectionCancellationTokenSource.Cancel ();

			treeviewFiles.Selection.Changed -= SetDiff;

			diffRenderer.DiffLineActivated -= HandleTreeviewFilesDiffLineActivated;

			textviewDetails.ButtonPressEvent -= TextviewDetails_ButtonPressEvent;
			labelDate.ButtonPressEvent -= LabelDate_ButtonPressEvent;

			labelAuthor.ButtonPressEvent -= LabelAuthor_ButtonPressEvent;
			labelRevision.ButtonPressEvent -= LabelRevision_ButtonPressEvent;

			revertButton.Clicked -= RevertRevisionClicked;
			revertToButton.Clicked -= RevertToRevisionClicked;
			refreshButton.Clicked -= RefreshClicked;
			Ide.Gui.Styles.Changed -= HandleStylesChanged;

			diffRenderer.Dispose ();
			messageRenderer.Dispose ();
			textRenderer.Dispose ();
			treeviewFiles.Dispose ();

			popupMenu.Dispose ();

			base.OnDestroyed ();
		}

		bool IsDestroyed { get; set; }

		static void DateFunc (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			var renderer = (CellRendererText)cell;
			var revision = (Revision)model.GetValue (iter, 0);
			// Grab today's day and the start of tomorrow's day to make Today/Yesterday calculations.
			var now = DateTime.Now;
			var age = new DateTime (now.Year, now.Month, now.Day).AddDays (1) - revision.Time;

			renderer.Text = age.Days >= 2 ?
				revision.Time.ToShortDateString () :
				revision.Time.Humanize (utcDate: false, dateToCompareAgainst: now);
		}

		static void GraphFunc (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			var renderer = (RevisionGraphCellRenderer)cell;
			Gtk.TreeIter node;
			model.GetIterFirst (out node);

			renderer.FirstNode = node.Equals (iter);
			model.IterNthChild (out node, model.IterNChildren () - 1);
			renderer.LastNode =  node.Equals (iter);
		}

		static string GetCurrentFilter (Gtk.TreeModel model)
		{
			TreeIter filterIter;
			string filter = string.Empty;
			if (model.GetIterFirst (out filterIter))
				filter = (string)model.GetValue (filterIter, 1);

			return filter;
		}

		static void MessageFunc (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			string filter = GetCurrentFilter (model);

			CellRendererText renderer = (CellRendererText)cell;
			var rev = (Revision)model.GetValue (iter, 0);
			if (string.IsNullOrEmpty (rev.Message)) {
				renderer.Text = GettextCatalog.GetString ("(No message)");
			} else {
				string message = RevisionHelpers.FormatMessage (rev.Message);
				int idx = message.IndexOf ('\n');
				if (idx > 0)
					message = message.Substring (0, idx);
				if (string.IsNullOrEmpty (filter))
					renderer.Text = message;
				else
					renderer.Markup = EscapeWithFilterMarker (message, filter);
			}
		}

		static void AuthorFunc (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			string filter = GetCurrentFilter (model);

			CellRendererText renderer = (CellRendererText)cell;
			var rev = (Revision)model.GetValue (iter, 0);
			string author = rev.Author;
			if (string.IsNullOrEmpty (author))
				return;
			int idx = author.IndexOf ("<", StringComparison.Ordinal);
			if (idx >= 0 && idx < author.IndexOf (">", StringComparison.Ordinal))
				author = author.Substring (0, idx).Trim ();
			if (string.IsNullOrEmpty (filter))
				renderer.Text = author;
			else
				renderer.Markup = EscapeWithFilterMarker (author, filter);
		}

		static void AuthorIconFunc (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			CellRendererImage renderer = (CellRendererImage)cell;
			var rev = (Revision)model.GetValue (iter, 0);
			if (string.IsNullOrEmpty (rev.Email))
				return;
			ImageLoader img = ImageService.GetUserIcon (rev.Email, 16);

			renderer.Image = img.Image;
			if (img.Downloading) {
				img.Completed += (sender, e) => {
					renderer.Image = img.Image;
					if (((ListStore)model).IterIsValid (iter))
						model.EmitRowChanged (model.GetPath (iter), iter);
				};
			}
		}

		static void RevisionFunc (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			string filter = GetCurrentFilter (model);

			CellRendererText renderer = (CellRendererText)cell;
			var rev = model.GetValue (iter, 0).ToString ();
			if (string.IsNullOrEmpty (filter))
				renderer.Text = rev;
			else
				renderer.Markup = EscapeWithFilterMarker (rev, filter);
		}

		static void SetDiffCellData (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			var rc = (CellRendererDiff)cell;
			var diffMode = (bool)model.GetValue (iter, 0);
			var lines = (string[])model.GetValue (iter, 1) ?? new string [] { "" };

			rc.InitCell (tree_column.TreeView, diffMode, lines, model.GetPath (iter));
		}

		protected override void OnSizeAllocated (Gdk.Rectangle allocation)
		{
			var old = Allocation;
			base.OnSizeAllocated (allocation);
			if (old.Width != allocation.Width || old.Height != allocation.Height) {
				hpaned1.Position = allocation.Width - 380;
				vpaned1.Position = allocation.Height / 2;
			}
		}

		public Revision SelectedRevision {
			get {
				TreeIter iter;
				if (!treeviewLog.Selection.GetSelected (out iter))
					return null;
				return (Revision)logstore.GetValue (iter, 0);
			}
			set {
				TreeIter iter;
				if (!treeviewLog.Model.GetIterFirst (out iter))
					return;
				do {
					var rev = (Revision)logstore.GetValue (iter, 0);
					if (rev.ToString () == value.ToString ()) {
						treeviewLog.Selection.SelectIter (iter);
						TreePath path = logstore.GetPath (iter);
						treeviewLog.ScrollToCell (path, treeviewLog.Columns[0], true, 0, 0);
						treeviewLog.SetCursorOnCell (path, treeviewLog.Columns[0], textRenderer, true);
						return;
					}
				} while (treeviewLog.Model.IterNext (ref iter));
			}
		}

		CancellationTokenSource selectionCancellationTokenSource = new CancellationTokenSource ();

		void TreeSelectionChanged (object o, EventArgs args)
		{
			Revision d = SelectedRevision;
			changedpathstore.Clear ();
			diffRenderer.Lines = null;
			textviewDetails.Buffer.Clear ();
			if (d == null)
				return;

			changedpathstore.AppendValues (null, null, null, GettextCatalog.GetString ("Retrieving history…"), null, null, null);

			selectionCancellationTokenSource.Cancel ();
			selectionCancellationTokenSource = new CancellationTokenSource ();
			var token = selectionCancellationTokenSource.Token;
			Task.Run (async () => await info.Repository.GetRevisionChangesAsync (d, token)).ContinueWith (result => {
				if (IsDestroyed)
					return;
				changedpathstore.Clear ();
				revertButton.GetNativeWidget<Gtk.Widget> ().Sensitive = revertToButton.GetNativeWidget<Gtk.Widget> ().Sensitive = true;
				Gtk.TreeIter selectIter = Gtk.TreeIter.Zero;
				bool select = false;
				foreach (RevisionPath rp in result.Result) {
					Xwt.Drawing.Image actionIcon;
					string action = null;
					if (rp.Action == RevisionAction.Add) {
						action = GettextCatalog.GetString ("Add");
						actionIcon = ImageService.GetIcon (Gtk.Stock.Add, Gtk.IconSize.Menu);
					} else if (rp.Action == RevisionAction.Delete) {
						action = GettextCatalog.GetString ("Delete");
						actionIcon = ImageService.GetIcon (Gtk.Stock.Remove, Gtk.IconSize.Menu);
					} else if (rp.Action == RevisionAction.Modify) {
						action = GettextCatalog.GetString ("Modify");
						actionIcon = ImageService.GetIcon ("gtk-edit", Gtk.IconSize.Menu);
					} else if (rp.Action == RevisionAction.Replace) {
						action = GettextCatalog.GetString ("Replace");
						actionIcon = ImageService.GetIcon ("gtk-edit", Gtk.IconSize.Menu);
					} else {
						action = rp.ActionDescription;
						actionIcon = ImageService.GetIcon (MonoDevelop.Ide.Gui.Stock.Empty, Gtk.IconSize.Menu);
					}
					Xwt.Drawing.Image fileIcon = IdeServices.DesktopService.GetIconForFile (rp.Path, Gtk.IconSize.Menu);
					var iter = changedpathstore.AppendValues (actionIcon, action, fileIcon, System.IO.Path.GetFileName (rp.Path), System.IO.Path.GetDirectoryName (rp.Path), rp.Path, null);
					if (rp.Path == preselectFile) {
						selectIter = iter;
						select = true;
					}
				}
				if (!string.IsNullOrEmpty (d.Email)) {
					imageUser.Show ();
					imageUser.LoadUserIcon (d.Email, 32);
				} else
					imageUser.Hide ();

				labelAuthor.Text = d.Author;
				labelDate.Text = d.Time.ToString ();
				string rev = d.Name;
				if (rev.Length > 15) {
					currentRevisionShortened = true;
					rev = d.ShortName;
				} else
					currentRevisionShortened = false;

				labelRevision.Text = GettextCatalog.GetString ("Revision: {0}", rev);
				textviewDetails.Buffer.Text = d.Message;

				if (select) {
					treeviewFiles.Selection.SelectIter (selectIter);
					treeviewFiles.ExpandRow (treeviewFiles.Model.GetPath (selectIter), true);
				}
			}, token, TaskContinuationOptions.OnlyOnRanToCompletion, Runtime.MainTaskScheduler);
		}

		void UpdateHistory ()
		{
			scrolledLoading.Hide ();
			scrolledLog.Show ();
			treeviewLog.FreezeChildNotify ();
			logstore.Clear ();
			var h = History;
			if (h == null)
				return;
			foreach (var rev in h) {
				if (MatchesFilter (rev))
					logstore.InsertWithValues (-1, rev, string.Empty);
			}
			SetLogSearchFilter (logstore, currentFilter);
			treeviewLog.ThawChildNotify ();
		}

		bool MatchesFilter (Revision rev)
		{
			if (string.IsNullOrEmpty (currentFilter))
				return true;
			if (rev.Author.IndexOf (currentFilter,StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;
			if (rev.Email.IndexOf (currentFilter,StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;
			if (rev.Message.IndexOf (currentFilter,StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;
			if (rev.Name.IndexOf (currentFilter,StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;
			if (rev.ShortName.IndexOf (currentFilter,StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;
			return false;
		}

		static string EscapeWithFilterMarker (string txt, string filter)
		{
			if (string.IsNullOrEmpty (filter))
				return GLib.Markup.EscapeText (txt);

			int i = txt.IndexOf (filter, StringComparison.CurrentCultureIgnoreCase);
			if (i == -1)
				return GLib.Markup.EscapeText (txt);

			StringBuilder sb = new StringBuilder ();
			int last = 0;
			while (i != -1) {
				sb.Append (GLib.Markup.EscapeText (txt.Substring (last, i - last)));
				sb.Append ("<span color='").Append (Styles.LogView.SearchSnippetTextColor).Append ("'>").Append (txt, i, filter.Length).Append ("</span>");
				last = i + filter.Length;
				i = txt.IndexOf (filter, last, StringComparison.CurrentCultureIgnoreCase);
			}
			if (last < txt.Length)
				sb.Append (GLib.Markup.EscapeText (txt.Substring (last, txt.Length - last)));
			return sb.ToString ();
		}

		static string GetSelectedTextFromLabel (Label label)
		{
			label.GetSelectionBounds (out int start, out int end);
			if (start != end) {
				return label.Text.Substring (start, end - start);
			}
			return null;
		}

		static string GetSelectedTextFromTextView (TextView textView)
		{
			textView.Buffer.GetSelectionBounds (out var A, out var B);
			return textView.Buffer.GetText (A, B, true);
		}

		internal string GetSelectedText ()
		{
			if (treeviewFiles.HasFocus ) {
				if (treeviewFiles.Selection.GetSelected (out var iter)) {
					if (changedpathstore.GetValue (iter, colDiff) is string [] items) {
						return string.Join (Environment.NewLine, items);
					}
					if (changedpathstore.GetValue (iter, colFile) is string file) {
						var path = changedpathstore.GetValue (iter, colPath) as string;
						var operation = changedpathstore.GetValue (iter, colOperationText) as string;
						return string.Format ("{0}, {1}, {2}", file, operation, path);
					}
				}
			}

			if (treeviewLog.HasFocus) {
				if (treeviewLog.Selection.GetSelected (out var iter)) {
					if (logstore.GetValue (iter, 0) is Revision revision) {
						return string.Format ("{0}, {1}, {2}, {3}", revision.ShortMessage, revision.Time, revision.Author, revision.Name);
					}
				}
			}

			if (textviewDetails.HasFocus) {
				return GetSelectedTextFromTextView (textviewDetails);
			}

			if (labelDate.HasFocus) {
				return GetSelectedTextFromLabel (labelDate);
			}

			if (labelAuthor.HasFocus) {
				return GetSelectedTextFromLabel (labelAuthor);
			}

			if (labelRevision.HasFocus) {
				return GetSelectedTextFromLabel (labelRevision);
			}

			return null;
		}
	}
}
