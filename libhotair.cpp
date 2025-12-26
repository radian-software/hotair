#include "steam_api.h"

extern "C" bool SteamAPI_Init() { return true; }

class HotairRemoteStorage : public ISteamRemoteStorage {
public:
  // NOTE
  //
  // Filenames are case-insensitive, and will be converted to lowercase
  // automatically. So "foo.bar" and "Foo.bar" are the same file, and if you
  // write "Foo.bar" then iterate the files, the filename returned will be
  // "foo.bar".
  //

  // file operations
  bool FileWrite(const char *pchFile, const void *pvData, int32 cubData) {
    return false;
  }
  int32 FileRead(const char *pchFile, void *pvData, int32 cubDataToRead) {
    return 0;
  }

  STEAM_CALL_RESULT(RemoteStorageFileWriteAsyncComplete_t)
  SteamAPICall_t FileWriteAsync(const char *pchFile, const void *pvData,
                                uint32 cubData) {
    return k_uAPICallInvalid;
  }

  STEAM_CALL_RESULT(RemoteStorageFileReadAsyncComplete_t)
  SteamAPICall_t FileReadAsync(const char *pchFile, uint32 nOffset,
                               uint32 cubToRead) {
    return k_uAPICallInvalid;
  }
  bool FileReadAsyncComplete(SteamAPICall_t hReadCall, void *pvBuffer,
                             uint32 cubToRead) {
    return k_uAPICallInvalid;
  }

  bool FileForget(const char *pchFile) { return false; }
  bool FileDelete(const char *pchFile) { return false; }
  STEAM_CALL_RESULT(RemoteStorageFileShareResult_t)
  SteamAPICall_t FileShare(const char *pchFile) { return k_uAPICallInvalid; }
  bool SetSyncPlatforms(const char *pchFile,
                        ERemoteStoragePlatform eRemoteStoragePlatform) {
    return false;
  }

  // file operations that cause network IO
  UGCFileWriteStreamHandle_t FileWriteStreamOpen(const char *pchFile) {
    return k_UGCFileStreamHandleInvalid;
  }
  bool FileWriteStreamWriteChunk(UGCFileWriteStreamHandle_t writeHandle,
                                 const void *pvData, int32 cubData) {
    return false;
  }
  bool FileWriteStreamClose(UGCFileWriteStreamHandle_t writeHandle) {
    return false;
  }
  bool FileWriteStreamCancel(UGCFileWriteStreamHandle_t writeHandle) {
    return false;
  }

  // file information
  bool FileExists(const char *pchFile) { return false; }
  bool FilePersisted(const char *pchFile) { return false; }
  int32 GetFileSize(const char *pchFile) { return 0; }
  int64 GetFileTimestamp(const char *pchFile) { return 0; }
  ERemoteStoragePlatform GetSyncPlatforms(const char *pchFile) {
    return k_ERemoteStoragePlatformAll;
  }

  // iteration
  int32 GetFileCount() { return 0; }
  const char *GetFileNameAndSize(int iFile, int32 *pnFileSizeInBytes) {
    return "";
  }

  // configuration management
  bool GetQuota(uint64 *pnTotalBytes, uint64 *puAvailableBytes) {
    *pnTotalBytes = 0;
    *puAvailableBytes = 0;
    return true;
  }
  bool IsCloudEnabledForAccount() { return false; }
  bool IsCloudEnabledForApp() { return false; }
  void SetCloudEnabledForApp(bool bEnabled) {
    //
  }

  // user generated content

  // Downloads a UGC file.  A priority value of 0 will download the file
  // immediately, otherwise it will wait to download the file until all
  // downloads with a lower priority value are completed.  Downloads with equal
  // priority will occur simultaneously.
  STEAM_CALL_RESULT(RemoteStorageDownloadUGCResult_t)
  SteamAPICall_t UGCDownload(UGCHandle_t hContent, uint32 unPriority) {
    return k_uAPICallInvalid;
  }

  // Gets the amount of data downloaded so far for a piece of content.
  // pnBytesExpected can be 0 if function returns false or if the transfer
  // hasn't started yet, so be careful to check for that before dividing to get
  // a percentage
  bool GetUGCDownloadProgress(UGCHandle_t hContent, int32 *pnBytesDownloaded,
                              int32 *pnBytesExpected) {
    return false;
  }

