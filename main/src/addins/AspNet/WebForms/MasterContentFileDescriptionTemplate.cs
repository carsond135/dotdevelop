// 
// MasterContentFileDescriptionTemplate.cs
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

using System;
using System.Collections.Generic;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.AspNet.Projects;
using MonoDevelop.AspNet.WebForms.Parser;
using MonoDevelop.Ide.Templates;
using MonoDevelop.AspNet.WebForms.Dom;

namespace MonoDevelop.AspNet.WebForms
{
	public class MasterContentFileDescriptionTemplate : SingleFileDescriptionTemplate
	{
		public override void ModifyTags (SolutionFolderItem policyParent, Project project, string language, string identifier, string fileName, ref Dictionary<string,string> tags)
		{
			base.ModifyTags (policyParent, project, language, identifier, fileName, ref tags);
			if (fileName == null)
				return;
			
			tags ["AspNetMaster"] = "";
			tags ["AspNetMasterContent"] = "";
			
			var aspProj = project.GetService<AspNetAppProjectFlavor> ();
			if (aspProj == null)
				throw new InvalidOperationException ("MasterContentFileDescriptionTemplate is only valid for ASP.NET projects");
			
			ProjectFile masterPage = null;
			
			var dialog = new MonoDevelop.Ide.Projects.ProjectFileSelectorDialog (project, null, "*.master");
			try {
				dialog.Title = GettextCatalog.GetString ("Select a Master Page...");
				int response = MonoDevelop.Ide.MessageService.RunCustomDialog (dialog);
				if (response == (int)Gtk.ResponseType.Ok)
					masterPage = dialog.SelectedFile;
			} finally {
				dialog.Destroy ();
				dialog.Dispose ();
			}
			if (masterPage == null)
				return;
			
			tags ["AspNetMaster"] = aspProj.LocalToVirtualPath (masterPage);
			
			try {
				var pd = IdeApp.TypeSystemService.ParseFile (project, masterPage.FilePath).Result
						as WebFormsParsedDocument;
				if (pd == null)
					return;
				
				var sb = new System.Text.StringBuilder ();
				foreach (string id in pd.XDocument.GetAllPlaceholderIds ()) {
					sb.Append ("<asp:Content ContentPlaceHolderID=\"");
					sb.Append (id);
					sb.Append ("\" ID=\"");
					sb.Append (id);
					sb.Append ("Content\" runat=\"server\">\n</asp:Content>\n");
				}
				
				tags["AspNetMasterContent"] = sb.ToString ();
			}
			catch (Exception ex) {
				//no big loss if we just insert blank space
				//it's just a template for the user to start editing
				LoggingService.LogWarning ("Error generating AspNetMasterContent for template", ex);
			}
		}
	}
}
