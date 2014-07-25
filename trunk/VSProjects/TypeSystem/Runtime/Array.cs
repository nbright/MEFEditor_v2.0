﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Utilities;
using Analyzing;
using Analyzing.Execution;

namespace TypeSystem.Runtime
{
    public class Array<ItemType> : IEnumerable<ItemType>, System.Collections.IEnumerable
        where ItemType : InstanceWrap
    {
        private readonly Dictionary<string, ItemType> _data = new Dictionary<string, ItemType>();

        public int Length { get; private set; }

        public MethodID SetItemMethod
        {
            get
            {
                var index = ParameterTypeInfo.Create("index", TypeDescriptor.Create<int>());
                var item = ParameterTypeInfo.Create("item", TypeDescriptor.Create<ItemType>());
                return Naming.Method(TypeDescriptor.Create("Array<" + item.Type.TypeName + ",1>"), Naming.IndexerSetter, false, index, item);
            }
        }

        public Array(int length)
        {
            //TODO multidimensional array
            Length = length;
        }

        public Array(IEnumerable data, AnalyzingContext context)
        {
            int i = 0;
            foreach (var item in data)
            {
                var toSet = item as InstanceWrap;
                if (toSet == null)
                {
                    var instance = item as Instance;
                    if (instance == null)
                    {
                        instance = context.Machine.CreateDirectInstance(item);
                    }
                    toSet = new InstanceWrap(instance);
                }


                set_Item(i, toSet as ItemType);
                ++i;
            }
            Length = _data.Count;
        }
        #region Supported array members

        public void set_Item(int index, ItemType instance)
        {
            _data[getKey(index)] = instance;
        }

        public ItemType get_Item(int index)
        {
            var key = getKey(index);

            ItemType value;
            _data.TryGetValue(key, out value);

            return value;
        }

        #endregion

        #region IEnumerable implementations

        /// <inheritdoc />
        public IEnumerator<ItemType> GetEnumerator()
        {
            return _data.Values.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _data.Values.GetEnumerator();
        }

        #endregion

        private string getKey(int index)
        {
            return index.ToString();
        }

        internal ResultType Unwrap<ResultType>()
        {
            //TODO multidimensional array
            var elementType = typeof(ResultType).GetElementType();
            var array = Array.CreateInstance(elementType, Length);

            for (int i = 0; i < Length; ++i)
            {
                var item = get_Item(i);

                object value;
                if (typeof(Instance).IsAssignableFrom(elementType))
                {
                    //instance shouldnt been unwrapped
                    value = item.Wrapped;
                }
                else if (item.Wrapped is DirectInstance)
                {
                    value = item.Wrapped.DirectValue;
                }
                else
                {
                    value = item.Wrapped;
                }

                var castedValue = TypeUtilities.DynamicCast(value, elementType);
                array.SetValue(castedValue, i);
            }

            var result = (ResultType)(object)array;
            return result;
        }
    }
}
