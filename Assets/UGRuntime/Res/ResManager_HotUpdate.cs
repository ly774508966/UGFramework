using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UGFramework.Log;
using UGFramework.Utility;
using UnityEngine;

namespace UGFramework.Res
{
    public partial class ResManager
    {
        // Resources hot updating process info
        public struct ProcessInfo
        {
            // Current updating filepath
            public string File;

            // If error is empty meaning success
            public string Error;

            // Index of current updating file in updating files
            public int Index;

            // Count of updating files
            public int Count;
        }

        void InitForHotUpdate()
        {
        }

        // TODO: Clear cache like luas, bundles .etc
        public Action<ProcessInfo> OnHotUpdate = delegate {};
        void NotifyHotUpdate(ProcessInfo processInfo)
        {
            if (this.OnHotUpdate == null)
                return;
            this.OnHotUpdate(processInfo);
        }
        void NotifyNoHotUpdate()
        {
            var processInfo = new ProcessInfo();
            processInfo.File = "";
            processInfo.Error = "";
            processInfo.Index = -1;
            processInfo.Count = 0;
            this.NotifyHotUpdate(processInfo);
        }

        /**
        * Call HotUpdate when game starting or loading
        */
        public void HotUpdate()
        {
            if (Application.isMobilePlatform == false && this.Simulate == false)
            {
                this.NotifyNoHotUpdate();
                return;
            }

    #if UNITY_DEBUG
            if (Directory.Exists(ResConfig.MOBILE_HOTUPDATE_PATH))
            {
                LogManager.Log(string.Format("HotUpdate: Delete MOBILE_HOTUPDATE_PATH({0})", ResConfig.MOBILE_HOTUPDATE_PATH));
                Directory.Delete(ResConfig.MOBILE_HOTUPDATE_PATH, true);
            }
    #endif

            // 1. Check whether need update assets 
            var localVersionFile = _GetLocalVersionFile(); 
            var remoteVersionFile = _GetRemoteVersionFile();
            var diffInfos = ResVersionFile.GetDiffInfos(localVersionFile, remoteVersionFile);

            if (diffInfos.Count <= 0)
                this.NotifyNoHotUpdate();
            // 2. Download and save new assets
            else
                this.StartCoroutine(this.DownloadAndSave(diffInfos));
            
            // 3. TODO: Compress and Load new assets to memory

            // 4. Save new versionFile
            if (diffInfos.Count > 0)
            {
                FileUtility.WriteFile(ResConfig.MOBILE_HOTUPDATE_PATH + "/" + ResConfig.VERSION_FILE, remoteVersionFile.Serialize());
            }
        }

        ResVersionFile _GetLocalVersionFile()
        {
            ResVersionFile versionFile;
            byte[] bytes = null;
            var txt = this.LoadTxtAtPath(ResConfig.VERSION_FILE, ref bytes);
            if (string.IsNullOrEmpty(txt) == false)
                versionFile = ResVersionFile.UnSerialize(txt);
            else
                versionFile = new ResVersionFile(0);
            return versionFile;
        }
        ResVersionFile _GetRemoteVersionFile()
        {
            ResVersionFile versionFile;
            var txt = this.DownloadTxt(ResConfig.VERSION_FILE);
            if (string.IsNullOrEmpty(txt) == false)
                versionFile = ResVersionFile.UnSerialize(txt);
            else
                versionFile = new ResVersionFile(0);
            return versionFile;
        }

        IEnumerator DownloadAndSave(List<ResVersionInfo> diffInfos)
        {
            for (int i = 0; i < diffInfos.Count; ++i)
            {
                var diffInfo = diffInfos[i];
                var path = diffInfo.File;
                yield return this.StartCoroutine(this.DownloadAssetAsync(path, (www) => {
                    var processInfo = new ProcessInfo();
                    processInfo.File = diffInfo.File;
                    processInfo.Index = i;
                    processInfo.Count = diffInfos.Count;

                    if (string.IsNullOrEmpty(www.error) == false)
                    {
                        processInfo.Error = "Download file failure " + www.error;
                        this.NotifyHotUpdate(processInfo);
                        return;
                    }

                    LogManager.Log(string.Format("DownloadAsync successfully file({0})", diffInfo.File));

                    var outputFullpath = ResConfig.MOBILE_HOTUPDATE_PATH + "/" + path; 
                    FileUtility.WriteFile(outputFullpath, www.bytes);
                    if (File.Exists(outputFullpath))
                    {
                        LogManager.Log(string.Format("Save successfully path({0})", outputFullpath));
                        processInfo.Error = string.Empty;
                    }
                    else
                    {
                        LogManager.Error(string.Format("Save failure path({0})", outputFullpath));
                        processInfo.Error = "Save file failure";
                    }

                    this.NotifyHotUpdate(processInfo);
                }));
            }
        }

        [ContextMenu("TestHotUpdate")]
        void TestHotUpdate()
        {
            this.HotUpdate();
        }
    }
}