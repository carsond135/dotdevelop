namespace MonoDevelopTests
open System
open NUnit.Framework
open MonoDevelop.FSharp
open FsUnit
open MonoDevelop.Debugger
open System.Threading.Tasks
open System.Runtime.CompilerServices

[<TestFixture>]
type DebuggerExpressionResolver() =

    [<SetUp;AsyncStateMachine(typeof<Task>)>]
    let ``run before test``() =
        FixtureSetup.initialiseMonoDevelopAsync()

    let content =
      """
type TestOne() =
  member val PropertyOne = "42" with get, set
  member x.PropertyTwo = "42"
  member x.FunctionOne(parameter) = ()

let local|One = TestOne()
let local|Two = localOne.Prope|rtyOne
let localThree = local|One.PropertyOne
let localFour = localOne.Property|One
let localFive = System.Convert.ToInt32(localOne.Property|Two)

type Test = { recordfield: string }
let a = { recordfield = "A" }
let x = a.record|field"""

    let getOffset expr =
        let startOffset = content.IndexOf (expr, StringComparison.Ordinal)
        let previousMarkers =
            content
            |> String.toArray
            |> Array.findIndices((=) '|')
            |> Array.filter(fun i -> i < startOffset-1)
            |> Array.length
        let offset = content.IndexOf('|',startOffset) - previousMarkers
        offset

    let resolveExpression (doc:TestDocument, content:string, offset:int) =
        let resolver = new FSharpDebuggerExpressionResolver() :> IDebuggerExpressionResolver
        Async.AwaitTask (resolver.ResolveExpressionAsync(doc.Editor, doc, offset, Async.DefaultCancellationToken))
        |> Async.RunSynchronously

    [<TestCase("local|One","localOne")>]
    [<TestCase("local|Two", "localTwo")>]
    [<TestCase("localOne.Prope|rtyOne", "localOne.PropertyOne")>]
    [<TestCase("local|One.PropertyOne", "localOne")>]
    [<TestCase("localOne.Property|One", "localOne.PropertyOne")>]
    [<TestCase("localOne.Property|Two", "localOne.PropertyTwo")>]
    [<TestCase("a.record|field", "a.recordfield")>]
    member x.TestBasicLocalVariable(localVariable, expected) =
        let basicOffset = getOffset (localVariable)
        let doc = TestHelpers.createDoc (content.Replace("|" ,"")) ""

        let loc = doc.Editor.OffsetToLocation basicOffset
        let lineTxt = doc.Editor.GetLineText(loc.Line, false)
        let markedLine = (String.replicate (loc.Column-1) " " + "^" )
        printfn "%s\n%s" lineTxt markedLine

        let debugDataTipInfo = resolveExpression (doc, content, basicOffset)
        debugDataTipInfo.Text |> should equal expected
