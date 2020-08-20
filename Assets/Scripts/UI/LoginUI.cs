using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    public Transform RoomListContainer;
    public TMP_InputField TitleField;
    public GameObject RoomButtonPrefab;

    private List<string> CurrentRooms = new List<string>();
    private Dictionary<string, Button> RoomName2Button = new Dictionary<string, Button>();

    void Start()
    {
        //PhotonConnection.OnRecvRoomList += OnRoomListUpdate;
    }

    /*
    void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        List<string> observedRooms = new List<string>(CurrentRooms);

        foreach(var room in roomList)
        {
            if (observedRooms.Contains(room.Name))
            {
                observedRooms.Remove(room.Name);
            }
            else
            {
                Debug.Log("Adding " + room.Name);
                var obj = GameObject.Instantiate(RoomButtonPrefab, RoomListContainer);
                Button button = obj.GetComponent<Button>();
                button.onClick.AddListener(() => { OnJoinRoomButtonClicked(room.Name); });
                RoomName2Button.Add(room.Name, button);
                TMP_Text text = obj.GetComponentInChildren<TextMeshProUGUI>();
                text.SetText(room.Name);
            }
            Debug.Log(room.Name);
        }

        // Remove all rooms that are no longer present
        foreach(var oldRoom in observedRooms)
        {
            Debug.Log("Removing room" + oldRoom);
            CurrentRooms.Remove(oldRoom);
            Button button = RoomName2Button[oldRoom];
            RoomName2Button.Remove(oldRoom);
            Destroy(button);
        }

    }
    */

    public void OnCreateRoomButtonClicked()
    {
        Orchestrator.Instance.CreateRoom(TitleField.text, null);
    }
    public void OnJoinRoomButtonClicked(string gameID)
    {
        Orchestrator.Instance.JoinRoom(gameID);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
