﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Analyzing.Editing;    

namespace Analyzing
{
    public abstract class Instance
    {
        readonly List<Edit> _edits = new List<Edit>();

        public readonly InstanceInfo Info;

        /// <summary>
        /// Determine unique ID during analyzing context. During execution ID may changed.        
        /// </summary>
        public string ID { get; private set; }

        /// <summary>
        /// Determine that instnace is dirty. It means that its state may not be correctly analyzed.
        /// This is usually caused by unknown operation processing
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Available edit actions for current instance
        /// </summary>
        public IEnumerable<Edit> Edits { get { return _edits; } }

        public abstract object DirectValue { get; }

        internal Instance(InstanceInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            Info = info;
        }

        internal void AddEdit(Edit edit)
        {
            if (!edit.IsEmpty)
            {
                _edits.Add(edit);
            }
        }

        /// <summary>
        /// Set default id for instance. Default ID can be overriden by hinted one.
        /// </summary>
        /// <param name="defaultID">ID used as default, if none better is hinted</param>
        internal void SetDefaultID(string defaultID)
        {
            ID = defaultID;
        }
    }
}
