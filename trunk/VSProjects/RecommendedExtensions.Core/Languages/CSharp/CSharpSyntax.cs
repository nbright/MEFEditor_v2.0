﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;

using MEFEditor.TypeSystem;

using RecommendedExtensions.Core.Languages.CSharp.Primitives;
using RecommendedExtensions.Core.Languages.CSharp.Interfaces;

namespace RecommendedExtensions.Core.Languages.CSharp
{
    /// <summary>
    /// Provide services for parsing C# syntax.
    /// </summary>
    public class CSharpSyntax
    {
        /// <summary>
        /// ID of CSharp language.
        /// </summary>
        public const string LanguageID = "{B5E9BD34-6D3E-4B5D-925E-8A43B79820B4}";

        #region Syntax element constants of C#

        /// <summary>
        /// Name of variable where this object is stored.
        /// </summary>
        internal const string ThisVariable = "this";

        /// <summary>
        /// Name of operator providing access to base members.
        /// </summary>
        internal const string BaseVariable = "base";

        /// <summary>
        /// Name of member initializer method (virtual method used for implementation purposes).
        /// </summary>
        internal const string MemberInitializer = "#initializer";

        /// <summary>
        /// Name of static member initializer method (virtual method used for implementation purposes).
        /// </summary>
        internal const string MemberStaticInitializer = "#static_initializer";

        /// <summary>
        /// Name of variable where this object is stored.
        /// </summary>
        internal const string ImplicitVariableType = "var";

        /// <summary>
        /// Operator used for return statement.
        /// </summary>
        internal const string ReturnOperator = "return";

        /// <summary>
        /// Operator used for new statement.
        /// </summary>
        internal const string NewOperator = "new";

        /// <summary>
        /// Operator for typeof expression.
        /// </summary>
        internal const string TypeOfOperator = "typeof";

        /// <summary>
        /// Operator used for if based statement.
        /// </summary>
        internal const string IfOperator = "if";

        /// <summary>
        /// Operator used for while based statement.
        /// </summary>
        internal const string WhileOperator = "while";

        /// <summary>
        /// Operator used for for based statement.
        /// </summary>
        internal const string ForOperator = "for";

        /// <summary>
        /// Operator used for for based statement.
        /// </summary>
        internal const string ForeachOperator = "foreach";

        /// <summary>
        /// Operator used for for based statement.
        /// </summary>
        internal const string SwitchOperator = "switch";

        /// <summary>
        /// Operator used for do based statement.
        /// </summary>
        internal const string DoOperator = "do";

        /// <summary>
        /// Operator used for incrementing variable value.
        /// </summary>
        internal const string IncrementOperator = "++";

        /// <summary>
        /// Operator used for decrementing variable value.
        /// </summary>
        internal const string DecrementOperator = "--";

        /// <summary>
        /// Operator used for negating value.
        /// </summary>
        internal const string NegateOperator = "!";

        /// <summary>
        /// Operator used for assigning variable value.
        /// </summary>
        internal const string AssignOperator = "=";

        /// <summary>
        /// Operator used for equality comparison.
        /// </summary>
        internal const string IsEqualOperator = "==";

        /// <summary>
        /// Keyword for break statement.
        /// </summary>
        internal const string BreakKeyword = "break";

        /// <summary>
        /// Keyword for continue statement.
        /// </summary>
        internal const string ContinueKeyword = "continue";

        #endregion

        /// <summary>
        /// The language layouts.
        /// </summary>
        readonly LanguageLayouts layouts;
        /// <summary>
        /// The _lexer.
        /// </summary>
        readonly ILexer _lexer;

        /// <summary>
        /// The known tokens.
        /// </summary>
        static readonly HashSet<string> KnownTokens = new HashSet<string>();

        /// <summary>
        /// The ending tokens.
        /// </summary>
        static readonly HashSet<string> EndingTokens = new HashSet<string>() { ";", ":", ",", ")", "}", "]", "in" };

        /// <summary>
        /// The preference operators.
        /// </summary>
        static readonly HashSet<string> PrefOperators = new HashSet<string>() { NegateOperator, "throw", "out", "ref", "const", ReturnOperator, NewOperator, "-", "+", IncrementOperator, DecrementOperator, "~" };

