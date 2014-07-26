﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AssemblyProviders.CSharp.Interfaces;
using AssemblyProviders.CSharp.Primitives;
using AssemblyProviders.CSharp.LanguageDefinitions;

namespace AssemblyProviders.CSharp
{
    public class SyntaxParser
    {
        CSharpSyntax _language;

        /// <summary>
        /// Parse given invoke info.
        /// </summary>
        /// <param name="services">Available parsing services.</param>
        /// <param name="invokeInfo">Instructions to parse.</param>
        /// <returns>Syntax abstract tree instructions.</returns>
        public CodeNode Parse(Source source)
        {
            var lexer = new Lexer(source);
            _language = new CSharpSyntax(lexer, _getTree);

            return _getTree();
        }

        public string GetSourceCode(string rawCode, out string preCode)
        {
            var trimmed = rawCode.Trim();
            if (!trimmed.StartsWith(":"))
            {
                preCode = null;
                return trimmed;
            }

            var splitChar = trimmed.IndexOf((char)0);
            preCode = trimmed.Substring(1, splitChar - 1).Trim() + ";";

            return trimmed.Substring(splitChar + 1).Trim();
        }

        /// <summary>
        /// Usporada podle priorit jednotlive Node ktere vezme z getNode
        /// </summary>        
        /// <returns></returns>
        private CodeNode _getTree()
        {
            var operands = new Stack<CodeNode>();
            var operators = new Stack<CodeNode>();

            CodeNode newNode = null;

            //determine context to next node 
            bool _expectPrefix = true;
            bool _expectPostfix = false;

            //until tree end token is reached
            while (newNode == null || !newNode.IsTreeEnding)
            {
                if (_language.End)
                    throw CSharpSyntax.ParsingException(newNode, "Expected syntax tree ending");

                //infix to tree, according to priorities                
                newNode = _language.Next(true);
                if (newNode == null)
                    throw new NotSupportedException("newNode cannot be null");

                //resolve prefix/postfix operators they go on stack as operands
                operatorContext(newNode, operands, _expectPrefix, _expectPostfix);



                //add operand on the stack - behind operand we excpect postfix/binary operator
                if (newNode.NodeType != NodeTypes.binaryOperator)
                {
                    _expectPostfix = true;
                    _expectPrefix = false;
                    operands.Push(newNode);
                    if (newNode.NodeType == NodeTypes.block)
                        break;

                    continue;
                }

                //we are adding new operator
                //satisfy all operators with lesser arity
                while (operators.Count > 0 && isLesser(newNode, operators.Peek()))
                    satisfyOperator(operators.Pop(), operands);

                //add operator on the stack
                operators.Push(newNode);

                //after operator we expect operand/prefix operator
                _expectPrefix = true;
                _expectPostfix = false;
            }

            //satisfy all pending operators
            while (operators.Count > 0)
                satisfyOperator(operators.Pop(), operands);

            //check stack state
            if (operands.Count > 1)
                throw CSharpSyntax.ParsingException(operands.Peek(), "Missing operator for operand {0}", operands.Peek());
            if (operators.Count > 0)
                throw CSharpSyntax.ParsingException(operators.Peek(), "Missing operand for operator {0}", operators.Peek());

            var result = operands.Pop();
            result.IsTreeEnding = true;
            return result;
        }

        /// <summary>
        /// repair newNode if prefix/postfix operator, according to expect prefix/postfix context
        /// </summary>
        /// <param name="newNode"></param>
        /// <param name="operands"></param>
        /// <param name="expectPrefix"></param>
        /// <param name="expectPostfix"></param>
        private void operatorContext(CodeNode newNode, Stack<CodeNode> operands, bool expectPrefix, bool expectPostfix)
        {
            bool shouldRepair = false;
            NodeTypes nodeType = newNode.NodeType;

            CodeNode argument = null;

            if (expectPrefix && _language.IsPrefixOperator(newNode.Value))
            {
                shouldRepair = true;
                nodeType = NodeTypes.prefixOperator;
                argument = _getTree();
            }
            else if (expectPostfix && _language.IsPostfixOperator(newNode.Value))
            {
                shouldRepair = true;
                nodeType = NodeTypes.postOperator;

                //postfix operator has argument already on the stack
                argument = operands.Pop();
            }

            if (!shouldRepair)
                //nothing to repair
                return;

            newNode.NodeType = nodeType;

            newNode.AddArgument(argument);
            newNode.IsTreeEnding |= argument.IsTreeEnding;
        }

        private bool isLesser(CodeNode node1, CodeNode node2)
        {
            return _language.HasLesserPriority(node1, node2);
        }


        /// <summary>        
        /// Satisfy given operator node. Satisfed operator is added into operands stack.        
        /// </summary>
        /// <param name="operatorNode">Operator to satisfy.</param>
        /// <param name="operands">Operands used for satisfying.</param>
        private void satisfyOperator(CodeNode operatorNode, Stack<CodeNode> operands)
        {
            var arity = _language.Arity(operatorNode);
            if (operands.Count < arity)
                throw CSharpSyntax.ParsingException(operatorNode, "There aren´t enough operands for the operator {0}", operatorNode.Value);

            var reverseStack = new Stack<CodeNode>();

            for (int i = 0; i < arity; i++) reverseStack.Push(operands.Pop());
            for (int i = 0; i < arity; i++) operatorNode.AddArgument(reverseStack.Pop());

            operands.Push(operatorNode);
        }
    }
}
