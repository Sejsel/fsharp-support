module JetBrains.ReSharper.Plugins.FSharp.Psi.Features.UnitTesting.Expecto

open System
open JetBrains.Application.UI.BindableLinq.Collections
open JetBrains.ProjectModel
open JetBrains.ProjectModel.Assemblies.AssemblyToAssemblyResolvers
open JetBrains.ProjectModel.Assemblies.Impl
open JetBrains.ReSharper.Feature.Services.ClrLanguages
open JetBrains.ReSharper.Plugins.FSharp.Psi.Util
open JetBrains.ReSharper.Plugins.FSharp.Util
open JetBrains.ReSharper.Psi
open JetBrains.ReSharper.Psi.Tree
open JetBrains.ReSharper.Resources.Shell
open JetBrains.ReSharper.UnitTestFramework
open JetBrains.ReSharper.UnitTestFramework.AttributeChecker
open JetBrains.ReSharper.UnitTestFramework.Elements
open JetBrains.ReSharper.UnitTestFramework.Exploration
open JetBrains.ReSharper.UnitTestFramework.Strategy
open JetBrains.Util.Reflection

let expectoAssemblyName = AssemblyNameInfoFactory.Create("Expecto")

let testsAttribute = clrTypeName "Expecto.TestsAttribute"
let pTestsAttribute = clrTypeName "Expecto.PTestsAttribute"
let fTestsAttribute = clrTypeName "Expecto.FTestsAttribute"

let attributes =
    [| testsAttribute
       pTestsAttribute
       fTestsAttribute |]


[<UnitTestProvider>]
type ExpectoProvider() =
    let isSupported project targetFrameworkId =
        use cookie = ReadLockCookie.Create()

        let mutable info = Unchecked.defaultof<_>
        ReferencedAssembliesService.IsProjectReferencingAssemblyByName(project, targetFrameworkId, expectoAssemblyName, &info) ||
        ReferencedProjectsService.IsProjectReferencingAssemblyByName(project, targetFrameworkId, expectoAssemblyName, &info)
    
    interface IUnitTestProvider with
        member x.ID = "Expecto"
        member x.Name = "Expecto"

        member x.IsElementOfKind(declaredElement: IDeclaredElement, elementKind: UnitTestElementKind) = false // todo
        member x.IsElementOfKind(element: IUnitTestElement, elementKind: UnitTestElementKind) = false // todo

        member x.IsSupported(project, targetFrameworkId) = isSupported project targetFrameworkId
        member x.IsSupported(_, project, targetFrameworkId) = isSupported project targetFrameworkId

        member x.CompareUnitTestElements(a, b) = 0 // todo
        member x.SupportsResultEventsForParentOf _ = false


[<SolutionComponent>]
type ExpectoService =
    { Provider: ExpectoProvider
      ElementManager: IUnitTestElementManager
      IdFactory: IUnitTestElementIdFactory }


type Processor(interrupted: Func<bool>, attributeChecker: IUnitTestAttributeChecker) =
    let checkForInterrupt () =
        if interrupted.Invoke() then
            raise (OperationCanceledException())

    let canBeTestElement (declaredElement: IDeclaredElement) =
        declaredElement :? IMethod || declaredElement :? IField || declaredElement :? IProperty

    interface IRecursiveElementProcessor with
        member x.ProcessingIsFinished = false
        member x.ProcessAfterInterior _ = ()

        member x.InteriorShouldBeProcessed(element) =
            not (element :? ITypeDeclaration || element :? ITypeMemberDeclaration)

        member x.ProcessBeforeInterior(element) =
            checkForInterrupt ()

            let declaration = element.As<IDeclaration>()
            if isNull declaration then () else

            let attributesOwner = declaration.DeclaredElement.As<IAttributesOwner>()
            if isNull attributesOwner || not (canBeTestElement attributesOwner) then () else

            if not (attributeChecker.HasDerivedAttribute(attributesOwner, attributes)) then () else

            
            
            ()


[<SolutionComponent>]
type ExpectoTestFileExplorer
        (service: ExpectoService, clrLanguages: ClrLanguagesKnown, attributeChecker: IUnitTestAttributeChecker) =
    interface IUnitTestExplorerFromFile with
        member x.Provider = service.Provider :> _

        member x.ProcessFile(file, observer, interrupted) =
            if not (Seq.contains file.Language clrLanguages.AllLanguages) then () else

            let projectFile = file.GetSourceFile().ToProjectFile()
            if isNull projectFile then () else

            let processor = Processor(interrupted, attributeChecker)
            file.ProcessDescendants(processor)


[<AllowNullLiteral>]
type ExpectoTestElement(id, name, declaredElement, service: ExpectoService) =
    let mutable parent: IUnitTestElement = Unchecked.defaultof<_>
    let children = new BindableSetCollectionWithoutIndexTracking<_>(UT.Locks.ReadLock, UnitTestElement.EqualityComparer)

    member x.RemoveChild(child) = children.Remove(child) |> ignore
    member x.AppendChild(child) = children.Add(child)

    interface IUnitTestElement with
        member x.Id = id
        member x.Kind = "Expecto tests"

        member x.ShortName = name
        member x.GetPresentation(_, _) = name

        member x.GetDeclaredElement() = declaredElement

        member x.GetProjectFiles() =
            declaredElement.GetSourceFiles()
            |> Seq.map (fun sourceFile -> sourceFile.ToProjectFile())

        member x.GetNamespace() =
            let cl = declaredElement.As<IClrDeclaredElement>()
            UnitTestElementNamespaceFactory.Create(cl.GetContainingType().GetClrName().NamespaceNames)

        member x.GetDisposition() =
            if not (isValid declaredElement) then UnitTestElementDisposition.InvalidDisposition else

            let locations =
                declaredElement.GetDeclarations()
                |> Seq.map (fun decl ->
                    let file = decl.GetContainingFile()
                    if isNull file then null else

                    let projectFile = file.GetSourceFile().ToProjectFile()
                    let nameRange = decl.GetNameDocumentRange().TextRange
                    UnitTestElementLocation(projectFile, nameRange, decl.GetDocumentRange().TextRange))

            UnitTestElementDisposition(locations, x)

        member x.Parent
            with get () = parent
            and set (value) =
                if value == parent then () else
                begin
                    use lock = UT.WriteLock()
                    if isNotNull parent then
                        (parent :?> ExpectoTestElement).RemoveChild(x)

                    if isNotNull value then
                        parent <- value
                        (parent :?> ExpectoTestElement).AppendChild(x)
                end
                service.ElementManager.FireElementChanged(x)

        member x.Children = children :> _

        member x.GetRunStrategy _ = DoNothingRunStrategy() :> _
        member x.GetTaskSequence(explicitElements, run) = null // todo

        member val OwnCategories = null with get, set
        member val Origin = Unchecked.defaultof<_> with get, set

        member x.Explicit = false
        member x.ExplicitReason = ""