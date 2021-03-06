// This file has been generated by the GUI designer. Do not modify.
namespace MonoDevelop.Ide.Projects
{
	internal partial class NewFileDialog
	{
		private global::Gtk.VBox vbox2;
		
		private global::Gtk.HPaned hpaned1;
		
		private global::Gtk.ScrolledWindow scrolledwindow1;
		
		private global::Gtk.TreeView catView;
		
		private global::Gtk.HPaned panedTemplates;
		
		private global::Gtk.VBox boxTemplates;
		
		private global::Gtk.ScrolledWindow scrolledInfo;
		
		private global::Gtk.VBox boxInfo;
		
		private global::Gtk.Label labelTemplateTitle;
		
		private global::Gtk.Label infoLabel;
		
		private global::Gtk.Label label1;
		
		private global::Gtk.HBox hbox2;
		
		private global::Gtk.Label label2;
		
		private global::Gtk.Entry nameEntry;
		
		private global::Gtk.VBox boxProject;
		
		private global::Gtk.HBox hbox3;
		
		private global::Gtk.CheckButton projectAddCheckbox;
		
		private global::Gtk.ComboBox projectAddCombo;
		
		private global::Gtk.HBox hbox4;
		
		private global::Gtk.Label projectPathLabel;
		
		private global::MonoDevelop.Components.FolderEntry projectFolderEntry;
		
		private global::Gtk.Button cancelButton;
		
		private global::Gtk.Button okButton;

