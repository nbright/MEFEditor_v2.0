﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzing.Editing.Transformations
{
    class AddCallTransformation : Transformation
    {
        /// <summary>
        /// Call provider that will be asked for call definition        
        /// </summary>
        private readonly CallProvider _provider;

        internal AddCallTransformation(CallProvider provider)
        {
            _provider = provider;
        }

        protected override void apply()
        {
            var call = _provider(View);
            if (call == null)
                return;

            var scopeTransform = new CommonScopeTransformation(call.Instances);
            View.Apply(scopeTransform);

            var instanceScopes = scopeTransform.InstanceScopes;
            if (scopeTransform.InstanceScopes == null)
            {
                View.Abort("Can't get instances to same scope");
                return;
            }

            var subsitutedCall = call.Substitute(instanceScopes.InstanceVariables);
            View.AppendCall(instanceScopes.ScopeBlock, subsitutedCall);
        }

        private void appendInstance(object testedObj, List<Instance> instances)
        {
            var inst = testedObj as Instance;
            if (inst == null)
                return;

            instances.Add(inst);
        }
    }
}
