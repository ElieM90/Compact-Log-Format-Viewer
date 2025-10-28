import * as angular from "angular";
import { ipcRenderer } from "electron";

angular.module("logViewerApp").directive("ftpConnection", () => {
  function ftpConnectionLink(scope) {
    scope.connection = {
      name: "",
      host: "",
      port: 21,
      username: "",
      password: "",
      saveConnection: true,
    };

    scope.connecting = false;
    scope.connectionError = "";
    scope.isEditMode = false;

    function activate() {
      // If editing an existing connection, populate the fields
      if (scope.existingConnection) {
        scope.connection.name = scope.existingConnection.name || "";
        scope.connection.host = scope.existingConnection.host || "";
        scope.connection.port = scope.existingConnection.port || 21;
        scope.connection.username = scope.existingConnection.username || "";
        scope.connection.password = scope.existingConnection.encryptedPassword || "";
        scope.isEditMode = true;
      }
    }

    scope.testConnection = () => {
      scope.connecting = true;
      scope.connectionError = "";
      ipcRenderer.send("logviewer.ftp.test-connection", {
        host: scope.connection.host,
        port: scope.connection.port,
        username: scope.connection.username,
        password: scope.connection.password,
      });
    };

    scope.connect = () => {
      if (
        !scope.connection.host ||
        !scope.connection.username ||
        !scope.connection.password
      ) {
        scope.connectionError = "Please fill in all required fields";
        return;
      }

      if (scope.connection.saveConnection && !scope.connection.name) {
        scope.connectionError = "Please provide a connection name to save it";
        return;
      }

      scope.connecting = true;
      scope.connectionError = "";

      ipcRenderer.send("logviewer.ftp.connect", {
        name: scope.connection.name,
        host: scope.connection.host,
        port: scope.connection.port,
        username: scope.connection.username,
        password: scope.connection.password,
        saveConnection: scope.connection.saveConnection,
      });
    };

    scope.cancel = () => {
      scope.connectionError = "";
      if (scope.onCancel) {
        scope.onCancel();
      }
    };

    // Listen for test connection result
    ipcRenderer.on("logviewer.ftp.test-connection-result", (event, result) => {
      console.log("FTP test result", result);
      scope.$apply(() => {
        scope.connecting = false;
        if (result.success) {
          scope.connectionError = "";
          alert("Connection successful!");
        } else {
          scope.connectionError = result.error || "Connection failed";
        }
      });
    });

    // Listen for connection result
    ipcRenderer.on("logviewer.ftp.connection-result", (event, result) => {
      scope.$apply(() => {
        scope.connecting = false;
        if (result.success) {
          scope.connectionError = "";
          if (scope.onConnect) {
            scope.onConnect({ connection: result.connection });
          }
        } else {
          scope.connectionError = result.error || "Connection failed";
        }
      });
    });

    scope.$on("$destroy", () => {
      ipcRenderer.removeAllListeners("logviewer.ftp.test-connection-result");
      ipcRenderer.removeAllListeners("logviewer.ftp.connection-result");
    });

    scope.$watch("existingConnection", (newVal) => {
      if (newVal) {
        activate();
      }
    });

    activate();
  }

  return {
    restrict: "E",
    templateUrl: "components/ftp-connection.html",
    scope: {
      show: "=",
      existingConnection: "=",
      onConnect: "&",
      onCancel: "&",
    },
    link: ftpConnectionLink,
  };
});
