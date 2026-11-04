namespace Dreamy.Debugger.Foundation
{
    using System.Diagnostics;
    using UnityEngine;

    /// <summary>
    /// Base MonoBehaviour which provides useful common functionality
    /// </summary>
    public abstract class BaseBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Get the Transform component, using a cached reference if possible.
        /// </summary>
        public Transform CachedTransform
        {
            [DebuggerStepThrough]
            [DebuggerNonUserCode]
            get
            {
                if (_transform == null)
                {
                    _transform = base.transform;
                }

                return _transform;
            }
        }

        /// <summary>
        /// Get the Collider component, using a cached reference if possible.
        /// </summary>


        /// <summary>
        /// Get the Collider component, using a cached reference if possible.
        /// </summary>


        /// <summary>
        /// Get the Rigidbody component, using a cached reference if possible.
        /// </summary>


        /// <summary>
        /// Get the Rigidbody2D component, using a cached reference if possible.
        /// </summary>


        /// <summary>
        /// Get the GameObject this behaviour is attached to, using a cached reference if possible.
        /// </summary>
        public GameObject CachedGameObject
        {
            [DebuggerStepThrough]
            [DebuggerNonUserCode]
            get
            {
                if (_gameObject == null)
                {
                    _gameObject = base.gameObject;
                }

                return _gameObject;
            }
        }

        // Override existing getters for legacy usage

        // ReSharper disable InconsistentNaming
        public new Transform transform
        {
            get { return CachedTransform; }
        }



        // ReSharper restore InconsistentNaming

        private Transform _transform;
        private GameObject _gameObject;

        /// <summary>
        /// Assert that the value is not null, disable the object and print a debug error message if it is.
        /// </summary>
        /// <param name="value">Object to check</param>
        /// <param name="fieldName">Debug name to pass in</param>
        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void AssertNotNull(object value, string fieldName = null)
        {
            DiagnosticsAssert.AssertNotNull(value, fieldName, this);
        }

        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void Assert(bool condition, string message = null)
        {
            DiagnosticsAssert.Assert(condition, message, this);
        }

        /// <summary>
        /// Assert that the value is not null, disable the object and print a debug error message if it is.
        /// </summary>
        /// <param name="value">Object to check</param>
        /// <param name="fieldName">Debug name to pass in</param>
        /// <returns>True if object is not null</returns>
        [Conditional("UNITY_EDITOR")]
        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void EditorAssertNotNull(object value, string fieldName = null)
        {
            AssertNotNull(value, fieldName);
        }

        [Conditional("UNITY_EDITOR")]
        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void EditorAssert(bool condition, string message = null)
        {
            Assert(condition, message);
        }
    }
}
