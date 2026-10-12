mergeInto(LibraryManager.library, {
  LuckboundSyncPersistentFileSystem: function (gameObjectPtr, callbackMethodPtr, populate) {
    var gameObjectName = UTF8ToString(gameObjectPtr);
    var callbackMethod = UTF8ToString(callbackMethodPtr);
    try {
      FS.syncfs(populate !== 0, function (error) {
        var result = error ? "error:" + String(error) : "ok";
        SendMessage(gameObjectName, callbackMethod, result);
      });
    } catch (error) {
      SendMessage(gameObjectName, callbackMethod, "error:" + String(error));
    }
  }
});
