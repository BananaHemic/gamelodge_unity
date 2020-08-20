﻿using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public abstract class LoopScrollRect : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler, ICanvasElement, ILayoutElement, ILayoutGroup
    {
        //==========LoopScrollRect==========
        protected struct ScrollElem
        {
            object userData;
            GameObject go;
        }
        private ILoopScrollPrefabSource _prefabSource;
        private ILoopScrollDataSource _dataSource;
        // TODO it would probably be a teensy bit faster to make this a linked list
        // Although, activeChildren is never very large, so list operations should be somewhat fast
        // Actually a circular queue could also do the trick
        private readonly List<GameObject> _activeChildren = new List<GameObject>(16);
        private readonly List<object> _allData = new List<object>(64);
        public int Count { get { return _allData.Count; } }
        public int ActiveCount { get { return _activeChildren.Count; } }

        //[Tooltip("Total count, negative means INFINITE mode")]
        //public int totalCount;

        protected bool _didAddOrRemoveThisFrame = false;
        protected float threshold = 0;
        [Tooltip("Reverse direction for dragging")]
        public bool reverseDirection = false;
        [Tooltip("Rubber scale for outside")]
        public float rubberScale = 1;

        [SerializeField]
        protected int _itemStartIndex = 0;
        [SerializeField]
        protected int _itemEndIndex = 0;
        private bool _isContentBoundsDirty = false;

        protected abstract float GetSize(RectTransform item);
        protected abstract float GetDimension(Vector2 vector);
        protected abstract Vector2 GetVector(float value);
        protected int directionSign = 0;

        private float m_ContentSpacing = -1;
        protected GridLayoutGroup m_GridLayout = null;
        protected float contentSpacing
        {
            get
            {
                if (m_ContentSpacing >= 0)
                {
                    return m_ContentSpacing;
                }
                m_ContentSpacing = 0;
                if (content != null)
                {
                    HorizontalOrVerticalLayoutGroup layout1 = content.GetComponent<HorizontalOrVerticalLayoutGroup>();
                    if (layout1 != null)
                    {
                        m_ContentSpacing = layout1.spacing;
                    }
                    m_GridLayout = content.GetComponent<GridLayoutGroup>();
                    if (m_GridLayout != null)
                    {
                        m_ContentSpacing = Mathf.Abs(GetDimension(m_GridLayout.spacing));
                    }
                }
                return m_ContentSpacing;
            }
        }

        private int m_ContentConstraintCount = 0;
        /// <summary>
        /// This is 1, unless we're using a grid layout,
        /// in which case it's the GridLayoutGroup
        /// constraint count
        /// </summary>
        protected int contentConstraintCount
        {
            get
            {
                if (m_ContentConstraintCount > 0)
                {
                    return m_ContentConstraintCount;
                }
                m_ContentConstraintCount = 1;
                if (content != null)
                {
                    GridLayoutGroup layout2 = content.GetComponent<GridLayoutGroup>();
                    if (layout2 != null)
                    {
                        if (layout2.constraint == GridLayoutGroup.Constraint.Flexible)
                        {
                            Debug.LogWarning("[LoopScrollRect] Flexible not supported yet");
                        }
                        m_ContentConstraintCount = layout2.constraintCount;
                    }
                }
                return m_ContentConstraintCount;
            }
        }

        // the first line
        int StartLine
        {
            get
            {
                return Mathf.CeilToInt((float)(_itemStartIndex) / contentConstraintCount);
            }
        }

        // how many lines we have for now
        int CurrentLines
        {
            get
            {
                return Mathf.CeilToInt((float)(_itemEndIndex - _itemStartIndex) / contentConstraintCount);
            }
        }

        // how many lines we have in total
        int TotalLines
        {
            get
            {
                return Mathf.CeilToInt((float)(_allData.Count) / contentConstraintCount);
            }
        }

        protected virtual bool UpdateItems(Bounds viewBounds, Bounds contentBounds) { return false; }
        //==========LoopScrollRect==========

        public enum MovementType
        {
            Unrestricted, // Unrestricted movement -- can scroll forever
            Elastic, // Restricted but flexible -- can go past the edges, but springs back in place
            Clamped, // Restricted movement where it's not possible to go past the edges
        }

        public enum ScrollbarVisibility
        {
            Permanent,
            AutoHide,
            AutoHideAndExpandViewport,
        }

        [Serializable]
        public class ScrollRectEvent : UnityEvent<Vector2> { }

        [SerializeField]
        private RectTransform m_Content;
        public RectTransform content { get { return m_Content; } set { m_Content = value; } }

        [SerializeField]
        private bool m_Horizontal = true;
        public bool horizontal { get { return m_Horizontal; } set { m_Horizontal = value; } }

        [SerializeField]
        private bool m_Vertical = true;
        public bool vertical { get { return m_Vertical; } set { m_Vertical = value; } }

        [SerializeField]
        private MovementType m_MovementType = MovementType.Elastic;
        public MovementType movementType { get { return m_MovementType; } set { m_MovementType = value; } }

        [SerializeField]
        private float m_Elasticity = 0.1f; // Only used for MovementType.Elastic
        public float elasticity { get { return m_Elasticity; } set { m_Elasticity = value; } }

        [SerializeField]
        private bool m_Inertia = true;
        public bool inertia { get { return m_Inertia; } set { m_Inertia = value; } }

        [SerializeField]
        private float m_DecelerationRate = 0.135f; // Only used when inertia is enabled
        public float decelerationRate { get { return m_DecelerationRate; } set { m_DecelerationRate = value; } }

        [SerializeField]
        private float m_ScrollSensitivity = 1.0f;
        public float scrollSensitivity { get { return m_ScrollSensitivity; } set { m_ScrollSensitivity = value; } }

        [SerializeField]
        private RectTransform m_Viewport;
        public RectTransform viewport { get { return m_Viewport; } set { m_Viewport = value; SetDirtyCaching(); } }

        [SerializeField]
        private Scrollbar m_HorizontalScrollbar;
        public Scrollbar horizontalScrollbar
        {
            get
            {
                return m_HorizontalScrollbar;
            }
            set
            {
                if (m_HorizontalScrollbar)
                    m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
                m_HorizontalScrollbar = value;
                if (m_HorizontalScrollbar)
                    m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
                SetDirtyCaching();
            }
        }

        [SerializeField]
        private Scrollbar m_VerticalScrollbar;
        public Scrollbar verticalScrollbar
        {
            get
            {
                return m_VerticalScrollbar;
            }
            set
            {
                if (m_VerticalScrollbar)
                    m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);
                m_VerticalScrollbar = value;
                if (m_VerticalScrollbar)
                    m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);
                SetDirtyCaching();
            }
        }

        [SerializeField]
        private ScrollbarVisibility m_HorizontalScrollbarVisibility;
        public ScrollbarVisibility horizontalScrollbarVisibility { get { return m_HorizontalScrollbarVisibility; } set { m_HorizontalScrollbarVisibility = value; SetDirtyCaching(); } }

        [SerializeField]
        private ScrollbarVisibility m_VerticalScrollbarVisibility;
        public ScrollbarVisibility verticalScrollbarVisibility { get { return m_VerticalScrollbarVisibility; } set { m_VerticalScrollbarVisibility = value; SetDirtyCaching(); } }

        [SerializeField]
        private float m_HorizontalScrollbarSpacing;
        public float horizontalScrollbarSpacing { get { return m_HorizontalScrollbarSpacing; } set { m_HorizontalScrollbarSpacing = value; SetDirty(); } }

        [SerializeField]
        private float m_VerticalScrollbarSpacing;
        public float verticalScrollbarSpacing { get { return m_VerticalScrollbarSpacing; } set { m_VerticalScrollbarSpacing = value; SetDirty(); } }

        [SerializeField]
        private ScrollRectEvent m_OnValueChanged = new ScrollRectEvent();
        public ScrollRectEvent onValueChanged { get { return m_OnValueChanged; } set { m_OnValueChanged = value; } }

        // The offset from handle position to mouse down position
        private Vector2 m_PointerStartLocalCursor = Vector2.zero;
        private Vector2 m_ContentStartPosition = Vector2.zero;

        private RectTransform m_ViewRect;

        protected RectTransform viewRect
        {
            get
            {
                if(m_ViewRect != null)
                    return m_ViewRect;

                m_ViewRect = m_Viewport;
                if (m_ViewRect == null)
                    m_ViewRect = (RectTransform)transform;
                return m_ViewRect;
            }
        }

        private Bounds m_ContentBounds;
        private Bounds m_ViewBounds;

        private Vector2 m_Velocity;
        public Vector2 velocity { get { return m_Velocity; } set { m_Velocity = value; } }

        private bool m_Dragging;

        private Vector2 m_PrevPosition = Vector2.zero;
        private Bounds m_PrevContentBounds;
        private Bounds m_PrevViewBounds;
        // In the Unity source, this defaults to false,
        // but making it instead default to true substantially
        // improves performance. I do not understand why this
        // doesn't default to true by default...
        // TODO figure out why
        private bool m_HasRebuiltLayout = true;

        private bool m_HSliderExpand;
        private bool m_VSliderExpand;
        private float m_HSliderHeight;
        private float m_VSliderWidth;

        [System.NonSerialized]
        private RectTransform m_Rect;
        private RectTransform rectTransform
        {
            get
            {
                if (m_Rect == null)
                    m_Rect = GetComponent<RectTransform>();
                return m_Rect;
            }
        }

        private RectTransform m_HorizontalScrollbarRect;
        private RectTransform m_VerticalScrollbarRect;

        private DrivenRectTransformTracker m_Tracker;

        //==========LoopScrollRect==========
        public void Init(ILoopScrollPrefabSource prefabSource, ILoopScrollDataSource dataSource)
        {
            _prefabSource = prefabSource;
            _dataSource = dataSource;
            flexibleWidth = -1;
        }
        public void ClearCells()
        {
            if (Application.isPlaying)
            {
                _itemStartIndex = 0;
                _itemEndIndex = 0;
                _allData.Clear();
                for(int i = 0; i < _activeChildren.Count; i++)
                    _prefabSource.ReturnObject(_activeChildren[i]);
                _activeChildren.Clear();
            }
        }
        public void AddItem(object obj, bool updateUI)
        {
            _allData.Add(obj);
            _didAddOrRemoveThisFrame = true;
            _isContentBoundsDirty = true;
            if (updateUI)
                UpdateBounds(true);
                //RefillCellsFromEnd(len - 1);
        }
        public void RemoveItem(object obj, bool updateUI)
        {
            // Get the index
            int index = -1;
            for(int i = 0; i < _allData.Count; i++)
            {
                //if(_allData[i] == obj)
                if(obj.Equals(_allData[i]))
                {
                    index = i;
                    break;
                }
            }
            if(index == -1)
            {
                Debug.LogError("Failed to remove item, could not find " + obj);
                return;
            }
            //Debug.Log("Will remove at " + index);
            // TODO add a flag for if the items in this list
            // are order-dependent. If not, we can use FastRemove
            _allData.RemoveAt(index);
            _didAddOrRemoveThisFrame = true;
            _isContentBoundsDirty = true;
            if (updateUI)
            {
                if(index < _itemStartIndex)
                {
                    _itemStartIndex--;
                    _itemEndIndex--;
                }
                else if(index < _itemEndIndex)
                {
                    // If the item is currently visible then
                    // we need to remove the actual instance
                    int activeChildIdx = index - _itemStartIndex;
                    //Debug.Log("Removing visible #" + activeChildIdx);
                    _prefabSource.ReturnObject(_activeChildren[activeChildIdx]);
                    _activeChildren.RemoveAt(activeChildIdx);
                    _itemEndIndex--;
                }

                UpdateBounds(true);
            }
        }
        public bool TryGetActiveObject(object userData, out GameObject elem)
        {
            for(int i = 0; i < _activeChildren.Count; i++)
            {
                if(_allData[_itemStartIndex + i] == userData) {
                    elem = _activeChildren[i];
                    return true;
                }
            }
            elem = null;
            return false;
        }
        public void ScrollToCell(int index, float speed)
        {
            if ((index < 0 || index >= _allData.Count))
            {
                Debug.LogWarningFormat("invalid index {0}", index);
                return;
            }
            if (speed <= 0)
            {
                Debug.LogWarningFormat("invalid speed {0}", speed);
                return;
            }
            StopAllCoroutines();
            StartCoroutine(ScrollToCellCoroutine(index, speed));
        }
        IEnumerator ScrollToCellCoroutine(int index, float speed)
        {
            bool needMoving = true;
            while (needMoving)
            {
                yield return null;
                if (!m_Dragging)
                {
                    float move = 0;
                    if (index < _itemStartIndex)
                    {
                        move = -Time.deltaTime * speed;
                    }
                    else if (index >= _itemEndIndex)
                    {
                        move = Time.deltaTime * speed;
                    }
                    else
                    {
                        m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
                        var m_ItemBounds = GetBounds4Item(index);
                        var offset = 0.0f;
                        if (directionSign == -1)
                            offset = reverseDirection ? (m_ViewBounds.min.y - m_ItemBounds.min.y) : (m_ViewBounds.max.y - m_ItemBounds.max.y);
                        else if (directionSign == 1)
                            offset = reverseDirection ? (m_ItemBounds.max.x - m_ViewBounds.max.x) : (m_ItemBounds.min.x - m_ViewBounds.min.x);
                        // check if we cannot move on
                        if (offset > 0 && _itemEndIndex == _allData.Count && !reverseDirection)
                        {
                            m_ItemBounds = GetBounds4Item(_allData.Count - 1);
                            // reach bottom
                            if ((directionSign == -1 && m_ItemBounds.min.y > m_ViewBounds.min.y) ||
                                (directionSign == 1 && m_ItemBounds.max.x < m_ViewBounds.max.x))
                            {
                                needMoving = false;
                                break;
                            }
                        }
                        else if (offset < 0 && _itemStartIndex == 0 && reverseDirection)
                        {
                            m_ItemBounds = GetBounds4Item(0);
                            if ((directionSign == -1 && m_ItemBounds.max.y < m_ViewBounds.max.y) ||
                                (directionSign == 1 && m_ItemBounds.min.x > m_ViewBounds.min.x))
                            {
                                needMoving = false;
                                break;
                            }
                        }

                        float maxMove = Time.deltaTime * speed;
                        if (Mathf.Abs(offset) < maxMove)
                        {
                            needMoving = false;
                            move = offset;
                        }
                        else
                            move = Mathf.Sign(offset) * maxMove;
                    }
                    if (move != 0)
                    {
                        Vector2 offset = GetVector(move);
                        content.anchoredPosition += offset;
                        m_PrevPosition += offset;
                        m_ContentStartPosition += offset;
                    }
                }
            }
            StopMovement();
            UpdatePrevData();
        }
        public void RefreshCells()
        {
            if (Application.isPlaying && this.isActiveAndEnabled)
            {
                _itemEndIndex = _itemStartIndex;
                // recycle items if we can
                for (int i = 0; i < _activeChildren.Count; i++)
                {
                    if (_itemEndIndex < _allData.Count)
                    {
                        object userData = _allData[_itemEndIndex];
                        _dataSource.ProvideData(_activeChildren[i], _itemEndIndex, userData);
                        _itemEndIndex++;
                    }
                    else
                    {
                        // Clear the rest, from i to the end
                        for(int j = i; j < _activeChildren.Count; j++)
                            _prefabSource.ReturnObject(_activeChildren[j]);
                        // Remove all after i
                        _activeChildren.RemoveRange(i, _activeChildren.Count - i);
                        break;
                    }
                }
            }
        }
        public void RefillCellsFromEnd(int offset = 0)
        {
            if (!Application.isPlaying || _prefabSource == null)
                return;

            StopMovement();
            _itemEndIndex = reverseDirection ? offset : _allData.Count - offset;
            _itemStartIndex = _itemEndIndex;

            if (_itemStartIndex % contentConstraintCount != 0)
                Debug.LogWarning("Grid will become strange since we can't fill items in the last line");

            for (int i = 0; i < _activeChildren.Count; i++)
                _prefabSource.ReturnObject(_activeChildren[i]);
            _activeChildren.Clear();

            float sizeToFill, sizeFilled = 0;
            if (directionSign == -1)
                sizeToFill = viewRect.rect.size.y;
            else
                sizeToFill = viewRect.rect.size.x;

            while (sizeToFill > sizeFilled)
            {
                float size = reverseDirection ? NewItemAtEnd() : NewItemAtStart();
                if (size <= 0)
                    break;
                sizeFilled += size;
            }

            Vector2 pos = m_Content.anchoredPosition;
            float dist = Mathf.Max(0, sizeFilled - sizeToFill);
            if (reverseDirection)
                dist = -dist;
            if (directionSign == -1)
                pos.y = dist;
            else if (directionSign == 1)
                pos.x = -dist;
            m_Content.anchoredPosition = pos;
        }
        public void RefillCells(int offset = 0, bool fillViewRect = false)
        {
            if (!Application.isPlaying || _prefabSource == null)
                return;

            StopMovement();
            _itemStartIndex = reverseDirection ? _allData.Count - offset : offset;
            //Debug.Log("Start now " + _itemStartIndex);
            _itemEndIndex = _itemStartIndex;

            if (_itemStartIndex % contentConstraintCount != 0)
                Debug.LogWarning("Grid will become strange since we can't fill items in the first line");

            // Don't `Canvas.ForceUpdateCanvases();` here, or it will new/delete cells to change itemTypeStart/End
            for (int i = 0; i < _activeChildren.Count; i++)
                _prefabSource.ReturnObject(_activeChildren[i]);
            _activeChildren.Clear();

            // If this is the first frame, then we probably have not
            // yet rebuilt the layout, so this would incorrectly find
            // the size to be 0. So we force rebuild now
            if(Time.frameCount == 1)
                Canvas.ForceUpdateCanvases();

            float sizeToFill, sizeFilled = 0;
            // m_ViewBounds may be not ready when RefillCells on Start
            if (directionSign == -1)
                sizeToFill = viewRect.rect.size.y;
            else
                sizeToFill = viewRect.rect.size.x;

            //Debug.Log("viewRect: " + viewRect.gameObject.name + " rect " + viewRect.rect.size.ToString());
            float itemSize = 0;
            //Debug.Log("size: " + sizeFilled + "/" + sizeToFill + " frame #" + Time.frameCount);

            while (sizeToFill > sizeFilled)
            {
                float size = reverseDirection ? NewItemAtStart() : NewItemAtEnd();
                if (size <= 0)
                    break;// This happens when we run out of objects needed
                else
                    itemSize = size;
                //Debug.Log("size: " + sizeFilled + "/" + sizeToFill + " just added " + size);
                sizeFilled += size;
            }

            if (fillViewRect && itemSize > 0 && sizeFilled < sizeToFill)
            {
                int itemsToAddCount = (int)((sizeToFill - sizeFilled) / itemSize);        //calculate how many items can be added above the offset, so it still is visible in the view
                int newOffset = offset - itemsToAddCount;
                if (newOffset < 0)
                    newOffset = 0;
                if (newOffset != offset)
                {
                    RefillCells(newOffset);                 //refill again, with the new offset value, and now with fillViewRect disabled.
                    // TODO should we return here?
                }
            }

            Vector2 pos = m_Content.anchoredPosition;
            if (directionSign == -1)
                pos.y = 0;
            else if (directionSign == 1)
                pos.x = 0;
            m_Content.anchoredPosition = pos;
        }
        protected float NewItemAtStart()
        {
            if (_itemStartIndex - contentConstraintCount < 0)
                return 0;
            float size = 0;
            for (int i = 0; i < contentConstraintCount; i++)
            {
                _itemStartIndex--;
                RectTransform newItem = InstantiateNextItem(_itemStartIndex);
                newItem.SetAsFirstSibling();
                _activeChildren.Insert(0, newItem.gameObject);
                //Debug.Log("Init at start #" + newItem.name);
                size = Mathf.Max(GetSize(newItem), size);
            }
            threshold = Mathf.Max(threshold, size * 1.5f);

            if (!reverseDirection)
            {
                Vector2 offset = GetVector(size);
                content.anchoredPosition += offset;
                m_PrevPosition += offset;
                m_ContentStartPosition += offset;
            }

            return size;
        }
        protected float DeleteItemAtStart()
        {
            // special case: when moving or dragging, we cannot simply delete start when we've reached the end
            if ((_itemEndIndex >= _allData.Count - 1 && verticalNormalizedPosition >= 1f)
                || _activeChildren.Count == 0)
            {
                return 0;
            }

            float size = 0;
            for (int i = 0; i < contentConstraintCount; i++)
            {
                GameObject obj = _activeChildren[0];
                //Debug.Log("Deleting at start #" + obj.name);
                size = Mathf.Max(GetSize(obj.transform as RectTransform), size);
                _prefabSource.ReturnObject(obj);
                _activeChildren.RemoveAt(0);
                _itemStartIndex++;
                //Debug.Log("Drag: " + m_Dragging + " vel: " + m_Velocity.y + " Delete Start index now " + _itemStartIndex + " end: " + _itemEndIndex + " count " + _allData.Count + " pos " + verticalNormalizedPosition);
                if (_activeChildren.Count == 0)
                    break;
            }

            if (!reverseDirection)
            {
                Vector2 offset = GetVector(size);
                content.anchoredPosition -= offset;
                m_PrevPosition -= offset;
                m_ContentStartPosition -= offset;
            }
            return size;
        }
        protected float NewItemAtEnd()
        {
            if (_itemEndIndex >= _allData.Count)
            {
                //Debug.LogWarning("Dropping new item at end, idx " + _itemEndIndex + " count " + totalCount);
                return 0;
            }
            float size = 0;
            // issue 4: fill lines to end first
            int count = contentConstraintCount - (_activeChildren.Count % contentConstraintCount);
            for (int i = 0; i < count; i++)
            {
                RectTransform newItem = InstantiateNextItem(_itemEndIndex);
                _activeChildren.Add(newItem.gameObject);
                //Debug.Log("Adding at end #" + newItem.name);
                size = Mathf.Max(GetSize(newItem), size);
                _itemEndIndex++;
                if (_itemEndIndex >= _allData.Count)
                    break;
            }
            threshold = Mathf.Max(threshold, size * 1.5f);

            if (reverseDirection)
            {
                Vector2 offset = GetVector(size);
                content.anchoredPosition -= offset;
                m_PrevPosition -= offset;
                m_ContentStartPosition -= offset;
            }

            return size;
        }
        protected float DeleteItemAtEnd()
        {
            //Debug.Log("constraint " + contentConstraintCount + " itemTypeStart " + _itemStartIndex + " children " + content.childCount);
            if (((m_Dragging || m_Velocity != Vector2.zero) && _itemStartIndex < contentConstraintCount)
                || _activeChildren.Count == 0)
            {
                return 0;
            }

            float size = 0;
            for (int i = 0; i < contentConstraintCount; i++)
            {
                GameObject obj = _activeChildren[_activeChildren.Count - 1];
                //Debug.Log("Deleting at end #" + obj.name);
                _activeChildren.RemoveAt(_activeChildren.Count - 1);
                RectTransform oldItem = obj.transform as RectTransform;
                size = Mathf.Max(GetSize(oldItem), size);
                _prefabSource.ReturnObject(obj);

                _itemEndIndex--;
                if (_activeChildren.Count == 0 || _itemEndIndex % contentConstraintCount == 0)
                    break;  //just delete the whole row
            }

            if (reverseDirection)
            {
                Vector2 offset = GetVector(size);
                content.anchoredPosition += offset;
                m_PrevPosition += offset;
                m_ContentStartPosition += offset;
            }
            //Debug.Log("Deleted, size now " + size);
            return size;
        }

        private RectTransform InstantiateNextItem(int itemIdx)
        {
            GameObject obj = _prefabSource.GetObject(content);
            RectTransform nextItem = obj.transform as RectTransform;
            //nextItem.SetParent(content, false);
            //nextItem.gameObject.SetActive(true);
            object userData = _allData[itemIdx];
            _dataSource.ProvideData(obj, itemIdx, userData);
            return nextItem;
        }
        //==========LoopScrollRect==========
        public virtual void Rebuild(CanvasUpdate executing)
        {
            if (executing == CanvasUpdate.Prelayout)
            {
                UpdateCachedData();
            }

            if (executing == CanvasUpdate.PostLayout)
            {
                UpdateBounds();
                UpdateScrollbars(Vector2.zero);
                UpdatePrevData();

                m_HasRebuiltLayout = true;
            }
        }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        void UpdateCachedData()
        {
            Transform transform = this.transform;
            m_HorizontalScrollbarRect = m_HorizontalScrollbar == null ? null : m_HorizontalScrollbar.transform as RectTransform;
            m_VerticalScrollbarRect = m_VerticalScrollbar == null ? null : m_VerticalScrollbar.transform as RectTransform;

            // These are true if either the elements are children, or they don't exist at all.
            bool viewIsChild = (viewRect.parent == transform);
            bool hScrollbarIsChild = (!m_HorizontalScrollbarRect || m_HorizontalScrollbarRect.parent == transform);
            bool vScrollbarIsChild = (!m_VerticalScrollbarRect || m_VerticalScrollbarRect.parent == transform);
            bool allAreChildren = (viewIsChild && hScrollbarIsChild && vScrollbarIsChild);

            m_HSliderExpand = allAreChildren && m_HorizontalScrollbarRect && horizontalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
            m_VSliderExpand = allAreChildren && m_VerticalScrollbarRect && verticalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
            m_HSliderHeight = (m_HorizontalScrollbarRect == null ? 0 : m_HorizontalScrollbarRect.rect.height);
            m_VSliderWidth = (m_VerticalScrollbarRect == null ? 0 : m_VerticalScrollbarRect.rect.width);
        }
        protected override void OnEnable()
        {
            base.OnEnable();

            if (m_HorizontalScrollbar)
                m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
            if (m_VerticalScrollbar)
                m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);

            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        }
        protected override void OnDisable()
        {
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

            if (m_HorizontalScrollbar)
                m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
            if (m_VerticalScrollbar)
                m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);

            m_HasRebuiltLayout = false;
            m_Tracker.Clear();
            m_Velocity = Vector2.zero;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            base.OnDisable();
        }
        public override bool IsActive()
        {
            return base.IsActive() && m_Content != null;
        }
        private void EnsureLayoutHasRebuilt()
        {
            if (!m_HasRebuiltLayout && !CanvasUpdateRegistry.IsRebuildingLayout())
            {
                //Debug.Log("layout rebuild");
                Canvas.ForceUpdateCanvases();
                _isContentBoundsDirty = false;
            }
        }
        public virtual void StopMovement()
        {
            m_Velocity = Vector2.zero;
        }
        public virtual void OnScroll(PointerEventData data)
        {
            if (!IsActive())
                return;

            EnsureLayoutHasRebuilt();
            UpdateBounds();

            Vector2 delta = data.scrollDelta;
            // Down is positive for scroll events, while in UI system up is positive.
            delta.y *= -1;
            if (vertical && !horizontal)
            {
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    delta.y = delta.x;
                delta.x = 0;
            }
            if (horizontal && !vertical)
            {
                if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                    delta.x = delta.y;
                delta.y = 0;
            }

            Vector2 position = m_Content.anchoredPosition;
            position += delta * m_ScrollSensitivity;
            if (m_MovementType == MovementType.Clamped)
                position += CalculateOffset(position - m_Content.anchoredPosition);

            SetContentAnchoredPosition(position);
            UpdateBounds();
        }
        public virtual void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            m_Velocity = Vector2.zero;
        }
        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (!IsActive())
                return;

            UpdateBounds();

            m_PointerStartLocalCursor = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out m_PointerStartLocalCursor);
            m_ContentStartPosition = m_Content.anchoredPosition;
            m_Dragging = true;
        }
        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            m_Dragging = false;
        }
        public virtual void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (!IsActive())
                return;

            Vector2 localCursor;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out localCursor))
                return;

            UpdateBounds();

            var pointerDelta = localCursor - m_PointerStartLocalCursor;
            Vector2 position = m_ContentStartPosition + pointerDelta;

            // Offset to get content into place in the view.
            Vector2 offset = CalculateOffset(position - m_Content.anchoredPosition);
            position += offset;
            if (m_MovementType == MovementType.Elastic)
            {
                //==========LoopScrollRect==========
                if (offset.x != 0)
                    position.x = position.x - RubberDelta(offset.x, m_ViewBounds.size.x) * rubberScale;
                if (offset.y != 0)
                    position.y = position.y - RubberDelta(offset.y, m_ViewBounds.size.y) * rubberScale;
                //==========LoopScrollRect==========
            }

            SetContentAnchoredPosition(position);
        }
        protected virtual void SetContentAnchoredPosition(Vector2 position)
        {
            if (!m_Horizontal)
                position.x = m_Content.anchoredPosition.x;
            if (!m_Vertical)
                position.y = m_Content.anchoredPosition.y;

            if (position != m_Content.anchoredPosition)
            {
                m_Content.anchoredPosition = position;
                UpdateBounds(true);
            }
        }
        private void UpdatePrevData()
        {
            if (m_Content == null)
                m_PrevPosition = Vector2.zero;
            else
                m_PrevPosition = m_Content.anchoredPosition;
            m_PrevViewBounds = m_ViewBounds;
            m_PrevContentBounds = m_ContentBounds;
        }
        private void UpdateScrollbars(Vector2 offset)
        {
            if (m_HorizontalScrollbar)
            {
                //==========LoopScrollRect==========
                if (m_ContentBounds.size.x > 0 && _allData.Count > 0)
                {
                    m_HorizontalScrollbar.size = Mathf.Clamp01((m_ViewBounds.size.x - Mathf.Abs(offset.x)) / m_ContentBounds.size.x * CurrentLines / TotalLines);
                }
                //==========LoopScrollRect==========
                else
                    m_HorizontalScrollbar.size = 1;

                m_HorizontalScrollbar.value = horizontalNormalizedPosition;
            }

            if (m_VerticalScrollbar)
            {
                //==========LoopScrollRect==========
                if (m_ContentBounds.size.y > 0 && _allData.Count > 0)
                {
                    m_VerticalScrollbar.size = Mathf.Clamp01((m_ViewBounds.size.y - Mathf.Abs(offset.y)) / m_ContentBounds.size.y * CurrentLines / TotalLines);
                }
                //==========LoopScrollRect==========
                else
                    m_VerticalScrollbar.size = 1;

                m_VerticalScrollbar.value = verticalNormalizedPosition;
            }
        }
        public Vector2 normalizedPosition
        {
            get
            {
                return new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition);
            }
            set
            {
                SetNormalizedPosition(value.x, 0);
                SetNormalizedPosition(value.y, 1);
            }
        }
        public float horizontalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                //==========LoopScrollRect==========
                if(_allData.Count > 0 && _itemEndIndex > _itemStartIndex)
                {
                    //TODO: consider contentSpacing
                    float elementSize = m_ContentBounds.size.x / CurrentLines;
                    float totalSize = elementSize * TotalLines;
                    float offset = m_ContentBounds.min.x - elementSize * StartLine;
                    
                    if (totalSize <= m_ViewBounds.size.x)
                        return (m_ViewBounds.min.x > offset) ? 1 : 0;
                    return (m_ViewBounds.min.x - offset) / (totalSize - m_ViewBounds.size.x);
                }
                else
                    return 0.5f;
                //==========LoopScrollRect==========
            }
            set
            {
                SetNormalizedPosition(value, 0);
            }
        }
        public float verticalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                //==========LoopScrollRect==========
                if(_allData.Count > 0 && _itemEndIndex > _itemStartIndex)
                {
                    //TODO: consider contentSpacinge
                    float elementSize = m_ContentBounds.size.y / CurrentLines;
                    float totalSize = elementSize * TotalLines;
                    float offset = m_ContentBounds.max.y + elementSize * StartLine;

                    if (totalSize <= m_ViewBounds.size.y)
                        return (offset > m_ViewBounds.max.y) ? 1 : 0;
                    return (offset - m_ViewBounds.max.y) / (totalSize - m_ViewBounds.size.y);
                }
                else
                    return 0.5f;
                //==========LoopScrollRect==========
            }
            set
            {
                SetNormalizedPosition(value, 1);
            }
        }
        
        private void SetHorizontalNormalizedPosition(float value) { SetNormalizedPosition(value, 0); }
        private void SetVerticalNormalizedPosition(float value) { SetNormalizedPosition(value, 1); }

        private void SetNormalizedPosition(float value, int axis)
        {
            //==========LoopScrollRect==========
            if (_itemEndIndex <= _itemStartIndex)
                return;
            //==========LoopScrollRect==========

            EnsureLayoutHasRebuilt();
            UpdateBounds();

            //==========LoopScrollRect==========
            Vector3 localPosition = m_Content.localPosition;
            float newLocalPosition = localPosition[axis];
            if (axis == 0)
            {
                float elementSize = m_ContentBounds.size.x / CurrentLines;
                float totalSize = elementSize * TotalLines;
                float offset = m_ContentBounds.min.x - elementSize * StartLine;

                newLocalPosition += m_ViewBounds.min.x - value * (totalSize - m_ViewBounds.size[axis]) - offset;
            }
            else if(axis == 1)
            {
                float elementSize = m_ContentBounds.size.y / CurrentLines;
                float totalSize = elementSize * TotalLines;
                float offset = m_ContentBounds.max.y + elementSize * StartLine;

                newLocalPosition -= offset - value * (totalSize - m_ViewBounds.size.y) - m_ViewBounds.max.y;
            }
            //==========LoopScrollRect==========

            if (Mathf.Abs(localPosition[axis] - newLocalPosition) > 0.01f)
            {
                localPosition[axis] = newLocalPosition;
                m_Content.localPosition = localPosition;
                m_Velocity[axis] = 0;
                UpdateBounds(true);
            }
        }

        private static float RubberDelta(float overStretching, float viewSize)
        {
            return (1 - (1 / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1))) * viewSize * Mathf.Sign(overStretching);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            SetDirty();
        }

        private bool hScrollingNeeded
        {
            get
            {
                if (Application.isPlaying)
                    return m_ContentBounds.size.x > m_ViewBounds.size.x + 0.01f;
                return true;
            }
        }
        private bool vScrollingNeeded
        {
            get
            {
                if (Application.isPlaying)
                    return m_ContentBounds.size.y > m_ViewBounds.size.y + 0.01f
                        || _itemStartIndex > 0
                        || _itemEndIndex < Count;
                return true;
            }
        }

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }

        public virtual float minWidth { get { return -1; } }
        public virtual float preferredWidth { get { return -1; } }
        public virtual float flexibleWidth { get; private set; }

        public virtual float minHeight { get { return -1; } }
        public virtual float preferredHeight { get { return -1; } }
        public virtual float flexibleHeight { get { return -1; } }

        public virtual int layoutPriority { get { return -1; } }

        public virtual void SetLayoutHorizontal()
        {
            m_Tracker.Clear();

            if (m_HSliderExpand || m_VSliderExpand)
            {
                m_Tracker.Add(this, viewRect,
                    DrivenTransformProperties.Anchors |
                    DrivenTransformProperties.SizeDelta |
                    DrivenTransformProperties.AnchoredPosition);

                // Make view full size to see if content fits.
                viewRect.anchorMin = Vector2.zero;
                viewRect.anchorMax = Vector2.one;
                viewRect.sizeDelta = Vector2.zero;
                viewRect.anchoredPosition = Vector2.zero;

                // Recalculate content layout with this size to see if it fits when there are no scrollbars.
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
                m_ContentBounds = GetBounds();
            }

            // If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it.
            if (m_VSliderExpand && vScrollingNeeded)
            {
                viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);

                // Recalculate content layout with this size to see if it fits vertically
                // when there is a vertical scrollbar (which may reflowed the content to make it taller).
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
                m_ContentBounds = GetBounds();
            }

            // If it doesn't fit horizontally, enable horizontal scrollbar and shrink view vertically to make room for it.
            if (m_HSliderExpand && hScrollingNeeded)
            {
                viewRect.sizeDelta = new Vector2(viewRect.sizeDelta.x, -(m_HSliderHeight + m_HorizontalScrollbarSpacing));
                m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
                m_ContentBounds = GetBounds();
            }

            // If the vertical slider didn't kick in the first time, and the horizontal one did,
            // we need to check again if the vertical slider now needs to kick in.
            // If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it.
            if (m_VSliderExpand && vScrollingNeeded && viewRect.sizeDelta.x == 0 && viewRect.sizeDelta.y < 0)
            {
                viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);
            }
        }

        public virtual void SetLayoutVertical()
        {
            UpdateScrollbarLayout();
            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = GetBounds();
        }

        void UpdateScrollbarVisibility()
        {
            if (m_VerticalScrollbar && m_VerticalScrollbarVisibility != ScrollbarVisibility.Permanent && m_VerticalScrollbar.gameObject.activeSelf != vScrollingNeeded)
                m_VerticalScrollbar.gameObject.SetActive(vScrollingNeeded);

            if (m_HorizontalScrollbar && m_HorizontalScrollbarVisibility != ScrollbarVisibility.Permanent && m_HorizontalScrollbar.gameObject.activeSelf != hScrollingNeeded)
                m_HorizontalScrollbar.gameObject.SetActive(hScrollingNeeded);
        }

        void UpdateScrollbarLayout()
        {
            if (m_VSliderExpand && m_HorizontalScrollbar)
            {
                m_Tracker.Add(this, m_HorizontalScrollbarRect,
                              DrivenTransformProperties.AnchorMinX |
                              DrivenTransformProperties.AnchorMaxX |
                              DrivenTransformProperties.SizeDeltaX |
                              DrivenTransformProperties.AnchoredPositionX);
                m_HorizontalScrollbarRect.anchorMin = new Vector2(0, m_HorizontalScrollbarRect.anchorMin.y);
                m_HorizontalScrollbarRect.anchorMax = new Vector2(1, m_HorizontalScrollbarRect.anchorMax.y);
                m_HorizontalScrollbarRect.anchoredPosition = new Vector2(0, m_HorizontalScrollbarRect.anchoredPosition.y);
                if (vScrollingNeeded)
                    m_HorizontalScrollbarRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), m_HorizontalScrollbarRect.sizeDelta.y);
                else
                    m_HorizontalScrollbarRect.sizeDelta = new Vector2(0, m_HorizontalScrollbarRect.sizeDelta.y);
            }

            if (m_HSliderExpand && m_VerticalScrollbar)
            {
                m_Tracker.Add(this, m_VerticalScrollbarRect,
                              DrivenTransformProperties.AnchorMinY |
                              DrivenTransformProperties.AnchorMaxY |
                              DrivenTransformProperties.SizeDeltaY |
                              DrivenTransformProperties.AnchoredPositionY);
                m_VerticalScrollbarRect.anchorMin = new Vector2(m_VerticalScrollbarRect.anchorMin.x, 0);
                m_VerticalScrollbarRect.anchorMax = new Vector2(m_VerticalScrollbarRect.anchorMax.x, 1);
                m_VerticalScrollbarRect.anchoredPosition = new Vector2(m_VerticalScrollbarRect.anchoredPosition.x, 0);
                if (hScrollingNeeded)
                    m_VerticalScrollbarRect.sizeDelta = new Vector2(m_VerticalScrollbarRect.sizeDelta.x, -(m_HSliderHeight + m_HorizontalScrollbarSpacing));
                else
                    m_VerticalScrollbarRect.sizeDelta = new Vector2(m_VerticalScrollbarRect.sizeDelta.x, 0);
            }
        }

        private void UpdateBounds(bool updateItems = false)
        {
            if (m_Content == null)
            {
                Debug.LogError("No content!");
                return;
            }

            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = GetBounds();
            //Debug.Log("view bounds from " + viewRect.gameObject.name + " " + m_ViewBounds, viewRect.gameObject);
            //Debug.Log("content bounds from " + m_Content.gameObject.name + " " + m_ContentBounds, m_Content.gameObject);

            // ============LoopScrollRect============
            // Don't do this in Rebuild
            if (Application.isPlaying && updateItems && UpdateItems(m_ViewBounds, m_ContentBounds))
            {
                // TODO this may slow
                Canvas.ForceUpdateCanvases();
                m_ContentBounds = GetBounds();
            }
            // ============LoopScrollRect============

            // Make sure content bounds are at least as large as view by adding padding if not.
            // One might think at first that if the content is smaller than the view, scrolling should be allowed.
            // However, that's not how scroll views normally work.
            // Scrolling is *only* possible when content is *larger* than view.
            // We use the pivot of the content rect to decide in which directions the content bounds should be expanded.
            // E.g. if pivot is at top, bounds are expanded downwards.
            // This also works nicely when ContentSizeFitter is used on the content.
            Vector3 contentSize = m_ContentBounds.size;
            Vector3 contentPos = m_ContentBounds.center;
            Vector3 excess = m_ViewBounds.size - contentSize;
            if (excess.x > 0)
            {
                contentPos.x -= excess.x * (m_Content.pivot.x - 0.5f);
                contentSize.x = m_ViewBounds.size.x;
            }
            if (excess.y > 0)
            {
                contentPos.y -= excess.y * (m_Content.pivot.y - 0.5f);
                contentSize.y = m_ViewBounds.size.y;
            }

            m_ContentBounds.size = contentSize;
            m_ContentBounds.center = contentPos;
        }

        private readonly Vector3[] m_Corners = new Vector3[4];
        private Bounds GetBounds()
        {
            if (m_Content == null)
            {
                Debug.LogError("No content");
                return new Bounds();
            }

            // Force refresh canvas as needed
            if (_isContentBoundsDirty)
            {
                _isContentBoundsDirty = false;
                Canvas.ForceUpdateCanvases();
            }

            var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var toLocal = viewRect.worldToLocalMatrix;
            m_Content.GetWorldCorners(m_Corners);
            for (int j = 0; j < 4; j++)
            {
                Vector3 v = toLocal.MultiplyPoint3x4(m_Corners[j]);
                vMin = Vector3.Min(v, vMin);
                vMax = Vector3.Max(v, vMax);
            }

            var bounds = new Bounds(vMin, Vector3.zero);
            bounds.Encapsulate(vMax);
            return bounds;
        }

        private Bounds GetBounds4Item(int index)
        {
            if (m_Content == null)
                return new Bounds();

            var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var toLocal = viewRect.worldToLocalMatrix;
            int offset = index - _itemStartIndex;
            if (offset < 0 || offset >= _activeChildren.Count)
                return new Bounds();
            var rt = _activeChildren[offset].transform as RectTransform;
            //var rt = m_Content.GetChild(offset) as RectTransform;
            if (rt == null)
                return new Bounds();
            rt.GetWorldCorners(m_Corners);
            for (int j = 0; j < 4; j++)
            {
                Vector3 v = toLocal.MultiplyPoint3x4(m_Corners[j]);
                vMin = Vector3.Min(v, vMin);
                vMax = Vector3.Max(v, vMax);
            }

            var bounds = new Bounds(vMin, Vector3.zero);
            bounds.Encapsulate(vMax);
            return bounds;
        }

        private Vector2 CalculateOffset(Vector2 delta)
        {
            Vector2 offset = Vector2.zero;
            if (m_MovementType == MovementType.Unrestricted)
                return offset;
            if (m_MovementType == MovementType.Clamped)
            {
                if (GetDimension(delta) < 0 && _itemStartIndex > 0)
                    return offset;
                if (GetDimension(delta) > 0 && _itemEndIndex < _allData.Count)
                    return offset;
            }

            Vector2 min = m_ContentBounds.min;
            Vector2 max = m_ContentBounds.max;

            if (m_Horizontal)
            {
                min.x += delta.x;
                max.x += delta.x;
                if (min.x > m_ViewBounds.min.x)
                    offset.x = m_ViewBounds.min.x - min.x;
                else if (max.x < m_ViewBounds.max.x)
                    offset.x = m_ViewBounds.max.x - max.x;
            }

            if (m_Vertical)
            {
                min.y += delta.y;
                max.y += delta.y;
                if (max.y < m_ViewBounds.max.y)
                    offset.y = m_ViewBounds.max.y - max.y;
                else if (min.y > m_ViewBounds.min.y)
                    offset.y = m_ViewBounds.min.y - min.y;
            }

            return offset;
        }

        protected void SetDirty()
        {
            if (!IsActive())
                return;

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected void SetDirtyCaching()
        {
            if (!IsActive())
                return;

            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            SetDirtyCaching();
        }
#endif
        protected virtual void LateUpdate()
        {
            if (!m_Content)
                return;

            EnsureLayoutHasRebuilt();
            UpdateScrollbarVisibility();
            UpdateBounds();
            float deltaTime = Time.unscaledDeltaTime;
            Vector2 offset = CalculateOffset(Vector2.zero);

            // Optimization: drop when vel/offset are small
            if (offset.sqrMagnitude < 0.01f)
                offset = Vector2.zero;
            if (m_Velocity.sqrMagnitude < 0.01f)
                m_Velocity = Vector2.zero;

            if (!m_Dragging && (offset != Vector2.zero || m_Velocity != Vector2.zero))
            {
                Vector2 position = m_Content.anchoredPosition;
                for (int axis = 0; axis < 2; axis++)
                {
                    // Apply spring physics if movement is elastic and content has an offset from the view.
                    if (m_MovementType == MovementType.Elastic && offset[axis] != 0)
                    {
                        float speed = m_Velocity[axis];
                        position[axis] = Mathf.SmoothDamp(m_Content.anchoredPosition[axis], m_Content.anchoredPosition[axis] + offset[axis], ref speed, m_Elasticity, Mathf.Infinity, deltaTime);
                        m_Velocity[axis] = speed;
                    }
                    // Else move content according to velocity with deceleration applied.
                    else if (m_Inertia)
                    {
                        m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                        if (Mathf.Abs(m_Velocity[axis]) < 1)
                            m_Velocity[axis] = 0;
                        position[axis] += m_Velocity[axis] * deltaTime;
                    }
                    // If we have neither elaticity or friction, there shouldn't be any velocity.
                    else
                    {
                        m_Velocity[axis] = 0;
                    }
                }

                //Debug.Log("Offset " + offset.ToPrettyString() + " vel " + m_Velocity.ToPrettyString());
                if (m_Velocity != Vector2.zero)
                {
                    if (m_MovementType == MovementType.Clamped)
                    {
                        offset = CalculateOffset(position - m_Content.anchoredPosition);
                        position += offset;
                    }

                    SetContentAnchoredPosition(position);
                }
            }

            if (m_Dragging && m_Inertia)
            {
                Vector3 newVelocity = (m_Content.anchoredPosition - m_PrevPosition) / deltaTime;
                m_Velocity = Vector3.Lerp(m_Velocity, newVelocity, deltaTime * 10);
            }

            if (_didAddOrRemoveThisFrame
                || m_ViewBounds != m_PrevViewBounds
                || m_ContentBounds != m_PrevContentBounds
                || m_Content.anchoredPosition != m_PrevPosition)
            {
                UpdateScrollbars(offset);
                m_OnValueChanged.Invoke(normalizedPosition);
                UpdatePrevData();
                _didAddOrRemoveThisFrame = false;
            }
        }
    }
}