  // Gets metadata for a file after it has been downloaded. This is the same
  // metadata given in the RemoteStorageDownloadUGCResult_t call result
  bool GetUGCDetails(UGCHandle_t hContent, AppId_t *pnAppID,
                     STEAM_OUT_STRING() char **ppchName,
                     int32 *pnFileSizeInBytes,
                     STEAM_OUT_STRUCT() CSteamID *pSteamIDOwner) {
    return false;
  }

  // After download, gets the content of the file.
  // Small files can be read all at once by calling this function with an offset
  // of 0 and cubDataToRead equal to the size of the file. Larger files can be
  // read in chunks to reduce memory usage (since both sides of the IPC client
  // and the game itself must allocate enough memory for each chunk).  Once the
  // last byte is read, the file is implicitly closed and further calls to
  // UGCRead will fail unless UGCDownload is called again. For especially large
  // files (anything over 100MB) it is a requirement that the file is read in
  // chunks.
  int32 UGCRead(UGCHandle_t hContent, void *pvData, int32 cubDataToRead,
                uint32 cOffset, EUGCReadAction eAction) {
    return 0;
  }

  // Functions to iterate through UGC that has finished downloading but has not
  // yet been read via UGCRead()
  int32 GetCachedUGCCount() { return 0; }
  UGCHandle_t GetCachedUGCHandle(int32 iCachedContent) {
    return k_UGCHandleInvalid;
  }

