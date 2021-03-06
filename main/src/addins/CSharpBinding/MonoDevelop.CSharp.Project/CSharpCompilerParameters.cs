//
// CSharpCompilerParameters.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2009 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host;
using MonoDevelop.Core;
using MonoDevelop.Core.Serialization;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

namespace MonoDevelop.CSharp.Project
{
	/// <summary>
	/// This class handles project specific compiler parameters
	/// </summary>
	public class CSharpCompilerParameters : DotNetCompilerParameters
	{
		// Configuration parameters
		FilePath codeAnalysisRuleSet;
		int? warninglevel = 4;

		[ItemProperty ("NoWarn", DefaultValue = "")]
		string noWarnings = String.Empty;

		bool? optimize = false;

		[ItemProperty ("AllowUnsafeBlocks", DefaultValue = false)]
		bool unsafecode = false;

		[ItemProperty ("CheckForOverflowUnderflow", DefaultValue = false)]
		bool generateOverflowChecks;

		[ItemProperty ("DefineConstants", DefaultValue = "")]
		string definesymbols = String.Empty;

		[ProjectPathItemProperty ("DocumentationFile")]
		FilePath documentationFile;

		[ItemProperty ("LangVersion", DefaultValue = "Default")]
		string langVersion = "Default";

		[ItemProperty ("NoStdLib", DefaultValue = false)]
		bool noStdLib;

		[ItemProperty ("TreatWarningsAsErrors", DefaultValue = false)]
		bool treatWarningsAsErrors;

		[ItemProperty ("PlatformTarget", DefaultValue = "anycpu")]
		string platformTarget = "anycpu";

		[ItemProperty ("WarningsNotAsErrors", DefaultValue = "")]
		string warningsNotAsErrors = "";

		[ItemProperty ("Nullable", DefaultValue = "")]
		string nullableContextOptions = "";

		string outputType;

		protected override void Write (IPropertySet pset)
		{
			pset.SetPropertyOrder ("DebugSymbols", "DebugType", "Optimize", "OutputPath", "DefineConstants", "ErrorReport", "WarningLevel", "TreatWarningsAsErrors", "DocumentationFile");

			base.Write (pset);

			if (optimize.HasValue)
				pset.SetValue ("Optimize", optimize.Value);
			if (warninglevel.HasValue)
				pset.SetValue ("WarningLevel", warninglevel.Value);
		}

		protected override void Read (IPropertySet pset)
		{
			base.Read (pset);

			var prop = pset.GetProperty ("GenerateDocumentation");
			if (prop != null && documentationFile != null) {
				if (prop.GetValue<bool> ())
					documentationFile = ParentConfiguration.CompiledOutputName.ChangeExtension (".xml");
				else
					documentationFile = null;
			}

			optimize = pset.GetValue ("Optimize", (bool?)null);
			warninglevel = pset.GetValue<int?> ("WarningLevel", null);
			outputType = pset.GetValue ("OutputType", "Library");
			codeAnalysisRuleSet = pset.GetPathValue ("CodeAnalysisRuleSet");
		}

		static MetadataReferenceResolver CreateMetadataReferenceResolver (IMetadataService metadataService, string projectDirectory, string outputDirectory)
		{
			ImmutableArray<string> assemblySearchPaths;
			if (projectDirectory != null && outputDirectory != null) {
				assemblySearchPaths = ImmutableArray.Create (projectDirectory, outputDirectory);
			} else if (projectDirectory != null) {
				assemblySearchPaths = ImmutableArray.Create (projectDirectory);
			} else if (outputDirectory != null) {
				assemblySearchPaths = ImmutableArray.Create (outputDirectory);
			} else {
				assemblySearchPaths = ImmutableArray<string>.Empty;
			}

			return new WorkspaceMetadataFileReferenceResolver (metadataService, new RelativePathResolver (assemblySearchPaths, baseDirectory: projectDirectory));
		}

