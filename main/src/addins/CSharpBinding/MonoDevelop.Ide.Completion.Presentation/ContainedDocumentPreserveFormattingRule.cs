using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Options;

namespace MonoDevelop.Ide.Completion.Presentation
{
	internal class ContainedDocumentPreserveFormattingRule : AbstractFormattingRule
	{
		public static readonly AbstractFormattingRule Instance = new ContainedDocumentPreserveFormattingRule ();

		private static readonly AdjustSpacesOperation s_preserveSpace = FormattingOperations.CreateAdjustSpacesOperation (0, AdjustSpacesOption.PreserveSpaces);
		private static readonly AdjustNewLinesOperation s_preserveLine = FormattingOperations.CreateAdjustNewLinesOperation (0, AdjustNewLinesOption.PreserveLines);

		public override AdjustSpacesOperation GetAdjustSpacesOperation (SyntaxToken previousToken, SyntaxToken currentToken, OptionSet optionSet, in NextGetAdjustSpacesOperation nextOperation)
		{
			var operation = base.GetAdjustSpacesOperation (previousToken, currentToken, optionSet, nextOperation);
			if (operation != null) {
				return s_preserveSpace;
			}

			return operation;
		}

		public override AdjustNewLinesOperation GetAdjustNewLinesOperation (SyntaxToken previousToken, SyntaxToken currentToken, OptionSet optionSet, in NextGetAdjustNewLinesOperation nextOperation)
		{
			var operation = base.GetAdjustNewLinesOperation (previousToken, currentToken, optionSet, nextOperation);
			if (operation != null) {
				return s_preserveLine;
			}

			return operation;
		}
	}
}
