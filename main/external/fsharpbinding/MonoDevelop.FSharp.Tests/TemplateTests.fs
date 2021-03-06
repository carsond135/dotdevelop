namespace MonoDevelopTests
open System
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading.Tasks
open FsUnit
open FSharp.Compiler.SourceCodeServices
open MonoDevelop.Core
open MonoDevelop.FSharp
open MonoDevelop.Ide
open MonoDevelop.Ide.Projects
open MonoDevelop.Ide.Templates
open MonoDevelop.PackageManagement.Tests.Helpers
open MonoDevelop.Projects
open MonoDevelop.Projects.MSBuild
open MonoDevelop.FSharp.Shared
open NUnit.Framework
open MonoDevelop.Ide.TypeSystem

[<TestFixture; UnitTests.RequireService(typeof<TypeSystemService>); UnitTests.RequireService(typeof<RootWorkspace>)>]
type ``Template tests``() =
    inherit UnitTests.TestBase()
    let toTask computation : Task = Async.StartImmediateAsTask computation :> _

    let monitor = UnitTests.Util.GetMonitor ()
    let templateService = TemplatingService()
    let templateMatch (template:SolutionTemplate) = 
        template.IsMatch (SolutionTemplateVisibility.All)

    let predicate = new Predicate<SolutionTemplate>(templateMatch)

    let rec flattenCategories (category: TemplateCategory) = 
        seq {
            yield category
            yield! category.Categories |> Seq.collect flattenCategories
        }

    let templatesDir = UnitTests.Util.TmpDir / "fsharp-buildtemplates"

    let getErrorsForProject (solution:Solution) =
        asyncSeq {
            let config = solution.DefaultConfigurationSelector
            let ctx = TargetEvaluationContext (LogVerbosity=MSBuildVerbosity.Quiet)
            let! result = solution.Build(monitor, config, ctx) |> Async.AwaitTask
            match result.HasWarnings, result.HasErrors with
            //| "Xamarin.tvOS.FSharp.SingleViewApp", _, false //MTOUCH : warning MT0094: Both profiling (--profiling) and incremental builds (--fastdev) is not supported when building for tvOS. Incremental builds have ben disabled.]
            | false, false ->
                // xbuild worked, now check for editor squiggles
                let projects =
                    solution.Items
                    |> Seq.ofType<DotNetProject> |> List.ofSeq
                for project in projects do
                    let checker = FSharpChecker.Create()
                    let! refs = project.GetReferences (config) |> Async.AwaitTask
                    let projectOptions = languageService.GetProjectOptionsFromProjectFile project config refs
                    let! checkResult = checker.ParseAndCheckProject projectOptions.Value
                    for error in checkResult.Errors do
                        yield "Editor error", error.FileName, error.Message
            | _ ->
                for error in result.Errors do
                    if not error.IsWarning then
                        yield "Build error", error.FileName, error.ErrorText
        }

    let testWithParameters (tt:string) (buildFolder:string) (parameters:string) =
        if not MonoDevelop.Core.Platform.IsMac then
            Assert.Ignore ()

        let projectTemplate = ProjectTemplate.ProjectTemplates |> Seq.find (fun t -> t.Id = tt)
        async {
            let dir = FilePath (templatesDir/buildFolder)
            dir.Delete()
            Directory.CreateDirectory (dir |> string) |> ignore
            let cinfo = new ProjectCreateInformation (ProjectBasePath = dir, ProjectName = tt, SolutionName = tt, SolutionPath = dir)
            cinfo.Parameters.["CreateSharedAssetsProject"] <- "False"
            cinfo.Parameters.["CreatePortableDotNetProject"] <- "True"
            cinfo.Parameters.["CreateMonoTouchProject"] <- "True"
            cinfo.Parameters.["UseXamarinAndroidSupportv7AppCompat"] <- "True"
            cinfo.Parameters.["CreateAndroidProject"] <- "True"
            cinfo.Parameters.["UseUniversal"] <- "True"
            cinfo.Parameters.["UseIPad"] <- "False"
            cinfo.Parameters.["UseIPhone"] <- "False"
            cinfo.Parameters.["CreateiOSUITest"] <- "False"
            cinfo.Parameters.["CreateAndroidUITest"] <- "False"
            cinfo.Parameters.["MinimumOSVersion"] <- "10.7"
            cinfo.Parameters.["AppIdentifier"] <- tt
            cinfo.Parameters.["AndroidMinSdkVersionAttribute"] <- "android:minSdkVersion=\"27\""
            cinfo.Parameters.["AndroidThemeAttribute"] <- ""

            for templateParameter in TemplateParameter.CreateParameters (parameters) do
                cinfo.Parameters.[templateParameter.Name] <- templateParameter.Value

            let! item = projectTemplate.CreateWorkspaceItem (cinfo) |> Async.AwaitTask
            use sln = item :?> Solution

            let createTemplate (template:SolutionTemplate) =
                let config = NewProjectConfiguration(
                                CreateSolution = false,
                                ProjectName = tt,
                                SolutionName = tt,
                                Location = (dir |> string)
                             )

                templateService.ProcessTemplate(template, config, sln.RootFolder)

            let folder = new SolutionFolder()
            let projects = sln.Items |> Seq.filter(fun i -> i :? DotNetProject) |> Seq.cast<DotNetProject> |> List.ofSeq

            // Save solution before installing NuGet packages to prevent any Imports from being added
            // in the wrong place. Android projects now use the Xamarin.Build.Download NuGet package which
            // will add its own .props Import at the top of the project file. Saving the project the first time
            // after installing this NuGet package results in the Xamarin.Android.FSharp.targets Import being
            // added at the top of the project which causes a compile error about the OutputType not being defined.
            // This is because the Import is grouped with the Xamarin.Build.Download .props Import which is inserted
            // at the top of the project file.
            do! sln.SaveAsync(monitor)
            do! NuGetPackageInstaller.InstallPackages (sln, projectTemplate.PackageReferencesForCreatedProjects)
            for error in getErrorsForProject sln do
                Assert.Fail (sprintf "%A" error)

            Assert.Pass()
        } |> toTask

    let test templateId = testWithParameters templateId templateId ""

    [<TestFixtureSetUp>]
    member x.Setup() =
        let config =
            """
            <configuration>
              <config>
                <add key="repositoryPath" value="packages" />
              </config>
              <packageSources>
                  <clear /> <!-- ensure only the sources defined below are used -->
                  <add key="NuGet official package source" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """
        Directory.CreateDirectory templatesDir |> ignore
        let configFileName = templatesDir/"NuGet.Config"
        File.WriteAllText (configFileName, config, Text.Encoding.UTF8)
        // HACK: Work around issue in "Xamarin Forms FSharp ClassLibrary" test
        // the template is broken and doesn't define a framework, so gets the default net45
        // however the base tests UnitTests.TestBase change the default to net40 resulting in
        //"Could not install package 'FSharp.Core 4.5.4'. You are trying to install this package into a project that targets '.NETFramework,Version=v4.0',"
        MonoDevelop.Projects.Services.ProjectService.DefaultTargetFramework
            <- Runtime.SystemAssemblyService.GetTargetFramework (MonoDevelop.Core.Assemblies.TargetFrameworkMoniker.NET_4_5);

    [<Test;AsyncStateMachine(typeof<Task>)>]
    member x.``FSharp portable project``() =
        let name = "FSharpPortableLibrary"
        let projectTemplate = ProjectTemplate.ProjectTemplates |> Seq.find (fun t -> t.Id = name)

        async {
            let dir = FilePath (templatesDir/"fsportable")
            dir.Delete()
            let cinfo = new ProjectCreateInformation (ProjectBasePath = dir, ProjectName = name, SolutionName = name, SolutionPath = dir)
            let! item = projectTemplate.CreateWorkspaceItem (cinfo) |> Async.AwaitTask
            let sln = item :?> Solution
            let proj = sln.Items.[0] :?> FSharpProject
            proj.IsPortableLibrary |> should equal true
        } |> toTask

    [<Test;AsyncStateMachine(typeof<Task>)>]
    member x.``Can build netcoreapp11 MVC web app``()=
        async {
            let projectPath = UnitTests.Util.GetSampleProject ("fsharp-aspnetcoremvc11", "aspnetcoremvc11.sln")

            let! w = Services.ProjectService.ReadWorkspaceItem (monitor, FilePath(projectPath)) |> Async.AwaitTask

            use solution = w :?> Solution

            let project =
                solution.Items
                |> Seq.ofType<DotNetProject>
                |> Seq.head

            let! res = project.RunTarget(monitor, "Restore", ConfigurationSelector.Default)
            do! project.ReevaluateProject (monitor)

            let fsharpFiles =
                project.Files
                |> Seq.filter(fun f -> f.FilePath.Extension = ".fs")
                |> Seq.map(fun f -> f.IsImported)

            fsharpFiles |> Seq.length |> should equal 3
            fsharpFiles |> Seq.iter(fun imported -> imported |> should equal false)

            let wwwrootFiles =
                project.Files
                |> Seq.filter(fun f -> f.FilePath.ToString().Contains("wwwroot"))
                |> Seq.map(fun f -> f.IsImported)

            wwwrootFiles |> Seq.length |> should equal 41
            wwwrootFiles |> Seq.iter(fun imported -> imported |> should equal true)

            for error in getErrorsForProject solution do
                Assert.Fail (sprintf "%A" error)

            Assert.Pass()
        } |> toTask

    [<Ignore("Currently not testable as SDK project is dependent on wizard being ran");AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Forms FSharp FormsApp``()= testWithParameters "Xamarin.Forms.FSharp.FormsApp" "Xamarin.Forms.FSharp.FormsApp" "SafeUserDefinedProjectName=Xamarin_Forms_FSharp_FormsApp_Shared"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``FSharpPortableLibrary``()= test "FSharpPortableLibrary"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Forms FSharp ClassLibrary``()= test "Xamarin.Forms.FSharp.ClassLibrary"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Forms FSharp UITestApp-Mac``()= test "Xamarin.Forms.FSharp.UITestApp-Mac"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin iOS FSharp SingleViewApp``()= test "Xamarin.iOS.FSharp.SingleViewApp"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin iOS FSharp ClassLibrary``()= test "Xamarin.iOS.FSharp.ClassLibrary"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin iOS FSharp UnitTestsApp``()= test "Xamarin.iOS.FSharp.UnitTestsApp"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Android FSharp AndroidApp``()= test "Xamarin.Android.FSharp.AndroidApp"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Android FSharp OpenGLGame``()= test "Xamarin.Android.FSharp.OpenGLGame"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Android FSharp ClassLibrary``()= test "Xamarin.Android.FSharp.ClassLibrary"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Android FSharp UnitTestApp``()= test "Xamarin.Android.FSharp.UnitTestApp"
    [<Ignore;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Mac FSharp CocoaApp-XIB``()= test "Xamarin.Mac.FSharp.CocoaApp-XIB"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin Mac FSharp ClassLibrary``()= test "Xamarin.Mac.FSharp.ClassLibrary"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``Xamarin tvOS FSharp SingleViewApp``()= test "Xamarin.tvOS.FSharp.SingleViewApp"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``MonoDevelop FSharp ConsoleProject``()= test "MonoDevelop.FSharp.ConsoleProject"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``FSharpGtkProject``()= test "FSharpGtkProject"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``MonoDevelop FSharp LibraryProject``()= test "MonoDevelop.FSharp.LibraryProject"
    [<Test;AsyncStateMachine(typeof<Task>)>]member x.``FSharpNUnitLibraryProject``()= test "FSharpNUnitLibraryProject"
    [<Ignore("Currently not testable");AsyncStateMachine(typeof<Task>)>]
    member x.``Xamarin Forms FSharp FormsApp Shared``() =
        testWithParameters "Xamarin.Forms.FSharp.FormsApp" "Xamarin.Forms.FSharp.FormsApp.Shared" "SafeUserDefinedProjectName=Xamarin_Forms_FSharp_FormsApp_Shared;CreateSharedAssetsProject=True;CreatePortableDotNetProject=False"

    [<Ignore("Currently not testable");AsyncStateMachine(typeof<Task>)>]
    member x.``Xamarin Forms FSharp FormsApp Shared with XAML``() =
        testWithParameters "Xamarin.Forms.FSharp.FormsApp" "Xamarin.Forms.FSharp.FormsApp.Shared.XAML" "CreateXamlProject=True;SafeUserDefinedProjectName=Xamarin_Forms_FSharp_FormsApp_Shared_XAML;CreateSharedAssetsProject=True;CreatePortableDotNetProject=False"