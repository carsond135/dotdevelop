// DebugValueWindow.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
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
//
//

// Note: This is only used by the old (Gtk) TextEditor.

using System;

using Gdk;
using Gtk;

using Mono.Debugging.Client;

using MonoDevelop.Ide;
using MonoDevelop.Core;
using MonoDevelop.Components;

namespace MonoDevelop.Debugger
{
	[Obsolete ("This API is only used by the old Gtk TextEditor")]
	class DebugValueWindow : PopoverWindow
	{
		readonly bool useNewTreeView = PropertyService.Get ("MonoDevelop.Debugger.UseNewTreeView", true);
		readonly ObjectValueTreeViewController controller;
		readonly ObjectValueTreeView objValueTreeView;
		readonly TreeView treeView;
		readonly ScrolledWindow sw;

		static readonly string innerTreeName = "MonoDevelop.SourceEditor.DebugValueWindow.ObjectValueTreeView";
		static string currentBgColor;

		static DebugValueWindow ()
		{
			UpdateTreeStyle (Ide.Gui.Styles.PopoverWindow.DefaultBackgroundColor.ToCairoColor ());
			Ide.Gui.Styles.Changed += (sender, e) => UpdateTreeStyle (Ide.Gui.Styles.PopoverWindow.DefaultBackgroundColor.ToCairoColor ());
		}

		static void UpdateTreeStyle (Cairo.Color newBgColor)
		{
			string oddRowColor, bgColor;

			bgColor = CairoExtensions.ColorGetHex (newBgColor);
			if (bgColor == currentBgColor)
				return;

			if (IdeApp.Preferences.UserInterfaceTheme == Ide.Theme.Light)
				oddRowColor = CairoExtensions.ColorGetHex (newBgColor.AddLight (-0.02));
			else
				oddRowColor = CairoExtensions.ColorGetHex (newBgColor.AddLight (-0.02));

			string rc = "style \"" + innerTreeName + "\" = \"treeview\" {\n";
			rc += string.Format ("GtkTreeView::odd-row-color = \"{0}\"\n", oddRowColor);
			rc += string.Format ("base[NORMAL] = \"{0}\"\n", bgColor);
			rc += "\n}\n";
			rc += string.Format ("widget \"*.{0}\" style \"{0}\" ", innerTreeName);

			Rc.ParseString (rc);
			currentBgColor = bgColor;
		}

		public DebugValueWindow (Gtk.Window transientFor, PinnedWatchLocation location, StackFrame frame, ObjectValue value, PinnedWatch watch) : base (Gtk.WindowType.Toplevel)
		{
			TypeHint = WindowTypeHint.PopupMenu;
			AllowShrink = false;
			AllowGrow = false;
			Decorated = false;

			TransientFor = transientFor;
			// Avoid getting the focus when the window is shown. We'll get it when the mouse enters the window
			AcceptFocus = false;

			sw = new ScrolledWindow {
				HscrollbarPolicy = PolicyType.Never,
				VscrollbarPolicy = PolicyType.Never
			};

			UpdateTreeStyle (Theme.BackgroundColor);

			if (useNewTreeView) {
				controller = new ObjectValueTreeViewController ();
				controller.SetStackFrame (frame);
				controller.AllowEditing = true;
				controller.PinnedWatch = watch;
				controller.PinnedWatchLocation = location;

				treeView = controller.GetGtkControl (ObjectValueTreeViewFlags.TooltipFlags);

				if (treeView is IObjectValueTreeView ovtv) {
					ovtv.StartEditing += OnStartEditing;
					ovtv.EndEditing += OnEndEditing;
					ovtv.NodePinned += OnPinStatusChanged;
				}

				controller.AddValue (value);
			} else {
				objValueTreeView = new ObjectValueTreeView ();
				objValueTreeView.RootPinAlwaysVisible = true;
				objValueTreeView.HeadersVisible = false;
				objValueTreeView.AllowEditing = true;
				objValueTreeView.AllowPinning = true;
				objValueTreeView.CompactView = true;
				objValueTreeView.PinnedWatch = watch;
				objValueTreeView.PinnedWatchLocation = location;
				objValueTreeView.Frame = frame;

				objValueTreeView.AddValue (value);

				objValueTreeView.PinStatusChanged += OnPinStatusChanged;
				objValueTreeView.StartEditing += OnStartEditing;
				objValueTreeView.EndEditing += OnEndEditing;

				treeView = objValueTreeView;
			}

			treeView.Name = innerTreeName;
			treeView.Selection.UnselectAll ();
			treeView.SizeAllocated += OnTreeSizeChanged;

			sw.Add (treeView);
			ContentBox.Add (sw);
			sw.ShowAll ();

			ShowArrow = true;
			Theme.CornerRadius = 3;
			PreviewWindowManager.WindowClosed += PreviewWindowManager_WindowClosed;
		}

