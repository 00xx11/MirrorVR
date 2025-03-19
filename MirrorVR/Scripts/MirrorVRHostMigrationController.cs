using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using EpicTransport;

using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;

namespace Mirror.VR
{
    [RequireComponent(typeof(NetworkIdentity))]
    [DisallowMultipleComponent]
    public class MirrorVRHostMigrationController : NetworkBehaviour
    {
        public static MirrorVRHostMigrationController Instance;

        [Header("Transfer")]
        //public GameObject[] ObjectsToTransfer;
        public TransferData LocalBackup = new TransferData();

        [Header("Settings")]
        public bool FetchDuringRuntime = true;

        private bool iAmNextHost;

        public string myLobbyId;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogError("You can only have one " + nameof(MirrorVRHostMigrationController) + " in your scene");
            }

            StartCoroutine(OnInstanceSet());
        }

        #region Updating Backup
        private void Update()
        {
            if (MirrorVRManager.ConnectedToLobby && MirrorVRManager.instance.hostMigration) //&& MirrorVRPlayer.LocalInstance.PlayerLobbyId < 3)  //We want only the 2 oldest clients besides the host to update the data to save performance.
            {
                FetchTransferData();
            }
            else if(LocalBackup.GameObjects != null || LocalBackup.OtherPlayers != null)//We want to clear the data otherwise, if there is any.
            {
                LocalBackup = new TransferData();
            }
        }

        
        public void FetchTransferData()
        {
            try
            {
                LocalBackup = new TransferData();

                //added to counter FindObjectsOfType being obsolete in Unity 2023.1+
#if UNITY_2023_1_OR_NEWER
                NetworkIdentity[] found = FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
#else
                NetworkIdentity[] found = FindObjectsOfType<NetworkIdentity>();
#endif

                foreach (NetworkIdentity obj in found) //We don't want to add the host migration controller
                {
                    if (obj.gameObject != gameObject && obj.gameObject.GetComponent<HMSkip>() == null) //We don't want the HM Controller and object that are marked with HMSKip
                    {
                        LocalBackup.GameObjects.Add(obj.gameObject);
                    }

                }

                while (MirrorVRManager.PlayerList == null) { return; }

                for (int i = 0; i < MirrorVRManager.PlayerList.Length; i++)
                {
                    if (MirrorVRManager.PlayerList[i].gameObject != null)
                    {
                        LocalBackup.OtherPlayers.Add(MirrorVRManager.PlayerList[i].ProductUserId, MirrorVRManager.PlayerList[i].gameObject);
                    }
                }
            }
            catch
            {

            }
            
        }
#endregion

        private IEnumerator OnInstanceSet()
        {
            yield return new WaitUntil(() => EOSSDKComponent.Instance != null);
            MirrorVRManager.instance.eosLobby.CreateLobbySucceeded += OnCreateLobby;
        }

        private void OnCreateLobby(List<Attribute> attributes)
        {
            if (iAmNextHost)//to check if the lobby is a migrated lobby
            {
                ClearUp();
            }
        }

        private void ClearUp()
        {
            iAmNextHost = false;
        }

        
        //now unused
        [Server]
        [ClientRpc(channel =Channels.Unreliable)]
        public void HostMigrate(ProductUserId TargetPlayerPUID) // We migrate to the target player
        {
            Debug.Log(TargetPlayerPUID);
            Debug.Log("Tried to promote " + TargetPlayerPUID);
            if (TargetPlayerPUID == EOSSDKComponent.LocalUserProductId)
            {
                Debug.Log("Client is next host");
                iAmNextHost = true;
                DoStuffAsNewHost();
            }

        }


        private void DoStuffAsNewHost()
        {
            Debug.Log("Saving back-up");
            for (int i = 0;i < LocalBackup.GameObjects.Count; i++)
            {
                DontDestroyOnLoad(LocalBackup.GameObjects[i]);//we want to save the objects
            }
            Debug.Log("Forcing clients into new lobby");
            CmdForceClientsIntoNewLobby(MirrorVRManager.instance.CurrentLobby);
            Debug.Log("Creating new lobby");
            MirrorVRManager.CreateLobby(MirrorVRManager.instance.CurrentLobby); //This will cause a room duplicate, which we want because we don't want to change the room code.
        }

        #region force join
        [Command(channel = Channels.Unreliable)]
        private void CmdForceClientsIntoNewLobby(string code)
        {
            RpcForceClientsIntoNewLobby(code);
            MirrorVRManager.instance.eosLobby.LeaveLobby();
        }

        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcForceClientsIntoNewLobby(string code)
        {
            if (!iAmNextHost)
            {
                MirrorVRManager.JoinLobby(code);
            }
        }
        #endregion

        // new expimental stuff
        public struct HostMigrationMessage : NetworkMessage
        {
            public string newHost;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (MirrorVRManager.instance.hostMigration)
            {
                NetworkServer.OnConnectedEvent += OnServerConnJoin;
                NetworkServer.OnDisconnectedEvent += OnServerConnLeave;

                myLobbyId = MirrorVRManager.instance.GetComponent<MirrorVRHUD>().lobbyID;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (MirrorVRManager.instance.hostMigration)
            {
                NetworkClient.RegisterHandler<HostMigrationMessage>(OnMessageGot);

                if (!isServer)
                {
                    Client.OnServerClosed += MirrorVRManager.instance.OnServerClosed;
                }
            }
        }

        private void OnServerConnJoin(NetworkConnectionToClient conn)
        {
            if (NetworkServer.connections.Count <= 1) return;
            StartCoroutine(SetServerConn(conn, true));
        }

        private void OnServerConnLeave(NetworkConnectionToClient conn)
        {
            if (NetworkServer.connections.Count <= 1) return;
            StartCoroutine(SetServerConn(conn, false));
        }

        private IEnumerator SetServerConn(NetworkConnectionToClient connn, bool joining)
        {
            yield return new WaitForSeconds(4);

            Debug.Log("Sending Message");
            HostMigrationMessage msg = new HostMigrationMessage()
            {
                newHost = MirrorVRManager.GetMemberByIndex(1).ToString(),
            };

            NetworkServer.SendToAll(msg, 1);

            Debug.Log(MirrorVRManager.GetMemberByIndex(1).ToString());
        }

        private void OnMessageGot(HostMigrationMessage msg)
        {
            Debug.Log($"HM Message Received: New Host - {msg.newHost}");
            MirrorVRManager.instance.nexthost = msg.newHost.ToString();
        }
    }

    [System.Serializable]
    public class TransferData
    {
        public List<GameObject> GameObjects = new List<GameObject>();
        public Dictionary<ProductUserId, GameObject> OtherPlayers = new Dictionary<ProductUserId, GameObject>();
    }
}

