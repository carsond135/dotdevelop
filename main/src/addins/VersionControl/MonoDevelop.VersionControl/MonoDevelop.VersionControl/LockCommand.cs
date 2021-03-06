// Lock.cs
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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;

namespace MonoDevelop.VersionControl
{
	public class LockCommand
	{
		public static async Task<bool> LockAsync (VersionControlItemList items, bool test, CancellationToken cancellationToken)
		{
			foreach (var item in items) {
				var info = await item.GetVersionInfoAsync (cancellationToken);
				if (!info.CanLock)
					return false;
			}
			if (test)
				return true;

			await new LockWorker (items).StartAsync (cancellationToken).ConfigureAwait (false);
			return true;
		}

		private class LockWorker : VersionControlTask
		{
			VersionControlItemList items;

			public LockWorker (VersionControlItemList items) {
				this.items = items;
			}

			protected override string GetDescription() {
				return GettextCatalog.GetString ("Locking...");
			}

			protected override Task RunAsync ()
			{
				foreach (VersionControlItemList list in items.SplitByRepository ()) {
					try {
						list [0].Repository.Lock (Monitor, list.Paths);
					} catch (Exception ex) {
						LoggingService.LogError ("Lock operation failed", ex);
						Monitor.ReportError (ex.Message, null);
						return Task.CompletedTask;
					}
				}
				Gtk.Application.Invoke ((o, args) => {
					VersionControlService.NotifyFileStatusChanged (items);
				});
				Monitor.ReportSuccess (GettextCatalog.GetString ("Lock operation completed."));
				return Task.CompletedTask;
			}
		}
	}
}
