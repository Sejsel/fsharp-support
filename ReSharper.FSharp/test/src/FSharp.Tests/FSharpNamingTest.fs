namespace JetBrains.ReSharper.Plugins.FSharp.Tests.Features

open JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename
open JetBrains.ReSharper.Feature.Services.Util
open JetBrains.ReSharper.Psi.Tree
open JetBrains.ReSharper.Plugins.FSharp.Tests.Common
open JetBrains.ReSharper.Psi
open JetBrains.ReSharper.Psi.Naming.Extentions
open JetBrains.ReSharper.Psi.Naming.Impl
open JetBrains.ReSharper.TestFramework
open NUnit.Framework

[<FSharpTest; TestPackages("FSharp.Core")>]
type FSharpNamingTest() =
    inherit BaseTestWithTextControl()

    override x.RelativeTestDataPath = "features/naming"
    
    [<Test>] member x.``Let 01 - Simple``() = x.DoNamedTest()
    [<Test>] member x.``Let 02 - Tuple``() = x.DoNamedTest()

    [<Test>] member x.``Match 01``() = x.DoNamedTest()
    [<Test>] member x.``Match 02 - Some``() = x.DoNamedTest()
    [<Test>] member x.``Match 03 - ValueSome``() = x.DoNamedTest()
    [<Test>] member x.``Match 04 - Ok``() = x.DoNamedTest()
    [<Test>] member x.``Match 05 - Some - Parens``() = x.DoNamedTest()
    [<Test>] member x.``Match 06 - No suggestion``() = x.DoNamedTest()

    [<Test>] member x.``Foreach 01``() = x.DoNamedTest()
    [<Test>] member x.``Indexer 01``() = x.DoNamedTest()

    [<Test>] member x.``Record field 01``() = x.DoNamedTest()
    [<Test>] member x.``Record field 02 - Parens``() = x.DoNamedTest() // todo: find reference to foo

    [<Test>] member x.``Type 01``() = x.DoNamedTest()

    override x.DoTest(lifetime, _) =
        let textControl = x.OpenTextControl(lifetime)
        let sourceFile = textControl.Document.GetPsiSourceFile(x.Solution)

        let declaration = TextControlToPsi.GetElementFromCaretPosition<IDeclaration>(x.Solution, textControl)
        let declaredElement = declaration.DeclaredElement
        let language = declaration.Language

        let suggestionManager = declaration.GetPsiServices().Naming.Suggestion
        let namesCollection =
            suggestionManager.CreateEmptyCollection(PluralityKinds.Unknown, language, true, sourceFile)

        let entryOptions = EntryOptions(subrootPolicy = SubrootPolicy.Decompose)
        namesCollection.Add(declaredElement, entryOptions)

        match declaredElement.As<ITypeOwner>() with
        | null -> ()
        | typeOwner -> namesCollection.Add(typeOwner.Type, entryOptions)

        let renameHelper = LanguageManager.Instance.TryGetService<RenameHelperBase>(language)
        renameHelper.AddExtraNames(namesCollection, declaredElement)

        let defaultName = declaredElement.ShortName
        let suggestionOptions = SuggestionOptions(UniqueNameContext = declaration, DefaultName = defaultName)
        let names = namesCollection.Prepare(declaredElement, suggestionOptions).AllNames()

        x.ExecuteWithGold(sourceFile.GetLocation(), fun writer ->
            for name in names do
                writer.WriteLine(name))