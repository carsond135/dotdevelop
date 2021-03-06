// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace ICSharpCode.NRefactory6.CSharp
{
	static class ArgumentSyntaxExtensions
	{
		public static SyntaxTokenList GenerateParameterModifiers(this ArgumentSyntax argument)
		{
			// If the argument was marked with ref or out, then do the same for the parameter.
			if (argument.RefOrOutKeyword.Kind() == SyntaxKind.RefKeyword ||
				argument.RefOrOutKeyword.Kind() == SyntaxKind.OutKeyword)
			{
				return SyntaxFactory.TokenList(SyntaxFactory.Token(argument.RefOrOutKeyword.Kind()));
			}

			return default(SyntaxTokenList);
		}

		public static RefKind GetRefKind(this ArgumentSyntax argument)
		{
			var refSyntaxKind = argument.RefOrOutKeyword.Kind();
			return
				refSyntaxKind == SyntaxKind.RefKeyword ? RefKind.Ref :
				refSyntaxKind == SyntaxKind.OutKeyword ? RefKind.Out : RefKind.None;
		}

		/// <summary>
		/// Returns the parameter to which this argument is passed. If <paramref name="allowParams"/>
		/// is true, the last parameter will be returned if it is params parameter and the index of
		/// the specified argument is greater than the number of parameters.
		/// </summary>
		public static IParameterSymbol DetermineParameter(
			this ArgumentSyntax argument,
			SemanticModel semanticModel,
			bool allowParams = false,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var argumentList = argument.Parent as BaseArgumentListSyntax;
			if (argumentList == null)
			{
				return null;
			}

			var invocableExpression = argumentList.Parent as ExpressionSyntax;
			if (invocableExpression == null)
			{
				return null;
			}

			var symbol = semanticModel.GetSymbolInfo(invocableExpression, cancellationToken).Symbol;
			if (symbol == null)
			{
				return null;
			}

			var parameters = symbol.GetParameters();

			// Handle named argument
			if (argument.NameColon != null && !argument.NameColon.IsMissing)
			{
				var name = argument.NameColon.Name.Identifier.ValueText;
				return parameters.FirstOrDefault(p => p.Name == name);
			}

			// Handle positional argument
			var index = argumentList.Arguments.IndexOf(argument);
			if (index < 0)
			{
				return null;
			}

			if (index < parameters.Length)
			{
				return parameters[index];
			}

			if (allowParams)
			{
				var lastParameter = parameters.LastOrDefault();
				if (lastParameter == null)
				{
					return null;
				}

				if (lastParameter.IsParams)
				{
					return lastParameter;
				}
			}

			return null;
		}

		public static ITypeSymbol DetermineParameterType(
			this ArgumentSyntax argument,
			SemanticModel semanticModel,
			CancellationToken cancellationToken)
		{
			// If a parameter appears to have a void return type, then just use 'object'
			// instead.
			var typeInfo = semanticModel.GetTypeInfo(argument.Expression, cancellationToken);
			if (typeInfo.Type != null && typeInfo.Type.SpecialType == SpecialType.System_Void)
			{
				return semanticModel.Compilation.ObjectType;
			}

			return semanticModel.GetType(argument.Expression, cancellationToken);
		}
	}
}
