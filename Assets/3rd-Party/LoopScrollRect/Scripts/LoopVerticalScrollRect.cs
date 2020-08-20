using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/Loop Vertical Scroll Rect", 51)]
    [DisallowMultipleComponent]
    public class LoopVerticalScrollRect : LoopScrollRect
    {
        public bool ConstantSize;
        public bool UseLayoutSize;
        [SerializeField]
        private float _cachedSize = -1f;

        protected override void Awake()
        {
            base.Awake();
            directionSign = -1;

#if UNITY_EDITOR
            GridLayoutGroup layout = content.GetComponent<GridLayoutGroup>();
            if (layout != null && layout.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
                Debug.LogError("[LoopHorizontalScrollRect] unsupported GridLayoutGroup constraint");
#endif
        }
        protected override float GetSize(RectTransform item)
        {
            if (ConstantSize && _cachedSize > 0f)
                return _cachedSize;

            float size = contentSpacing;
            if (m_GridLayout != null)
            {
                size += m_GridLayout.cellSize.y;
            }
            else
            {
                // For some reason, this fucks up for my kind of objects
                if(UseLayoutSize)
                    size += LayoutUtility.GetPreferredHeight(item);
                else
                    size += item.sizeDelta.y;
                //Debug.Log("Size of " + item.gameObject.name + " is " + size);
            }
            _cachedSize = size;
            return size;
        }
        protected override float GetDimension(Vector2 vector)
        {
            return vector.y;
        }
        protected override Vector2 GetVector(float value)
        {
            return new Vector2(0, value);
        }
        protected override bool UpdateItems(Bounds viewBounds, Bounds contentBounds)
        {
            bool changed = false;
            //Debug.Log("Update items");

            if(_cachedSize >= 0)
            {
                int numAdded = Mathf.CeilToInt((contentBounds.min.y - viewBounds.min.y) / _cachedSize);
                //Debug.Log("Will add " + numAdded + " count " + Count + " start " + _itemStartIndex + " end " + _itemEndIndex);

                // HACK TODO
                // When the user is scrolling, the bounds can change by a lot
                // previously, we would keep adding elements until we get to
                // the right spot, and then delete to meet the bounds. This
                // worked, but scrolling would mean that a lot of objects had
                // to be instantiated. To get around this, we just calculate if 
                // we'll need to add more than 4 elements, and if so we just refill
                // ideally we'd do something more clever and not do a whole refill
                // when we could instead just remove and then add elements

                int absNumAdded = Math.Abs(numAdded);
                if (absNumAdded > 4 && Count > absNumAdded)
                {
                    int newStart = _itemStartIndex + numAdded;
                    if (newStart <= 0)
                        newStart = 0;
                    else if (newStart >= Count - ActiveCount + 1)
                        newStart = Count - ActiveCount + 1;
                    //Debug.Log("Offset " + newStart + " from " + _itemStartIndex);
                    RefillCells(newStart, true);
                    return true;
                }
            }

            int endAdded = 0;
            float totalSize = 0;
            while(viewBounds.min.y < contentBounds.min.y - totalSize)
            {
                float size = NewItemAtEnd();
                if (size <= 0)
                    break;
                totalSize += size;
                endAdded++;
                changed = true;
            }

            int startAdded = 0;
            totalSize = 0;
            while (viewBounds.max.y > contentBounds.max.y + totalSize)
            {
                float size = NewItemAtStart();
                if (size <= 0)
                    break;
                totalSize += size;
                startAdded++;
                changed = true;
            }

            int endDeleted = 0;
            totalSize = 0;
            while (viewBounds.min.y > contentBounds.min.y + threshold + totalSize)
            {
                //Debug.Log("will try to delete at end");
                float size = DeleteItemAtEnd();
                if (size <= 0)
                    break;
                totalSize += size;
                endDeleted++;
                changed = true;
            }

            int startDeleted = 0;
            totalSize = 0;
            while (viewBounds.max.y < contentBounds.max.y - threshold - totalSize)
            {
                //Debug.Log("will try to delete at start");
                float size = DeleteItemAtStart();
                if (size <= 0)
                    break;
                totalSize += size;
                startDeleted++;
                changed = true;
            }

            //if (changed)
                //Debug.Log("#" + Time.frameCount + " eAdd " + endAdded + " sAdd " + startAdded + " sDel " + startDeleted + " eDel " + endDeleted);
                //{
                //Debug.Log("Bounds v: " + viewBounds.min.y + "->" + viewBounds.max.y + " c: " + contentBounds.min.y + "->" + contentBounds.max.y);
            //}

            return changed;
        }
    }
}