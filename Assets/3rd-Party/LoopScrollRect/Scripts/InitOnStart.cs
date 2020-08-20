using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace SG
{
    [RequireComponent(typeof(UnityEngine.UI.LoopScrollRect))]
    [DisallowMultipleComponent]
    public class InitOnStart : MonoBehaviour
    {
        public int totalCount = 20;
        public int NumToDelete = 3;
        public string ObjectResourceName;

        private LoopScrollRect _scrollRect;
        void Start()
        {
            _scrollRect = GetComponent<LoopScrollRect>();
            var dataSource = new LoopScrollPrefabSource();
            dataSource.prefabName = ObjectResourceName;
            _scrollRect.Init(dataSource, LoopScrollSendIndexSource.Instance);
            for(int i = 0; i < totalCount; i++)
            {
                _scrollRect.AddItem((object)i, false);
            }

            _scrollRect.RefillCells();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
                _scrollRect.AddItem(totalCount++, true);
            if (Input.GetKeyDown(KeyCode.O))
                _scrollRect.RemoveItem(NumToDelete++, true);
        }
    }
}