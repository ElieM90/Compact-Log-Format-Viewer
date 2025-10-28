import { dialog, ipcMain, webContents, BrowserWindow } from "electron";
import { updateMenuEnabledState } from "./appmenu";
import { openFileDialog } from "./file";
import * as webapi from "./webapi";
import axios from "axios";
import log from "electron-log";

// Listen for IPCEvents from the view/renderer
ipcMain.on("logviewer.open-file-dialog", () => {
  // arg is empty - we simply wanting to be notified that user trying to open a file dialog

  // Get focused window
  const currentWindow = BrowserWindow.getFocusedWindow()?.webContents;
  openFileDialog(currentWindow);
});

ipcMain.on(
  "logviewer.dragged-file",
  (event: Electron.IpcMainEvent, filePath: string) => {
    // Get focused window
    const currentWindow = webContents.getFocusedWebContents();

    // Disable the file open menu item & enable the close menu item
    updateMenuEnabledState("logviewer.open", false);
    updateMenuEnabledState("logviewer.close", true);
    updateMenuEnabledState("logviewer.reload", true);
    updateMenuEnabledState("logviewer.export", true);

    // Call the Web API with the selected file
    webapi.openFile(filePath, currentWindow);
  }
);

ipcMain.on("logviewer.get-logs", (event: Electron.IpcMainEvent, arg: any) => {
  // Get focused window
  console.log("get logs", arg);
  const currentWindow = webContents.getFocusedWebContents();

  webapi.getLogs(
    currentWindow,
    arg.pageNumber,
    arg.filterExpression,
    arg.sortOrder
  );
});

ipcMain.on(
  "logviewer.export-done",
  (event: Electron.IpcMainEvent, arg: any) => {
    dialog.showMessageBox({
      message: `File sucessfully exported at ${arg.file}`,
      title: "File Saved",
    });
  }
);

ipcMain.on("logviewer.reload-file-after-notify", () => {
  // arg is empty - we simply wanting to be notified that user trying to open a file dialog

  // Get focused window
  const currentWindow = BrowserWindow.getFocusedWindow()?.webContents;
  webapi.reload(currentWindow);
});

// FTP-related IPC events

ipcMain.on(
  "logviewer.ftp.test-connection",
  async (event: Electron.IpcMainEvent, request: any) => {
    try {
      log.info("Sending FTP request", request);
      const response = await axios.post(
        "http://localhost:45678/api/Viewer/ftp/test",
        request
      );
      event.sender.send("logviewer.ftp.test-connection-result", {
        success: response.data,
        error: null,
      });
    } catch (error) {
      event.sender.send("logviewer.ftp.test-connection-result", {
        success: false,
        error: error.response?.data || error.message,
      });
    }
  }
);

ipcMain.on(
  "logviewer.ftp.connect",
  async (event: Electron.IpcMainEvent, request: any) => {
    try {
      log.info("Sending FTP request", request);
      const response = await axios.post(
        "http://localhost:45678/api/Viewer/ftp/test",
        request
      );
      if (response.data) {
        // Save the connection if requested
        if (request.saveConnection && request.name) {
          try {
            await axios.post(
              "http://localhost:45678/api/Viewer/ftp/connections",
              {
                name: request.name,
                host: request.host,
                port: request.port,
                username: request.username,
                encryptedPassword: request.password,
              }
            );
          } catch (saveError) {
            log.error("Failed to save connection", saveError);
          }
        }

        event.sender.send("logviewer.ftp.connection-result", {
          success: true,
          connection: request,
        });
      } else {
        event.sender.send("logviewer.ftp.connection-result", {
          success: false,
          error: "Connection test failed",
        });
      }
    } catch (error) {
      event.sender.send("logviewer.ftp.connection-result", {
        success: false,
        error: error.response?.data || error.message,
      });
    }
  }
);

ipcMain.on(
  "logviewer.ftp.list-directory",
  async (event: Electron.IpcMainEvent, request: any) => {
    try {
      const response = await axios.post(
        "http://localhost:45678/api/Viewer/ftp/list",
        request
      );
      event.sender.send("logviewer.ftp.directory-listed", response.data);
    } catch (error) {
      event.sender.send("logviewer.ftp.directory-listed", {
        success: false,
        errorMessage: error.response?.data || error.message,
        files: [],
      });
    }
  }
);

ipcMain.on(
  "logviewer.ftp.open-file",
  async (event: Electron.IpcMainEvent, data: any) => {
    const currentWindow = webContents.getFocusedWebContents();

    try {
      // Build the request with the file path
      const request = {
        host: data.connection.host,
        port: data.connection.port,
        username: data.connection.username,
        password: data.connection.password,
        remotePath: data.file.fullPath,
        saveConnection: data.connection.saveConnection,
      };

      // Call the backend to download and open the file
      const response = await axios.post(
        "http://localhost:45678/api/Viewer/ftp/open",
        request
      );

      // Update menu state
      updateMenuEnabledState("logviewer.open", false);
      updateMenuEnabledState("logviewer.close", true);
      updateMenuEnabledState("logviewer.reload", true);
      updateMenuEnabledState("logviewer.export", true);

      // Notify renderer that file is opened
      currentWindow.send("logviewer.file-opened");

      // Get and send totals, errors, and logs
      // These are called internally by the backend after opening the file
      webapi.getLogs(currentWindow, 1, "");
    } catch (error) {
      dialog.showErrorBox("FTP Error", error.response?.data || error.message);
    }
  }
);

ipcMain.on(
  "logviewer.ftp.get-all-connections",
  async (event: Electron.IpcMainEvent) => {
    try {
      const response = await axios.get(
        "http://localhost:45678/api/Viewer/ftp/connections"
      );
      event.sender.send("logviewer.ftp.all-connections-loaded", response.data);
    } catch (error) {
      event.sender.send("logviewer.ftp.all-connections-loaded", []);
    }
  }
);

ipcMain.on(
  "logviewer.ftp.delete-connection",
  async (event: Electron.IpcMainEvent, name: string) => {
    try {
      await axios.delete(
        `http://localhost:45678/api/Viewer/ftp/connections/${encodeURIComponent(name)}`
      );
      event.sender.send("logviewer.ftp.connection-deleted");
    } catch (error) {
      log.error("Failed to delete FTP connection", error);
    }
  }
);
