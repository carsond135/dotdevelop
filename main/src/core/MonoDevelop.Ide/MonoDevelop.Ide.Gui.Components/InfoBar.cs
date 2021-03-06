//
// InfoBar.cs
//
// Author:
//       Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2018 Microsoft Inc.
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
using System.Collections.Immutable;
using System.Linq;
using Xwt;
using Xwt.Drawing;

namespace MonoDevelop.Ide.Gui.Components
{
	sealed class XwtInfoBar : Widget
	{
		static Image closeImage = Image.FromResource ("pad-close-9.png");
		static Image closeImageInactive = Image.FromResource ("pad-close-9.png").WithAlpha (0.5);

		readonly Label descriptionLabel;
		Action onDispose;
		Size minTextSize = Size.Zero;

		public XwtInfoBar (string description, Action onDispose, params InfoBarItem[] items)
		{
			items ??= Array.Empty<InfoBarItem> ();

			this.onDispose = onDispose;

			var mainBox = new HBox {
				BackgroundColor = Styles.NotificationBar.BarBackgroundColor,
				MinHeight = 30
			};

			mainBox.PackStart (new ImageView (ImageService.GetIcon (Stock.Information, Gtk.IconSize.Menu)), marginLeft: 11);
			mainBox.PackStart (descriptionLabel = new Label (description));

			int firstItem = 0;
			if (items.Length > 0 && items[0].Kind == InfoBarItemKind.Hyperlink) {
				firstItem = 1;
				var link = new InfoBarLink {
					Text = items [0].Title,
				};
				link.AddAction (items [0].Action);
				if (items [0].CloseAfter)
					link.AddAction (() => Dispose ());
				mainBox.PackStart (new Label ("–"));
				mainBox.PackStart (link);
			}

			var closeButton = new InfoBarCloseButton {
				Image = closeImageInactive,
				MarginRight = 9,
			};
			closeButton.AddAction (() => Dispose ());

			var widgets = new List<Widget> (items.Length);

			for (int i = items.Length - 1; i >= firstItem; i--) {
				var item = items [i];
				// TODO: abstract this into a factory.
				Widget toAdd = null;
				switch (item.Kind)
				{
				case InfoBarItemKind.Button:
					var btn = new InfoBarButton {
						Label = item.Title,
						LabelColor = Styles.NotificationBar.ButtonLabelColor,
						Style = ButtonStyle.Normal,

						MinWidth = 77,
					};

					btn.AddAction (item.Action);
					if (item.CloseAfter)
						btn.AddAction (() => Dispose ());
					toAdd = btn;
					break;
				// Creates a clickable hyperlink
				case InfoBarItemKind.Hyperlink:
					var link = new InfoBarLink {
						Text = item.Title,
					};
					link.AddAction (item.Action);
					if (item.CloseAfter)
						link.AddAction (() => Dispose ());
					toAdd = link;
					break;
				// We only have 1 close button, we attach all close actions to it
				case InfoBarItemKind.Close:
					closeButton.AddAction (item.Action);
					break;
				}

				if (toAdd != null)
					widgets.Add (toAdd);
			}

			mainBox.PackEnd (closeButton);
			foreach (var widget in widgets)
				mainBox.PackEnd (widget);

			if (IdeApp.Preferences == null || IdeApp.Preferences.UserInterfaceTheme == Theme.Light) {
				Content = new FrameBox (mainBox) {
					BorderWidthBottom = 1,
					BorderColor = Styles.NotificationBar.BarBorderColor,
				};
			} else {
				Content = mainBox;
			}
		}

		protected override void Dispose (bool disposing)
		{
			onDispose?.Invoke ();
			onDispose = null;

			base.Dispose (disposing);
		}

		protected override void OnBoundsChanged ()
		{
			if (minTextSize.IsZero && !string.IsNullOrEmpty (descriptionLabel.Text)) {
				var measureLayout = new TextLayout {
					Text = descriptionLabel.Text,
					Font = descriptionLabel.Font
				};
				minTextSize = measureLayout.GetSize ();
			}
			if (descriptionLabel.Size.Width < minTextSize.Width) {
				TooltipText = descriptionLabel.Text;
				descriptionLabel.Ellipsize = EllipsizeMode.End;
				descriptionLabel.ExpandHorizontal = true;
			} else {
				TooltipText = string.Empty;
				descriptionLabel.Ellipsize = EllipsizeMode.None;
				descriptionLabel.ExpandHorizontal = false;
			}
			base.OnBoundsChanged ();
		}

		sealed class InfoBarCloseButton : InfoBarButton
		{
			public InfoBarCloseButton ()
			{
				Style = ButtonStyle.Borderless;
				BackgroundColor = Styles.NotificationBar.BarBackgroundColor;
				ImagePosition = ContentPosition.Center;
				Opacity = 0.5;

				MouseEntered += (o, args) => ((InfoBarCloseButton)o).Image = closeImage;
				MouseExited += (o, args) => ((InfoBarCloseButton)o).Image = closeImageInactive;
			}
		}

		class InfoBarButton : Button
		{
			List<Action> actions = new List<Action> ();
			public void AddAction (Action action) => actions.Add (action);

			protected override void OnClicked (EventArgs e)
			{
				foreach (var action in actions)
					action?.Invoke ();
				base.OnClicked (e);
			}

			protected override void Dispose (bool disposing)
			{
				actions = null;
				base.Dispose (disposing);
			}
		}

		sealed class InfoBarLink : LinkLabel
		{
			List<Action> actions = new List<Action> ();
			public void AddAction (Action action) => actions.Add (action);

			protected override void OnNavigateToUrl (NavigateToUrlEventArgs e)
			{
				foreach (var action in actions)
					action?.Invoke ();
				e.SetHandled ();
				base.OnNavigateToUrl (e);
			}

			protected override void Dispose (bool disposing)
			{
				actions = null;
				base.Dispose (disposing);
			}
		}
	}
}
