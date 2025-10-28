import * as angular from "angular";
import { ipcRenderer } from "electron";

angular.module("logViewerApp").directive("ftpSelector", () => {
  function ftpSelectorLink(scope) {
    scope.savedConnections = [];
    scope.loading = true;
    scope.selectedConnection = null;

    function activate() {
      loadSavedConnections();
    }

    function loadSavedConnections() {
      scope.loading = true;
      ipcRenderer.send("logviewer.ftp.get-all-connections");
    }

    scope.selectConnection = (connection) => {
      scope.selectedConnection = connection;
    };

    scope.connectToSelected = () => {
      if (!scope.selectedConnection) {
        return;
      }

      if (scope.onConnect) {
        scope.onConnect({ connection: scope.selectedConnection });
      }
    };

    scope.createNewConnection = () => {
      if (scope.onCreateNew) {
        scope.onCreateNew();
      }
    };

    scope.deleteConnection = (connection, event) => {
      event.stopPropagation();

      if (confirm(`Are you sure you want to delete the connection '${connection.name}'?`)) {
        ipcRenderer.send("logviewer.ftp.delete-connection", connection.name);
      }
    };

    scope.cancel = () => {
      if (scope.onCancel) {
        scope.onCancel();
      }
    };

    // Listen for all connections loaded
    ipcRenderer.on("logviewer.ftp.all-connections-loaded", (event, connections) => {
      scope.$apply(() => {
        scope.savedConnections = connections || [];
        scope.loading = false;
      });
    });

    // Listen for connection deleted
    ipcRenderer.on("logviewer.ftp.connection-deleted", () => {
      scope.$apply(() => {
        loadSavedConnections();
      });
    });

    scope.$on("$destroy", () => {
      ipcRenderer.removeAllListeners("logviewer.ftp.all-connections-loaded");
      ipcRenderer.removeAllListeners("logviewer.ftp.connection-deleted");
    });

    activate();
  }

  return {
    restrict: "E",
    templateUrl: "components/ftp-selector.html",
    scope: {
      show: "=",
      onConnect: "&",
      onCreateNew: "&",
      onCancel: "&",
    },
    link: ftpSelectorLink,
  };
});
