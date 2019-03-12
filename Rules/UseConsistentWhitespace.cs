// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
#if !CORECLR
using System.ComponentModel.Composition;
#endif
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using Microsoft.PowerShell.ScriptAnalyzer.Generic;

namespace Microsoft.PowerShell.ScriptAnalyzer.BuiltinRules
{
    /// <summary>
    /// UseConsistentWhitespace: Checks if whitespace usage is consistent throughout the source file.
    /// </summary>
#if !CORECLR
    [Export(typeof(IScriptRule))]
#endif
    public class UseConsistentWhitespace : ConfigurableRule
    {
        private enum ErrorKind { BeforeOpeningBrace, Paren, Operator, SeparatorComma, SeparatorSemi, AfterOpeningBrace, BeforeClosingBrace, BeforePipe, AfterPipe };
        private const int whiteSpaceSize = 1;
        private const string whiteSpace = " ";
        private readonly SortedSet<TokenKind> openParenKeywordWhitelist = new SortedSet<TokenKind>()
        {
            TokenKind.If,
            TokenKind.ElseIf,
            TokenKind.Switch,
            TokenKind.For,
            TokenKind.Foreach,
            TokenKind.While
        };

        private List<Func<TokenOperations, IEnumerable<DiagnosticRecord>>> violationFinders
                = new List<Func<TokenOperations, IEnumerable<DiagnosticRecord>>>();

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckOpenBrace { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckInnerBrace { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckPipe { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckOpenParen { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckOperator { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckSeparator { get; protected set; }

        public override void ConfigureRule(IDictionary<string, object> paramValueMap)
        {
            base.ConfigureRule(paramValueMap);
            if (CheckOpenBrace)
            {
                violationFinders.Add(FindOpenBraceViolations);
            }

            if (CheckInnerBrace)
            {
                violationFinders.Add(FindInnerBraceViolations);
            }

            if (CheckPipe)
            {
                violationFinders.Add(FindPipeViolations);
            }

            if (CheckOpenParen)
            {
                violationFinders.Add(FindOpenParenViolations);
            }

            if (CheckOperator)
            {
                violationFinders.Add(FindOperatorViolations);
            }

            if (CheckSeparator)
            {
                violationFinders.Add(FindSeparatorViolations);
            }
        }

        /// <summary>
        /// Analyzes the given ast to find the [violation]
        /// </summary>
        /// <param name="ast">AST to be analyzed. This should be non-null</param>
        /// <param name="fileName">Name of file that corresponds to the input AST.</param>
        /// <returns>A an enumerable type containing the violations</returns>
        public override IEnumerable<DiagnosticRecord> AnalyzeScript(Ast ast, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException("ast");
            }

            var tokenOperations = new TokenOperations(Helper.Instance.Tokens, ast);
            var diagnosticRecords = Enumerable.Empty<DiagnosticRecord>();
            foreach (var violationFinder in violationFinders)
            {
                diagnosticRecords = diagnosticRecords.Concat(violationFinder(tokenOperations));
            }

            return diagnosticRecords.ToArray(); // force evaluation here
        }

        /// <summary>
        /// Retrieves the common name of this rule.
        /// </summary>
        public override string GetCommonName()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceCommonName);
        }

        /// <summary>
        /// Retrieves the description of this rule.
        /// </summary>
        public override string GetDescription()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceDescription);
        }