		public override CompilationOptions CreateCompilationOptions ()
		{
			var project = (CSharpProject)ParentProject;
			var workspace = IdeApp.TypeSystemService.GetWorkspace (project.ParentSolution);
			var metadataReferenceResolver = CreateMetadataReferenceResolver (
					workspace.Services.GetService<IMetadataService> (),
					project.BaseDirectory,
					ParentConfiguration.OutputDirectory
			);

			var outputKind = outputType == null ? GetOutputKindFromProject (project) : OutputTypeToOutputKind (outputType);
			bool isLibrary = outputKind == OutputKind.DynamicallyLinkedLibrary;
			string mainTypeName = project.MainClass;
			if (isLibrary || mainTypeName == string.Empty) {
				// empty string is not accepted by Roslyn
				mainTypeName = null;
			}

			var options = new CSharpCompilationOptions (
				outputKind,
				mainTypeName: mainTypeName,
				scriptClassName: "Script",
				optimizationLevel: Optimize ? OptimizationLevel.Release : OptimizationLevel.Debug,
				checkOverflow: GenerateOverflowChecks,
				allowUnsafe: UnsafeCode,
				cryptoKeyFile: ParentConfiguration.SignAssembly ? ParentConfiguration.AssemblyKeyFile : null,
				cryptoPublicKey: ImmutableArray<byte>.Empty,
				platform: GetPlatform (),
				publicSign: ParentConfiguration.PublicSign,
				delaySign: ParentConfiguration.DelaySign,
				generalDiagnosticOption: TreatWarningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default,
				warningLevel: WarningLevel,
				specificDiagnosticOptions: GetSpecificDiagnosticOptions (),
				concurrentBuild: true,
				metadataReferenceResolver: metadataReferenceResolver,
				assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
				strongNameProvider: new DesktopStrongNameProvider (),
				nullableContextOptions: NullableContextOptions
			);

			return options;
		}

		static OutputKind GetOutputKindFromProject (CSharpProject project)
		{
			switch (project.CompileTarget) {
			case CompileTarget.Exe:
				return OutputKind.ConsoleApplication;
			case CompileTarget.WinExe:
				return OutputKind.WindowsApplication;
			case CompileTarget.Module:
				return OutputKind.NetModule;
			default:
				return OutputKind.DynamicallyLinkedLibrary;
			}
		}

		static OutputKind OutputTypeToOutputKind (string outputType)
		{
			switch (outputType.ToLowerInvariant ()) {
			case "exe":
				return OutputKind.ConsoleApplication;
			case "winexe":
				return OutputKind.WindowsApplication;
			case "module":
				return OutputKind.NetModule;
			default:
				return OutputKind.DynamicallyLinkedLibrary;
			}
		}

		Dictionary<string, ReportDiagnostic> GetSpecificDiagnosticOptions ()
		{
			var result = new Dictionary<string, ReportDiagnostic> ();

			var globalRuleSet = IdeApp.TypeSystemService.RuleSetManager.GetGlobalRuleSet ();
			if (globalRuleSet != null) {
				AddSpecificDiagnosticOptions (result, globalRuleSet);
			}

			var ruleSet = GetRuleSet (codeAnalysisRuleSet);
			if (ruleSet != null) {
				AddSpecificDiagnosticOptions (result, ruleSet);
			}

			foreach (var warning in GetSuppressedWarnings ()) {
				result [warning] = ReportDiagnostic.Suppress;
			}

			return result;
		}

		static RuleSet GetRuleSet (FilePath ruleSetFileName)
		{
			try {
				if (ruleSetFileName.IsNotNull && File.Exists (ruleSetFileName)) {
					return RuleSet.LoadEffectiveRuleSetFromFile (ruleSetFileName);
				}
			} catch (Exception ex) {
				LoggingService.LogError (string.Format ("Unable to load ruleset from file: {0}", ruleSetFileName), ex);
			}
			return null;
		}

		static void AddSpecificDiagnosticOptions (Dictionary<string, ReportDiagnostic> result, RuleSet ruleSet)
		{
			foreach (var kv in ruleSet.SpecificDiagnosticOptions) {
				result [kv.Key] = kv.Value;
			}
		}

		Microsoft.CodeAnalysis.Platform GetPlatform ()
		{
			Microsoft.CodeAnalysis.Platform platform;
			if (Enum.TryParse (PlatformTarget, true, out platform))
				return platform;

			return Microsoft.CodeAnalysis.Platform.AnyCpu;
		}

