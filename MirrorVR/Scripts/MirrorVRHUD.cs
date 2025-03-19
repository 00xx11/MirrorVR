using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using EpicTransport;
using Epic.OnlineServices.Lobby;
using Attribute = Epic.OnlineServices.Lobby.Attribute;
using UnityEngine.UI;


namespace Mirror.VR
{
    [RequireComponent(typeof(EOSLobby))]
    public class MirrorVRHUD : MonoBehaviour
    {
        public EOSLobby eosLobby;

        [Header("Network Manager")]
        public NetworkManager manager;

        [Header("Lobby Settings")]
        public string lobbyName = "Mirror VR Lobby";
        public float FPS;
        public string playerID = "";
        public string lobbyID = "";

        private string connectionState = "Not Logged In";
        private bool _showLobbyList = false;
        private bool _showPlayerList = false;

        private List<LobbyDetails> _foundLobbies = new List<LobbyDetails>();
        private List<Attribute> _lobbyData = new List<Attribute>();

        public const string LobbyNameKey = "LobbyName";
        public const string HostNameKey = "HostName";

        private void Awake()
        {
            eosLobby = GetComponent<EOSLobby>();

        }
        private void Start()
        {
#if UNITY_EDITOR
            StartCoroutine(FramesPerSecond());
            InvokeRepeating("Tick", 1f, 1f);
#endif
        }


#if UNITY_EDITOR
        private IEnumerator FramesPerSecond()
        {
            while (true)
            {
                float fps = (int)(1f / Time.deltaTime);
                FPS = fps;

                yield return new WaitForSeconds(0.2f);
            }
        }
#endif

#if UNITY_EDITOR
        private void Tick()//1sec tick
        {
            if (EOSSDKComponent.LocalUserProductId != null)
            {
                connectionState = "Logged In";
            }

            if (eosLobby.ConnectedToLobby)
            {
                LobbyDetailsCopyInfoOptions copyInfoOptions = new LobbyDetailsCopyInfoOptions { };
                LobbyDetailsInfo? detailsinfo;
                eosLobby.ConnectedLobbyDetails.CopyInfo(ref copyInfoOptions, out detailsinfo);
                lobbyID = detailsinfo.Value.LobbyId;
            }
            else
            {
                lobbyID = string.Empty;
            }
        }
#endif

        public void CreateLobbyy(string LobbyName, int MaxPlayers)
        {
            eosLobby.CreateLobby((uint)MaxPlayers, LobbyPermissionLevel.Publicadvertised, false,
                    new AttributeData[]
                    {
                    new AttributeData
                    {
                        Key = LobbyNameKey, Value = LobbyName
                    },
                    new AttributeData
                    {
                        Key = HostNameKey, Value = EOSSDKComponent.DisplayName
                    }

                    });
        }



        private void OnEnable()
        {
            eosLobby.CreateLobbySucceeded += OnCreateLobbySuccess;
            eosLobby.JoinLobbySucceeded += OnJoinLobbySuccess;
            eosLobby.FindLobbiesSucceeded += OnFindLobbiesSuccess;
            eosLobby.LeaveLobbySucceeded += OnLeaveLobbySuccess;
            eosLobby.CreateLobbyFailed += CreateLobbyFailed;
        }

        private void CreateLobbyFailed(string errorMessage)
        {
            Debug.LogError("Failed to create lobby: " + errorMessage);
        }

        private void OnDisable()
        {
            eosLobby.CreateLobbySucceeded -= OnCreateLobbySuccess;
            eosLobby.JoinLobbySucceeded -= OnJoinLobbySuccess;
            eosLobby.FindLobbiesSucceeded -= OnFindLobbiesSuccess;
            eosLobby.LeaveLobbySucceeded -= OnLeaveLobbySuccess;
        }


        private void OnCreateLobbySuccess(List<Attribute> attributes)
        {
            _lobbyData = attributes;
            _showPlayerList = true;
            _showLobbyList = false;

            manager.StartHost();
        }

        private void OnJoinLobbySuccess(List<Attribute> attributes)
        {
            _lobbyData = attributes;
            _showPlayerList = true;
            _showLobbyList = false;

            Attribute hostAddressAttribute = attributes.Find((x) => x.Data.HasValue && x.Data.Value.Key == EOSLobby.hostAddressKey);
            if (!hostAddressAttribute.Data.HasValue)
            {
                Debug.LogError("Host address not found in lobby attributes. Cannot connect to host.");
                return;
            }

            manager.networkAddress = hostAddressAttribute.Data.Value.Value.AsUtf8;
            manager.StartClient();


        }




        //callback for FindLobbiesSucceeded
        private void OnFindLobbiesSuccess(List<LobbyDetails> lobbiesFound)
        {
            _foundLobbies = lobbiesFound;
            _showPlayerList = false;
            _showLobbyList = true;
        }