		public DebuggerSession GetDebuggerSession ()
		{
			if (useNewTreeView)
				return controller.GetStackFrame ()?.DebuggerSession;

			return objValueTreeView.Frame?.DebuggerSession;
		}

		void OnStartEditing (object sender, EventArgs args)
		{
			Modal = true;
		}

		void OnEndEditing (object sender, EventArgs args)
		{
			Modal = false;
		}

		void OnPinStatusChanged (object sender, EventArgs args)
		{
			Destroy ();
		}

		protected override void OnDestroyed ()
		{
			if (useNewTreeView) {
				if (treeView is IObjectValueTreeView ovtv) {
					ovtv.StartEditing -= OnStartEditing;
					ovtv.EndEditing -= OnEndEditing;
					ovtv.NodePinned -= OnPinStatusChanged;
				}
			} else {
				objValueTreeView.PinStatusChanged -= OnPinStatusChanged;
				objValueTreeView.StartEditing -= OnStartEditing;
				objValueTreeView.EndEditing -= OnEndEditing;
			}

			treeView.SizeAllocated -= OnTreeSizeChanged;

			PreviewWindowManager.WindowClosed -= PreviewWindowManager_WindowClosed;

			base.OnDestroyed ();
		}

		protected override bool OnEnterNotifyEvent (EventCrossing evnt)
		{
			if (!AcceptFocus)
				AcceptFocus = true;
			return base.OnEnterNotifyEvent (evnt);
		}

		void OnTreeSizeChanged (object s, SizeAllocatedArgs a)
		{
			int x, y, w, h;
			GetPosition (out x, out y);
			h = (int)sw.Vadjustment.Upper;
			w = (int)sw.Hadjustment.Upper;
			int dy = y + h - Screen.Height;
			int dx = x + w - Screen.Width;

			if (dy > 0 && sw.VscrollbarPolicy == PolicyType.Never) {
				sw.VscrollbarPolicy = PolicyType.Always;
				sw.HeightRequest = h - dy - 20;
			} else if (sw.VscrollbarPolicy == PolicyType.Always && sw.Vadjustment.Upper == sw.Vadjustment.PageSize) {
				sw.VscrollbarPolicy = PolicyType.Never;
				sw.HeightRequest = -1;
			}

			if (dx > 0 && sw.HscrollbarPolicy == PolicyType.Never) {
				sw.HscrollbarPolicy = PolicyType.Always;
				sw.WidthRequest = w - dx - 20;
			} else if (sw.HscrollbarPolicy == PolicyType.Always && sw.Hadjustment.Upper == sw.Hadjustment.PageSize) {
				sw.HscrollbarPolicy = PolicyType.Never;
				sw.WidthRequest = -1;
			}
			// Force a redraw of the whole window. This is a workaround for bug 7538
			QueueDraw ();
		}

		protected override void OnSizeAllocated (Rectangle allocation)
		{
			if (Platform.IsMac || Platform.IsWindows) {
				// fails on linux see: Bug 8481 - Debug value tooltips very often appear at the top-left corner of the screen instead of near the element to inspect 
				const int edgeGap = 2;
				int oldY, x, y;

				GetPosition (out x, out y);
				oldY = y;

				var geometry = IdeServices.DesktopService.GetUsableMonitorGeometry (Screen.Number, Screen.GetMonitorAtPoint (x, y));
				int top = (int)geometry.Top;
				if (allocation.Height <= geometry.Height && y + allocation.Height >= geometry.Y + geometry.Height - edgeGap)
					y = top + ((int)geometry.Height - allocation.Height - edgeGap);
				if (y < top + edgeGap)
					y = top + edgeGap;

				if (y != oldY) {
					Move (x, y);
					// If the window is moved, hide the arrow since it will be pointing to the wrong place
					ShowArrow = false;
				}
			}
			base.OnSizeAllocated (allocation);
		}

		void PreviewWindowManager_WindowClosed (object sender, EventArgs e)
		{
			// When Preview window is closed we want to put focus(IsActive=true) back on DebugValueWindow
			// otherwise CommandManager will think IDE doesn't have any window Active/Focused and think
			// user switched to another app and DebugValueWindow will closed itself on "FocusOut" event
			Present ();
		}
	}
}