		IEnumerable<string> GetSuppressedWarnings ()
		{
			string warnings = NoWarnings ?? string.Empty;
			var items = warnings.Split (new [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct ();

			foreach (string warning in items) {
				if (int.TryParse (warning, out _))
					yield return "CS" + warning;
				else
					yield return warning;
			}
		}

		public override ParseOptions CreateParseOptions (DotNetProjectConfiguration configuration)
		{
			var symbols = GetDefineSymbols ();
			if (configuration != null)
				symbols = symbols.Concat (configuration.GetDefineSymbols ()).Distinct ();
			LanguageVersionFacts.TryParse (langVersion, out LanguageVersion lv);

			return new CSharpParseOptions (
				lv,
				DocumentationMode.Parse,
				SourceCodeKind.Regular,
				ImmutableArray<string>.Empty.AddRange (symbols)
			);
		}

		public NullableContextOptions NullableContextOptions {
			get {
				switch (nullableContextOptions.ToLower ()) {
				case "enable":
					return NullableContextOptions.Enable;
				case "warnings":
					return NullableContextOptions.Warnings;
				case "annotations":
					return NullableContextOptions.Annotations;
				case "": // NOTE: Will need to update this if default ever changes
				case "disable":
					return NullableContextOptions.Disable;
				default:
					LoggingService.LogError ("Unknown Nullable string '" + nullableContextOptions + "'");
					return NullableContextOptions.Disable;
				}
			}
			set {
				try {
					if (NullableContextOptions == value) {
						return;
					}
				} catch (Exception) { }

				nullableContextOptions = value.ToString ();
				NotifyChange ();
			}
		}


		public LanguageVersion LangVersion {
			get {
				if (!LanguageVersionFacts.TryParse (langVersion, out LanguageVersion val)) {
					throw new Exception ("Unknown LangVersion string '" + langVersion + "'");
				}
				return val;
			}
			set {
				try {
					if (LangVersion == value) {
						return;
					}
				} catch (Exception) { }

				langVersion = LanguageVersionToString (value);
				NotifyChange ();
			}
		}

		#region Code Generation

		public override void AddDefineSymbol (string symbol)
		{
			var symbols = new List<string> (GetDefineSymbols ());
			symbols.Add (symbol);
			definesymbols = string.Join (";", symbols) + ";";
		}

		public override IEnumerable<string> GetDefineSymbols ()
		{
			return definesymbols.Split (';', ',', ' ', '\t').Where (s => SyntaxFacts.IsValidIdentifier (s) && !string.IsNullOrWhiteSpace (s));
		}

		public override void RemoveDefineSymbol (string symbol)
		{
			var symbols = new List<string> (GetDefineSymbols ());
			symbols.Remove (symbol);

			if (symbols.Count > 0)
				definesymbols = string.Join (";", symbols) + ";";
			else
				definesymbols = string.Empty;
		}

		public string DefineSymbols {
			get {
				return definesymbols;
			}
			set {
				if (definesymbols == (value ?? string.Empty))
					return;
				definesymbols = value ?? string.Empty;
				NotifyChange ();
			}
		}

		public bool Optimize {
			get {
				return optimize ?? false;
			}
			set {
				if (value == Optimize)
					return;
				optimize = value;
				NotifyChange ();
			}
		}

		public bool UnsafeCode {
			get {
				return unsafecode;
			}
			set {
				if (unsafecode == value)
					return;
				unsafecode = value;
				NotifyChange ();
			}
		}

		public bool GenerateOverflowChecks {
			get {
				return generateOverflowChecks;
			}
			set {
				if (generateOverflowChecks == value)
					return;
				generateOverflowChecks = value;
				NotifyChange ();
			}
		}

		public FilePath DocumentationFile {
			get {
				return documentationFile;
			}
			set {
				if (documentationFile == value)
					return;
				documentationFile = value;
				NotifyChange ();
			}
		}

		public string PlatformTarget {
			get {
				return platformTarget;
			}
			set {
				if (platformTarget == (value ?? string.Empty))
					return;
				platformTarget = value ?? string.Empty;
				NotifyChange ();
			}
		}

		#endregion

		#region Errors and Warnings
		public int WarningLevel {
			get {
				return warninglevel ?? 4;
			}
			set {
				int? newLevel = warninglevel ;
				if (warninglevel.HasValue) {
					newLevel = value;
				} else {
					if (value != 4)
						newLevel = value;
				}
				if (warninglevel == newLevel)
					return;
				warninglevel = newLevel;
				NotifyChange ();
			}
		}

		public string NoWarnings {
			get {
				return noWarnings;
			}
			set {
				if (noWarnings == value)
					return;
				noWarnings = value;
				NotifyChange ();
			}
		}

		public override bool NoStdLib {
			get {
				return noStdLib;
			}
			set {
				if (noStdLib == value)
					return;
				noStdLib = value;
				NotifyChange ();
			}
		}

		public bool TreatWarningsAsErrors {
			get {
				return treatWarningsAsErrors;
			}
			set {
				if (treatWarningsAsErrors == value)
					return;
				treatWarningsAsErrors = value;
				NotifyChange ();
			}
		}

		public string WarningsNotAsErrors {
			get {
				return warningsNotAsErrors;
			}
			set {
				if (warningsNotAsErrors == value)
					return;
				warningsNotAsErrors = value;
				NotifyChange ();
			}
		}
		#endregion

		internal static string LanguageVersionToString (LanguageVersion value)
			=> LanguageVersionFacts.ToDisplayString (value);

		void NotifyChange ()
		{
			ParentProject?.NotifyModified ("CompilerParameters");
		}
	}
}
