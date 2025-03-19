using UnityEngine;
using PlayEveryWare;
using PlayEveryWare.EpicOnlineServices.Samples;
using Mirror.VR;
using EpicTransport;
using System;
using Epic.OnlineServices.TitleStorage;
using System.Collections;

namespace Mirror.VR
{
    public class MirrorVRDataStorage : MonoBehaviour
    {
        public static MirrorVRDataStorage instance { get; private set; }


        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
                throw new NotSupportedException("You already have another MirrorVRDataStorage in the scene. You may only have one per scene.");
        }

        /// <summary>
        /// Retrieves the content of a file in string format.
        /// </summary>
        /// <param name="FileName">The name of the file you want to retrieve. Includes file extension.</param>
        /// <param name="Callback">The callback that will contain your content.</param>
        /// <returns>The content of the file in string format</returns>
        public static void PlayerDataStorageRetrieveContent(string FileName, Action<string> Callback)
        {
            if (!EOSSDKComponent.Initialized)
            {
                Debug.LogWarning("Cannot use Player Data Storage before EOSSDKCOMPONENT is initialized. Please wait until EOSSDKCOMPONENT is initialized.");
                return;
            }

            string RetrievedContent = "";
            PlayerDataStorageService.Instance.DownloadFile(FileName, () =>
            {
                RetrievedContent = PlayerDataStorageService.Instance.GetCachedFileContent(FileName);

                //Return the content trough the callback
                Callback(RetrievedContent);
            });
        }

        /// <summary>
        /// Write to a file if it exists, if it doesn't it will create it.
        /// </summary>
        /// <param name="FileName">The name of the file you want to write.</param>
        /// <param name="Content">The content of the file you want to write.</param>
        public static void PlayerDataStorageWriteFile(string FileName, string Content)
        {
            if (!EOSSDKComponent.Initialized)
            {
                Debug.LogWarning("Cannot use Player Data Storage before EOSSDKCOMPONENT is initialized. Please wait until EOSSDKCOMPONENT is initialized.");
                return;
            }

            Debug.Log("Writing file");
            PlayerDataStorageService.Instance.AddFile(FileName, Content, EOSSDKComponent.LocalUserProductId);
        }

        /* W.I.P.
        public static string titledatastorageretrievecontent(string filename)
        {
            string retrievedcontent = "";

            titlestorageservice.instance.downloadfile(filename, (titlestoragefiletransferrequest) =>
            {
                titlestorageservice.instance.getlocallycacheddata().trygetvalue("tags", out retrievedcontent);
            });
            return retrievedcontent;
        }
        */
    }
}

