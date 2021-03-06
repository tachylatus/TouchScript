/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using UnityEngine;

namespace TouchScript.Hit
{
    internal class TouchHit : ITouchHit
    {
        #region Public properties

        public Transform Transform { get; private set; }

        #endregion

        #region Constructors

        public TouchHit() {}

        #endregion

        #region Internal methods

        internal void INTERNAL_InitWith(Transform value)
        {
            Transform = value;
        }

        internal virtual void INTERNAL_Reset()
        {
            Transform = null;
        }

        #endregion
    }
}
