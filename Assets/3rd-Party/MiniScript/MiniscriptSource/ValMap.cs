using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// ValMap represents a MiniScript map, which under the hood is just a Dictionary
	/// of Value, Value pairs.
	/// </summary>
	public class ValMap : PoolableValue {

		// Assignment override function: return true to cancel (override)
		// the assignment, or false to allow it to happen as normal.
		public delegate bool AssignOverrideFunc(Value key, Value value);
		public AssignOverrideFunc assignOverride;
        [ThreadStatic]
        protected static ValuePool<ValMap> _valuePool;
        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;
#if MINISCRIPT_DEBUG
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }
        private static int _num;
        public int _id;
#endif

		private readonly Dictionary<Value, Value> map;
        private readonly List<Value> _mapKeys = new List<Value>();
        private readonly List<Value> _allKeys = new List<Value>();
        private readonly List<Value> _allValues = new List<Value>();
        // Common values in the map, which we keep here for perf
        private Value selfVal;
        private Value isaVal;
        private Value eventsVal;
        private Value eventValsVal;
        private Value isAtEndVal;
        private Value xVal;
        private Value yVal;
        private Value zVal;
        private Value wVal;
        private Value nameVal;
		private Value positionVal;
		private Value rotationVal;
		private Value velocityVal;
		private Value angularVelocityVal;
		private Value forwardVal;
		private Value rightVal;
		private Value timeVal;
		private Value deltaTimeVal;
		private Value frameCountVal;
        // Whether we need to regenerate our cache
        private bool _isCountDirty = false;
        private bool _isKeysDirty = false;
        private bool _isValuesDirty = false;
        private int _cachedCount = 0;

		private ValMap(bool usePool) : base(usePool) {
			this.map = new Dictionary<Value, Value>(RValueEqualityComparer.instance);
#if MINISCRIPT_DEBUG
            _id = _num++;
#endif
		}
        public static ValMap Create()
        {
            //Console.WriteLine("Creating ValMap ID " + _num);
            if (_valuePool == null)
                _valuePool = new ValuePool<ValMap>();
            else
            {
                ValMap valMap = _valuePool.GetInstance();
                if (valMap != null)
                {
                    valMap._refCount = 1;
#if MINISCRIPT_DEBUG
                    valMap._id = _num++;
#endif
                    return valMap;
                }
            }
#if MINISCRIPT_DEBUG
            _numInstancesAllocated++;
#endif
            return new ValMap(true);
        }
#if MINISCRIPT_DEBUG
        public override void Ref()
        {
            base.Ref();
            //Console.WriteLine("ValMap Ref ref count " + base._refCount);
        }
        public override void Unref()
        {
            if (_refCount == 0)
                Console.WriteLine("Extra unref for map ID #" + _id);
            base.Unref();

            // Handle de-ref when a map self-references.
            // it only works when there's a single level of
            // self reference, and it's fairly expensive, such that
            // I'd rather just leak
            //int numBackReferences = 0;
            //foreach(var kvp in map)
            //{
            //    ValMap valMap = kvp.Value as ValMap;
            //    if(valMap != null)
            //    {
            //        if (valMap.ContainsValue(this))
            //            numBackReferences++;
            //    }
            //    else
            //    {
            //        ValList valList = kvp.Value as ValList;
            //        if (valList != null)
            //        { }
            //    }
            //}
            //if(numBackReferences == _refCount)
            //{
            //    Console.WriteLine("Unreffing, we contain all our own references!");
            //    for(int i = 0; i < numBackReferences; i++)
            //        base.Unref();
            //}

            //Console.WriteLine("ValMap unref ref count " + base._refCount);
        }