		protected virtual void Build ()
		{
			MonoDevelop.Components.Gui.Initialize (this);
			// Widget MonoDevelop.Ide.Projects.NewFileDialog
			this.Name = "MonoDevelop.Ide.Projects.NewFileDialog";
			this.Title = global::Mono.Unix.Catalog.GetString ("New File");
			this.WindowPosition = ((global::Gtk.WindowPosition)(4));
			this.BorderWidth = ((uint)(6));
			// Internal child MonoDevelop.Ide.Projects.NewFileDialog.VBox
			global::Gtk.VBox w1 = this.VBox;
			w1.Name = "dialog1_VBox";
			w1.Spacing = 6;
			w1.BorderWidth = ((uint)(2));
			// Container child dialog1_VBox.Gtk.Box+BoxChild
			this.vbox2 = new global::Gtk.VBox ();
			this.vbox2.Name = "vbox2";
			this.vbox2.Spacing = 6;
			this.vbox2.BorderWidth = ((uint)(6));
			// Container child vbox2.Gtk.Box+BoxChild
			this.hpaned1 = new global::Gtk.HPaned ();
			this.hpaned1.CanFocus = true;
			this.hpaned1.Name = "hpaned1";
			this.hpaned1.Position = 192;
			// Container child hpaned1.Gtk.Paned+PanedChild
			this.scrolledwindow1 = new global::Gtk.ScrolledWindow ();
			this.scrolledwindow1.CanFocus = true;
			this.scrolledwindow1.Name = "scrolledwindow1";
			this.scrolledwindow1.ShadowType = ((global::Gtk.ShadowType)(1));
			// Container child scrolledwindow1.Gtk.Container+ContainerChild
			this.catView = new global::Gtk.TreeView ();
			this.catView.WidthRequest = 160;
			this.catView.CanFocus = true;
			this.catView.Name = "catView";
			this.catView.HeadersVisible = false;
			this.scrolledwindow1.Add (this.catView);
			this.hpaned1.Add (this.scrolledwindow1);
			global::Gtk.Paned.PanedChild w3 = ((global::Gtk.Paned.PanedChild)(this.hpaned1 [this.scrolledwindow1]));
			w3.Resize = false;
			// Container child hpaned1.Gtk.Paned+PanedChild
			this.panedTemplates = new global::Gtk.HPaned ();
			this.panedTemplates.CanFocus = true;
			this.panedTemplates.Name = "panedTemplates";
			this.panedTemplates.Position = 292;
			// Container child panedTemplates.Gtk.Paned+PanedChild
			this.boxTemplates = new global::Gtk.VBox ();
			this.boxTemplates.Name = "boxTemplates";
			this.boxTemplates.Spacing = 6;
			this.panedTemplates.Add (this.boxTemplates);
			global::Gtk.Paned.PanedChild w4 = ((global::Gtk.Paned.PanedChild)(this.panedTemplates [this.boxTemplates]));
			w4.Resize = false;
			// Container child panedTemplates.Gtk.Paned+PanedChild
			this.scrolledInfo = new global::Gtk.ScrolledWindow ();
			this.scrolledInfo.CanFocus = true;
			this.scrolledInfo.Name = "scrolledInfo";
			this.scrolledInfo.HscrollbarPolicy = ((global::Gtk.PolicyType)(2));
			this.scrolledInfo.ShadowType = ((global::Gtk.ShadowType)(1));
			// Container child scrolledInfo.Gtk.Container+ContainerChild
			global::Gtk.Viewport w5 = new global::Gtk.Viewport ();
			w5.ShadowType = ((global::Gtk.ShadowType)(0));
			// Container child GtkViewport.Gtk.Container+ContainerChild
			this.boxInfo = new global::Gtk.VBox ();
			this.boxInfo.Name = "boxInfo";
			this.boxInfo.Spacing = 6;
			this.boxInfo.BorderWidth = ((uint)(3));
			// Container child boxInfo.Gtk.Box+BoxChild
			this.labelTemplateTitle = new global::Gtk.Label ();
			this.labelTemplateTitle.WidthRequest = 145;
			this.labelTemplateTitle.Name = "labelTemplateTitle";
			this.labelTemplateTitle.Xalign = 0F;
			this.labelTemplateTitle.LabelProp = global::Mono.Unix.Catalog.GetString ("<b>Console Project</b>");
			this.labelTemplateTitle.UseMarkup = true;
			this.labelTemplateTitle.Wrap = true;
			this.boxInfo.Add (this.labelTemplateTitle);
			global::Gtk.Box.BoxChild w6 = ((global::Gtk.Box.BoxChild)(this.boxInfo [this.labelTemplateTitle]));
			w6.Position = 0;
			w6.Expand = false;
			w6.Fill = false;
			// Container child boxInfo.Gtk.Box+BoxChild
			this.infoLabel = new global::Gtk.Label ();
			this.infoLabel.WidthRequest = 145;
			this.infoLabel.Name = "infoLabel";
			this.infoLabel.Xalign = 0F;
			this.infoLabel.Yalign = 0F;
			this.infoLabel.LabelProp = global::Mono.Unix.Catalog.GetString ("Creates a new C# Project");
			this.infoLabel.Wrap = true;
			this.boxInfo.Add (this.infoLabel);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.boxInfo [this.infoLabel]));
			w7.Position = 1;
			w7.Expand = false;
			w7.Fill = false;
			w5.Add (this.boxInfo);
			this.scrolledInfo.Add (w5);
			this.panedTemplates.Add (this.scrolledInfo);
			global::Gtk.Paned.PanedChild w10 = ((global::Gtk.Paned.PanedChild)(this.panedTemplates [this.scrolledInfo]));
			w10.Resize = false;
			this.hpaned1.Add (this.panedTemplates);
			this.vbox2.Add (this.hpaned1);
			global::Gtk.Box.BoxChild w12 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.hpaned1]));
			w12.Position = 0;
			// Container child vbox2.Gtk.Box+BoxChild
			this.label1 = new global::Gtk.Label ();
			this.label1.WidthRequest = 1;
			this.label1.HeightRequest = 1;
			this.label1.Name = "label1";
			this.vbox2.Add (this.label1);
			global::Gtk.Box.BoxChild w13 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.label1]));
			w13.Position = 1;
			w13.Expand = false;
			w13.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.hbox2 = new global::Gtk.HBox ();
			this.hbox2.Name = "hbox2";
			this.hbox2.Spacing = 6;
			// Container child hbox2.Gtk.Box+BoxChild
			this.label2 = new global::Gtk.Label ();
			this.label2.Name = "label2";
			this.label2.LabelProp = global::Mono.Unix.Catalog.GetString ("Name:");
			this.hbox2.Add (this.label2);
			global::Gtk.Box.BoxChild w14 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.label2]));
			w14.Position = 0;
			w14.Expand = false;
			w14.Fill = false;
			// Container child hbox2.Gtk.Box+BoxChild
			this.nameEntry = new global::Gtk.Entry ();
			this.nameEntry.CanFocus = true;
			this.nameEntry.Name = "nameEntry";
			this.nameEntry.IsEditable = true;
			this.nameEntry.InvisibleChar = '???';
			this.hbox2.Add (this.nameEntry);
			global::Gtk.Box.BoxChild w15 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.nameEntry]));
			w15.Position = 1;
			this.vbox2.Add (this.hbox2);
			global::Gtk.Box.BoxChild w16 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.hbox2]));
			w16.Position = 2;
			w16.Expand = false;
			w16.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.boxProject = new global::Gtk.VBox ();
			this.boxProject.Name = "boxProject";
			this.boxProject.Spacing = 6;
			// Container child boxProject.Gtk.Box+BoxChild
			this.hbox3 = new global::Gtk.HBox ();
			this.hbox3.Name = "hbox3";
			this.hbox3.Spacing = 6;
			// Container child hbox3.Gtk.Box+BoxChild
			this.projectAddCheckbox = new global::Gtk.CheckButton ();
			this.projectAddCheckbox.CanFocus = true;
			this.projectAddCheckbox.Name = "projectAddCheckbox";
			this.projectAddCheckbox.Label = global::Mono.Unix.Catalog.GetString ("_Add to project:");
			this.projectAddCheckbox.DrawIndicator = true;
			this.projectAddCheckbox.UseUnderline = true;
			this.hbox3.Add (this.projectAddCheckbox);
			global::Gtk.Box.BoxChild w17 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.projectAddCheckbox]));
			w17.Position = 0;
			w17.Expand = false;
			w17.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.projectAddCombo = global::Gtk.ComboBox.NewText ();
			this.projectAddCombo.Name = "projectAddCombo";
			this.hbox3.Add (this.projectAddCombo);
			global::Gtk.Box.BoxChild w18 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.projectAddCombo]));
			w18.Position = 1;
			this.boxProject.Add (this.hbox3);
			global::Gtk.Box.BoxChild w19 = ((global::Gtk.Box.BoxChild)(this.boxProject [this.hbox3]));
			w19.Position = 0;
			w19.Expand = false;
			w19.Fill = false;
			// Container child boxProject.Gtk.Box+BoxChild
			this.hbox4 = new global::Gtk.HBox ();
			this.hbox4.Name = "hbox4";
			this.hbox4.Spacing = 6;
			// Container child hbox4.Gtk.Box+BoxChild
			this.projectPathLabel = new global::Gtk.Label ();
			this.projectPathLabel.Name = "projectPathLabel";
			this.projectPathLabel.LabelProp = global::Mono.Unix.Catalog.GetString ("Path:");
			this.hbox4.Add (this.projectPathLabel);
			global::Gtk.Box.BoxChild w20 = ((global::Gtk.Box.BoxChild)(this.hbox4 [this.projectPathLabel]));
			w20.Position = 0;
			w20.Expand = false;
			w20.Fill = false;
			// Container child hbox4.Gtk.Box+BoxChild
			this.projectFolderEntry = new global::MonoDevelop.Components.FolderEntry ();
			this.projectFolderEntry.Name = "projectFolderEntry";
			this.projectFolderEntry.DisplayAsRelativePath = false;
			this.hbox4.Add (this.projectFolderEntry);
			global::Gtk.Box.BoxChild w21 = ((global::Gtk.Box.BoxChild)(this.hbox4 [this.projectFolderEntry]));
			w21.Position = 1;
			this.boxProject.Add (this.hbox4);
			global::Gtk.Box.BoxChild w22 = ((global::Gtk.Box.BoxChild)(this.boxProject [this.hbox4]));
			w22.Position = 1;
			w22.Expand = false;
			w22.Fill = false;
			this.vbox2.Add (this.boxProject);
			global::Gtk.Box.BoxChild w23 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.boxProject]));
			w23.Position = 3;
			w23.Expand = false;
			w23.Fill = false;
			w1.Add (this.vbox2);
			global::Gtk.Box.BoxChild w24 = ((global::Gtk.Box.BoxChild)(w1 [this.vbox2]));
			w24.Position = 0;
			// Internal child MonoDevelop.Ide.Projects.NewFileDialog.ActionArea
			global::Gtk.HButtonBox w25 = this.ActionArea;
			w25.Name = "dialog1_ActionArea";
			w25.Spacing = 6;
			w25.BorderWidth = ((uint)(5));
			w25.LayoutStyle = ((global::Gtk.ButtonBoxStyle)(4));
			// Container child dialog1_ActionArea.Gtk.ButtonBox+ButtonBoxChild
			this.cancelButton = new global::Gtk.Button ();
			this.cancelButton.CanDefault = true;
			this.cancelButton.CanFocus = true;
			this.cancelButton.Name = "cancelButton";
			this.cancelButton.UseStock = true;
			this.cancelButton.UseUnderline = true;
			this.cancelButton.Label = "gtk-cancel";
			this.AddActionWidget (this.cancelButton, -6);
			global::Gtk.ButtonBox.ButtonBoxChild w26 = ((global::Gtk.ButtonBox.ButtonBoxChild)(w25 [this.cancelButton]));
			w26.Expand = false;
			w26.Fill = false;
			// Container child dialog1_ActionArea.Gtk.ButtonBox+ButtonBoxChild
			this.okButton = new global::Gtk.Button ();
			this.okButton.CanDefault = true;
			this.okButton.CanFocus = true;
			this.okButton.Name = "okButton";
			this.okButton.UseStock = true;
			this.okButton.UseUnderline = true;
			this.okButton.Label = "gtk-new";
			w25.Add (this.okButton);
			global::Gtk.ButtonBox.ButtonBoxChild w27 = ((global::Gtk.ButtonBox.ButtonBoxChild)(w25 [this.okButton]));
			w27.Position = 1;
			w27.Expand = false;
			w27.Fill = false;
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.DefaultWidth = 718;
			this.DefaultHeight = 524;
			this.boxProject.Hide ();
			this.Hide ();
			this.scrolledInfo.SizeAllocated += new global::Gtk.SizeAllocatedHandler (this.OnScrolledInfoSizeAllocated);
		}
	}
}
