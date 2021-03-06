﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MEFEditor.Analyzing;
using MEFEditor.Analyzing.Editing;

using RecommendedExtensions.Core.Languages.CSharp.Interfaces;

namespace RecommendedExtensions.Core.Languages.CSharp.Transformations
{
    class CallProvider : CallTransformProvider
    {
        private readonly INodeAST _call;
        private HashSet<int> _optionals;

        internal CallProvider(INodeAST callNode)
        {
            _call = callNode;
            _call.Source.CompilationInfo.RegisterCallProvider(callNode, this);
        }

        public override RemoveTransformProvider RemoveArgument(int argumentIndex, bool keepSideEffect)
        {
            var argNode = _call.Arguments[argumentIndex - 1];

            return new SourceRemoveProvider((view, source) =>
            {
                if (keepSideEffect)
                {
                    source.ExcludeNode(view, argNode);
                }
                else
                {
                    source.RemoveNode(view, argNode);
                }
            }, argNode);
        }

        public override Transformation RewriteArgument(int argumentIndex, ValueProvider valuePovider)
        {
            if (_call.Arguments.Length <= argumentIndex - 1)
                return null;

            var argNode = _call.Arguments[argumentIndex - 1];

            return new SourceTransformation((view, source) =>
            {              
                source.Rewrite(view, argNode, valuePovider(view));
            }, _call.Source);
        }

        public override Transformation AppendArgument(int argumentIndex, ValueProvider valueProvider)
        {
            var index = argumentIndex - 1;
            if (_call.Arguments.Length != index)
                //cannot append argument
                return null;

            return new SourceTransformation((view, source) =>
            {
                var value = valueProvider(view);
                if (view.IsAborted)
                    return;

                source.AppendArgument(view, _call, value);
            }, _call.Source);
        }

        public override RemoveTransformProvider Remove()
        {
            return new SourceRemoveProvider((view, source) =>
            {
                source.RemoveNode(view, _call);
            }, _call);
        }

        public override NavigationAction GetNavigation()
        {
            return () =>
            {
                _call.StartingToken.Position.Navigate();
            };
        }

        public override bool IsOptionalArgument(int argumentIndex)
        {
            if (_optionals == null)
                //no argument is optional
                return false;

            return _optionals.Contains(argumentIndex);
        }

        public override void SetOptionalArgument(int index)
        {
            if (_optionals == null)
                _optionals = new HashSet<int>();

            _optionals.Add(index);
        }
    }
}
