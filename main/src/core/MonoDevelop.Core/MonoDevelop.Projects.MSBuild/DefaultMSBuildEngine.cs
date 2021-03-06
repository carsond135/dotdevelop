//
// DefaultMSBuildEngine.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc (http://www.xamarin.com)
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
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MonoDevelop.Core;
using MonoDevelop.Projects.MSBuild.Conditions;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using System.Collections.Immutable;

namespace MonoDevelop.Projects.MSBuild
{
	class DefaultMSBuildEngine: MSBuildEngine
	{
		Dictionary<FilePath, LoadedProjectInfo> loadedProjects = new Dictionary<FilePath, LoadedProjectInfo> ();

		// For test purposes.
		internal static Func<MSBuildProject, MSBuildEvaluationContext> GetEvaluationContext = null;

		class LoadedProjectInfo
		{
			public MSBuildProject Project;
			public DateTime LastWriteTime;
			public int ReferenceCount = 1;
			public bool NeedsLoad = false;
			public object LockObject = new object ();
		}

		class ProjectInfo
		{
			public ProjectInfo Parent;
			public MSBuildProject Project;
			public List<MSBuildItemEvaluated> EvaluatedItemsIgnoringCondition = new List<MSBuildItemEvaluated> ();
			public List<MSBuildItemEvaluated> EvaluatedItems = new List<MSBuildItemEvaluated> ();
			public List<MSBuildItemEvaluated> EvaluatedItemDefinitions = new List<MSBuildItemEvaluated> ();
			public Dictionary<string,PropertyInfo> Properties = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
			public Dictionary<MSBuildImport,string> Imports = new Dictionary<MSBuildImport, string> ();
			public Dictionary<string,string> GlobalProperties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
			public List<MSBuildTarget> Targets = new List<MSBuildTarget> ();
			public List<MSBuildTarget> TargetsIgnoringCondition = new List<MSBuildTarget> ();
			public List<MSBuildProject> ReferencedProjects = new List<MSBuildProject> ();
			public Dictionary<MSBuildImport, List<ProjectInfo>> ImportedProjects = new Dictionary<MSBuildImport, List<ProjectInfo>> ();
			public ConditionedPropertyCollection ConditionedProperties = new ConditionedPropertyCollection ();
			public List<GlobInfo> GlobIncludes = new List<GlobInfo> ();
			public bool OnlyEvaluateProperties;

			public MSBuildProject GetRootMSBuildProject ()
			{
				if (Parent != null)
					return Parent.GetRootMSBuildProject ();
				return Project;
			}
        }

		class PropertyInfo
		{
			public string Name;
			public string Value;
			public string FinalValue;
			public bool IsImported;
			public bool DefinedMultipleTimes;
		}

		class GlobInfo
		{
			public MSBuildItem Item;
			public string Include;
			public Regex ExcludeRegex;
			public Regex DirectoryExcludeRegex;
			public Regex RemoveRegex;
			public bool Condition;
			public string [] IncludeSplit;

			public List<GlobInfo> Updates;
			public Regex UpdateRegex;
		}

		#region implemented abstract members of MSBuildEngine

		static HashSet<string> knownItemFunctions;
		static HashSet<string> knownStringItemFunctions;