        /// <summary>
        /// Retrieves the name of this rule.
        /// </summary>
        public override string GetName()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.NameSpaceFormat,
                GetSourceName(),
                Strings.UseConsistentWhitespaceName);
        }

        /// <summary>
        /// Retrieves the severity of the rule: error, warning or information.
        /// </summary>
        public override RuleSeverity GetSeverity()
        {
            return RuleSeverity.Warning;
        }

        /// <summary>
        /// Gets the severity of the returned diagnostic record: error, warning, or information.
        /// </summary>
        /// <returns></returns>
        public DiagnosticSeverity GetDiagnosticSeverity()
        {
            return DiagnosticSeverity.Warning;
        }

        /// <summary>
        /// Retrieves the name of the module/assembly the rule is from.
        /// </summary>
        public override string GetSourceName()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.SourceName);
        }

        /// <summary>
        /// Retrieves the type of the rule, Builtin, Managed or Module.
        /// </summary>
        public override SourceType GetSourceType()
        {
            return SourceType.Builtin;
        }

        private bool IsOperator(Token token)
        {
            return TokenTraits.HasTrait(token.Kind, TokenFlags.AssignmentOperator)
                    || TokenTraits.HasTrait(token.Kind, TokenFlags.BinaryPrecedenceAdd)
                    || TokenTraits.HasTrait(token.Kind, TokenFlags.BinaryPrecedenceMultiply)
                    || token.Kind == TokenKind.AndAnd
                    || token.Kind == TokenKind.OrOr;
        }

        private string GetError(ErrorKind kind)
        {
            switch (kind)
            {
                case ErrorKind.BeforeOpeningBrace:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorBeforeOpeningBrace);
                case ErrorKind.AfterOpeningBrace:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorAfterOpeningBrace);
                case ErrorKind.BeforeClosingBrace:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorBeforeClosingInnerBrace);
                case ErrorKind.Operator:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorOperator);
                case ErrorKind.BeforePipe:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSpaceBeforePipe);
                case ErrorKind.AfterPipe:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSpaceAfterPipe);
                case ErrorKind.SeparatorComma:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSeparatorComma);
                case ErrorKind.SeparatorSemi:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSeparatorSemi);
                default:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorBeforeParen);
            }
        }

        private IEnumerable<DiagnosticRecord> FindOpenBraceViolations(TokenOperations tokenOperations)
        {
            foreach (var lcurly in tokenOperations.GetTokenNodes(TokenKind.LCurly))
            {

                if (lcurly.Previous == null
                    || !IsPreviousTokenOnSameLine(lcurly)
                    || lcurly.Previous.Value.Kind == TokenKind.LCurly
                    || ((lcurly.Previous.Value.TokenFlags & TokenFlags.MemberName) == TokenFlags.MemberName))
                {
                    continue;
                }

                if (!IsPreviousTokenApartByWhitespace(lcurly))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.BeforeOpeningBrace),
                        lcurly.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(lcurly.Previous.Value, lcurly.Value, lcurly.Next.Value, false, true).ToList());
                }
            }
        }

        private IEnumerable<DiagnosticRecord> FindInnerBraceViolations(TokenOperations tokenOperations)
        {
            foreach (var lCurly in tokenOperations.GetTokenNodes(TokenKind.LCurly))
            {
                if (lCurly.Next == null
                    || !IsPreviousTokenOnSameLine(lCurly)
                    || lCurly.Next.Value.Kind == TokenKind.NewLine
                    || lCurly.Next.Value.Kind == TokenKind.LineContinuation
                    )
                {
                    continue;
                }

                if (!IsNextTokenApartByWhitespace(lCurly))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.AfterOpeningBrace),
                        lCurly.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(lCurly.Previous.Value, lCurly.Value, lCurly.Next.Value, true, false).ToList());
                }
            }

            foreach (var rCurly in tokenOperations.GetTokenNodes(TokenKind.RCurly))
            {
                if (rCurly.Previous == null
                    || !IsPreviousTokenOnSameLine(rCurly)
                    || rCurly.Previous.Value.Kind == TokenKind.LCurly
                    || rCurly.Previous.Value.Kind == TokenKind.NewLine
                    || rCurly.Previous.Value.Kind == TokenKind.LineContinuation
                    )
                {
                    continue;
                }

                if (!IsPreviousTokenApartByWhitespace(rCurly))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.BeforeClosingBrace),
                        rCurly.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(rCurly.Previous.Value, rCurly.Value, rCurly.Next.Value, false, true).ToList());
                }
            }
        }

        private IEnumerable<DiagnosticRecord> FindPipeViolations(TokenOperations tokenOperations)
        {
            foreach (var pipe in tokenOperations.GetTokenNodes(TokenKind.Pipe))
            {
                if (pipe.Next == null
                    || !IsPreviousTokenOnSameLine(pipe)
                    || pipe.Next.Value.Kind == TokenKind.Pipe
                    || pipe.Next.Value.Kind == TokenKind.NewLine
                    || pipe.Next.Value.Kind == TokenKind.LineContinuation
                    )
                {
                    continue;
                }

                if (!IsNextTokenApartByWhitespace(pipe))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.AfterPipe),
                        pipe.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(pipe.Previous.Value, pipe.Value, pipe.Next.Value, true, false).ToList());
                }
            }

            foreach (var pipe in tokenOperations.GetTokenNodes(TokenKind.Pipe))
            {
                if (pipe.Previous == null
                    || !IsPreviousTokenOnSameLine(pipe)
                    || pipe.Previous.Value.Kind == TokenKind.Pipe
                    || pipe.Previous.Value.Kind == TokenKind.NewLine
                    || pipe.Previous.Value.Kind == TokenKind.LineContinuation
                    )
                {
                    continue;
                }

                if (!IsPreviousTokenApartByWhitespace(pipe))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.BeforePipe),
                        pipe.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(pipe.Previous.Value, pipe.Value, pipe.Next.Value, false, true).ToList());
                }
            }
        }

        private IEnumerable<DiagnosticRecord> FindOpenParenViolations(TokenOperations tokenOperations)
        {
            foreach (var lparen in tokenOperations.GetTokenNodes(TokenKind.LParen))
            {
                if (lparen.Previous != null
                    && IsPreviousTokenOnSameLine(lparen)
                    && TokenTraits.HasTrait(lparen.Previous.Value.Kind, TokenFlags.Keyword)
                    && IsKeyword(lparen.Previous.Value)
                    && !IsPreviousTokenApartByWhitespace(lparen))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.Paren),
                        lparen.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(lparen.Previous.Value, lparen.Value, lparen.Next.Value, false, true).ToList());
                }
            }
        }

        private bool IsSeparator(Token token)
        {
            return token.Kind == TokenKind.Comma || token.Kind == TokenKind.Semi;
        }

        private IEnumerable<DiagnosticRecord> FindSeparatorViolations(TokenOperations tokenOperations)
        {
            Func<LinkedListNode<Token>, bool> predicate = node =>
            {
                return node.Next != null
                    && node.Next.Value.Kind != TokenKind.NewLine
                    && node.Next.Value.Kind != TokenKind.EndOfInput // semicolon can be followed by end of input
                    && !IsPreviousTokenApartByWhitespace(node.Next);
            };

            foreach (var tokenNode in tokenOperations.GetTokenNodes(IsSeparator).Where(predicate))
            {
                var errorKind = tokenNode.Value.Kind == TokenKind.Comma
                    ? ErrorKind.SeparatorComma
                    : ErrorKind.SeparatorSemi;
                yield return getDiagnosticRecord(
                    tokenNode.Value,
                    errorKind,
                    GetCorrections(
                        tokenNode.Previous.Value,
                        tokenNode.Value,
                        tokenNode.Next.Value,
                        true,
                        false));
            }
        }

        private DiagnosticRecord getDiagnosticRecord(
            Token token,
            ErrorKind errKind,
            List<CorrectionExtent> corrections)
        {
            return new DiagnosticRecord(
                GetError(errKind),
                token.Extent,
                GetName(),
                GetDiagnosticSeverity(),
                token.Extent.File,
                null,
                corrections);
        }

        private bool IsKeyword(Token token)
        {
            return openParenKeywordWhitelist.Contains(token.Kind);
        }

        private bool IsPreviousTokenApartByWhitespace(LinkedListNode<Token> tokenNode)
        {
            return whiteSpaceSize ==
                (tokenNode.Value.Extent.StartColumnNumber - tokenNode.Previous.Value.Extent.EndColumnNumber);
        }

        private bool IsNextTokenApartByWhitespace(LinkedListNode<Token> tokenNode)
        {
            return whiteSpaceSize ==
                (tokenNode.Next.Value.Extent.StartColumnNumber - tokenNode.Value.Extent.EndColumnNumber);
        }

        private bool IsPreviousTokenOnSameLineAndApartByWhitespace(LinkedListNode<Token> tokenNode)
        {
            return IsPreviousTokenOnSameLine(tokenNode) && IsPreviousTokenApartByWhitespace(tokenNode);
        }

        private IEnumerable<DiagnosticRecord> FindOperatorViolations(TokenOperations tokenOperations)
        {
            foreach (var tokenNode in tokenOperations.GetTokenNodes(IsOperator))
            {
                if (tokenNode.Previous == null
                    || tokenNode.Next == null
                    || tokenNode.Value.Kind == TokenKind.DotDot)
                {
                    continue;
                }

                // exclude unary operator for cases like $foo.bar(-$Var)
                if (TokenTraits.HasTrait(tokenNode.Value.Kind, TokenFlags.UnaryOperator) &&
                    tokenNode.Previous.Value.Kind == TokenKind.LParen &&
                    tokenNode.Next.Value.Kind == TokenKind.Variable)
                {
                    continue;
                }

                var hasWhitespaceBefore = IsPreviousTokenOnSameLineAndApartByWhitespace(tokenNode);
                var hasWhitespaceAfter = tokenNode.Next.Value.Kind == TokenKind.NewLine
                            || IsPreviousTokenOnSameLineAndApartByWhitespace(tokenNode.Next);

                if (!hasWhitespaceAfter || !hasWhitespaceBefore)
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.Operator),
                        tokenNode.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(
                            tokenNode.Previous.Value,
                            tokenNode.Value,
                            tokenNode.Next.Value,
                            hasWhitespaceBefore,
                            hasWhitespaceAfter));
                }
            }
        }

        private List<CorrectionExtent> GetCorrections(
            Token prevToken,
            Token token,
            Token nextToken,
            bool hasWhitespaceBefore, // if this is false, then the returned correction extent will add a whitespace before the token
            bool hasWhitespaceAfter   // if this is false, then the returned correction extent will add a whitespace after the token
            )
        {
            var sb = new StringBuilder();
            IScriptExtent e1 = token.Extent;
            if (!hasWhitespaceBefore)
            {
                sb.Append(whiteSpace);
                e1 = prevToken.Extent;
            }

            var e2 = token.Extent;
            if (!hasWhitespaceAfter)
            {
                if (!hasWhitespaceBefore)
                {
                    sb.Append(token.Text);
                }

                e2 = nextToken.Extent;
                sb.Append(whiteSpace);
            }

            var extent = new ScriptExtent(
                new ScriptPosition(e1.File, e1.EndLineNumber, e1.EndColumnNumber, null),
                new ScriptPosition(e2.File, e2.StartLineNumber, e2.StartColumnNumber, null));
            return new List<CorrectionExtent>()
            {
                new CorrectionExtent(
                extent,
                sb.ToString(),
                token.Extent.File,
                GetError(ErrorKind.Operator))
            };
        }


        private bool IsPreviousTokenOnSameLine(LinkedListNode<Token> lparen)
        {
            return lparen.Previous.Value.Extent.EndLineNumber == lparen.Value.Extent.StartLineNumber;
        }

    }
}