        /// <summary>
        /// The postfix operators.
        /// </summary>
        static readonly HashSet<string> PostOperators = new HashSet<string>() { IncrementOperator, DecrementOperator };

        /// <summary>
        /// The binary operators.
        /// </summary>
        static readonly Dictionary<string, int> BinOperators = new Dictionary<string, int>(){
            {":",50},
            {"=",100},        
            {"/=",100},      
            {"*=",100},      
            {"+=",100},      
            {"-=",100},      
            {"&=",100},      
            {"|=",100},      
            {"~=",100},      
            {"==",150},      
            {"!=",150},  
            {"&",200},      
            {"|",200},      
            {"&&",200},      
            {"||",200},      
            {"+",200},
            {"-",200},
            {"*",300},
            {"/",300},
            {"<",400},
            {">",400},
            {"<=",400},
            {">=",400},
        };

        /// <summary>
        /// Determine if there are not next tokens from lexer.
        /// </summary>
        /// <value><c>true</c> if there are no tokens; otherwise, <c>false</c>.</value>
        public bool End { get { return _lexer.End; } }

        /// <summary>
        /// Create CSharpSyntax object.
        /// </summary>
        /// <param name="lexer">Lexer which will be used for getting tokens.</param>
        /// <param name="nextTree">Encapsulate method which return next node tree from parser.</param>
        internal CSharpSyntax(ILexer lexer, GetNextTree nextTree)
        {
            _lexer = lexer;
            layouts = new LanguageLayouts(nextTree, _lexer);
            KnownTokens.UnionWith(BinOperators.Keys);
            KnownTokens.UnionWith(EndingTokens);
            KnownTokens.UnionWith(PrefOperators);
            KnownTokens.UnionWith(PostOperators);
        }


        /// <summary>
        /// Create exception for parsing error detected in context of given node.
        /// </summary>
        /// <param name="node">Node where error has been found.</param>
        /// <param name="descriptionFormat">Format of error description.</param>
        /// <param name="formatArguments">Arguments for format description.</param>
        /// <returns>Created exception.</returns>
        public static ParsingException ParsingException(INodeAST node, string descriptionFormat, params object[] formatArguments)
        {
            var description = string.Format(descriptionFormat, formatArguments);

            var errorFormat = "{0}";
            var position = 0;
            Action navigation = null;
            if (node != null)
            {
                errorFormat += " near {1}";
                if (node.StartingToken != null)
                {
                    errorFormat += " at offset {2}";
                }

                navigation = () => navigate(node);
            }

            var error = string.Format(errorFormat, description, node, position);
            throw new ParsingException(error, navigation);
        }

        /// <summary>
        /// Create exception for parsing error detected in context of given token.
        /// </summary>
        /// <param name="token">Token where error has been found.</param>
        /// <param name="descriptionFormat">Format of error description.</param>
        /// <param name="formatArguments">The format arguments.</param>
        /// <returns>Created exception.</returns>
        public static ParsingException ParsingException(IToken token, string descriptionFormat, params object[] formatArguments)
        {
            var description = string.Format(descriptionFormat, formatArguments);
            var error = string.Format("{0} near {1} at offset {2}", description, token, token.Position.Offset);
            throw new ParsingException(error, () => navigate(token));
        }

        /// <summary>
        /// Determines whether [has lesser priority] [the specified node].
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="node2">The node2.</param>
        /// <returns><c>true</c> if [has lesser priority] [the specified node]; otherwise, <c>false</c>.</returns>
        public bool HasLesserPriority(INodeAST node, INodeAST node2)
        {
            if (!BinOperators.ContainsKey(node.Value)) return false;
            if (!BinOperators.ContainsKey(node2.Value)) return true;
            return BinOperators[node.Value] < BinOperators[node2.Value];
        }
        /// <summary>
        /// Return number of expected operands for given node.
        /// </summary>
        /// <param name="node">Node which arity is returned.</param>
        /// <returns>Arity of node.</returns>
        public int Arity(INodeAST node)
        {
            //unary nodes are resolved in context.
            if (node.NodeType == NodeTypes.binaryOperator) return 2;
            return 0;
        }