		static DefaultMSBuildEngine ()
		{
			// List of string functions that can be used as item functions
			knownStringItemFunctions = new HashSet<string> (typeof (string).GetMethods (System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select (m => m.Name));

			// List of known item functions
			knownItemFunctions = new HashSet<string> (new [] { "Count", "DirectoryName", "Distinct", "DistinctWithCase", "Reverse", "AnyHaveMetadataValue", "ClearMetadata", "HasMetadata", "Metadata", "WithMetadataValue" });

			// This collection will contain all item functions, including the string functions
			knownItemFunctions.UnionWith (knownStringItemFunctions);
		}

		public DefaultMSBuildEngine (MSBuildEngineManager manager): base (manager)
		{
		}

		public override object LoadProject (MSBuildProject project, string xml, FilePath fileName)
		{
			return project;
		}

		public override void UnloadProject (object project)
		{
		}

		MSBuildProject LoadProject (MSBuildEvaluationContext context, FilePath fileName)
		{
			fileName = fileName.CanonicalPath;

			LoadedProjectInfo pi;
			lock (loadedProjects) {
				if (!loadedProjects.TryGetValue (fileName, out pi)) {
					loadedProjects [fileName] = pi = new LoadedProjectInfo ();
				}
				pi.ReferenceCount++;
			}

			lock (pi.LockObject) {
				if (pi.Project == null) {
					pi.Project = new MSBuildProject (EngineManager);
					pi.NeedsLoad = true;
				}

				if (!pi.NeedsLoad)
					pi.NeedsLoad = pi.LastWriteTime != File.GetLastWriteTimeUtc (fileName);

				if (pi.NeedsLoad) {
					LogBeginProjectFileLoad (context, fileName);
					pi.LastWriteTime = File.GetLastWriteTimeUtc (fileName);
					pi.Project.Load (fileName, new MSBuildXmlReader { ForEvaluation = true });
					pi.NeedsLoad = false;
					LogEndProjectFileLoad (context);
				}

				return pi.Project;
			}
		}

		void UnloadProject (MSBuildProject project)
		{
			var fileName = project.FileName.CanonicalPath;
			LoadedProjectInfo pi;
			lock (loadedProjects) {
				if (!loadedProjects.TryGetValue (fileName, out pi))
					return;

				pi.ReferenceCount--;
				if (pi.ReferenceCount == 0) {
					loadedProjects.Remove (fileName);
					lock (pi.LockObject)
						pi.Project = null;
					project.Dispose ();
					//Console.WriteLine ("Unloaded: " + fileName);
				}
			}
		}

		public override object CreateProjectInstance (object project)
		{
			var pi = new ProjectInfo {
				Project = (MSBuildProject) project
			};
			return pi;
		}

		public override void DisposeProjectInstance (object projectInstance)
		{
			var pi = (ProjectInfo) projectInstance;
			foreach (var p in pi.ReferencedProjects)
				UnloadProject (p);
		}

		public override void Evaluate (object projectInstance)
		{
			Evaluate (projectInstance, false);
		}

		public override void Evaluate (object projectInstance, bool onlyEvaluateProperties)
		{
			var pi = (ProjectInfo) projectInstance;

			pi.EvaluatedItemsIgnoringCondition.Clear ();
			pi.EvaluatedItems.Clear ();
			pi.EvaluatedItemDefinitions.Clear ();
			pi.Properties.Clear ();
			pi.Imports.Clear ();
			pi.Targets.Clear ();
			pi.TargetsIgnoringCondition.Clear ();
			pi.OnlyEvaluateProperties = onlyEvaluateProperties;

			// Unload referenced projects after evaluating to avoid unnecessary unload + load
			var oldRefProjects = pi.ReferencedProjects;
			pi.ReferencedProjects = new List<MSBuildProject> ();

			try {
				var context = GetEvaluationContext?.Invoke (pi.Project) ?? new MSBuildEvaluationContext ();
				foreach (var p in pi.GlobalProperties) {
					context.SetPropertyValue (p.Key, p.Value);
					StoreProperty (pi, p.Key, p.Value, p.Value);
				}
#if MSBUILD_EVALUATION_STATS
				context.Log = new ConsoleMSBuildEngineLogger ();
#endif
				LogBeginEvaluationStage (context, "Evaluating Project: " + pi.Project.FileName);
				LogInitialEnvironment (context);
				EvaluateProject (pi, context);
				LogEndEvaluationStage (context);
			} finally {
				foreach (var p in oldRefProjects)
					UnloadProject (p);
				pi.ImportedProjects.Clear ();
			}
		}

		static char [] sdkPathSeparator = { ';' };
		void EvaluateProject (ProjectInfo pi, MSBuildEvaluationContext context)
		{
			context.InitEvaluation (pi.Project);
			var objects = pi.Project.GetAllObjects ();

			string[] implicitSdks = pi.Project.GetImplicitlyImportedSdks ();
			if (implicitSdks.Length > 0) {
				var rootProject = pi.GetRootMSBuildProject ();

				objects = implicitSdks.Select (sdkPath => new MSBuildImport { Sdk = sdkPath, Project = "Sdk.props" })
				                      .Concat (objects)
				                      .Concat (implicitSdks.Select (sdkPath => new MSBuildImport { Sdk = sdkPath, Project = "Sdk.targets" }));
			}

			// If there is a .user project file load it using a fake import item added at the end of the objects list
			if (File.Exists (pi.Project.FileName + ".user"))
				objects = objects.Concat (new MSBuildImport { Project = pi.Project.FileName + ".user" });

			objects = objects.ToList ();

			LogBeginEvaluationStage (context, "Evaluating Properties");
			LogBeginEvalProject (context, pi);

			EvaluateObjects (pi, context, objects, false);

			LogEndEvalProject (context, pi);
			LogEndEvaluationStage (context);

			if (!pi.OnlyEvaluateProperties) {
				LogBeginEvaluationStage (context, "Evaluating Items");
				LogBeginEvalProject (context, pi);

				EvaluateObjects (pi, context, objects, true);

				LogEndEvalProject (context, pi);
				LogEndEvaluationStage (context);
			}

			// Once items have been evaluated, we need to re-evaluate properties that contain item transformations
			// (or that contain references to properties that have transformations).

			foreach (var propName in context.GetPropertiesNeedingTransformEvaluation ()) {
				PropertyInfo prop;
				if (pi.Properties.TryGetValue (propName, out prop)) {
					// Execute the transformation
					prop.FinalValue = context.EvaluateWithItems (prop.FinalValue, pi.EvaluatedItems);

					// Set the resulting value back to the context, so other properties depending on this
					// one will get the new value when re-evaluated.
					context.SetPropertyValue (propName, prop.FinalValue);
				}
			}
		}

		void EvaluateProject (ProjectInfo pi, MSBuildEvaluationContext context, bool evalItems)
		{
			context.InitEvaluation (pi.Project);
			
			LogBeginEvalProject (context, pi);
			EvaluateObjects (pi, context, pi.Project.GetAllObjects (), evalItems);
			LogEndEvalProject (context, pi);
		}

		void EvaluateObjects (ProjectInfo pi, MSBuildEvaluationContext context, IEnumerable<MSBuildObject> objects, bool evalItems)
		{
			foreach (var ob in objects) {
				if (evalItems) {
					if (ob is MSBuildItemGroup)
						Evaluate (pi, context, (MSBuildItemGroup)ob);
					else if (ob is MSBuildTarget)
						Evaluate (pi, context, (MSBuildTarget)ob);
					else if (ob is MSBuildItemDefinitionGroup)
						Evaluate (pi, context, (MSBuildItemDefinitionGroup)ob);
				} else {
					if (ob is MSBuildPropertyGroup)
						Evaluate (pi, context, (MSBuildPropertyGroup)ob);
				}
				if (ob is MSBuildImportGroup)
					Evaluate (pi, context, (MSBuildImportGroup)ob, evalItems);
				else if (ob is MSBuildImport)
					Evaluate (pi, context, (MSBuildImport)ob, evalItems);
				else if (ob is MSBuildChoose)
					Evaluate (pi, context, (MSBuildChoose)ob, evalItems);
			}
		}

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildPropertyGroup group)
		{
			if (!SafeParseAndEvaluate (project, context, group.Condition, true)) {
				return;
			}

			foreach (var prop in group.GetProperties ())
				Evaluate (project, context, prop);
		}

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItemGroup items)
		{
			bool conditionIsTrue = SafeParseAndEvaluate (project, context, items.Condition);

			foreach (var item in items.Items) {

				var trueCond = conditionIsTrue && SafeParseAndEvaluate (project, context, item.Condition);

				if (!string.IsNullOrEmpty (item.Update)) {
					var update = context.EvaluateString (item.Update);

					var it = CreateEvaluatedItem (context, project, project.Project, item, update);

					var updateContext = new MSBuildEvaluationContext (context);
					if (update.IndexOf (';') == -1)
						UpdateItem (project, updateContext, item, update, trueCond, it);
					else {
						foreach (var inc in update.Split (new [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
							UpdateItem (project, updateContext, item, inc, trueCond, it);
					}
				} else if (!string.IsNullOrEmpty (item.Remove)) {
					var remove = context.EvaluateString (item.Remove);

					if (remove.IndexOf (';') == -1)
						RemoveItem (project, item, remove, trueCond);
					else {
						foreach (var inc in remove.Split (new [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
							RemoveItem (project, item, inc, trueCond);
					}
				} else if (!string.IsNullOrEmpty (item.Include)) {
					var include = context.EvaluateString (item.Include);
					var exclude = context.EvaluateString (item.Exclude);

					var it = CreateEvaluatedItem (context, project, project.Project, item, include);

					var excludeRegex = !string.IsNullOrEmpty (exclude) ? new Regex (ExcludeToRegex (exclude)) : null;

					Regex directoryExcludeRegex = null;
					if (!string.IsNullOrEmpty (exclude)) {
						string regex = ExcludeToRegex (exclude, true);
						if (!string.IsNullOrEmpty (regex)) {
							directoryExcludeRegex = new Regex (regex);
						}
					}

					if (it.Include.IndexOf (';') == -1)
						AddItem (project, context, item, it, it.Include, excludeRegex, directoryExcludeRegex, trueCond);
					else {
						foreach (var inc in it.Include.Split (new [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
							AddItem (project, context, item, it, inc, excludeRegex, directoryExcludeRegex, trueCond);
					}
				}
			}
		}

		void UpdateItem (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItem item, string update, bool trueCond, MSBuildItemEvaluated it)
		{
			if (IsWildcardInclude (update)) {
				var regex = new Regex (ExcludeToRegex (update));
				AddUpdateToGlobInclude (project, item, update, regex);
				var rootProject = project.GetRootMSBuildProject ();
				foreach (var f in GetIncludesForWildcardFilePath (rootProject, update)) {
					var fileName = rootProject.BaseDirectory.Combine (f.Replace ('\\', '/'));
					context.SetItemContext (update, fileName, null);
					UpdateEvaluatedItemInAllProjects (project, context, item, f, trueCond, it);
				}
			} else
				UpdateEvaluatedItemInAllProjects (project, null, item, update, trueCond, it);
		}

		void AddUpdateToGlobInclude (ProjectInfo project, MSBuildItem item, string update, Regex updateRegex)
		{
			do {
				foreach (var globInclude in project.GlobIncludes) {
					if (globInclude.Item.Name != item.Name)
						continue;

					if (globInclude.Updates == null)
						globInclude.Updates = new List<GlobInfo> ();
					globInclude.Updates.Add (new GlobInfo { Include = update, IncludeSplit = SplitWildcardFilePath (update), Item = item, UpdateRegex = updateRegex });
				}
				project = project.Parent;
			} while (project != null);
		}

		void UpdateEvaluatedItemInAllProjects (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItem item, string include, bool trueCond, MSBuildItemEvaluated it)
		{
			do {
				UpdateEvaluatedItem (project, context, item, include, trueCond, it);
				project = project.Parent;
			} while (project != null);
		}

		void UpdateEvaluatedItem (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItem item, string include, bool trueCond, MSBuildItemEvaluated it)
		{
			if (trueCond) {
				foreach (var item2 in project.EvaluatedItems) {
					if (item2.Name == item.Name && item2.Include == include) {
						if (context != null) {
							UpdateProperties (project, context, item, item2);
						} else {
							foreach (var evaluatedProp in ((MSBuildPropertyGroupEvaluated)it.Metadata).GetProperties ()) {
								((MSBuildPropertyGroupEvaluated)item2.Metadata).SetProperty (evaluatedProp.Name, evaluatedProp);
							}
						}
						item2.AddSourceItem (item);
					}
				}
			}

			foreach (var item2 in project.EvaluatedItemsIgnoringCondition) {
				if (item2.Name == item.Name && item2.Include == include) {
					if (context != null) {
						UpdateProperties (project, context, item, item2);
					} else {
						foreach (var evaluatedProp in ((MSBuildPropertyGroupEvaluated)it.Metadata).GetProperties ()) {
							((MSBuildPropertyGroupEvaluated)item2.Metadata).SetProperty (evaluatedProp.Name, evaluatedProp);
						}
					}
					item2.AddSourceItem (item);
				}
			}
		}

		void UpdateProperties (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItem item, MSBuildItemEvaluated evaluatedItem)
		{
			var rootProject = project.GetRootMSBuildProject ();

			foreach (var p in item.Metadata.GetProperties ()) {
				if (SafeParseAndEvaluate (project, context, p.Condition, true)) {
					string evaluatedValue = context.EvaluateString (p.Value);
					string unevaluatedValue = p.Value;

					if (rootProject != item.ParentProject) {
						// Use the same evaluated value as the property value so expanded metadata properties from a wildcard
						// item are not saved in the project file.
						unevaluatedValue = evaluatedValue;
					}

					var evaluatedProp = new MSBuildPropertyEvaluated (project.Project, p.Name, unevaluatedValue, evaluatedValue) { Condition = p.Condition };
					((MSBuildPropertyGroupEvaluated)evaluatedItem.Metadata).SetProperty (evaluatedProp.Name, evaluatedProp);
				}
			}
		}

		/// <summary>
		/// Only supports exact match when looking for a glob.
		/// </summary>
		static Regex GetDirectoryExcludeRegex (ProjectInfo project, string wildcard)
		{
			return project.GlobIncludes.Where (g => g.Condition && g.Include == wildcard)
				.Select (g => g.DirectoryExcludeRegex)
				.FirstOrDefault ();
		}

		static void RemoveItem (ProjectInfo project, MSBuildItem item, string remove, bool trueCond)
		{
			if (IsWildcardInclude (remove)) {
				AddRemoveToGlobInclude (project, item, remove);
				var rootProject = project.GetRootMSBuildProject ();
				var directoryExcludeRegex = GetDirectoryExcludeRegex (project, remove);
				foreach (var f in GetIncludesForWildcardFilePath (rootProject, remove, directoryExcludeRegex))
					RemoveEvaluatedItemFromAllProjects (project, item, f, trueCond);
			} else
				RemoveEvaluatedItemFromAllProjects (project, item, remove, trueCond);
		}

		/// <summary>
		/// Adds a glob remove to the corresponding glob include. This remove is then checked
		/// in FindGlobItemsIncludingFile so that a glob item is not returned if the file was removed
		/// from that glob.
		/// </summary>
		static void AddRemoveToGlobInclude (ProjectInfo project, MSBuildItem item, string remove)
		{
			var exclude = ExcludeToRegex (remove);
			do {
				foreach (var globInclude in project.GlobIncludes) {
					if (globInclude.Item.Name != item.Name)
						continue;

					if (globInclude.RemoveRegex != null)
						exclude = globInclude.RemoveRegex + "|" + exclude;
					globInclude.RemoveRegex = new Regex (exclude);
				}
				project = project.Parent;
			} while (project != null);
		}

		static void RemoveEvaluatedItemFromAllProjects (ProjectInfo project, MSBuildItem item, string include, bool trueCond)
		{
			do {
				RemoveEvaluatedItem (project, item, include, trueCond);
				project = project.Parent;
			} while (project != null);
		}

		static void RemoveEvaluatedItem (ProjectInfo project, MSBuildItem item, string include, bool trueCond)
		{
			if (trueCond)
				project.EvaluatedItems.RemoveAll (it => it.Name == item.Name && it.Include == include);
			project.EvaluatedItemsIgnoringCondition.RemoveAll (it => it.Name == item.Name && it.Include == include);
		}

		void AddItem (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItem item, MSBuildItemEvaluated it, string include, Regex excludeRegex, Regex directoryExcludeRegex, bool trueCond)
		{
			// Don't add the result from any item that has an empty include. MSBuild never returns those.
			if (include == string.Empty)
				return;
			
			if (IsIncludeTransform (include)) {
				// This is a transform
				List<MSBuildItemEvaluated> evalItems;
				var transformExp = include.AsSpan (2, include.Length - 3);
				if (ExecuteTransform (project, context, item, transformExp, out evalItems)) {
					foreach (var newItem in evalItems) {
						project.EvaluatedItemsIgnoringCondition.Add (newItem);
						if (trueCond)
							project.EvaluatedItems.Add (newItem);
					}
				}
			} else if (IsWildcardInclude (include)) {
				project.GlobIncludes.Add (new GlobInfo { Item = item, Include = include, IncludeSplit = SplitWildcardFilePath (include), ExcludeRegex = excludeRegex, DirectoryExcludeRegex = directoryExcludeRegex, Condition = trueCond });
				foreach (var eit in ExpandWildcardFilePath (project, context, item, include, directoryExcludeRegex)) {
					if (excludeRegex != null && excludeRegex.IsMatch (eit.Include))
						continue;
					project.EvaluatedItemsIgnoringCondition.Add (eit);
					if (trueCond)
						project.EvaluatedItems.Add (eit);
				}
			} else if (include != it.Include) {
				if (excludeRegex != null && excludeRegex.IsMatch (include))
					return;
				it = CreateEvaluatedItem (context, project, project.Project, item, include);
				project.EvaluatedItemsIgnoringCondition.Add (it);
				if (trueCond)
					project.EvaluatedItems.Add (it);
			} else {
				if (excludeRegex != null && excludeRegex.IsMatch (include))
					return;
				project.EvaluatedItemsIgnoringCondition.Add (it);
				if (trueCond)
					project.EvaluatedItems.Add (it);
			}
		}

		bool ExecuteTransform (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItem item, ReadOnlySpan<char> transformExp, out List<MSBuildItemEvaluated> items)
		{
			bool ignoreMetadata = false;

			items = new List<MSBuildItemEvaluated> ();
			string itemName, expression, itemFunction; object [] itemFunctionArgs;

			// This call parses the transforms and extracts: the name of the item list to transform, the whole transform expression (or null if there isn't). If the expression can be
			// parsed as an item funciton, then it returns the function name and the list of arguments. Otherwise those parameters are null.
			if (!ParseTransformExpression (context, transformExp, out itemName, out expression, out itemFunction, out itemFunctionArgs))
				return false;

			// Get the items mathing the referenced item list
			var transformItems = project.EvaluatedItems.Where (i => i.Name == itemName).ToArray ();

			if (itemFunction != null) {
				// First of all, try to execute the function as a summary function, that is, a function that returns a single value for
				// the whole list (such as Count).
				// After that, try executing as a list transformation function: a function that changes the order or filters out items from the list.

				string result;
				if (ExecuteSummaryItemFunction (transformItems, itemFunction, itemFunctionArgs, out result)) {
					// The item function returns a value. Just create an item with that value
					var newItem = new MSBuildItemEvaluated (project.Project, item.Name, item.Include, result);
					project.EvaluatedItemsIgnoringCondition.Add (newItem);
					items.Add (newItem);
					return true;
				} else if (ExecuteTransformItemListFunction (ref transformItems, itemFunction, itemFunctionArgs, out ignoreMetadata)) {
					expression = null;
					itemFunction = null;
				}
			}

			foreach (var eit in transformItems) {
				// Some item functions cause the erasure of metadata. Take that into account now.
				context.SetItemContext (null, eit.Include, null, ignoreMetadata || item == null ? null : eit.Metadata);
				try {
					// If there is a function that transforms the include of the item, it needs to be applied now. Otherwise just use the transform expression
					// as include, or the transformed item include if there is no expression.

					string evaluatedInclude; bool skip;
					if (itemFunction != null && ExecuteTransformIncludeItemFunction (context, eit, itemFunction, itemFunctionArgs, out evaluatedInclude, out skip)) {
						if (skip) continue;
					} else if (expression != null)
						evaluatedInclude = context.EvaluateString (expression);
					else
						evaluatedInclude = eit.Include;

					var newItem = new MSBuildItemEvaluated (project.Project, item.Name, item.Include, evaluatedInclude);
					if (!ignoreMetadata) {
						var md = new Dictionary<string, IMSBuildPropertyEvaluated> ();
						// Add metadata from the evaluated item
						var col = (MSBuildPropertyGroupEvaluated)eit.Metadata;
						foreach (var p in col.GetProperties ()) {
							md [p.Name] = new MSBuildPropertyEvaluated (project.Project, p.Name, p.UnevaluatedValue, p.Value);
						}
						// Now override metadata from the new item definition
						foreach (var c in item.Metadata.GetProperties ()) {
							if (SafeParseAndEvaluate (project, context, c.Condition, true))
								md [c.Name] = new MSBuildPropertyEvaluated (project.Project, c.Name, c.Value, context.EvaluateString (c.Value));
						}
						((MSBuildPropertyGroupEvaluated)newItem.Metadata).SetProperties (md);
					}
					newItem.AddSourceItem (item);
					newItem.Condition = item.Condition;
					items.Add (newItem);
				} finally {
					context.ClearItemContext ();
				}
			}
			return true;
		}

		internal static bool ExecuteStringTransform (List<MSBuildItemEvaluated> evaluatedItemsCollection, MSBuildEvaluationContext context, ReadOnlySpan<char> transformExp, out string items)
		{
			// This method works mostly like ExecuteTransform, but instead of returning a list of items, it returns a string as result.
			// Since there is no need to create full blown evaluated items, it can be more efficient than ExecuteTransform.

			items = "";

			string itemName, expression, itemFunction; object [] itemFunctionArgs;
			if (!ParseTransformExpression (context, transformExp, out itemName, out expression, out itemFunction, out itemFunctionArgs))
				return false;

			var transformItems = evaluatedItemsCollection.Where (i => i.Name == itemName).ToArray ();
			if (itemFunction != null) {
				string result; bool ignoreMetadata;
				if (ExecuteSummaryItemFunction (transformItems, itemFunction, itemFunctionArgs, out result)) {
					// The item function returns a value. Just return it.
					items = result;
					return true;
				} else if (ExecuteTransformItemListFunction (ref transformItems, itemFunction, itemFunctionArgs, out ignoreMetadata)) {
					var sb = StringBuilderCache.Allocate ();
					for (int n = 0; n < transformItems.Length; n++) {
						if (n > 0)
							sb.Append (';');
						sb.Append (transformItems[n].Include);
					}	
					items = StringBuilderCache.ReturnAndFree (sb);
					return true;
				}
			}

			var sbi = StringBuilderCache.Allocate ();

			int count = 0;
			foreach (var eit in transformItems) {
				context.SetItemContext (null, eit.Include, null, eit.Metadata);
				try {
					string evaluatedInclude; bool skip;
					if (itemFunction != null && ExecuteTransformIncludeItemFunction (context, eit, itemFunction, itemFunctionArgs, out evaluatedInclude, out skip)) {
						if (skip) continue;
					} else if (expression != null)
						evaluatedInclude = context.EvaluateString (expression);
					else
						evaluatedInclude = eit.Include;

					if (count++ > 0)
						sbi.Append (';');
					sbi.Append (evaluatedInclude);

				} finally {
					context.ClearItemContext ();
				}
			}
			items = Core.StringBuilderCache.ReturnAndFree (sbi);
			return true;
		}

		static bool ParseTransformExpression (MSBuildEvaluationContext context, ReadOnlySpan<char> include, out string itemName, out string expression, out string itemFunction, out object [] itemFunctionArgs)
		{
			// This method parses the transforms and extracts: the name of the item list to transform, the whole transform expression (or null if there isn't). If the expression can be
			// parsed as an item funciton, then it returns the function name and the list of arguments. Otherwise those parameters are null.
		
			expression = null;
			itemFunction = null;
			itemFunctionArgs = null;
		
			int i = include.IndexOf ("->".AsSpan (), StringComparison.Ordinal);
			if (i == -1) {
				itemName = include.Trim ().ToString ();
				return itemName.Length > 0;
			}
			itemName = include.Slice (0, i).Trim ().ToString ();
			if (itemName.Length == 0)
				return false;
			
			var expressionSpan = include.Slice (i + 2).Trim ();
			if (expressionSpan.Length > 1 && expressionSpan [0]=='\'' && expressionSpan [expressionSpan.Length - 1] == '\'') {
				expression = expressionSpan.Slice (1, expressionSpan.Length - 2).ToString ();
				return true;
			}
			expression = expressionSpan.ToString ();
			i = expressionSpan.IndexOf ('(');
			if (i == -1)
				return true;

			var func = expressionSpan.Slice (0, i).Trim ().ToString ();
			if (knownItemFunctions.Contains (func)) {
				itemFunction = func;
				i++;
				context.EvaluateParameters (expressionSpan, ref i, out itemFunctionArgs);
				return true;
			}
			return true;
		}

		static bool ExecuteTransformIncludeItemFunction (MSBuildEvaluationContext context, MSBuildItemEvaluated item, string itemFunction, object [] itemFunctionArgs, out string evaluatedInclude, out bool skip)
		{
			evaluatedInclude = null;
			skip = false;

			switch (itemFunction) {
			case "DirectoryName":
				var path = MSBuildProjectService.FromMSBuildPath (context.Project.BaseDirectory, item.Include);
				evaluatedInclude = Path.GetDirectoryName (path);
				return true;
			case "Metadata":
				if (itemFunctionArgs.Length != 1)
					return false;
				var p = item.Metadata.GetProperty (itemFunctionArgs [0].ToString ());
				if (p == null) {
					skip = true;
					return true;
				} else {
					evaluatedInclude = p.Value;
					return true;
				}
			}
			if (knownStringItemFunctions.Contains (itemFunction)) {
				object res;
				if (context.EvaluateMember (itemFunction.AsSpan (), typeof (string), itemFunction, item.Include, itemFunctionArgs, out res)) {
					evaluatedInclude = res.ToString ();
					return true;
				}
			}
			return false;
		}

		static bool ExecuteSummaryItemFunction (MSBuildItemEvaluated [] transformItems, string itemFunction, object [] itemFunctionArgs, out string result)
		{
			result = null;
			switch (itemFunction) {
			case "Count":
				result = transformItems.Length.ToString ();
				return true;
			case "AnyHaveMetadataValue":
				if (itemFunctionArgs.Length != 2)
					return false;
				result = transformItems.Any (it => string.Compare (it.Metadata.GetValue (itemFunctionArgs [0].ToString ()), itemFunctionArgs [1].ToString (), true) == 0).ToString ().ToLower ();
				return true;
			}
			return false;
		}

		static bool ExecuteTransformItemListFunction (ref MSBuildItemEvaluated[] transformItems, string itemFunction, object[] itemFunctionArgs, out bool ignoreMetadata)
		{
			switch (itemFunction) {
			case "Reverse":
				ignoreMetadata = false;
				transformItems = transformItems.Reverse ().ToArray ();
				return true;
			case "HasMetadata":
				ignoreMetadata = false;
				if (itemFunctionArgs.Length != 1)
					return false;
				transformItems = transformItems.Where (it => it.Metadata.HasProperty (itemFunctionArgs [0].ToString ())).ToArray ();
				return true;
			case "WithMetadataValue":
				ignoreMetadata = false;
				if (itemFunctionArgs.Length != 2)
					return false;
				transformItems = transformItems.Where (it => string.Compare (it.Metadata.GetValue (itemFunctionArgs [0].ToString ()), itemFunctionArgs [1].ToString (), true) == 0).ToArray ();
				return true;
			case "ClearMetadata":
				ignoreMetadata = true;
				return true;
			case "Distinct":
			case "DistinctWithCase":
				var values = new HashSet<string> (itemFunction == "Distinct" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
				var result = new List<MSBuildItemEvaluated> ();
				foreach (var it in transformItems) {
					if (values.Add (it.Include))
						result.Add (it);
				}
				transformItems = result.ToArray ();
				ignoreMetadata = true;
				return true;
			}
			ignoreMetadata = false;
			return false;
		}

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildImportGroup imports, bool evalItems)
		{
			if (!SafeParseAndEvaluate (project, context, imports.Condition, true))
				return;

			foreach (var item in imports.Imports)
				Evaluate (project, context, item, evalItems);
		}

		static bool IsWildcardInclude (string include)
		{
			return include.IndexOfAny (wildcards) != -1;
		}

		IEnumerable<MSBuildItemEvaluated> ExpandWildcardFilePath (ProjectInfo pinfo, MSBuildEvaluationContext context, MSBuildItem sourceItem, string path, Regex directoryExcludeRegex)
		{
			var subpath = SplitWildcardFilePath (path);
		
			MSBuildProject project = pinfo.Project;
			WildcardExpansionFunc<MSBuildItemEvaluated> func = delegate (string file, string include, string recursiveDir) {
				return CreateEvaluatedItem (context, pinfo, project, sourceItem, include, file, recursiveDir);
			};
			MSBuildProject rootProject = pinfo.GetRootMSBuildProject ();
			return ExpandWildcardFilePath (rootProject, rootProject.BaseDirectory, FilePath.Null, false, subpath, func, directoryExcludeRegex);
		}

		static IEnumerable<string> GetIncludesForWildcardFilePath (MSBuildProject project, string path, Regex directoryExcludeRegex = null)
		{
			var subpath = SplitWildcardFilePath (path);

			WildcardExpansionFunc<string> func = (file, include, recursiveDir) => include;
			return ExpandWildcardFilePath (project, project.BaseDirectory, FilePath.Null, false, subpath, func, directoryExcludeRegex);
		}

		static string[] SplitWildcardFilePath (string path)
		{
			path = path.Replace ('/', '\\');
			if (path == "**" || path.EndsWith ("\\**", StringComparison.Ordinal))
				path = path + "\\*";
			return path.Split ('\\');
		}

		delegate T WildcardExpansionFunc<T> (string filePath, string include, string recursiveDir);

		static IEnumerable<T> ExpandWildcardFilePath<T> (MSBuildProject project, FilePath basePath, FilePath baseRecursiveDir, bool recursive, in ReadOnlySpan<string> filePathInput, WildcardExpansionFunc<T> func, Regex directoryExcludeRegex)
		{
			var res = Enumerable.Empty<T> ();

			if (filePathInput.Length == 0)
				return res;

			var path = filePathInput [0];
			var filePath = filePathInput.Slice (1);

			if (path == "..")
				return ExpandWildcardFilePath (project, basePath.ParentDirectory, baseRecursiveDir, recursive, filePath, func, directoryExcludeRegex);

			if (path == ".")
				return ExpandWildcardFilePath (project, basePath, baseRecursiveDir, recursive, filePath, func, directoryExcludeRegex);

			if (directoryExcludeRegex != null && directoryExcludeRegex.IsMatch (basePath.ToString ().Replace ('/', '\\')))
				return res;

			if (!Directory.Exists (basePath))
				return res;

			if (path == "**") {
				// if this is the last component of the path, there isn't any file specifier, so there is no possible match
				if (filePath.Length == 0)
					return res;

				// If baseRecursiveDir has already been set, don't overwrite it.
				if (baseRecursiveDir.IsNullOrEmpty)
					baseRecursiveDir = basePath;

				return ExpandWildcardFilePath (project, basePath, baseRecursiveDir, true, filePath, func, directoryExcludeRegex);
			}

			if (filePath.Length == 0) {
				// Last path component. It has to be a file specifier.
				string baseDir = basePath.ToRelative (project.BaseDirectory).ToString ().Replace ('/', '\\');
				if (baseDir == ".")
					baseDir = "";
				else if (!baseDir.EndsWith ("\\", StringComparison.Ordinal))
					baseDir += '\\';
				var recursiveDir = baseRecursiveDir.IsNullOrEmpty ? FilePath.Null : basePath.ToRelative (baseRecursiveDir);
				res = FastConcat (res, Directory.GetFiles (basePath, path).Select (f => func (f, baseDir + Path.GetFileName (f), recursiveDir)));
			} else {
				// Directory specifier
				// Look for matching directories.
				// The search here is non-recursive, not matter what the 'recursive' parameter says, since we are trying to match a subpath.
				// The recursive search is done below.

				if (path.IndexOfAny (wildcards) != -1) {
					foreach (var dir in Directory.EnumerateDirectories (basePath, path))
						res = FastConcat (res, ExpandWildcardFilePath (project, dir, baseRecursiveDir, false, filePath, func, directoryExcludeRegex));
				} else
					res = FastConcat (res, ExpandWildcardFilePath (project, basePath.Combine (path), baseRecursiveDir, false, filePath, func, directoryExcludeRegex));
			}

			if (recursive) {
				// Recursive search. Try to match the remaining subpath in all subdirectories.
				foreach (var dir in Directory.EnumerateDirectories (basePath))
					res = FastConcat (res, ExpandWildcardFilePath (project, dir, baseRecursiveDir, true, filePathInput, func, directoryExcludeRegex));
			}

			return res;

			static IEnumerable<T> FastConcat (IEnumerable<T> first, IEnumerable<T> maybeEmpty)
				=> maybeEmpty == Enumerable.Empty<T> () ? first : first.Concat (maybeEmpty);
		}

		static string ExcludeToRegex (string exclude, bool excludeDirectoriesOnly = false)
		{
			exclude = exclude.Replace ('/', '\\').Replace (@"\\", @"\");
			var sb = StringBuilderCache.Allocate ();
			foreach (var ep in exclude.Split (new char [] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
				var ex = ep.AsSpan ().Trim ();
				if (excludeDirectoriesOnly) {
					if (ex.EndsWith (@"\**".AsSpan (), StringComparison.OrdinalIgnoreCase))
						ex = ex.Slice (0, ex.Length - 3);
					else
						continue;
				}
                if (sb.Length > 0)
					sb.Append ('|');
				sb.Append ('^');
                for (int n = 0; n < ex.Length; n++) {
					var c = ex [n];
					if (c == '*') {
						if (n < ex.Length - 1 && ex [n + 1] == '*') {
							if (n < ex.Length - 2 && ex [n + 2] == '\\') {
								// zero or more subdirectories
								sb.Append ("(.*\\\\)?");
								n += 2;
							} else {
								sb.Append (".*");
								n++;
							}
						}
						else
							sb.Append ("[^\\\\]*");
					} else if (regexEscapeChars.Contains (c)) {
						sb.Append ('\\').Append (c);
					} else
						sb.Append (c);
				}
				sb.Append ('$');
			}
			return Core.StringBuilderCache.ReturnAndFree (sb);
        }

		static char [] regexEscapeChars = { '\\', '^', '$', '{', '}', '[', ']', '(', ')', '.', '*', '+', '?', '|', '<', '>', '-', '&' };

		static bool IsIncludeTransform (string include)
		{
			return include.Length > 3 && include [0] == '@' && include [1] == '(' && include [include.Length - 1] == ')';
		}

		MSBuildItemEvaluated CreateEvaluatedItem (MSBuildEvaluationContext context, ProjectInfo pinfo, MSBuildProject project, MSBuildItem sourceItem, string include, string evaluatedFile = null, string recursiveDir = null)
		{
			include = StringInternPool.AddShared (include);

			var it = new MSBuildItemEvaluated (project, sourceItem.Name, sourceItem.Include, include);
			var md = new Dictionary<string,IMSBuildPropertyEvaluated> ();
			// Only evaluate properties for non-transforms.
			if (!IsIncludeTransform (include)) {
				try {
					context.SetItemContext (include, evaluatedFile, recursiveDir);
					foreach (var c in sourceItem.Metadata.GetProperties ()) {
						if (SafeParseAndEvaluate (pinfo, context, c.Condition, true))
							md [c.Name] = new MSBuildPropertyEvaluated (project, c.Name, c.Value, context.EvaluateString (c.Value)) { Condition = c.Condition };
					}
				} finally {
					context.ClearItemContext ();
				}
			}
			((MSBuildPropertyGroupEvaluated)it.Metadata).SetProperties (md);
			it.AddSourceItem (sourceItem);
			it.Condition = sourceItem.Condition;
			return it;
		}

		static char[] wildcards = { '*', '?' };

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildProperty prop)
		{
			if (!SafeParseAndEvaluate (project, context, prop.Condition, true)) {
				return;
			}

			bool needsItemEvaluation;
			var val = context.Evaluate (prop.UnevaluatedValue, out needsItemEvaluation);
			if (needsItemEvaluation)
				context.SetPropertyNeedsTransformEvaluation (prop.Name);
			StoreProperty (project, prop.Name, prop.UnevaluatedValue, val);
			context.SetPropertyValue (prop.Name, val);
		}

		IReadOnlyList<ProjectInfo> GetImportedProjects (ProjectInfo project, MSBuildImport import)
		{
			List<ProjectInfo> prefProjects;
			if (project.ImportedProjects.TryGetValue (import, out prefProjects))
				return prefProjects;
			return Array.Empty<ProjectInfo> ();
		}

		void AddImportedProject (ProjectInfo project, MSBuildImport import, ProjectInfo imported)
		{
			List<ProjectInfo> prefProjects;
			if (!project.ImportedProjects.TryGetValue (import, out prefProjects))
				project.ImportedProjects [import] = prefProjects = new List<ProjectInfo> ();
			prefProjects.Add (imported);
        }

		void DisposeImportedProjects (ProjectInfo pi)
		{
			foreach (var imported in pi.ImportedProjects.Values.SelectMany (i => i)) {
				DisposeImportedProjects (imported);
				DisposeProjectInstance (imported);
			}
		}

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildImport import, bool evalItems)
		{
			if (evalItems) {
				// Properties have already been evaluated
				// Don't evaluate properties, only items and other elements
				var importedProjects = GetImportedProjects (project, import);
				for (int i = 0; i < importedProjects.Count; ++i) {
					var p = importedProjects [i];
					
					EvaluateProject (p, new MSBuildEvaluationContext (context), true);

					foreach (var it in p.EvaluatedItems) {
						it.IsImported = true;
						project.EvaluatedItems.Add (it);
					}
					foreach (var it in p.EvaluatedItemsIgnoringCondition) {
						it.IsImported = true;
						project.EvaluatedItemsIgnoringCondition.Add (it);
					}
					foreach (var it in p.EvaluatedItemDefinitions) {
						project.EvaluatedItemDefinitions.Add (it);
					}
					foreach (var t in p.Targets) {
						t.IsImported = true;
						project.Targets.Add (t);
					}
					foreach (var t in p.TargetsIgnoringCondition) {
						t.IsImported = true;
						project.TargetsIgnoringCondition.Add (t);
					}
					project.ConditionedProperties.Append (p.ConditionedProperties);
					project.GlobIncludes.AddRange (p.GlobIncludes);
				}
				return;
            }


			// Try importing the files using the import as is

			bool keepSearching;

			// If the import is an SDK import, this will contain the SDKs path used to resolve the import.
			string resolvedSdksPath;

			var files = GetImportFiles (project, context, import, null, null, out resolvedSdksPath, out keepSearching);
			if (files != null) {
				foreach (var f in files)
					ImportFile (project, context, import, f, resolvedSdksPath);
			}

			// We may need to keep searching if the import was not found, or if the import had a wildcard.
			// In that case, look in fallback search paths

			if (keepSearching) {
				// Short-circuit if we don't have an import that is done via a property.
				int propertyStart = import.Project.IndexOf ("$(", StringComparison.Ordinal);
				if (propertyStart == -1)
					return;
				
				var importSearchPaths = context.GetProjectImportSearchPaths ();
				for (int i = 0; i < importSearchPaths.Count; ++i) {
					var prop = importSearchPaths [i];

					// Start searching from where the property was found.
					if (import.Project.IndexOf (prop.MSBuildProperty, propertyStart, StringComparison.OrdinalIgnoreCase) == -1)
						continue;

					files = GetImportFiles (project, context, import, prop.Property, prop.Path, out resolvedSdksPath, out keepSearching);
					if (files != null) {
						foreach (var f in files)
							ImportFile (project, context, import, f, resolvedSdksPath);
					}
					if (!keepSearching)
						break;
				}
			}
		}

		string[] GetImportFiles (ProjectInfo project, MSBuildEvaluationContext context, MSBuildImport import, string pathProperty, string pathPropertyValue, out string resolvedSdksPath, out bool keepSearching)
		{
			// This methods looks for targets in location specified by the import, and replacing pathProperty by a specific value.

			if (pathPropertyValue != null) {
				var tempCtx = new MSBuildEvaluationContext (context);
				var mep = MSBuildProjectService.ToMSBuildPath (null, pathPropertyValue);
				tempCtx.SetContextualPropertyValue (pathProperty, mep);
				context = tempCtx;
			}

			resolvedSdksPath = null;
			var projectPath = context.EvaluateString (import.Project);
			project.Imports [import] = projectPath;

			string basePath;
			if (!string.IsNullOrEmpty (import.Sdk) && SdkReference.TryParse (import.Sdk, out var sdkRef)) {
				basePath = SdkResolution.GetResolver (project.GetRootMSBuildProject ().TargetRuntime).GetSdkPath (sdkRef, CustomLoggingService.Instance, null, project.Project.FileName, project.Project.SolutionDirectory);
				if (basePath == null) {
					keepSearching = true;
					return null;
				} else
					// We return here the value of $(MSBuildSDKsPath) where this SDK is located
					resolvedSdksPath = ((FilePath)basePath).ParentDirectory.ParentDirectory;
			} else
				basePath = project.Project.BaseDirectory;

			if (!SafeParseAndEvaluate (project, context, import.Condition, true, basePath)) {
				// Condition evaluates to false. Keep searching because maybe another value for the path property makes
				// the condition evaluate to true.
				keepSearching = true;
				return null;
			}

			string path = MSBuildProjectService.FromMSBuildPath (basePath, projectPath);

			var fileName = Path.GetFileName (path);

			if (fileName.IndexOfAny (wildcards) == -1) {
				// Not a wildcard. Keep searching if the file doesn't exist.
				var result = File.Exists (path) ? new [] { path } : null;
				keepSearching = result == null;
				return result;
			}
			else {
				// Wildcard import. Always keep searching since we want to import all files that match from all search paths.
				keepSearching = true;
				path = Path.GetDirectoryName (path);
				if (!Directory.Exists (path))
					return null;
				var files = Directory.GetFiles (path, fileName);
				Array.Sort (files);
				return files;
			}
		}

		void ImportFile (ProjectInfo project, MSBuildEvaluationContext context, MSBuildImport import, string file, string resolvedSdksPath)
		{
			if (!File.Exists (file))
				return;

			context.AddImport (file);

			var pref = LoadProject (context, file);
			project.ReferencedProjects.Add (pref);

			var prefProject = new ProjectInfo { Project = pref, Parent = project };
			AddImportedProject (project, import, prefProject);

			var refCtx = new MSBuildEvaluationContext (context);

			// If the imported file belongs to an SDK, set the MSBuildSDKsPath property since some
			// sdk files use that to reference other targets from the same sdk.
			if (resolvedSdksPath != null)
				refCtx.SetContextualPropertyValue ("MSBuildSDKsPath", resolvedSdksPath);

			EvaluateProject (prefProject, refCtx, false);

			foreach (var p in prefProject.Properties) {
				p.Value.IsImported = true;

				// If the importer project already has a value for this property, flag it as defined multiple times
				if (project.Properties.ContainsKey (p.Key))
					p.Value.DefinedMultipleTimes = true;
				
				project.Properties [p.Key] = p.Value;
			}

			context.RemoveImport (file);
		}

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildChoose choose, bool evalItems)
		{
			foreach (var op in choose.GetOptions ()) {
				if (op.IsOtherwise || SafeParseAndEvaluate (project, context, op.Condition, true)) {
					EvaluateObjects (project, context, op.GetAllObjects (), evalItems);
					break;
				}
			}
		}

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildTarget target)
		{
			bool condIsTrue = SafeParseAndEvaluate (project, context, target.Condition);
			var newTarget = new MSBuildTarget (target.Name, target.Tasks);
			newTarget.AfterTargets = context.EvaluateString (target.AfterTargets);
			newTarget.Inputs = context.EvaluateString (target.Inputs);
			newTarget.Outputs = context.EvaluateString (target.Outputs);
			newTarget.BeforeTargets = context.EvaluateString (target.BeforeTargets);
			newTarget.DependsOnTargets = context.EvaluateString (target.DependsOnTargets);
			newTarget.Returns = context.EvaluateString (target.Returns);
			newTarget.KeepDuplicateOutputs = context.EvaluateString (target.KeepDuplicateOutputs);
			project.TargetsIgnoringCondition.Add (newTarget);
			if (condIsTrue)
				project.Targets.Add (newTarget);
		}

		void Evaluate (ProjectInfo project, MSBuildEvaluationContext context, MSBuildItemDefinitionGroup items)
		{
			bool conditionIsTrue = SafeParseAndEvaluate (project, context, items.Condition);

			foreach (var item in items.Items) {
				var trueCond = conditionIsTrue && SafeParseAndEvaluate (project, context, item.Condition);
				if (trueCond) {
					var it = CreateEvaluatedItem (context, project, project.Project, item, string.Empty);
					project.EvaluatedItemDefinitions.Add (it);
				}
			}
		}

		ImmutableDictionary<string, ConditionExpression> conditionCache = ImmutableDictionary<string, ConditionExpression>.Empty;
		bool SafeParseAndEvaluate (ProjectInfo project, MSBuildEvaluationContext context, string condition, bool collectConditionedProperties = false, string customEvalBasePath = null)
		{
			try {
				if (string.IsNullOrEmpty (condition))
					return true;

				context.CustomFullDirectoryName = customEvalBasePath;

				try {
					ConditionExpression ce = ImmutableInterlocked.GetOrAdd (ref conditionCache, condition, key => ConditionParser.ParseCondition (key));

					if (collectConditionedProperties)
						ce.CollectConditionProperties (project.ConditionedProperties);

					if (!ce.TryEvaluateToBool (context, out bool value))
						throw new InvalidProjectFileException (String.Format ("Can not evaluate \"{0}\" to bool.", condition));

					return value;
				} catch (ExpressionParseException epe) {
					throw new InvalidProjectFileException (
						String.Format ("Unable to parse condition \"{0}\" : {1}", condition, epe.Message),
						epe);
				} catch (ExpressionEvaluationException epe) {
					throw new InvalidProjectFileException (
						String.Format ("Unable to evaluate condition \"{0}\" : {1}", condition, epe.Message),
						epe);
				}
			}
			catch {
				// The condition is likely to be invalid
				return false;
			}
			finally {
				context.CustomFullDirectoryName = null;
			}
		}

		void StoreProperty (ProjectInfo project, string name, string value, string finalValue)
		{
			PropertyInfo pi;
			if (project.Properties.TryGetValue (name, out pi)) {
				pi.Value = value;
				pi.FinalValue = finalValue;
				pi.DefinedMultipleTimes = true;
			} else
				project.Properties [name] = new PropertyInfo { Name = name, Value = value, FinalValue = finalValue };
		}

		public override bool GetItemHasMetadata (object item, string name)
		{
			var it = item as MSBuildItem;
			if (it != null)
				return it.Metadata.HasProperty (name);
			return ((IMSBuildItemEvaluated) item).Metadata.HasProperty (name);
		}

		public override string GetItemMetadata (object item, string name)
		{
			var it = item as MSBuildItem;
			if (it != null)
				return it.Metadata.GetValue (name);
			return ((IMSBuildItemEvaluated)item).Metadata.GetValue (name);
		}

		public override string GetEvaluatedItemMetadata (object item, string name)
		{
			IMSBuildItemEvaluated it = (IMSBuildItemEvaluated) item;
			return it.Metadata.GetValue (name);
		}

		public override IEnumerable<string> GetItemMetadataNames (object item)
		{
			var it = item as MSBuildItem;
			if (it != null)
				return it.Metadata.GetProperties ().Select (p => p.Name);
			return ((IMSBuildItemEvaluated)item).Metadata.GetProperties ().Select (p => p.Name);
		}

		public override IEnumerable<object> GetImports (object projectInstance)
		{
			return ((ProjectInfo)projectInstance).Project.Imports;
		}

		public override string GetImportEvaluatedProjectPath (object projectInstance, object import)
		{
			return ((ProjectInfo)projectInstance).Imports [(MSBuildImport)import];
		}

		public override IEnumerable<object> GetEvaluatedItems (object projectInstance)
		{
			return ((ProjectInfo)projectInstance).EvaluatedItems;
		}

		public override IEnumerable<object> GetEvaluatedItemsIgnoringCondition (object projectInstance)
		{
			return ((ProjectInfo)projectInstance).EvaluatedItemsIgnoringCondition;
		}

		public override IEnumerable<object> GetEvaluatedProperties (object projectInstance)
		{
			return ((ProjectInfo)projectInstance).Properties.Values;
		}

		public override void GetItemInfo (object item, out string name, out string include, out string finalItemSpec, out bool imported)
		{
			var it = (MSBuildItem) item;
			name = it.Name;
			include = it.Include;
			finalItemSpec = it.Include;
			imported = it.IsImported;
		}

		public override void GetEvaluatedItemInfo (object item, out string name, out string include, out string finalItemSpec, out bool imported)
		{
			var it = (IMSBuildItemEvaluated) item;
			name = it.Name;
			include = it.Include;
			finalItemSpec = it.Include;
			imported = it.IsImported;
		}

		public override void GetPropertyInfo (object property, out string name, out string value, out string finalValue, out bool definedMultipleTimes)
		{
			var prop = (PropertyInfo)property;
			name = prop.Name;
			value = prop.Value;
			finalValue = prop.FinalValue;
			definedMultipleTimes = prop.DefinedMultipleTimes;
		}

		public override IEnumerable<MSBuildTarget> GetTargets (object projectInstance)
		{
			return ((ProjectInfo)projectInstance).Targets;
		}

		public override IEnumerable<MSBuildTarget> GetTargetsIgnoringCondition (object projectInstance)
		{
			return ((ProjectInfo)projectInstance).TargetsIgnoringCondition;
		}

		public override void SetGlobalProperty (object projectInstance, string property, string value)
		{
			var pi = (ProjectInfo)projectInstance;
			pi.GlobalProperties [property] = value;
		}

		public override void RemoveGlobalProperty (object projectInstance, string property)
		{
			var pi = (ProjectInfo)projectInstance;
			pi.GlobalProperties.Remove (property);
		}

		public override ConditionedPropertyCollection GetConditionedProperties (object projectInstance)
		{
			var pi = (ProjectInfo)projectInstance;
			return pi.ConditionedProperties;
		}

		public override IEnumerable<MSBuildItem> FindGlobItemsIncludingFile (object projectInstance, string include)
		{
			var pi = (ProjectInfo)projectInstance;
			string filePath = MSBuildProjectService.FromMSBuildPath (pi.Project.BaseDirectory, include);
			foreach (var g in pi.GlobIncludes) {
				if (!g.Condition)
					continue;

				if (g.ExcludeRegex != null) {
					if (g.ExcludeRegex.IsMatch (include))
						continue;
				}
				if (g.RemoveRegex != null) {
					if (g.RemoveRegex.IsMatch (include))
						continue;
				}
				if (IsIncludedInGlob (pi.Project.BaseDirectory, filePath, false, g.IncludeSplit))
					yield return g.Item;
			}
		}

		internal override IEnumerable<MSBuildItem> FindUpdateGlobItemsIncludingFile (object projectInstance, string include, MSBuildItem globItem)
		{
			var pi = (ProjectInfo)projectInstance;
			foreach (var g in pi.GlobIncludes.Where (g => g.Condition && g.Item == globItem && g.Updates != null)) {
				foreach (var update in g.Updates) {
					if (update.UpdateRegex.IsMatch (include)) {
						yield return update.Item;
					}
				}
			}
		}

		bool IsIncludedInGlob (FilePath basePath, FilePath file, bool recursive, in ReadOnlySpan<string> filePath)
		{
			if (filePath.Length <= 0)
				return false;

			var path = filePath [0];

			if (path == "..")
				return IsIncludedInGlob (basePath.ParentDirectory, file, recursive, filePath.Slice (1));

			if (path == ".")
				return IsIncludedInGlob (basePath, file, recursive, filePath.Slice (1));

			if (!Directory.Exists (basePath))
				return false;

			if (path == "**") {
				// if this is the last component of the path, there isn't any file specifier, so there is no possible match
				if (filePath.Length <= 1)
					return false;
				return IsIncludedInGlob (basePath, file, true, filePath.Slice (1));
			}

			if (filePath.Length == 1) {
				// Last path component. It has to be a file specifier.
				if (!file.IsChildPathOf (basePath))
					return false;

				foreach (var f in Directory.EnumerateFiles (basePath, path)) {
					if (f == file)
						return true;
				}
			} else {
				// Directory specifier
				// Look for matching directories.
				// The search here is non-recursive, not matter what the 'recursive' parameter says, since we are trying to match a subpath.
				// The recursive search is done below.

				if (path.IndexOfAny (wildcards) != -1) {
					foreach (var dir in Directory.EnumerateDirectories (basePath, path)) {
						if (IsIncludedInGlob (dir, file, false, filePath.Slice (1)))
							return true;
					}
				} else if (IsIncludedInGlob (basePath.Combine (path), file, false, filePath.Slice (1)))
					return true;
			}

			if (recursive) {
				// Recursive search. Try to match the remaining subpath in all subdirectories.
				foreach (var dir in Directory.EnumerateDirectories (basePath))
					if (IsIncludedInGlob (dir, file, true, filePath))
						return true;
			}

			return false;
		}

		internal override IEnumerable<object> GetEvaluatedItemDefinitions (object projectInstance)
		{
			return ((ProjectInfo)projectInstance).EvaluatedItemDefinitions;
		}

		#endregion

		#region Logging


		private void LogBeginEvaluationStage (MSBuildEvaluationContext context, string v)
		{
			if (context.Log != null) {
				context.Log.PushTask (v);
				context.Log.Indent += 2;
			}
		}

		private void LogEndEvaluationStage (MSBuildEvaluationContext context)
		{
			if (context.Log != null) {
				context.Log.PopTask ();
				context.Log.LogMessage ("");
			}
		}

		public void LogInitialEnvironment (MSBuildEvaluationContext context)
		{
			if (context.Log != null && context.Log.Flags.HasFlag (MSBuildLogFlags.Properties)) {
				context.Log.LogMessage ("Environment at start of build:");
				context.Dump ();
				context.Log.LogMessage ("");
			}
		}

		void LogBeginProjectFileLoad (MSBuildEvaluationContext context, FilePath fileName)
		{
			if (context.Log != null)
				context.Log.PushTask ("Load Project: " + fileName);
		}

		void LogEndProjectFileLoad (MSBuildEvaluationContext context)
		{
			if (context.Log != null)
				context.Log.PopTask ();
		}

		void LogBeginEvalProject (MSBuildEvaluationContext context, ProjectInfo pinfo)
		{
			if (context.Log != null) {
				context.Log.PushTask ("Evaluate Project: " + pinfo.Project.FileName);
				if (context.Log.Flags.HasFlag (MSBuildLogFlags.Properties)) {
					context.Log.LogMessage ("");
					context.Log.LogMessage ("Initial Properties:");
					context.Dump ();
					context.Log.LogMessage ("");
				}
			}
		}

		void LogEndEvalProject (MSBuildEvaluationContext context, ProjectInfo pinfo)
		{
			if (context.Log != null)
				context.Log.PopTask ();
		}

		#endregion
	}

	abstract class MSBuildEngineLogger
	{
		class LogTask {
			public string Name;
			public long Timestamp;
		}

		long initialTimestamp;
		Stack<LogTask> timeStack = new Stack<LogTask> ();

		internal int Indent { get; set; }

		public MSBuildLogFlags Flags { get; set; }

		public MSBuildEngineLogger ()
		{
			initialTimestamp = System.Diagnostics.Stopwatch.GetTimestamp ();
		}

		public void PushTask (string name)
		{
			var tt = System.Diagnostics.Stopwatch.GetTimestamp ();
			var elapsed = (tt - initialTimestamp) / (System.Diagnostics.Stopwatch.Frequency / 1000);
			LogMessage (name + " (t:" + elapsed + ")");
			Indent += 2;
			timeStack.Push (new LogTask { Name = name, Timestamp = tt });
		}

		public void PopTask ()
		{
			var t = System.Diagnostics.Stopwatch.GetTimestamp ();
			var task = timeStack.Pop ();
			var elapsed = (t - task.Timestamp) / (System.Diagnostics.Stopwatch.Frequency / 1000);
			Indent -= 2;
			LogMessage ($"Done {task.Name} ({elapsed}ms)");
		}

		public abstract void LogMessage (string s);
	}

	enum MSBuildLogFlags
	{
		Properties = 0b0001,
		Items = 0b0010
	}

	class ConsoleMSBuildEngineLogger: MSBuildEngineLogger
	{
		public override void LogMessage (string s)
		{
			Console.WriteLine (new string (' ', Indent) + s);
		}
	}
}