#endif
        protected override void ResetState()
        {
            Clear();
        }
        public void Clear()
        {
            foreach(var kvp in map)
            {
                kvp.Key?.Unref();
                kvp.Value?.Unref();
            }
            map.Clear();
            _mapKeys.Clear();
            _allValues.Clear();
            _allKeys.Clear();
            _isCountDirty = false;
            _isKeysDirty = false;
            _isValuesDirty = false;
            _cachedCount = 0;

            selfVal?.Unref();
            selfVal = null;
            isaVal?.Unref();
            isaVal = null;
            eventsVal?.Unref();
            eventsVal = null;
            eventValsVal?.Unref();
            eventValsVal = null;
            isAtEndVal?.Unref();
            isAtEndVal = null;
            xVal?.Unref();
            xVal = null;
            yVal?.Unref();
            yVal = null;
            zVal?.Unref();
            zVal = null;
            wVal?.Unref();
            wVal = null;
            nameVal?.Unref();
            nameVal = null;
            positionVal?.Unref();
            positionVal = null;
            rotationVal?.Unref();
            rotationVal = null;
            velocityVal?.Unref();
            velocityVal = null;
            angularVelocityVal?.Unref();
            angularVelocityVal = null;
            forwardVal?.Unref();
            forwardVal = null;
            rightVal?.Unref();
            rightVal = null;
            timeVal?.Unref();
            timeVal = null;
            deltaTimeVal?.Unref();
            deltaTimeVal = null;
            frameCountVal?.Unref();
            frameCountVal = null;
        }
        protected override void ReturnToPool()
        {
            //Console.WriteLine("Returning ValMap ID " + _id);
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValMap>();
            _valuePool.ReturnToPool(this);
        }

		/// <summary>
		/// Get the number of entries in this map.
		/// </summary>
		public int Count {
			get {
                if (_isCountDirty)
                {
                    // Count the values that we have stored
                    // as built-in
                    int numBuiltIn = 0;
                    if (selfVal != null) numBuiltIn++;
                    if (isaVal != null) numBuiltIn++;
                    if (eventsVal != null) numBuiltIn++;
                    if (eventValsVal != null) numBuiltIn++;
                    if (isAtEndVal != null) numBuiltIn++;
                    if (xVal != null) numBuiltIn++;
                    if (yVal != null) numBuiltIn++;
                    if (zVal != null) numBuiltIn++;
                    if (wVal != null) numBuiltIn++;
                    if (nameVal != null) numBuiltIn++;
                    if (positionVal != null) numBuiltIn++;
                    if (rotationVal != null) numBuiltIn++;
                    if (velocityVal != null) numBuiltIn++;
                    if (angularVelocityVal != null) numBuiltIn++;
                    if (forwardVal != null) numBuiltIn++;
                    if (rightVal != null) numBuiltIn++;
                    if (timeVal != null) numBuiltIn++;
                    if (deltaTimeVal != null) numBuiltIn++;
                    if (frameCountVal != null) numBuiltIn++;

                    _cachedCount = numBuiltIn + map.Count;
                    _isCountDirty = false;
                }
                return _cachedCount;
            }
		}
        private void GenerateKeysList()
        {
            _allKeys.Clear();
            if (selfVal != null)
                _allKeys.Add(ValString.selfStr);
            if (isaVal != null)
                _allKeys.Add(ValString.magicIsA);
            if (eventsVal != null)
                _allKeys.Add(ValString.eventsStr);
            if (eventValsVal != null)
                _allKeys.Add(ValString.eventValsStr);
            if (isAtEndVal != null)
                _allKeys.Add(ValString.isAtEndStr);
            if (xVal != null)
                _allKeys.Add(ValString.xStr);
            if (yVal != null)
                _allKeys.Add(ValString.yStr);
            if (zVal != null)
                _allKeys.Add(ValString.zStr);
            if (wVal != null)
                _allKeys.Add(ValString.wStr);
            if (nameVal != null)
                _allKeys.Add(ValString.nameStr);
            if (positionVal != null)
                _allKeys.Add(ValString.positionStr);
            if (rotationVal != null)
                _allKeys.Add(ValString.rotationStr);
            if (velocityVal != null)
                _allKeys.Add(ValString.velocityStr);
            if (angularVelocityVal != null)
                _allKeys.Add(ValString.angularVelocityStr);
            if (forwardVal != null)
                _allKeys.Add(ValString.forwardStr);
            if (rightVal != null)
                _allKeys.Add(ValString.rightStr);
            if (timeVal != null)
                _allKeys.Add(ValString.timeStr);
            if (deltaTimeVal != null)
                _allKeys.Add(ValString.deltaTimeStr);
            if (frameCountVal != null)
                _allKeys.Add(ValString.frameCountStr);
            // We can't use the _mapKeys b/c it's
            // not in the same order
            //_allKeys.AddRange(_mapKeys);
            foreach (var key in map.Keys)
                _allKeys.Add(key);
            _isKeysDirty = false;
        }
        private void GenerateValuesList()
        {
            _allValues.Clear();
            if (selfVal != null)
                _allValues.Add(selfVal);
            if (isaVal != null)
                _allValues.Add(isaVal);
            if (eventsVal != null)
                _allValues.Add(eventsVal);
            if (eventValsVal != null)
                _allValues.Add(eventValsVal);
            if (isAtEndVal != null)
                _allValues.Add(isAtEndVal);
            if (xVal != null)
                _allValues.Add(xVal);
            if (yVal != null)
                _allValues.Add(yVal);
            if (zVal != null)
                _allValues.Add(zVal);
            if (wVal != null)
                _allValues.Add(wVal);
            if (nameVal != null)
                _allValues.Add(nameVal);
            if (positionVal != null)
                _allValues.Add(positionVal);
            if (rotationVal != null)
                _allValues.Add(rotationVal);
            if (velocityVal != null)
                _allValues.Add(velocityVal);
            if (angularVelocityVal != null)
                _allValues.Add(angularVelocityVal);
            if (forwardVal != null)
                _allValues.Add(forwardVal);
            if (rightVal != null)
                _allValues.Add(rightVal);
            if (timeVal != null)
                _allValues.Add(timeVal);
            if (deltaTimeVal != null)
                _allValues.Add(deltaTimeVal);
            if (frameCountVal != null)
                _allValues.Add(frameCountVal);
            foreach (var val in map.Values)
                _allValues.Add(val);
            _isValuesDirty = false;
        }
		
		/// <summary>
		/// Return the KeyCollection for this map.
		/// </summary>
		public List<Value> Keys {
            get
            {
                if (_isKeysDirty)
                    GenerateKeysList();
                return _allKeys;
            }
		}

		/// <summary>
		/// Return a list of Values for this map.
        /// NB: this list stays owned by the ValMap
		/// </summary>
		public List<Value> Values {
			get {
                if (_isValuesDirty)
                    GenerateValuesList();
                return _allValues;
            }
		}
		
        private bool TryGetInternalBuiltIn(string identifier, out Value value)
        {
            switch (identifier)
            {
                case "self":
                    value = selfVal;
                    return true;
                case "__isa":
                    value = isaVal;
                    return true;
                case "__events":
                    value = eventsVal;
                    return true;
                case "__eventVals":
                    value = eventValsVal;
                    return true;
                case "__isAtEnd":
                    value = isAtEndVal;
                    return true;
                case "x":
                    value = xVal;
                    return true;
                case "y":
                    value = yVal;
                    return true;
                case "z":
                    value = zVal;
                    return true;
                case "w":
                    value = wVal;
                    return true;
                case "name":
                    value = nameVal;
                    return true;
                case "position":
                    value = positionVal;
                    return true;
                case "rotation":
                    value = rotationVal;
                    return true;
                case "velocity":
                    value = velocityVal;
                    return true;
                case "angularVelocity":
                    value = angularVelocityVal;
                    return true;
                case "forward":
                    value = forwardVal;
                    return true;
                case "right":
                    value = rightVal;
                    return true;
                case "time":
                    value = timeVal;
                    return true;
                case "deltaTime":
                    value = deltaTimeVal;
                    return true;
                case "frameCount":
                    value = frameCountVal;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }
        private bool TryGetInternalBuiltIn(Value identifier, out Value value)
        {
            ValString str = identifier as ValString;
            if(str == null)
            {
                value = null;
                return false;
            }
            // If the ValString wasn't made by ValString as a built-in, we
            // can exit early
            if (!str.IsBuiltIn)
            {
                value = null;
                return false;
            }
            // Internally, unity turns string switch case into a dictionary
            // which is fast, but working with the hashes can be sorta slow
            // so we just use the instance ID on the val string, as int
            // comparison is very fast
            //TODO this could be a static BST or a hash map!
            int recvID = str.InstanceID;
            if (recvID == ValString.selfStr.InstanceID)
            {
                value = selfVal;
                return true;
            }
            if(recvID == ValString.magicIsA.InstanceID)
            {
                value = isaVal;
                return true;
            }
            if(recvID == ValString.eventsStr.InstanceID)
            {
                value = eventsVal;
                return true;
            }
            if(recvID == ValString.eventValsStr.InstanceID)
            {
                value = eventValsVal;
                return true;
            }
            if(recvID == ValString.isAtEndStr.InstanceID)
            {
                value = isAtEndVal;
                return true;
            }
            if(recvID == ValString.xStr.InstanceID)
            {
                value = xVal;
                return true;
            }
            if(recvID == ValString.yStr.InstanceID)
            {
                value = yVal;
                return true;
            }
            if(recvID == ValString.zStr.InstanceID)
            {
                value = zVal;
                return true;
            }
            if(recvID == ValString.wStr.InstanceID)
            {
                value = wVal;
                return true;
            }
            if(recvID == ValString.nameStr.InstanceID)
            {
                value = nameVal;
                return true;
            }
            if(recvID == ValString.positionStr.InstanceID)
            {
                value = positionVal;
                return true;
            }
            if(recvID == ValString.rotationStr.InstanceID)
            {
                value = rotationVal;
                return true;
            }
            if(recvID == ValString.velocityStr.InstanceID)
            {
                value = velocityVal;
                return true;
            }
            if(recvID == ValString.angularVelocityStr.InstanceID)
            {
                value = angularVelocityVal;
                return true;
            }
            if(recvID == ValString.forwardStr.InstanceID)
            {
                value = forwardVal;
                return true;
            }
            if(recvID == ValString.rightStr.InstanceID)
            {
                value = rightVal;
                return true;
            }
            if(recvID == ValString.timeStr.InstanceID)
            {
                value = timeVal;
                return true;
            }
            if(recvID == ValString.deltaTimeStr.InstanceID)
            {
                value = deltaTimeVal;
                return true;
            }
            if(recvID == ValString.frameCountStr.InstanceID)
            {
                value = frameCountVal;
                return true;
            }
            value = null;
            return false;
        }
        private bool TrySetInternalBuiltIn(Value identifier, Value value, out Value previousVal)
        {
            ValString str = identifier as ValString;
            if(str == null)
            {
                previousVal = null;
                return false;
            }
            // If the ValString wasn't made by ValString as a built-in, we
            // can exit early
            if (!str.IsBuiltIn)
            {
                previousVal = null;
                return false;
            }
            int recvID = str.InstanceID;
            if (recvID == ValString.selfStr.InstanceID)
            {
                previousVal = selfVal;
                selfVal?.Unref();
                selfVal = value;
                return true;
            }
            if(recvID == ValString.magicIsA.InstanceID)
            {
                previousVal = isaVal;
                isaVal?.Unref();
                isaVal = value;
                return true;
            }
            if(recvID == ValString.eventsStr.InstanceID)
            {
                previousVal = eventsVal;
                eventsVal?.Unref();
                eventsVal = value;
                return true;
            }
            if(recvID == ValString.eventValsStr.InstanceID)
            {
                previousVal = eventValsVal;
                eventValsVal?.Unref();
                eventValsVal = value;
                return true;
            }
            if(recvID == ValString.isAtEndStr.InstanceID)
            {
                previousVal = isAtEndVal;
                isAtEndVal?.Unref();
                isAtEndVal = value;
                return true;
            }
            if(recvID == ValString.xStr.InstanceID)
            {
                previousVal = xVal;
                xVal?.Unref();
                xVal = value;
                return true;
            }
            if(recvID == ValString.yStr.InstanceID)
            {
                previousVal = yVal;
                yVal?.Unref();
                yVal = value;
                return true;
            }
            if(recvID == ValString.zStr.InstanceID)
            {
                previousVal = zVal;
                zVal?.Unref();
                zVal = value;
                return true;
            }
            if(recvID == ValString.wStr.InstanceID)
            {
                previousVal = wVal;
                wVal?.Unref();
                wVal = value;
                return true;
            }
            if(recvID == ValString.nameStr.InstanceID)
            {
                previousVal = nameVal;
                nameVal?.Unref();
                nameVal = value;
                return true;
            }
            if(recvID == ValString.positionStr.InstanceID)
            {
                previousVal = positionVal;
                positionVal?.Unref();
                positionVal = value;
                return true;
            }
            if(recvID == ValString.rotationStr.InstanceID)
            {
                previousVal = rotationVal;
                rotationVal?.Unref();
                rotationVal = value;
                return true;
            }
            if(recvID == ValString.velocityStr.InstanceID)
            {
                previousVal = velocityVal;
                velocityVal?.Unref();
                velocityVal = value;
                return true;
            }
            if(recvID == ValString.angularVelocityStr.InstanceID)
            {
                previousVal = angularVelocityVal;
                angularVelocityVal?.Unref();
                angularVelocityVal = value;
                return true;
            }
            if(recvID == ValString.forwardStr.InstanceID)
            {
                previousVal = forwardVal;
                forwardVal?.Unref();
                forwardVal = value;
                return true;
            }
            if(recvID == ValString.rightStr.InstanceID)
            {
                previousVal = rightVal;
                rightVal?.Unref();
                rightVal = value;
                return true;
            }
            if(recvID == ValString.timeStr.InstanceID)
            {
                previousVal = timeVal;
                timeVal?.Unref();
                timeVal = value;
                return true;
            }
            if(recvID == ValString.deltaTimeStr.InstanceID)
            {
                previousVal = deltaTimeVal;
                deltaTimeVal?.Unref();
                deltaTimeVal = value;
                return true;
            }
            if(recvID == ValString.frameCountStr.InstanceID)
            {
                previousVal = frameCountVal;
                frameCountVal?.Unref();
                frameCountVal = value;
                return true;
            }
            previousVal = null;
            return false;
        }

		/// <summary>
		/// Set the value associated with the given key (index).  This is where
		/// we take the opportunity to look for an assignment override function,
		/// and if found, give that a chance to handle it instead.
		/// </summary>
		public override void SetElem(Value index, Value value) {
            SetElem(index, value, true);
		}
		public void SetElem(Value index, Value value, bool takeValueRef, bool takeIndexRef=true) {
            ValNumber newNum = value as ValNumber;
            //Console.WriteLine("Map set elem " + index.ToString() + ": " + value.ToString());
            if (takeValueRef)
                value?.Ref();
            if (takeIndexRef)
                index?.Ref();
			if (index == null) index = ValNull.instance;

			if (assignOverride == null || !assignOverride(index, value)) {

                // Check against common entries first, for perf
                ValString indexStr = index as ValString;
                if(indexStr != null)
                {
                    // We want to replicate the behavior of a map, so to
                    // preserve the way that you can set a key to null, we
                    // simply store a ValNull here, and pull out a ValNull
                    // later but just return a null
                    Value builtInVal = value ?? ValNull.instance;
                    if (TrySetInternalBuiltIn(indexStr, builtInVal, out Value oldVal))
                    {
                        // If we're overwriting a value, keep count/keys
                        if(oldVal != null)
                        {
                            _isValuesDirty = true;
                        }
                        else
                        {
                            _isCountDirty = true;
                            _isKeysDirty = true;
                            _isValuesDirty = true;
                        }
                        return;
                    }
                }

                if(map.TryGetValue(index, out Value existing))
                {
                    // Unref the value that's currently there
                    if(existing != null)
                        existing.Unref(); // There can be null entries in this list
                    // Try to get the key that's there and unref it
                    Value existingKey = RemoveBySwap(_mapKeys, index);
                    map.Remove(existingKey);
                    if (existingKey != null)
                        existingKey.Unref();

                    // Overwrote value, count didn't change but keys/values did
                    _isKeysDirty = true;
                    _isValuesDirty = true;
                }
                else
                {
                    _isCountDirty = true;
                    _isKeysDirty = true;
                    _isValuesDirty = true;
                }
                _mapKeys.Add(index);
                map[index] = value;
			}
		}
        public bool Remove(Value keyVal)
        {
            // Check against common entries first, for perf
            ValString indexStr = keyVal as ValString;
            Value existing;
            if(indexStr != null)
            {
                if (TrySetInternalBuiltIn(indexStr, null, out existing))
                {
                    // We return true only if we had an existing value
                    if(existing != null)
                    {
                        _isCountDirty = true;
                        _isKeysDirty = true;
                        _isValuesDirty = true;
                        return true;
                    }
                    return false;
                }
            }
            // Pull the current key/value so that we can unref it
            if(map.TryGetValue(keyVal, out existing))
            {
                existing.Unref();
                // Try to get the key that's there and unref it
                Value existingKey = RemoveBySwap(_mapKeys, keyVal);
                if (existingKey != null)
                    existingKey.Unref();
                map.Remove(keyVal);
                _isCountDirty = true;
                _isKeysDirty = true;
                _isValuesDirty = true;
                return true;
            }
            return false;
        }
		public void SetElem(string index, Value value, bool takeValueRef) {
            ValString keyStr = ValString.Create(index);
            SetElem(keyStr, value, takeValueRef);
            keyStr.Unref();
		}
        // O(n)
        private Value RemoveBySwap(List<Value> list, Value newKey)
        {
            for(int i = 0; i < _mapKeys.Count; i++)
            {
                Value key = _mapKeys[i];
                if (key.Equality(newKey) == 1.0)
                {
                    _mapKeys[i] = _mapKeys[_mapKeys.Count - 1];
                    _mapKeys.RemoveAt(_mapKeys.Count - 1);
                    return key;
                }
            }
            return null;
        }
        /// <summary>
        /// Accessor to get/set on element of this map by a string key, walking
        /// the __isa chain as needed.  (Note that if you want to avoid that, then
        /// simply look up your value in .map directly.)
        /// </summary>
        /// <param name="identifier">string key to get/set</param>
        /// <returns>value associated with that key</returns>
        public Value this [string identifier] {
            //TODO I think we might unecessarily be walking up the _isa chain here
			get { 
				var idVal = ValString.Create(identifier);
                Value result = Lookup(idVal);
                idVal.Unref();
                return result;
			}
			set {
                SetElem(identifier, value, true);
            }
		}

		public Value this [Value identifier] {
			get {
                if(TryGetValue(identifier, out Value ret))
                    return ret;
                return null;
			}
			set {
                SetElem(identifier, value, true);
            }
		}
		
		/// <summary>
		/// Convenience method to check whether the map contains a given string key.
		/// </summary>
		/// <param name="identifier">string key to check for</param>
		/// <returns>true if the map contains that key; false otherwise</returns>
		public bool ContainsKey(string identifier) {
            if(TryGetInternalBuiltIn(identifier, out Value existing))
                return existing != null;
			var idVal = ValString.Create(identifier);
			bool result = map.ContainsKey(idVal);
            idVal.Unref();
			return result;
		}
		
		/// <summary>
		/// Convenience method to check whether this map contains a given key
		/// (of arbitrary type).
		/// </summary>
		/// <param name="key">key to check for</param>
		/// <returns>true if the map contains that key; false otherwise</returns>
		public bool ContainsKey(Value key) {
			if (key == null)
                key = ValNull.instance;
            else
            {
                if (TryGetInternalBuiltIn(key, out Value existing))
                    return existing != null;
            }
			return map.ContainsKey(key);
		}
		
		/// <summary>
		/// Look up the given identifier as quickly as possible, without
		/// walking the __isa chain or doing anything fancy.  (This is used
		/// when looking up local variables.)
		/// </summary>
		/// <param name="identifier">identifier to look up</param>
		/// <returns>true if found, false if not</returns>
		public bool TryGetValue(string identifier, out Value value) {
			var idVal = ValString.Create(identifier);
			bool result = TryGetValue(idVal, out value);
            idVal.Unref();
			return result;
		}
		/// <summary>
		/// Look up the given identifier as quickly as possible, without
		/// walking the __isa chain or doing anything fancy.  (This is used
		/// when looking up local variables.)
		/// </summary>
		/// <param name="identifier">identifier to look up</param>
		/// <returns>true if found, false if not</returns>
		public bool TryGetValue(Value identifier, out Value value) {
            if (TryGetInternalBuiltIn(identifier, out value))
            {
                if (value == null)
                    return false; // Not found
                if (value is ValNull)// 
                    value = null;
                return true;
            }
			bool result = map.TryGetValue(identifier, out value);
			return result;
		}
		
		/// <summary>
		/// Look up a value in this dictionary, walking the __isa chain to find
		/// it in a parent object if necessary.
		/// </summary>
		/// <param name="key">key to search for</param>
		/// <returns>value associated with that key, or null if not found</returns>
		public Value Lookup(Value key) {
			if (key == null) key = ValNull.instance;
			Value result = null;
			ValMap obj = this;
			while (obj != null) {
				if (obj.TryGetValue(key, out result)) return result;
				Value parent;
				if (!obj.TryGetValue(ValString.magicIsA, out parent)) break;
				obj = parent as ValMap;
			}
			return null;
		}
		
		/// <summary>
		/// Look up a value in this dictionary, walking the __isa chain to find
		/// it in a parent object if necessary; return both the value found an
		/// (via the output parameter) the map it was found in.
		/// </summary>
		/// <param name="key">key to search for</param>
		/// <returns>value associated with that key, or null if not found</returns>
		public Value Lookup(Value key, out ValMap valueFoundIn) {
			if (key == null) key = ValNull.instance;
			Value result = null;
			ValMap obj = this;
			while (obj != null) {
				if (obj.TryGetValue(key, out result)) {
					valueFoundIn = obj;
					return result;
				}
				Value parent;
				if (!obj.TryGetValue(ValString.magicIsA, out parent)) break;
				obj = parent as ValMap;
			}
			valueFoundIn = null;
			return null;
		}
		
		public override Value FullEval(Context context) {
            // Evaluate each of our elements, and if any of those is
            // a variable or temp, then resolve those now.
            var keys = Keys;
            var vals = Values;
			for(int i = 0; i < keys.Count; i++) {
                Value key = keys[i];
                Value value = vals[i];
				if (key is ValTemp || key is ValVar) {
					Remove(key);
					key = key.Val(context, true);
                    SetElem(key, value);
				}
				if (value is ValTemp || value is ValVar) {
                    SetElem(key, value.Val(context, true));
				}
			}
			return this;
		}

		public ValMap EvalCopy(Context context) {
			// Create a copy of this map, evaluating its members as we go.
			// This is used when a map literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			var result = ValMap.Create();
            var keys = Keys;
            var values = Values;
			for(int i = 0; i < keys.Count; i++) {
				Value key = keys[i];
                Value value = values[i];
				if (key is ValTemp || key is ValVar)
                    key = key.Val(context, false);
				if (value is ValTemp || value is ValVar)
                    value = value.Val(context, false);
                result.SetElem(key, value, true, true);
			}
			return result;
		}

		public override string CodeForm(Machine vm, int recursionLimit=-1) {
			if (recursionLimit == 0) return "{...}";
			if (recursionLimit > 0 && recursionLimit < 3 && vm != null) {
				string shortName = vm.FindShortName(this);
				if (shortName != null) return shortName;
			}
            //TODO this will break with recursion!
            if (_workingStringBuilder == null)
                _workingStringBuilder = new StringBuilder();
            else
                _workingStringBuilder.Clear();
            _workingStringBuilder.Append("{");
			int i = 0;
            var keys = Keys;
            var values = Values;
            for(int j = 0; j < keys.Count; j++)
            {
                Value key = keys[j];
                Value val = values[j];
				int nextRecurLimit = recursionLimit - 1;
				if (key == ValString.magicIsA)
                    nextRecurLimit = 1;
                _workingStringBuilder.Append(key.CodeForm(vm, nextRecurLimit));
                _workingStringBuilder.Append(": ");
                _workingStringBuilder.Append(val == null ? "null" : val.CodeForm(vm, nextRecurLimit));
                if(++i != Count)
                    _workingStringBuilder.Append(", ");
			}
            _workingStringBuilder.Append("}");
            return _workingStringBuilder.ToString();
		}

		public override string ToString(Machine vm) {
			return CodeForm(vm, 3);
		}

		public override bool IsA(Value type, Machine vm) {
			// If the given type is the magic 'map' type, then we're definitely
			// one of those.  Otherwise, we have to walk the __isa chain.
			if (type == vm.mapType) return true;
			Value p = isaVal;
			while (p != null) {
				if (p == type) return true;
				if (!(p is ValMap)) return false;
				((ValMap)p).TryGetValue(ValString.magicIsA, out p);
			}
			return false;
		}

        public override bool BoolValue() {
			// A map is considered true if it is nonempty.
			return Count > 0;
		}

		public override int Hash(int recursionDepth=16) {
			int result = Count.GetHashCode();
			if (recursionDepth < 0) return result;  // (important to recurse an odd number of times, due to bit flipping)
            var keys = Keys;
            var vals = Values;
			for(int i = 0; i < keys.Count; i++) {
				result ^= keys[i].Hash(recursionDepth-1);
                Value val = vals[i];
				if (val != null)
                    result ^= val.Hash(recursionDepth-1);
			}
			return result;
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			if (!(rhs is ValMap)) return 0;
            ValMap rhm = rhs as ValMap;
			if (rhm == this) return 1;  // (same map)
			if (Count != rhm.Count) return 0;
			if (recursionDepth < 1) return 0.5;		// in too deep
			double result = 1;
            var ourKeys = Keys;
            var ourVals = Values;
            var theirKeys = rhm.Keys;
            var theirVals = rhm.Values;

            for(int i = 0; i < ourKeys.Count; i++) {

                if (ourKeys[i] != theirKeys[i])
                    return 0;
                Value ourVal = ourVals[i];
                Value theirVal = theirVals[i];
                if (ourVal == null && theirVal != null)
                    return 0;
                if (ourVal == null && theirVal == null)
                    continue;
				result *= ourVal.Equality(theirVal, recursionDepth-1);
				if (result <= 0) break;
			}
			return result;
		}

		/// <summary>
		/// Get the indicated key/value pair as another map containing "key" and "value".
		/// (This is used when iterating over a map with "for".)
		/// </summary>
		/// <param name="index">0-based index of key/value pair to get.</param>
		/// <returns>new map containing "key" and "value" with the requested key/value pair</returns>
		public ValMap GetKeyValuePair(int index) {
            var keys = Keys;
			if (index < 0 || index >= keys.Count) {
				throw new IndexException("index " + index + " out of range for map");
			}
            var val = Values[index];
            var key = keys[index];
			var result = ValMap.Create();
            result.SetElem(keyStr, (key is ValNull) ? null : key, true, true);
            result.SetElem(valStr, val, true, true);
			return result;
		}

		public override bool CanSetElem() { return true; }

        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValMapTypeInt;
        }

        static ValString keyStr = ValString.Create("key", false);
		static ValString valStr = ValString.Create("value", false);
	}
}
