//
// FindImplementingMembersHandler.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://xamarin.com)
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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide;
using MonoDevelop.Ide.FindInFiles;
using Microsoft.CodeAnalysis;
using MonoDevelop.Components.Commands;
using MonoDevelop.Refactoring;
using ICSharpCode.NRefactory6.CSharp;
using Microsoft.CodeAnalysis.CSharp;

namespace MonoDevelop.CSharp.Navigation
{
	class FindImplementingMembersHandler : CommandHandler
	{
		protected async override Task UpdateAsync (CommandInfo info, CancellationToken cancelToken)
		{
			var sym = await GetNamedTypeAtCaret (IdeApp.Workbench.ActiveDocument);
			info.Enabled = TryGetInterfaceType (sym, out var interfaceType, out var implementingType);
			info.Bypass = !info.Enabled;
		}

		protected async override void Run ()
		{
			var metadata = Counters.CreateNavigateToMetadata ("ImplementingMembers");
			using (var timer = Counters.NavigateTo.BeginTiming (metadata)) {
				var doc = IdeApp.Workbench.ActiveDocument;
				var sym = await GetNamedTypeAtCaret (doc);
				if (sym == null) {
					metadata.SetUserFault ();
					return;
				}

				using (var source = new CancellationTokenSource ()) {
					try {
						await FindImplementingSymbols (await doc.GetCompilationAsync (), sym, source);
						metadata.SetResult (true);
					} finally {
						metadata.UpdateUserCancellation (source.Token);
					}
				}
			}
		}

		static async Task<RefactoringSymbolInfo> GetNamedTypeAtCaret (Ide.Gui.Document doc)
		{
			if (doc == null || doc.Editor == null)
				return null;
			var info = await RefactoringSymbolInfo.GetSymbolInfoAsync (doc.DocumentContext, doc.Editor);

			if (info.Node?.Parent.IsKind (SyntaxKind.SimpleBaseType) != true)
				return null;

			return info;
		}

		Task FindImplementingSymbols (Compilation compilation, RefactoringSymbolInfo info, CancellationTokenSource cancellationTokenSource)
		{
			if (!TryGetInterfaceType (info, out var interfaceType, out var implementingType))
				return Task.CompletedTask;

			return Task.Run (delegate {
				var searchMonitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor (true, true);
				using (var monitor = searchMonitor.WithCancellationSource (cancellationTokenSource)) {
					foreach (var interfaceMember in interfaceType.GetMembers ()) {
						if (monitor.CancellationToken.IsCancellationRequested)
							return;
						var impl = implementingType.FindImplementationForInterfaceMember (interfaceMember);
						if (impl == null)
							continue;
						var loc = impl.Locations.First ();
						searchMonitor.ReportResult (new MemberReference (impl, loc.SourceTree.FilePath, loc.SourceSpan.Start, loc.SourceSpan.Length));
					}
					foreach (var iFace in interfaceType.AllInterfaces) {

						foreach (var interfaceMember in iFace.GetMembers ()) {
							if (monitor.CancellationToken.IsCancellationRequested)
								return;

							var impl = implementingType.FindImplementationForInterfaceMember (interfaceMember);
							if (impl == null)
								continue;
							var loc = impl.Locations.First ();
							searchMonitor.ReportResult (new MemberReference (impl, loc.SourceTree.FilePath, loc.SourceSpan.Start, loc.SourceSpan.Length));
						}
					}

				}
			});
		}

		static bool TryGetInterfaceType (RefactoringSymbolInfo sym, out ITypeSymbol interfaceType, out INamedTypeSymbol implementingType)
		{
			interfaceType = null;
			implementingType = null;
			if (sym == null)
				return false;
			interfaceType = sym.Symbol as ITypeSymbol;
			var parentTypeNode = sym.Node?.Parent?.Parent?.Parent;
			if (parentTypeNode == null || interfaceType.TypeKind != TypeKind.Interface)
				return false;
			implementingType = sym.Model.GetDeclaredSymbol (parentTypeNode) as INamedTypeSymbol;
			return implementingType != null;
		}
	}
}