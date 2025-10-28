import * as angular from "angular";
import { ipcRenderer } from "electron";

angular.module("logViewerApp").directive("ftpBrowser", () => {

    function ftpBrowserLink(scope) {

        scope.files = [];
        scope.currentPath = "/";
        scope.loading = false;
        scope.error = "";
        scope.selectedFile = null;

        function activate() {
            if (scope.connection) {
                loadDirectory(scope.currentPath);
            }
        }

        function loadDirectory(path) {
            scope.loading = true;
            scope.error = "";

            ipcRenderer.send("logviewer.ftp.list-directory", {
                host: scope.connection.host,
                port: scope.connection.port,
                username: scope.connection.username,
                password: scope.connection.password,
                remotePath: path
            });
        }

        scope.navigateToDirectory = (directory) => {
            scope.currentPath = directory.fullPath;
            loadDirectory(scope.currentPath);
        };

        scope.navigateUp = () => {
            if (scope.currentPath !== "/") {
                const parts = scope.currentPath.split("/");
                parts.pop();
                scope.currentPath = parts.length > 1 ? parts.join("/") : "/";
                loadDirectory(scope.currentPath);
            }
        };

        scope.selectFile = (file) => {
            scope.selectedFile = file;
        };

        scope.openFile = () => {
            if (!scope.selectedFile || scope.selectedFile.isDirectory) {
                return;
            }

            if (scope.onOpenFile) {
                scope.onOpenFile({
                    file: scope.selectedFile,
                    connection: scope.connection
                });
            }
        };

        scope.cancel = () => {
            if (scope.onCancel) {
                scope.onCancel();
            }
        };

        scope.isLogFile = (file) => {
            if (file.isDirectory) return false;
            const ext = file.extension ? file.extension.toLowerCase() : "";
            return ext === ".txt" || ext === ".json" || ext === ".clef";
        };

        scope.formatSize = (bytes) => {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + " KB";
            return (bytes / (1024 * 1024)).toFixed(2) + " MB";
        };

        // Listen for directory listing result
        ipcRenderer.on("logviewer.ftp.directory-listed", (event, result) => {
            scope.$apply(() => {
                scope.loading = false;
                if (result.success) {
                    scope.files = result.files || [];
                    scope.currentPath = result.currentPath;
                    scope.error = "";
                } else {
                    scope.error = result.errorMessage || "Failed to list directory";
                    scope.files = [];
                }
            });
        });

        scope.$on("$destroy", () => {
            ipcRenderer.removeAllListeners("logviewer.ftp.directory-listed");
        });

        scope.$watch("connection", (newVal) => {
            if (newVal) {
                activate();
            }
        });

        activate();
    }

    return {
        restrict: "E",
        templateUrl: "components/ftp-browser.html",
        scope: {
            show: "=",
            connection: "=",
            onOpenFile: "&",
            onCancel: "&"
        },
        link: ftpBrowserLink,
    };
});
