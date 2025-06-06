using System;
using System.Threading;

using UnityEditor;

using Codice.Client.BaseCommands;
using Codice.Client.Commands;
using Codice.Client.Common.EventTracking;
using Codice.CM.Common;
using Codice.LogWrapper;
using PlasticGui;
using PlasticGui.WebApi.Responses;
using PlasticGui.WorkspaceWindow;
using PlasticGui.WorkspaceWindow.Update;
using Unity.PlasticSCM.Editor.AssetUtils;
using Unity.PlasticSCM.Editor.UI;
using Unity.PlasticSCM.Editor.WebApi;
using Unity.PlasticSCM.Editor.Configuration;

namespace Unity.PlasticSCM.Editor.Hub.Operations
{
    internal class DownloadRepository
    {
        internal static void LaunchOperation(OperationParams parameters)
        {
            DownloadRepository downloadOperation = new DownloadRepository();

            downloadOperation.DownloadRepositoryOperation(parameters);
        }

        void DownloadRepositoryOperation(OperationParams parameters)
        {
            RefreshAsset.BeforeLongAssetOperation();

            try
            {
                BuildProgressSpeedAndRemainingTime.ProgressData progressData =
                    new BuildProgressSpeedAndRemainingTime.ProgressData(DateTime.Now);

                ThreadPool.QueueUserWorkItem(
                    DownloadRepositoryToPathIfNeeded,
                    parameters);

                while (!mOperationFinished)
                {
                    if (mDisplayProgress)
                    {
                        DisplayProgress(
                            mUpdateNotifier.GetUpdateStatus(),
                            progressData,
                            parameters.Repository);
                    }

                    Thread.Sleep(150);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                RefreshAsset.AfterLongAssetOperation();

                if (!mOperationFailed)
                {
                    PlasticPlugin.Enable();
                    ShowWindow.Plastic();
                }
            }
        }

        void DownloadRepositoryToPathIfNeeded(object state)
        {
            OperationParams parameters = (OperationParams)state;

            try
            {
                mDisplayProgress = true;

                TokenExchangeResponse tokenExchangeResponse =
                    AutoConfig.PlasticCredentials(
                        parameters.AccessToken,
                        parameters.RepositorySpec.Server);

                if (tokenExchangeResponse.Error != null)
                {
                    mOperationFailed = true;

                    LogTokenExchangeErrorInConsole(tokenExchangeResponse.Error);
                    return;
                }

                TrackFeatureUseEvent.For(
                    parameters.RepositorySpec, TrackFeatureUseEvent.
                        Features.UnityPackage.DownloadRepositoryFromHub);

                WorkspaceInfo wkInfo = CreateWorkspace.
                    CreateWorkspaceForRepSpec(
                        parameters.RepositorySpec,
                        parameters.WorkspaceFullPath,
                        mLog);

                PlasticGui.Plastic.API.Update(
                    wkInfo.ClientPath,
                    UpdateFlags.None,
                    null,
                    mUpdateNotifier);
            }
            catch (Exception ex)
            {
                LogException(ex);
                LogExceptionErrorInConsole(ex);

                mOperationFailed = true;
            }
            finally
            {
                mOperationFinished = true;
            }
        }

        static void DisplayProgress(
            UpdateOperationStatus status,
            BuildProgressSpeedAndRemainingTime.ProgressData progressData,
            string cloudRepository)
        {
            string totalProgressMessage = UpdateProgressRender.
                GetProgressString(status, progressData);

            float totalProgressPercent = GetProgressBarPercent.
                ForTransfer(status.UpdatedSize, status.TotalSize) / 100f;

            EditorUtility.DisplayProgressBar(
                string.Format("{0} {1}",
                    PlasticLocalization.Name.DownloadingProgress.GetString(),
                    cloudRepository),
                totalProgressMessage, totalProgressPercent);
        }

        static void LogTokenExchangeErrorInConsole(ErrorResponse.ErrorFields error)
        {
            UnityEngine.Debug.LogErrorFormat(
                PlasticLocalization.Name.ErrorDownloadingProjectFromRepository.GetString(),
                string.Format("Unable to get TokenExchangeResponse: {0} [code {1}]",
                    error.Message, error.ErrorCode));
        }

        static void LogExceptionErrorInConsole(Exception ex)
        {
            UnityEngine.Debug.LogErrorFormat(
                PlasticLocalization.Name.ErrorDownloadingProjectFromRepository.GetString(),
                ex.Message);
        }

        static void LogException(Exception ex)
        {
            mLog.WarnFormat("Message: {0}", ex.Message);

            mLog.DebugFormat(
                "StackTrace:{0}{1}",
                Environment.NewLine, ex.StackTrace);
        }

        DownloadRepository()
        {
        }

        volatile bool mOperationFinished = false;
        volatile bool mOperationFailed = false;
        volatile bool mDisplayProgress;

        UpdateNotifier mUpdateNotifier = new UpdateNotifier();

        static readonly ILog mLog = PlasticApp.GetLogger("DownloadRepository");
    }
}
