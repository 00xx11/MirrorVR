using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using EpicTransport;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Sanctions;

using Mirror.VR.PasswordAttribute;

using Oculus.Platform;
using Oculus.Platform.Models;

namespace Mirror.VR
{
    [RequireComponent(typeof(NetworkManager))]
    [DisallowMultipleComponent]
    [HelpURL("https://github.com/MirrorVR/MirrorVR/wiki")]

    public class MirrorVRManager : MonoBehaviour
    {
        public static MirrorVRManager instance { get; private set; }

        [Header("Network Manager")]
        [Tooltip("The Network Manager attached to this GameObject.")] public NetworkManager networkManager;
        [SerializeField] internal EOSLobby eosLobby;

        [Header("Player Transforms")]
        [Tooltip("The player head, usually the Main Camera.")] 
        public Transform head;
        [Tooltip("The player's left hand, usually the Left Hand Controller.")]
        public Transform leftHand;
        [Tooltip("The player's right hand, usually the Right Hand Controller.")]
        public Transform rightHand;

        [Header("API Keys")]
        [SerializeField, PasswordField] private string productName;
        [SerializeField, PasswordField] private string productId;
        [SerializeField, PasswordField] private string sandboxId;
        [SerializeField, PasswordField] private string deploymentId;
        [SerializeField, PasswordField] private string clientId;
        [SerializeField, PasswordField] private string clientSecret;

        [Header("Player Data Storage")]
        [Tooltip("Used to encrypt player data storage")]
        [SerializeField, PasswordField] private string encryptionKey;

        [Header("Settings")]
        [Tooltip("If the user should connect to a room on start.")] public bool automaticallyJoinRoom = false;
        [Tooltip("The default limit of users in a room, the max you can do using EOS Lobbies is 64.")] public int roomLimit = 10;

        [Space]

        [Header("--BETA FEATURES--")]
        [Space]

        [Header("Voice Chat")]
        [Tooltip("Keep this enabled if you want to use our default voice chat service.")] public bool enableDefaultVoiceChat = true;
        [Space]

        [Header("Host Migration (NOT WORKING)")]
        [Tooltip("Enable this if you want to use the host migration feature. (When the original host disconnects, a new host is assigned instead of the whole lobby disconnecting.)")]
        [SerializeField] internal bool hostMigration = false;

        [Header("Testing")]
        [Tooltip("Use ONLY if you are not able to login by normal means.")] public bool useEpicLogin = false;


        internal string CurrentLobby;
        internal string CurrentLobbyId;

        public static bool ConnectedToLobby
        {
            get
            {
                return instance.eosLobby.ConnectedToLobby;
            }
        }
        public static MirrorVRPlayer[] PlayerList;

        #region LobbyCodeSystem
        public static bool AllowedToDoLobbyStuff = false;
        private const string LobbyCodeKey = "LobbyCode";
        private const string LobbyNameKey = "LobbyName";
        private const string HostNameKey = "HostName";
        private const string QueueKey = "Queue";
        #endregion

        private string oculususerid;
        private string oculususername;
        private string oculusnonce;

        private static int RoomLimit => instance.roomLimit;

        internal string nexthost;
        

        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
                throw new NotSupportedException("You already have another MirrorVRManager in the scene. You may only have one per scene.");


            if (roomLimit > 64)
                throw new ArgumentOutOfRangeException(nameof(roomLimit), "Your max room limit on MirrorVRManager is over 64 players. You can't have more than 64 players in a lobby. Please fix this value in your MirrorVRManager script.");

            if (new[] { productName, productId, sandboxId, deploymentId, clientId, clientSecret }.Any(string.IsNullOrEmpty))
                throw new NullReferenceException("One or more of your API Keys on MirrorVRManager are null. Please make sure you fill them out with the correct values.");

            //for unfinished stuff
            if (hostMigration) throw new NotImplementedException("Host Migration is not fully done yet. Please disable it in your MirrorVRManager script.");

            LoginWithOculus();

            StartCoroutine(FetchBans());
            StartCoroutine(OnLogin());

            InvokeRepeating(nameof(Tick), 0.2f, 0.2f);
        }

        public void OnApplicationQuit()
        {
            Disconnect();
        }

        public void OnDisable()
        {
            UnityEngine.Application.quitting -= OnQuit;
            Disconnect();
        }

        private void OnEnable()
        {
            instance.eosLobby.FindLobbiesSucceeded += FindSucces;
            UnityEngine.Application.quitting += OnQuit;
        }