  // publishing UGC
  STEAM_CALL_RESULT(RemoteStoragePublishFileProgress_t)
  SteamAPICall_t PublishWorkshopFile(
      const char *pchFile, const char *pchPreviewFile, AppId_t nConsumerAppId,
      const char *pchTitle, const char *pchDescription,
      ERemoteStoragePublishedFileVisibility eVisibility,
      SteamParamStringArray_t *pTags, EWorkshopFileType eWorkshopFileType) {
    return k_uAPICallInvalid;
  }
  PublishedFileUpdateHandle_t
  CreatePublishedFileUpdateRequest(PublishedFileId_t unPublishedFileId) {
    return k_PublishedFileUpdateHandleInvalid;
  }
  bool UpdatePublishedFileFile(PublishedFileUpdateHandle_t updateHandle,
                               const char *pchFile) {
    return false;
  }
  bool UpdatePublishedFilePreviewFile(PublishedFileUpdateHandle_t updateHandle,
                                      const char *pchPreviewFile) {
    return false;
  }
  bool UpdatePublishedFileTitle(PublishedFileUpdateHandle_t updateHandle,
                                const char *pchTitle) {
    return false;
  }
  bool UpdatePublishedFileDescription(PublishedFileUpdateHandle_t updateHandle,
                                      const char *pchDescription) {
    return false;
  }
  bool UpdatePublishedFileVisibility(
      PublishedFileUpdateHandle_t updateHandle,
      ERemoteStoragePublishedFileVisibility eVisibility) {
    return false;
  }
  bool UpdatePublishedFileTags(PublishedFileUpdateHandle_t updateHandle,
                               SteamParamStringArray_t *pTags) {
    return false;
  }
  STEAM_CALL_RESULT(RemoteStorageUpdatePublishedFileResult_t)
  SteamAPICall_t
  CommitPublishedFileUpdate(PublishedFileUpdateHandle_t updateHandle) {
    return k_uAPICallInvalid;
  }
  // Gets published file details for the given publishedfileid.  If
  // unMaxSecondsOld is greater than 0, cached data may be returned, depending
  // on how long ago it was cached.  A value of 0 will force a refresh. A value
  // of k_WorkshopForceLoadPublishedFileDetailsFromCache will use cached data if
  // it exists, no matter how old it is.
  STEAM_CALL_RESULT(RemoteStorageGetPublishedFileDetailsResult_t)
  SteamAPICall_t GetPublishedFileDetails(PublishedFileId_t unPublishedFileId,
                                         uint32 unMaxSecondsOld) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageDeletePublishedFileResult_t)
  SteamAPICall_t DeletePublishedFile(PublishedFileId_t unPublishedFileId) {
    return k_uAPICallInvalid;
  }
  // enumerate the files that the current user published with this app
  STEAM_CALL_RESULT(RemoteStorageEnumerateUserPublishedFilesResult_t)
  SteamAPICall_t EnumerateUserPublishedFiles(uint32 unStartIndex) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageSubscribePublishedFileResult_t)
  SteamAPICall_t SubscribePublishedFile(PublishedFileId_t unPublishedFileId) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageEnumerateUserSubscribedFilesResult_t)
  SteamAPICall_t EnumerateUserSubscribedFiles(uint32 unStartIndex) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageUnsubscribePublishedFileResult_t)
  SteamAPICall_t UnsubscribePublishedFile(PublishedFileId_t unPublishedFileId) {
    return k_uAPICallInvalid;
  }
  bool UpdatePublishedFileSetChangeDescription(
      PublishedFileUpdateHandle_t updateHandle,
      const char *pchChangeDescription) {
    return false;
  }
  STEAM_CALL_RESULT(RemoteStorageGetPublishedItemVoteDetailsResult_t)
  SteamAPICall_t
  GetPublishedItemVoteDetails(PublishedFileId_t unPublishedFileId) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageUpdateUserPublishedItemVoteResult_t)
  SteamAPICall_t
  UpdateUserPublishedItemVote(PublishedFileId_t unPublishedFileId,
                              bool bVoteUp) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageGetPublishedItemVoteDetailsResult_t)
  SteamAPICall_t
  GetUserPublishedItemVoteDetails(PublishedFileId_t unPublishedFileId) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageEnumerateUserPublishedFilesResult_t)
  SteamAPICall_t
  EnumerateUserSharedWorkshopFiles(CSteamID steamId, uint32 unStartIndex,
                                   SteamParamStringArray_t *pRequiredTags,
                                   SteamParamStringArray_t *pExcludedTags) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStoragePublishFileProgress_t)
  SteamAPICall_t PublishVideo(EWorkshopVideoProvider eVideoProvider,
                              const char *pchVideoAccount,
                              const char *pchVideoIdentifier,
                              const char *pchPreviewFile,
                              AppId_t nConsumerAppId, const char *pchTitle,
                              const char *pchDescription,
                              ERemoteStoragePublishedFileVisibility eVisibility,
                              SteamParamStringArray_t *pTags) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageSetUserPublishedFileActionResult_t)
  SteamAPICall_t SetUserPublishedFileAction(PublishedFileId_t unPublishedFileId,
                                            EWorkshopFileAction eAction) {
    return k_uAPICallInvalid;
  }
  STEAM_CALL_RESULT(RemoteStorageEnumeratePublishedFilesByUserActionResult_t)
  SteamAPICall_t
  EnumeratePublishedFilesByUserAction(EWorkshopFileAction eAction,
                                      uint32 unStartIndex) {
    return k_uAPICallInvalid;
  }
  // this method enumerates the public view of workshop files
  STEAM_CALL_RESULT(RemoteStorageEnumerateWorkshopFilesResult_t)
  SteamAPICall_t
  EnumeratePublishedWorkshopFiles(EWorkshopEnumerationType eEnumerationType,
                                  uint32 unStartIndex, uint32 unCount,
                                  uint32 unDays, SteamParamStringArray_t *pTags,
                                  SteamParamStringArray_t *pUserTags) {
    return k_uAPICallInvalid;
  }

  STEAM_CALL_RESULT(RemoteStorageDownloadUGCResult_t)
  SteamAPICall_t UGCDownloadToLocation(UGCHandle_t hContent,
                                       const char *pchLocation,
                                       uint32 unPriority) {
    return k_uAPICallInvalid;
  }

  // Cloud dynamic state change notification
  int32 GetLocalFileChangeCount() { return false; }
  const char *GetLocalFileChange(int iFile,
                                 ERemoteStorageLocalFileChange *pEChangeType,
                                 ERemoteStorageFilePathType *pEFilePathType) {
    return "";
  }

  // Indicate to Steam the beginning / end of a set of local file
  // operations - for example, writing a game save that requires updating two
  // files.
  bool BeginFileWriteBatch() { return false; }
  bool EndFileWriteBatch() { return false; }
};

extern "C" ISteamRemoteStorage *SteamRemoteStorage() {
  return new HotairRemoteStorage();
}
