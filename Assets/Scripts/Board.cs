using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Board : GenericSingleton<Board>
{
    public float Width;
    public float Height;
    public bool Visible;
    public GameObject BoardModel;
    public Material BoardMaterial;
    public Collider MainCollider;
    
    /// <summary>
    /// We turn the board off when the width/height is 0
    /// </summary>
    private bool _isBoardOn = true;

    protected override void Awake()
    {
        base.Awake();
        Vector3 scale = transform.localScale;
        Width = scale.x;
        Height = scale.y;
        Visible = true;

        // Prevent the file system from being dirtied in editor
#if UNITY_EDITOR
        BoardMaterial = BoardModel.GetComponent<MeshRenderer>().material;
#endif
    }
    public void OnGameStateLoaded(DRGameState gameState)
    {
        OnLocalChangeToBoardSize(gameState.Width, gameState.Height, false);
        OnLocalChangeToBoardVisibility(gameState.BoardVisible, false);
        WorldSettingsViewWorldPanel.Instance.OnNetworkChangeBoardVisibility(Visible);
        WorldSettingsViewWorldPanel.Instance.OnNetworkChangeBoardSize(Width, Height);
    }
    private void UpdateBoardScale()
    {
        BoardMaterial.mainTextureScale = new Vector2(Width, Height);
        // For 0 in either dimension, just disable to prevent weird physics
        if (Width == 0 || Height == 0)
        {
            gameObject.SetActive(false);
            _isBoardOn = false;
        }
        else
        {
            transform.localScale = new Vector3(Width, Height, 0.5f);
            if (!_isBoardOn)
            {
                gameObject.SetActive(true);
                _isBoardOn = true;
            }
        }
    }
    public void OnLocalChangeToBoardSize(float width, float height, bool network=true)
    {
        if (Width == width && Height == height)
            return;
        Width = width;
        Height = height;
        UpdateBoardScale();

        if (!network)
            return;

        using(DarkRiftWriter writer = DarkRiftWriter.Create(8))
        {
            writer.Write(width);
            writer.Write(height);
            using (Message msg = Message.Create(ServerTags.BoardSizeChange, writer))
                DarkRiftConnection.Instance.SendReliableMessage(msg);
        }
    }
    public void OnNetworkChangeBoardSize(float width, float height)
    {
        Width = width;
        Height = height;
        UpdateBoardScale();
        WorldSettingsViewWorldPanel.Instance.OnNetworkChangeBoardSize(Width, Height);
    }
    public void OnLocalChangeToBoardVisibility(bool visible, bool network=true)
    {
        if (Visible == visible)
            return;
        Visible = visible;
        BoardModel.SetActive(visible);
        Debug.Log("Local change board visibility " + Visible);

        if (!network)
            return;

        using(DarkRiftWriter writer = DarkRiftWriter.Create(1))
        {
            writer.Write(visible);
            using (Message msg = Message.Create(ServerTags.BoardVisiblityChange, writer))
                DarkRiftConnection.Instance.SendReliableMessage(msg);
        }
    }
    public void OnNetworkChangeBoardVisibility(bool visible)
    {
        Visible = visible;
        BoardModel.SetActive(visible);
        Debug.Log("net change board visibility " + Visible);
        WorldSettingsViewWorldPanel.Instance.OnNetworkChangeBoardVisibility(Visible);
    }
}
