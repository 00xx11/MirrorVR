using System.Collections.Generic;
using UnityEngine;

using TMPro;

using EpicTransport;
using Epic.OnlineServices;
using Epic.OnlineServices.Reports;
using UnityEditor;
using Mirror.VR;

namespace Mirror.VR
{
    public class MirrorVRPlayer : NetworkBehaviour
    {
        [Header("Object References")]
        public Transform headRef;
        public Transform bodyRef;
        public Transform leftRef;
        public Transform rightRef;
        [Space]
        public TMP_Text nameText;


        [Header("Player Info"), Space(15)]
        [SyncVar(hook = nameof(OnPlayerNameChanged))] internal string playerName = string.Empty;

        //not done yet
        //[Header("Cosmetics")]
        //public CosmeticSlot[] cosmetics = new CosmeticSlot[0];

        //[Header("Color")]
        [SyncVar, HideInInspector] public Color currentPlayerColor;
        [Space(15)]
        public List<Renderer> TargetMesh = new List<Renderer>();

        public static MirrorVRPlayer LocalInstance;
        /// <summary>
        /// The ProductUserId of this client
        /// </summary>
        public ProductUserId ProductUserId;
        /// <summary>
        /// The identifier of this client in the current lobby
        /// </summary>
        [HideInInspector] public int PlayerLobbyId;

        private void Awake()
        {
            DontDestroyOnLoad(this);

            if (LocalInstance == null && isLocalPlayer)//
                LocalInstance = this;
            InvokeRepeating(nameof(Tick), 0.2f, 0.2f);
        }

        private void Start()
        {
            gameObject.name = $"Player ({EOSSDKComponent.DisplayName})"; //TODO: this is temporary, here until we get a name system.
        }

        public void ReportPlayer(PlayerReportsCategory category = PlayerReportsCategory.Other, string message = "None")
        {
            ReportsInterface ri = EOSSDKComponent.GetReportsInterface();

            SendPlayerBehaviorReportOptions options = new SendPlayerBehaviorReportOptions()
            {
                ReportedUserId = ProductUserId,
                ReporterUserId = EOSSDKComponent.LocalUserProductId,
                Category = category,
                Message = message,
            };

            ri.SendPlayerBehaviorReport(ref options, null, OnSendReport);
        }

        private void OnSendReport(ref SendPlayerBehaviorReportCompleteCallbackInfo data)
        {
            if (data.ResultCode == Result.Success)
            {
                Debug.Log("Succesfully reported player.");
            }
            else
            {
                Debug.LogError("Failed to report player: " + data.ResultCode);
            }
        }

        public void SetPlayerColor(Color color)
        {
            if (isLocalPlayer)
            {
                currentPlayerColor = color;
            }
        }

        private void Tick()
        {
            if (LocalInstance == null && isLocalPlayer)
                LocalInstance = this;
            if (isLocalPlayer)
            {
                playerName = EOSSDKComponent.DisplayName;
            }
            nameText.text = playerName;
            if (TargetMesh.Count > 0)
            {
                for (int i = 0; i < TargetMesh.Count; i++)
                {
                    var tar = TargetMesh[i];
                    tar.material.color = currentPlayerColor;
                }
            }
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                ProductUserId = EOSSDKComponent.LocalUserProductId;

                while (MirrorVRManager.PlayerList == null) { return; }

                for (int i = 0; i < MirrorVRManager.PlayerList.Length; i++)
                {
                    if (MirrorVRManager.PlayerList[i] == LocalInstance)
                    {
                        PlayerLobbyId = i;
                    }
                }

                headRef.position = MirrorVRManager.instance.head.transform.position;
                headRef.rotation = MirrorVRManager.instance.head.transform.rotation;

                bodyRef.position = new Vector3(headRef.position.x, headRef.position.y + -0.35f, headRef.position.z);
                bodyRef.rotation = new Quaternion(0, headRef.rotation.y, 0, headRef.rotation.w);

                leftRef.position = MirrorVRManager.instance.leftHand.transform.position;
                leftRef.rotation = MirrorVRManager.instance.leftHand.transform.rotation;

                rightRef.position = MirrorVRManager.instance.rightHand.transform.position;
                rightRef.rotation = MirrorVRManager.instance.rightHand.transform.rotation;


            }
        }

        public void OnPlayerNameChanged(string _old, string _new) => nameText.text = _new;


        public void EquipCosmetic(string CosmeticId, MirrorVRCosmeticManager.CosmeticType type)
        {
            /* rework to add custom cosmetic types
            Transform targetBase;
            switch (type)
            {
                case MirrorVRCosmeticManager.CosmeticType.Head:
                    targetBase = HeadCosmetics;break;
                case MirrorVRCosmeticManager.CosmeticType.Body:
                    targetBase = BodyCosmetics; break;
                case MirrorVRCosmeticManager.CosmeticType.LeftHand:
                    targetBase = LeftHandCosmetics;break;
                case MirrorVRCosmeticManager.CosmeticType.RightHand:
                    targetBase = RightHandCosmetics;break;
                case MirrorVRCosmeticManager.CosmeticType.Face:
                    targetBase = FaceCosmetics; break;
            }*/
        }

        /*
        public void RequestUpdatePlayerValues()
        {
            CmdUpdatePlayerValues(playerName, currentPlayerColor);
        }

        [Command]
        private void CmdUpdatePlayerValues(string playername, Color playercolor)
        {

        }

        [ClientRpc]
        private void UpdateCurrentPlayerValues()
        {

        }*/
    }

    [System.Serializable]
    public class CosmeticSlot
    {
        public string name;
        public Transform slotObject;
    }



#if UNITY_EDITOR
    [CustomEditor(typeof(MirrorVRPlayer))]
    public class MirrorVRPlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            MirrorVRPlayer player = (MirrorVRPlayer)target;

            base.OnInspectorGUI();

            /*if (GUILayout.Button("Report this player"))
            {
                player.ReportPlayer(PlayerReportsCategory.Other, "Report from editor");
            }*/
        }
    }
#endif
}