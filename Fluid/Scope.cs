﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid
{
    public class Scope
    {
        private readonly Dictionary<string, FluidValue> _properties = new Dictionary<string, FluidValue>();
        private readonly Scope _parent;
        private readonly bool _checkParent;

        public Scope()
        {
            _parent = null;
        }

        public Scope(Scope parent, bool checkParent = true)
        {
            _parent = parent;
            _checkParent = checkParent;
        }

        public IEnumerable<string> Properties => _properties.Keys;

        /// <summary>
        /// Returns the value with the specified name in the chain of scopes, or undefined
        /// if it doesn't exist.
        /// </summary>
        /// <param name="name">The name of the value to return.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluidValue GetValue(string name)
        {
            if (name == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(name));
            }

            if (_properties.TryGetValue(name, out var result))
            {
                return result;
            }

            return _checkParent && _parent != null
                ? _parent.GetValue(name)
                : NilValue.Instance;
        }

        public void Delete(string name)
        {
            _properties.Remove(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(string name, FluidValue value)
        {
            _properties[name] = value;
        }

        public Scope EnterChildScope(bool keepParent = true)
        {
            return new Scope(this, keepParent);
        }

        public Scope ReleaseScope()
        {
            return _parent;
        }

        public FluidValue GetIndex(FluidValue index)
        {
            return GetValue(index.ToString());
        }
    }
}
