// This file has been generated by the GUI designer. Do not modify.
namespace MonoDevelop.Ide.Gui.Dialogs
{
	internal partial class NewLayoutDialog
	{
		private global::Gtk.Alignment alignment1;
		private global::Gtk.VBox vbox2;
		private global::Gtk.HBox hbox45;
		private global::Gtk.Label label72;
		private global::Gtk.Entry layoutName;
		private global::Gtk.Label validationMessage;
		private global::Gtk.Button button309;
		private global::Gtk.Button newButton;

		protected virtual void Build ()
		{
			MonoDevelop.Components.Gui.Initialize (this);
			// Widget MonoDevelop.Ide.Gui.Dialogs.NewLayoutDialog
			this.Name = "MonoDevelop.Ide.Gui.Dialogs.NewLayoutDialog";
			this.Title = global::Mono.Unix.Catalog.GetString ("New Layout");
			this.TypeHint = ((global::Gdk.WindowTypeHint)(1));
			this.BorderWidth = ((uint)(6));
			this.Resizable = false;
			this.AllowGrow = false;
			// Internal child MonoDevelop.Ide.Gui.Dialogs.NewLayoutDialog.VBox
			global::Gtk.VBox w1 = this.VBox;
			w1.Name = "dialog-vbox4";
			w1.Spacing = 6;
			w1.BorderWidth = ((uint)(2));
			// Container child dialog-vbox4.Gtk.Box+BoxChild
			this.alignment1 = new global::Gtk.Alignment (0.5F, 0.5F, 1F, 1F);
			this.alignment1.Name = "alignment1";
			// Container child alignment1.Gtk.Container+ContainerChild
			this.vbox2 = new global::Gtk.VBox ();
			this.vbox2.Name = "vbox2";
			this.vbox2.Spacing = 6;
			this.vbox2.BorderWidth = ((uint)(6));
			// Container child vbox2.Gtk.Box+BoxChild
			this.hbox45 = new global::Gtk.HBox ();
			this.hbox45.Name = "hbox45";
			this.hbox45.Spacing = 6;
			// Container child hbox45.Gtk.Box+BoxChild
			this.label72 = new global::Gtk.Label ();
			this.label72.Name = "label72";
			this.label72.Xalign = 0F;
			this.label72.LabelProp = global::Mono.Unix.Catalog.GetString ("Layout name:");
			this.hbox45.Add (this.label72);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.hbox45 [this.label72]));
			w2.Position = 0;
			w2.Expand = false;
			w2.Fill = false;
			// Container child hbox45.Gtk.Box+BoxChild
			this.layoutName = new global::Gtk.Entry ();
			this.layoutName.WidthRequest = 320;
			this.layoutName.Name = "layoutName";
			this.layoutName.IsEditable = true;
			this.layoutName.ActivatesDefault = true;
			this.layoutName.InvisibleChar = '???';
			this.hbox45.Add (this.layoutName);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.hbox45 [this.layoutName]));
			w3.Position = 1;
			this.vbox2.Add (this.hbox45);
			global::Gtk.Box.BoxChild w4 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.hbox45]));
			w4.Position = 0;
			w4.Expand = false;
			w4.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.validationMessage = new global::Gtk.Label ();
			this.validationMessage.Name = "validationMessage";
			this.validationMessage.Xalign = 0F;
			this.vbox2.Add (this.validationMessage);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.validationMessage]));
			w5.Position = 1;
			w5.Expand = false;
			w5.Fill = false;
			this.alignment1.Add (this.vbox2);
			w1.Add (this.alignment1);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(w1 [this.alignment1]));
			w7.Position = 0;
			w7.Expand = false;
			w7.Fill = false;
			// Internal child MonoDevelop.Ide.Gui.Dialogs.NewLayoutDialog.ActionArea
			global::Gtk.HButtonBox w8 = this.ActionArea;
			w8.Name = "GtkDialog_ActionArea";
			w8.Spacing = 6;
			w8.BorderWidth = ((uint)(5));
			w8.LayoutStyle = ((global::Gtk.ButtonBoxStyle)(4));
			// Container child GtkDialog_ActionArea.Gtk.ButtonBox+ButtonBoxChild
			this.button309 = new global::Gtk.Button ();
			this.button309.CanFocus = true;
			this.button309.Name = "button309";
			this.button309.UseStock = true;
			this.button309.UseUnderline = true;
			this.button309.Label = "gtk-cancel";
			this.AddActionWidget (this.button309, -6);
			global::Gtk.ButtonBox.ButtonBoxChild w9 = ((global::Gtk.ButtonBox.ButtonBoxChild)(w8 [this.button309]));
			w9.Expand = false;
			w9.Fill = false;
			// Container child GtkDialog_ActionArea.Gtk.ButtonBox+ButtonBoxChild
			this.newButton = new global::Gtk.Button ();
			this.newButton.CanDefault = true;
			this.newButton.CanFocus = true;
			this.newButton.Name = "newButton";
			this.newButton.UseUnderline = true;
			this.newButton.Label = global::Mono.Unix.Catalog.GetString ("Create _Layout");
			this.AddActionWidget (this.newButton, -5);
			global::Gtk.ButtonBox.ButtonBoxChild w10 = ((global::Gtk.ButtonBox.ButtonBoxChild)(w8 [this.newButton]));
			w10.Position = 1;
			w10.Expand = false;
			w10.Fill = false;
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.DefaultWidth = 459;
			this.DefaultHeight = 162;
			this.newButton.HasDefault = true;
			this.Hide ();
		}
	}
}
