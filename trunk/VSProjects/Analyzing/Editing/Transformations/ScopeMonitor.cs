﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Utilities;

using Analyzing.Execution;

namespace Analyzing.Editing.Transformations
{
    class ScopeMonitor
    {
        /// <summary>
        /// Instances that are monitored by current monitor
        /// </summary>
        private readonly HashSet<Instance> _monitoredInstances;

        /// <summary>
        /// Scopes indexed by their instances
        /// We use only non-degenerated scopes
        /// </summary>
        private readonly MultiDictionary<Instance, InstanceScope> _scopes = new MultiDictionary<Instance, InstanceScope>();

        public ScopeMonitor(IEnumerable<Instance> instances, ExecutionView view)
        {
            _monitoredInstances = new HashSet<Instance>(instances);

            var instanceStarts = from instance in instances where instance.CreationBlock != null select instance.CreationBlock;
            var latestStart = view.EarliestBlock(instanceStarts);

            initializeScopes(view, latestStart);
        }

        internal ScopeStepper CreateStepper()
        {
            return new ScopeStepper(_scopes);
        }

        private void initializeScopes(ExecutionView view, ExecutedBlock latestStart)
        {
            var current = latestStart;

            //initialize active scope index
            var activeScopes = new Dictionary<Instance, Dictionary<VariableName, ExecutedBlock>>();
            foreach (var instance in _monitoredInstances)
            {
                activeScopes.Add(instance, new Dictionary<VariableName, ExecutedBlock>());
            }

            //search block for scopes
            while (current != null)
            {
                foreach (var instance in _monitoredInstances)
                {
                    var scopeStarts = view.ScopeStarts(current, instance);
                    var scopeEnds = view.ScopeEnds(current, instance);

                    //activate new scope starts
                    var scopesIndex = activeScopes[instance];
                    foreach (var variable in scopeStarts)
                    {
                        scopesIndex[variable] = current;
                    }

                    foreach (var variable in scopeEnds)
                    {
                        ExecutedBlock scopeStart;
                        if (!scopesIndex.TryGetValue(variable, out scopeStart))
                            //there is no matching start
                            continue;

                        scopesIndex.Remove(variable);

                        if (scopeStart == current)
                            //empty scope
                            continue;

                        var scope = new InstanceScope(variable, instance, scopeStart, current);
                        _scopes.Add(instance, scope);
                    }
                }

                current = view.NextBlock(current);
            }

            //handle non-closed scopes
            foreach (var instance in _monitoredInstances)
            {
                //activate new scope starts
                var scopesIndex = activeScopes[instance];

                foreach (var scopePair in scopesIndex)
                {
                    var scope = new InstanceScope(scopePair.Key, instance, scopePair.Value, current);
                    _scopes.Add(instance, scope);
                }
            }
        }
    }
}
