﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MEFEditor.Drawing
{
    /// <summary>
    /// All implementations has to be immutable in the way, that
    /// passing view into edit action cannot change the view.
    /// </summary>
    public abstract class EditViewBase
    {
        /// <summary>
        /// Error occured in the view during commit or view creation
        /// </summary>
        public abstract string Error { get; }

        /// <summary>
        /// Determine that view has error
        /// </summary>
        public bool HasError { get { return Error != null; } }

        /// <summary>
        /// Commit current view.
        /// </summary>
        /// <returns>Error raised during commit or null if there is no error</returns>
        protected abstract void commit();


        /// <summary>
        /// Commit view 
        /// </summary>
        /// <returns></returns>
        internal bool Commit()
        {
            if (HasError)
                throw new NotSupportedException("Cannot commit because of error: " + Error);

            commit();

            return !HasError;
        }

    }
}
