//
// Scaffolder.cs
//
// Author:
//       jasonimison <jaimison@microsoft.com>
//
// Copyright (c) 2019 Microsoft
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
using System.Collections.Generic;
using MonoDevelop.Core;

namespace MonoDevelop.AspNetCore.Scaffolding
{
	class RazorPageScaffolder : RazorPageScaffolderBase
	{
		public RazorPageScaffolder (ScaffolderArgs args): base(args)
		{
		}

		public override string Name => GettextCatalog.GetString("Razor Page");

		public override IEnumerable<ScaffolderField> Fields => fields ?? GetFields();

		IEnumerable<ScaffolderField> GetFields()
		{
			var scriptLibrariesField = ReferenceScriptLibrariesField;
			scriptLibrariesField.Enabled = false;

			var options = new List<BoolField> {
				PageModelField,
				PartialViewField,
				scriptLibrariesField,
				LayoutPageField
			};

			fields = new ScaffolderField [] {
				NameField,
				new ArgumentField("Empty"),
				new BoolFieldList(options),
				CustomLayoutField
			};

			return fields;
		}
	}
}