        private IEnumerator OnLogin()
        {
            AllowedToDoLobbyStuff = false;
            yield return new WaitUntil(() => EOSSDKComponent.Initialized);
            FindOpenLobbies();
            yield return new WaitUntil(() => AllowedToDoLobbyStuff);

            if (automaticallyJoinRoom)
            {
                JoinRandomLobby();
            }
        }

        private IEnumerator FetchBans()
        {
            yield return new WaitUntil(() => EOSSDKComponent.Initialized);

            SanctionsInterface SInterface = EOSSDKComponent.GetSanctionsInterface();

            QueryActivePlayerSanctionsOptions ops = new QueryActivePlayerSanctionsOptions
            {
                LocalUserId = EOSSDKComponent.LocalUserProductId,
                TargetUserId = EOSSDKComponent.LocalUserProductId,
            };

            object returnedData = new object();

            SInterface.QueryActivePlayerSanctions(ref ops, returnedData, (ref Epic.OnlineServices.Sanctions.QueryActivePlayerSanctionsCallbackInfo bbb) =>
            {
                if (bbb.ResultCode == Result.Success)
                {
                    //Debug.Log("Retrieved active sanctions");
                    GetPlayerSanctionCountOptions o = new GetPlayerSanctionCountOptions
                    {
                        TargetUserId = EOSSDKComponent.LocalUserProductId,
                    };
                    if (SInterface.GetPlayerSanctionCount(ref o) > 0)
                    {
                        DestroyImmediate(EOSSDKComponent.Instance);
                        UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.FatalError);
                    }
                }
                if (bbb.ResultCode == Result.UserBanned)
                {
                    UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.FatalError);
                }
            });
        }

        private void Tick()
        {
            if (eosLobby.ConnectedToLobby)
            {
                CurrentLobby = GetLobbyCode();
#if UNITY_2023_1_OR_NEWER
                PlayerList = FindObjectsByType<MirrorVRPlayer>(FindObjectsSortMode.None);
#else
                PlayerList = FindObjectsOfType<MirrorVRPlayer>();
#endif

                LobbyDetailsCopyInfoOptions copyInfoOptions = new LobbyDetailsCopyInfoOptions { };
                LobbyDetailsInfo? detailsinfo;
                eosLobby.ConnectedLobbyDetails.CopyInfo(ref copyInfoOptions, out detailsinfo);
                CurrentLobbyId = detailsinfo.Value.LobbyId;
            }
        }

        private void LoginWithOculus()
        {
            if (UnityEngine.Application.platform == RuntimePlatform.OSXEditor || ShallUseEpic)
            {
                EOSSDKComponent.Instance.SetToEpicCreds();
            }
            else
            {
                Core.AsyncInitialize();
                Entitlements.IsUserEntitledToApplication().OnComplete(result =>
                {
                    if (!result.IsError)
                    {
                        Users.GetLoggedInUser().OnComplete(m =>
                        {
                            if (!m.IsError)
                            {
                                User userr = m.GetUser();
                                oculususerid = userr.ID.ToString();
                                oculususername = userr.OculusID;

                                Users.GetUserProof().OnComplete(r =>
                                {
                                    if (!r.IsError)
                                    {
                                        oculusnonce = r.Data.Value;

                                        string Name = PlayerPrefs.GetString("Username", oculususername);

                                        EOSSDKComponent.Instance.SetCredentials(productName, productId, sandboxId, deploymentId, clientId, clientSecret, oculususerid, oculusnonce, encryptionKey, Name);
                                        EOSSDKComponent.Initialize();
                                    }
                                    else
                                    {
                                        // quit app
                                    }
                                });
                            }
                        });
                    }
                });
            }
        }

        private bool ShallUseEpic => UnityEngine.Application.isEditor && useEpicLogin;

        /// <summary>
        /// Joins a lobby using the provided room code. Though it is strongly recommended to use <see cref="JoinOrCreateLobby(string)"/> instead.
        /// </summary>
        /// <param name="lobbycode">The room code entered.</param>
        public static void JoinLobby(string lobbycode)
        {
            if (instance.networkManager.isNetworkActive) Disconnect();

            LobbySearch search = new LobbySearch();
            List<LobbyDetails> foundlobbies = new List<LobbyDetails>();

            var createLobbySearchOptions = new CreateLobbySearchOptions { MaxResults = 1 };
            EOSSDKComponent.GetLobbyInterface().CreateLobbySearch(ref createLobbySearchOptions, out search);

            var ao = new LobbySearchSetParameterOptions() { Parameter = new AttributeData { Key = LobbyCodeKey, Value = lobbycode } };
            search.SetParameter(ref ao);

            var lobbySearchFindOptions = new LobbySearchFindOptions { LocalUserId = EOSSDKComponent.LocalUserProductId };
            search.Find(ref lobbySearchFindOptions, null, (ref LobbySearchFindCallbackInfo callback) =>
            {
                if (callback.ResultCode != Result.Success)
                {
                    Debug.LogError($"There was an error while joining lobby {lobbycode}. Error: {callback.ResultCode.ToString()}");
                    return;
                }

                var lobbySearchGetSearchResultCountOptions = new LobbySearchGetSearchResultCountOptions { };
                for (int i = 0; i < search.GetSearchResultCount(ref lobbySearchGetSearchResultCountOptions); i++)
                {
                    LobbyDetails lobbyInformation;
                    var options = new LobbySearchCopySearchResultByIndexOptions();
                    options.LobbyIndex = (uint)i;
                    search.CopySearchResultByIndex(ref options, out lobbyInformation);
                    foundlobbies.Add(lobbyInformation);
                }

                if (foundlobbies.Count > 0)
                {
                    instance.eosLobby.JoinLobby(foundlobbies[0]);
                }
                else
                {
                    Debug.LogError($"There was an error while joining lobby {lobbycode}. Error: Room does not exist.");
                }
            });
        }

        /// <summary>
        /// Creates a new room. Though it is strongly recommended to use <see cref="JoinOrCreateLobby(string)"/> instead.
        /// </summary>
        /// <param name="lobbycode">The room code entered.</param>
        /// <param name="ExtraAttributes">Optional attributes</param>
        public static void CreateLobby(string lobbycode, AttributeData[] ExtraAttributes = null)
        {
            AttributeData[] att =
            {
                new AttributeData() {Key = LobbyNameKey, Value = lobbycode },
                new AttributeData() {Key = HostNameKey, Value = EOSSDKComponent.DisplayName },
                new AttributeData() {Key = LobbyCodeKey, Value = lobbycode },
            };
            if (ExtraAttributes != null)
            {
                for (int i = 0; i < ExtraAttributes.Length; i++)
                {
                    att.Append(ExtraAttributes[i]);
                }
            }


            Debug.Log($"Creating Lobby: {lobbycode}");
            Disconnect();
            instance.eosLobby.CreateLobby((uint)RoomLimit, LobbyPermissionLevel.Publicadvertised, false, att);
        }

        /// <summary>
        /// A combination of <see cref="JoinLobby(string)"/> and <see cref="CreateLobby(string)"/> that will first check if that room exists already, and if it does join it, but if it doesn't, create a new room.
        /// </summary>
        /// <param name="lobbycode">The lobby code entered.</param>
        /// <param name="ExtraAttributes">Optional attributes, will only be added if lobby does not exist.</param>
        public static void JoinOrCreateLobby(string lobbycode, AttributeData[] ExtraAttributes = null)
        {
            Debug.Log($"Joining or creating lobby: {lobbycode}.");
            Disconnect();

            LobbySearch search = new LobbySearch();
            List<LobbyDetails> foundlobbies = new List<LobbyDetails>();

            var createLobbySearchOptions = new CreateLobbySearchOptions { MaxResults = 1 };
            EOSSDKComponent.GetLobbyInterface().CreateLobbySearch(ref createLobbySearchOptions, out search);

            var ao = new LobbySearchSetParameterOptions() { Parameter = new AttributeData { Key = LobbyCodeKey, Value = lobbycode } };
            search.SetParameter(ref ao);

            var lobbySearchFindOptions = new LobbySearchFindOptions { LocalUserId = EOSSDKComponent.LocalUserProductId };
            search.Find(ref lobbySearchFindOptions, null, (ref LobbySearchFindCallbackInfo callback) =>
            {
                if (callback.ResultCode != Result.Success)
                {
                    Debug.LogError($"There was an error while joining or creating lobby {lobbycode}. Error: {callback.ResultCode.ToString()}");
                    return;
                }

                var lobbySearchGetSearchResultCountOptions = new LobbySearchGetSearchResultCountOptions { };
                for (int i = 0; i < search.GetSearchResultCount(ref lobbySearchGetSearchResultCountOptions); i++)
                {
                    LobbyDetails lobbyInformation;
                    var options = new LobbySearchCopySearchResultByIndexOptions();
                    options.LobbyIndex = (uint)i;
                    search.CopySearchResultByIndex(ref options, out lobbyInformation);
                    foundlobbies.Add(lobbyInformation);
                }

                if (foundlobbies.Count > 0)
                {
                    instance.eosLobby.JoinLobby(foundlobbies[0]);
                }
                else
                {
                    AttributeData[] att =
                    {
                        new AttributeData() {Key = LobbyNameKey, Value = lobbycode },
                        new AttributeData() {Key = HostNameKey, Value = EOSSDKComponent.DisplayName },
                        new AttributeData() {Key = LobbyCodeKey, Value = lobbycode },
                    };
                    if (ExtraAttributes != null)
                    {
                        for (int i = 0; i < ExtraAttributes.Length; i++)
                        {
                            att.Append(ExtraAttributes[i]);
                        }
                    }
                    


                    instance.eosLobby.CreateLobby((uint)RoomLimit, LobbyPermissionLevel.Publicadvertised, false, att);
                }
            });
        }

        /// <summary>
        /// Joins a random lobby.
        /// </summary>
        /// <param name="ExtraAttributes">Optional attributes, only used when creating a new lobby.</param>
        public static void JoinRandomLobby(AttributeData[] ExtraAttributes = null)
        {
            JoinRandomLobbyInternal(FindOpenLobbies(), ExtraAttributes);
        }

        private static void JoinRandomLobbyInternal(List<LobbyDetails> lobbies, AttributeData[] ExtraAttributes = null)
        {
            if (lobbies.Count != 0)
            {
                int randint = UnityEngine.Random.Range(0, lobbies.Count);

                LobbyDetailsCopyAttributeByKeyOptions op = new LobbyDetailsCopyAttributeByKeyOptions
                {
                    AttrKey = LobbyCodeKey,
                };
                Epic.OnlineServices.Lobby.Attribute? outat;
                lobbies[randint].CopyAttributeByKey(ref op, out outat);
                var lobbyname = outat.Value.Data.Value.Value.AsUtf8;
                JoinLobby(lobbyname);
            }
            else
            {
                CreateLobby(UnityEngine.Random.Range(10000, 99999).ToString(), ExtraAttributes);
            }
            
        }

        #region DO NOT TOUCH
        private static List<LobbyDetails> OpenLobbiesDONOTUSE;
        private static void FindSucces(List<LobbyDetails> foundLobbies)
        {
            AllowedToDoLobbyStuff = true;
            OpenLobbiesDONOTUSE = foundLobbies;
        }
        public static List<LobbyDetails> FindOpenLobbies()
        {
            instance.eosLobby.FindLobbies();
            return OpenLobbiesDONOTUSE;
        }
        #endregion


        /// <summary>
        /// Sets the player username based on the provided string.
        /// </summary>
        /// <param name="username">The new player username.</param>
        /// <param name="SaveWithPref">Should the username be saved with Unity Player Prefs?</param>
        public static void SetUsername(string username, bool SaveWithPref = true)
        {
            EOSSDKComponent.DisplayName = username;
            if (SaveWithPref)
            {
                PlayerPrefs.SetString("Username", username);
            }
            Debug.Log($"Username set to: {username}, Saved with prefs: {SaveWithPref}");
        }

        public static string GetUsername() => EOSSDKComponent.DisplayName;

        public static string GetLobbyCode()
        {
            if (MirrorVRManager.instance.eosLobby.ConnectedToLobby)
            {
                LobbyDetailsCopyAttributeByKeyOptions op = new LobbyDetailsCopyAttributeByKeyOptions
                {
                    AttrKey = LobbyCodeKey,
                };
                Epic.OnlineServices.Lobby.Attribute? outat;
                MirrorVRManager.instance.eosLobby.ConnectedLobbyDetails.CopyAttributeByKey(ref op, out outat);
                return outat.Value.Data.Value.Value.AsUtf8;
            }
            else
                return string.Empty;

        }

        /// <summary>
        /// Check if the local player is the lobby hosr
        /// </summary>
        public static bool LocalPlayerIsHost()
        {
            if (MirrorVRManager.instance.eosLobby.ConnectedToLobby && UnityEngine.Application.isPlaying)
            {
                return GetLobbyHost() == EOSSDKComponent.LocalUserProductId;
            }
            else
            {
                return false;
            }
            
        }

        public static MirrorVRPlayer GetLocalPlayer()
        {
            return MirrorVRPlayer.LocalInstance;
        }

        /// <summary>
        /// Get the current owner of the lobby
        /// </summary>
        public static ProductUserId GetLobbyHost()
        {
            if (!quitting)
            {
                if (MirrorVRManager.instance.eosLobby.ConnectedToLobby)
                {
                    LobbyDetailsGetLobbyOwnerOptions sdsd = new LobbyDetailsGetLobbyOwnerOptions();
                    return MirrorVRManager.instance.eosLobby.ConnectedLobbyDetails.GetLobbyOwner(ref sdsd);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return new ProductUserId();
            }
                
        }

        /// <summary>
        /// Get the number of members associatted with this lobby
        /// </summary>
        public static int GetMemberCount()
        {
            if (MirrorVRManager.instance.eosLobby.ConnectedToLobby)
            {
                LobbyDetailsGetMemberCountOptions options = new LobbyDetailsGetMemberCountOptions();
                return (int)MirrorVRManager.instance.eosLobby.ConnectedLobbyDetails.GetMemberCount(ref options);
            }
            else
            {
                Debug.LogError("Player is not in a lobby, can not retrieve member count.");
                return 0;
            }
        }

        /// <summary>
        /// <see cref="GetMemberByIndex" /> is used to immediately retrieve individual members registered with a lobby..
        /// </summary>
        public static ProductUserId GetMemberByIndex(int index)
        {
            if (MirrorVRManager.instance.eosLobby.ConnectedToLobby)
            {
                LobbyDetailsGetMemberByIndexOptions o = new LobbyDetailsGetMemberByIndexOptions
                {
                    MemberIndex = (uint)index
                };

                return MirrorVRManager.instance.eosLobby.ConnectedLobbyDetails.GetMemberByIndex(ref o);
            }
            else
            {
                Debug.LogError("Player is not in a lobby, can not retrieve member by index.");
                return null;
            }
        }


        /// <summary>
        /// Disconnects from the current lobby and do host migration if enabled.
        /// </summary>
        public static void Disconnect(bool DoHMIfPos = true)
        {
            //if (!MirrorVRManager.instance.eosLobby.ConnectedToLobby) { return; }
            /*if (MirrorVRManager.instance.hostMigration && LocalPlayerIsHost() &&GetMemberCount() > 1 && DoHMIfPos)
            {
                ProductUserId target = GetMemberByIndex(1);
                Debug.Log("Migrating to " + target);
                Mirror.VR.MirrorVRHostMigrationController.Instance.HostMigrate(target);//Oldest member besides host
            }*/

            if (!quitting)
            {
                NetworkManager.singleton.StopClient();
                NetworkManager.singleton.StopHost();
            }
        }

        /// <summary>
        /// Sets the player color based on the  color provided.
        /// </summary>
        /// <param name="color"></param>
        public static void SetColor(Color color)
        {
            //not finished
            //MirrorVRPlayer.LocalInstance.SetPlayerColor(color);
        }

        /// <summary>
        /// Equips the specified cosmetic.
        /// </summary>
        /// <param name="cosmeticName">The name of the cosmetic.</param>
        /// <param name="cosmeticType">The cosmetic type.</param>
        /// <param name="equip">If true, the cosmetic will be enabled. If false, the cosmetic will be unequipped.</param>
        public static void SetCosmetic(string cosmeticName, string cosmeticType, bool equip)
        {
            //not done
        }

        #region Quitting Detection
        public static bool quitting = false;


        

        private void OnQuit()
        {
            quitting = true;
        }
        #endregion

        #region Host Migration
        public void OnServerClosed()
        {
            if (hostMigration)
            {
                Debug.Log($"Next host: {nexthost}");//, My ID: {EOSSDKComponent.LocalUserProductId.ToString()}");

                if (nexthost == EOSSDKComponent.LocalUserProductId.ToString())
                {
                    Debug.Log("Client is next host");
                    CreateLobby(CurrentLobby);

                    EosTransport.hostMigrationInProgress = true;
                    Invoke(nameof(NoHM), 15);
                }
                else
                {
                    Debug.Log("Client is NOT new host");
                    StartCoroutine(WaitUntilNewServer());
                }
            }
        }

        private void NoHM()
        {
            EosTransport.hostMigrationInProgress = false;
        }

        private IEnumerator WaitUntilNewServer()
        {
            List<LobbyDetails> foundLobbies = new List<LobbyDetails>();

            //NetworkClient.Disconnect();
            //eosLobby.LeaveLobby();

            yield return new WaitForSeconds(5);
            Debug.Log("Connection is now disconnected");

            LobbySearch search = new LobbySearch();
            var createLobbySearchOptions = new CreateLobbySearchOptions { MaxResults = 1 };
            EOSSDKComponent.GetLobbyInterface().CreateLobbySearch(ref createLobbySearchOptions, out search);
            var op = new LobbySearchSetParameterOptions() { Parameter = new AttributeData { Key = EOSLobby.hostAddressKey, Value = nexthost } };
            search.SetParameter(ref op);

            var lobbySearchFindOptions = new LobbySearchFindOptions { LocalUserId = EOSSDKComponent.LocalUserProductId };
            search.Find(ref lobbySearchFindOptions, null, (ref LobbySearchFindCallbackInfo callback) => {
                //if the search was unsuccessful, invoke an error event and return
                if (callback.ResultCode != Result.Success)
                {
                    Debug.LogError("There was an error while finding lobbies. Error: " + callback.ResultCode);
                    return;
                }

                foundLobbies.Clear();

                //for each lobby found, add data to details
                var lobbySearchGetSearchResultCountOptions = new LobbySearchGetSearchResultCountOptions { };
                for (int i = 0; i < search.GetSearchResultCount(ref lobbySearchGetSearchResultCountOptions); i++)
                {
                    LobbyDetails lobbyInformation;
                    var options = new LobbySearchCopySearchResultByIndexOptions();
                    options.LobbyIndex = (uint)i;
                    search.CopySearchResultByIndex(ref options, out lobbyInformation);
                    foundLobbies.Add(lobbyInformation);
                }

                if (foundLobbies.Count > 0)
                {
                    foreach (var lobby in foundLobbies)
                    {
                        LobbyDetailsCopyAttributeByKeyOptions ido = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = EOSLobby.hostAddressKey };
                        Epic.OnlineServices.Lobby.Attribute? host;
                        lobby.CopyAttributeByKey(ref ido, out host);

                        LobbyDetailsCopyAttributeByKeyOptions no = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyCodeKey };
                        Epic.OnlineServices.Lobby.Attribute? nam;
                        lobby.CopyAttributeByKey(ref ido, out nam);

                        Debug.Log($"Lobby Name: {nam.Value.Data.Value.Value.AsUtf8}, Lobby Host Address: {host.Value.Data.Value.Value.AsUtf8}");
                    }

                    LobbyDetailsCopyAttributeByKeyOptions dfo = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = EOSLobby.hostAddressKey };
                    Epic.OnlineServices.Lobby.Attribute? hos;
                    foundLobbies[0].CopyAttributeByKey(ref dfo, out hos);

                    Debug.Log($"Joining Lobby {hos.Value.Data.Value.Value.AsUtf8}");
                    eosLobby.JoinLobby(foundLobbies[0]);
                }
                else
                {
                    Debug.LogError("Found no lobbies");
                }
            });
        }
        #endregion
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MirrorVRManager))]
    public class MirrorVRManagerEditor : Editor
    {
        private Texture2D _longerlogo;

        public override void OnInspectorGUI()
        {
            #region Logo
            if (_longerlogo == null)
                _longerlogo = Resources.Load<Texture2D>("MirrorVRLogoLong");

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_longerlogo);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            #endregion

            MirrorVRManager manager = (MirrorVRManager)target;

            base.OnInspectorGUI();

            /*if (GUILayout.Button("Write player data"))
            {
                string[] fruits = new string[]
                {
                    "Kiwi",
                    "Banana",
                    "Mango",
                };
                MirrorVRManager.WriteOrAddFile("Tests/fruit.txt", fruits);
            }


            if (GUILayout.Button("Be red lol"))
            {
                MirrorVRPlayer.LocalInstance.SetPlayerColor(Color.red);
            }*/

            if (!MirrorVRManager.quitting)
            {
                var Options = new LobbyDetailsGetMemberCountOptions();

                if (manager.eosLobby.ConnectedToLobby)
                {
                    GUILayout.Space(15);
                    GUILayout.Label($"Current Lobby Name: {manager.CurrentLobby}");
                    GUILayout.Label($"Current Lobby ID: {manager.CurrentLobbyId}");
                    GUILayout.Label($"Current Player Count: {MirrorVRManager.instance.eosLobby.ConnectedLobbyDetails.GetMemberCount(ref Options)}");
                }
            }
        }
    }
#endif
}
