using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(UnityEngine.UI.GridLayoutGroup))]
public class AdjustGridLayoutCellSize : MonoBehaviour
{
    public enum ExpandSetting { X, Y, };

    public ExpandSetting expandingSetting;
    GridLayoutGroup gridlayout;
    int maxConstraintCount = 0;
    RectTransform layoutRect;

    private void Awake()
    {
        gridlayout = GetComponent<GridLayoutGroup>();
    }

    // Start is called before the first frame update
    void Start()
    {
        UpdateCellSize();
    }

    private void OnRectTransformDimensionsChange()
    {
        Debug.Log("rect change");
        UpdateCellSize();
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateCellSize();
    }
#endif

    private void UpdateCellSize()
    {
        maxConstraintCount = gridlayout.constraintCount;
        layoutRect = gridlayout.gameObject.GetComponent<RectTransform>();

        if (expandingSetting == ExpandSetting.X)
        {
            float width = layoutRect.rect.width;
            width -= gridlayout.padding.left;
            width -= gridlayout.padding.right;
            float sizePerCell = width / maxConstraintCount;
            gridlayout.cellSize = new Vector2(sizePerCell, gridlayout.cellSize.y);
        }
        else if (expandingSetting == ExpandSetting.Y)
        {
            float height = layoutRect.rect.height;
            float sizePerCell = height / maxConstraintCount;
            height -= gridlayout.padding.top;
            height -= gridlayout.padding.bottom;
            gridlayout.cellSize = new Vector2(gridlayout.cellSize.x, sizePerCell);
        }
    }
}