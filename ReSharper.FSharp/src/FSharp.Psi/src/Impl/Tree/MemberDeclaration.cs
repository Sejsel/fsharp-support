using System;
using FSharp.Compiler.SourceCodeServices;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.DeclaredElement;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Tree;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
{
  internal partial class MemberDeclaration : IFunctionDeclaration
  {
    IFunction IFunctionDeclaration.DeclaredElement => base.DeclaredElement as IFunction;
    protected override string DeclaredElementName => NameIdentifier.GetCompiledName(Attributes);

    public override IFSharpIdentifierLikeNode NameIdentifier => (IFSharpIdentifierLikeNode) Identifier;

    protected override FSharpSymbolUse GetSymbolDeclaration(TreeTextRange identifierRange) =>
      ObjExprNavigator.GetByMember(this) != null
        ? FSharpFile.GetSymbolUse(identifierRange.StartOffset.Offset)
        : base.GetSymbolDeclaration(identifierRange);

    protected override IDeclaredElement CreateDeclaredElement() =>
      GetFSharpSymbol() is { } fcsSymbol
        ? CreateDeclaredElement(fcsSymbol)
        : null;

    protected override IDeclaredElement CreateDeclaredElement(FSharpSymbol fcsSymbol)
    {
      if (!(fcsSymbol is FSharpMemberOrFunctionOrValue mfv)) return null;

      if (mfv.IsProperty)
        return new FSharpProperty<MemberDeclaration>(this, mfv);

      var property = mfv.AccessorProperty?.Value;
      if (property != null)
      {
        var cliEvent = property.EventForFSharpProperty?.Value;
        return cliEvent != null
          ? (ITypeMember) new FSharpCliEvent<MemberDeclaration>(this)
          : new FSharpProperty<MemberDeclaration>(this, property);
      }

      var compiledName = mfv.CompiledName;
      if (!mfv.IsInstanceMember && compiledName.StartsWith("op_", StringComparison.Ordinal))
      {
        switch (compiledName)
        {
          case StandardOperatorNames.Explicit:
            return new FSharpConversionOperator<MemberDeclaration>(this, true);
          case StandardOperatorNames.Implicit:
            return new FSharpConversionOperator<MemberDeclaration>(this, false);
        }

        return new FSharpSignOperator<MemberDeclaration>(this);
      }

      return new FSharpMethod<MemberDeclaration>(this);
    }

    public bool IsExplicitImplementation =>
      InterfaceImplementationNavigator.GetByTypeMember(this) != null ||
      ObjExprNavigator.GetByMemberDeclaration(this) is { } objExpr && objExpr.ArgExpression == null ||
      ObjExprNavigator.GetByInterfaceMember(this) != null;

    public override bool IsStatic => StaticKeyword != null;

    public override bool IsOverride =>
      MemberKeyword?.GetTokenType() is var tokenType &&
      tokenType == FSharpTokenType.OVERRIDE || tokenType == FSharpTokenType.DEFAULT;

    public override TreeTextRange GetNameIdentifierRange() =>
      NameIdentifier.GetMemberNameIdentifierRange();
  }
}