        /// <summary>
        /// Apply layout according to current lexer token.
        /// </summary>
        /// <returns>CodeNode created from layout.</returns>
        private CodeNode applyLayout()
        {
            switch (_lexer.Current.Value)
            {
                case CSharpSyntax.DoOperator:
                    return layouts.DoLayout();
                case CSharpSyntax.ForOperator:
                    return layouts.ForLayout();
                case CSharpSyntax.ForeachOperator:
                    return layouts.ForeachLayout();
                case CSharpSyntax.SwitchOperator:
                    return layouts.SwitchLayout();
                case CSharpSyntax.IfOperator:
                case CSharpSyntax.WhileOperator:
                    return layouts.CondBlockLayout();
                case "[":
                    return layouts.ImplicitArray();
                case "{":
                    return layouts.SequenceLayout();
                case "(":
                    return layouts.BracketLayout();
                case CSharpSyntax.ContinueKeyword:
                case CSharpSyntax.BreakKeyword:
                    return layouts.KeywordLayout();
                default:
                    return layouts.HierarchyLayout();
            }
        }

        /// <summary>
        /// Helper for source navigation.
        /// </summary>
        /// <param name="node">Navigation target.</param>
        private static void navigate(INodeAST node)
        {
            navigate(node.StartingToken);
        }

        /// <summary>
        /// Helper for source navigation.
        /// </summary>
        /// <param name="token">Navigation target.</param>
        private static void navigate(IToken token)
        {
            token.Position.Navigate();
        }

        /// <summary>
        /// Return next node created from tokens in _lexer.
        /// </summary>
        /// <param name="withContext">Determine if context should be used for created node.</param>
        /// <returns>Next CodeNode.</returns>
        internal CodeNode Next(bool withContext)
        {
            var currentToken = _lexer.Current;
            var currentValue = currentToken.Value;
            CodeNode result = null;

            if (EndingTokens.Contains(currentValue)) result = new CodeNode(currentToken, NodeTypes.empty);
            else if (!KnownTokens.Contains(currentValue)) result = applyLayout();
            else if (BinOperators.ContainsKey(currentValue)) result = new CodeNode(_lexer.Move(), NodeTypes.binaryOperator);
            else if (PostOperators.Contains(currentValue)) result = new CodeNode(_lexer.Move(), NodeTypes.postOperator);
            else if (PrefOperators.Contains(currentValue)) result = new CodeNode(_lexer.Move(), NodeTypes.prefixOperator);

            if (result != null)
            {
                //refresh information
                currentToken = _lexer.Current;
                currentValue = currentToken.Value;

                result.IsTreeEnding = _lexer.End || EndingTokens.Contains(currentValue);
                if (withContext) result = checkContext(result);
                return result;
            }
            throw ParsingException(currentToken, "Unknown token : {0}", currentValue);
        }

        /// <summary>
        /// Check context for given node.
        /// </summary>
        /// <param name="node">Node which context is checked.</param>
        /// <returns>Node repaired according to context.</returns>
        private CodeNode checkContext(CodeNode node)
        {
            if (node == null || _lexer.End)
                //ending token
                return node;
            if (node.IsTreeEnding)
                //no context for tree ending node
                return node;

            if (node.NodeType == NodeTypes.hierarchy && isHierarchy())
                //variable declaration
                return new CodeNode(NodeTypes.declaration, node, Next(false));

            if (node.NodeType == NodeTypes.bracket && node.Child == null && isHierarchy())
            {
                //conversion expression - doesnt have child and hierarchy node is next
                return new CodeNode(NodeTypes.conversion, node.Arguments[0] as CodeNode, Next(true));
            }

            return node;
        }

        /// <summary>
        /// Determine if current token in lexer is hierarchy node.
        /// </summary>
        /// <returns>True if current lexer value is hierarchy token.</returns>
        private bool isHierarchy()
        {
            return !KnownTokens.Contains(_lexer.Current.Value);
        }

        /// <summary>
        /// Test if token is postfix operator.
        /// </summary>
        /// <param name="token">Tested token.</param>
        /// <returns>True if token is postfix operator.</returns>
        internal bool IsPostfixOperator(string token)
        {
            return PostOperators.Contains(token);
        }

        /// <summary>
        /// Test if token is prefix operator.
        /// </summary>
        /// <param name="token">Tested token.</param>
        /// <returns>True if token is prefix operator.</returns>
        internal bool IsPrefixOperator(string token)
        {
            return PrefOperators.Contains(token);
        }
    }
}