        //when the lobby was left successfully, stop the host/client
        private void OnLeaveLobbySuccess()
        {
            manager.StopHost();
            manager.StopClient();
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            // Debug.LogError("OnGUI");
            //if the component is not initialized then dont continue
            //if (!EOSSDKComponent.Initialized)
            //{
            //    return;
            //}

            if (EOSSDKComponent.Initialized)
                playerID = EOSSDKComponent.LocalUserProductId.ToString();

            //start UI
            GUILayout.BeginHorizontal();

            //draw side buttons
            DrawMenuButtons();

            //draw scroll view
            GUILayout.BeginScrollView(Vector2.zero, GUILayout.MaxHeight(400));
            GUILayout.Label("State: " + connectionState);
            GUILayout.Label("FPS: " + FPS);

            GUILayout.Label("PlayerProductID: " + playerID);

            GUILayout.Label("LobbyID: " + lobbyID);

            //runs when we want to show the lobby list
            if (_showLobbyList && !_showPlayerList)
            {
                DrawLobbyList();
            }
            //runs when we want to show the player list and we are connected to a lobby
            else if (!_showLobbyList && _showPlayerList && eosLobby.ConnectedToLobby)
            {
                DrawLobbyMenu();
            }

            GUILayout.EndScrollView();

            GUILayout.EndHorizontal();
        }

        private void DrawMenuButtons()
        {
            //start button column
            GUILayout.BeginVertical();

            //decide if we should enable the create and find lobby buttons
            //prevents user from creating or searching for lobbies when in a lobby
            GUI.enabled = !eosLobby.ConnectedToLobby;

            #region Draw Create Lobby Button

            GUILayout.BeginHorizontal();

            //create lobby button
            GUI.enabled = MirrorVRManager.AllowedToDoLobbyStuff;
            if (GUILayout.Button("Create lobby"))
            {
                //CreateLobbyy(lobbyName, Mirror.VR.MirrorVRManager.instance.roomLimit);
                MirrorVRManager.CreateLobby(lobbyName);
            }

            GUI.enabled = MirrorVRManager.AllowedToDoLobbyStuff;
            if (GUILayout.Button("Create or join lobby"))
            {
                MirrorVRManager.JoinOrCreateLobby(lobbyName);
            }

            GUI.enabled = MirrorVRManager.AllowedToDoLobbyStuff;
            if (GUILayout.Button("Join random lobby"))
            {
                MirrorVRManager.JoinRandomLobby();
            }
            GUI.enabled = true;

            lobbyName = GUILayout.TextField(lobbyName, 40, GUILayout.Width(200));

            GUILayout.EndHorizontal();

            #endregion

            //find lobby button
            GUI.enabled = MirrorVRManager.AllowedToDoLobbyStuff;
            if (GUILayout.Button("Find Lobbies"))
            {
                eosLobby.FindLobbies();
            }
            GUI.enabled = true;


            //decide if we should enable the leave lobby button
            //only enabled when the user is connected to a lobby
            GUI.enabled = eosLobby.ConnectedToLobby;

            if (GUILayout.Button("Leave Lobby"))
            {
                MirrorVRManager.Disconnect();
            }

            GUI.enabled = true;

            GUILayout.EndVertical();
        }

        private void DrawLobbyList()
        {
            //draw labels
            GUILayout.BeginHorizontal();
            GUILayout.Label("Lobby Name", GUILayout.Width(220));
            GUILayout.Label("Player Count");
            GUILayout.EndHorizontal();

            //draw lobbies
            foreach (LobbyDetails lobby in _foundLobbies)
            {
                //get lobby name
                Attribute? lobbyNameAttribute = new Attribute();
                LobbyDetailsCopyAttributeByKeyOptions copyOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyNameKey };
                lobby.CopyAttributeByKey(ref copyOptions, out lobbyNameAttribute);

                //draw the lobby result
                GUILayout.BeginHorizontal(GUILayout.Width(400), GUILayout.MaxWidth(400));

                if (lobbyNameAttribute.HasValue && lobbyNameAttribute.Value.Data.HasValue)
                {
                    var data = lobbyNameAttribute.Value.Data.Value;
                    //draw lobby name
                    GUILayout.Label(data.Value.AsUtf8.Length > 30 ? data.Value.AsUtf8.ToString().Substring(0, 27).Trim() + "..." : data.Value.AsUtf8, GUILayout.Width(175));
                    GUILayout.Space(75);
                }
                //draw player count
                LobbyDetailsGetMemberCountOptions memberCountOptions = new LobbyDetailsGetMemberCountOptions { };
                GUILayout.Label(lobby.GetMemberCount(ref memberCountOptions).ToString());
                GUILayout.Space(75);

                //draw join button
                if (GUILayout.Button("Join", GUILayout.ExpandWidth(false)))
                {
                    eosLobby.JoinLobby(lobby, new[] { LobbyNameKey });
                }

                GUILayout.EndHorizontal();
            }
        }

        private void DrawLobbyMenu()
        {
            //draws the lobby name
            var lobbyNameAttribute = _lobbyData.Find((x) => x.Data.HasValue && x.Data.Value.Key == LobbyNameKey);
            if (!lobbyNameAttribute.Data.HasValue)
            {
                return;
            }
            GUILayout.Label("Name: " + lobbyNameAttribute.Data.Value.Value.AsUtf8);

            //draws players
            LobbyDetailsGetMemberCountOptions memberCountOptions = new LobbyDetailsGetMemberCountOptions();
            var playerCount = eosLobby.ConnectedLobbyDetails.GetMemberCount(ref memberCountOptions);
            for (int i = 0; i < playerCount; i++)
            {
                GUILayout.Label("Player " + i);
            }
        }
#endif
    }
}
