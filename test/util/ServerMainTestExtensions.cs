using System.Reflection;

using Vintagestory.Server;

namespace Haven.Test;

public static class ServerMainTestExtensions {

  static readonly FieldInfo systemsField =
      typeof(ServerMain)
          .GetField("Systems", BindingFlags.Instance | BindingFlags.NonPublic);

  public static void LoadChunksInline(this ServerMain server) {
    ServerSystem[] systems = (ServerSystem[])systemsField.GetValue(server);
    Type serverSystemSupplyChunksType = server.GetType().Assembly.GetType(
        "Vintagestory.Server.ServerSystemSupplyChunks");

    foreach (ServerSystem system in systems) {
      if (serverSystemSupplyChunksType.IsInstanceOfType(system)) {
        // The supply chunks system processes the chunk loading queues inside
        // OnSeparateThreadTick.
        system.OnSeparateThreadTick();
        // Run the chunk loaded callbacks.
        server.ProcessMainThreadTasks();
        return;
      }
    }
    throw new ArgumentException("Server is missing the SystemSupplyChunks",
                                nameof(server));
  }

  public static void SaveGameInline(this ServerMain server) {
    ServerSystem[] systems = (ServerSystem[])systemsField.GetValue(server);
    Type serverSystemLoadAndSaveGame = server.GetType().Assembly.GetType(
        "Vintagestory.Server.ServerSystemLoadAndSaveGame");

    foreach (ServerSystem system in systems) {
      if (serverSystemLoadAndSaveGame.IsInstanceOfType(system)) {
        // Triggering a regular save game would cause it to save on a background
        // thread. Call SaveGameWorld with the first argument set to false to
        // save inline.
        MethodInfo saveGameWorldMethod = system.GetType().GetMethod(
            "SaveGameWorld", BindingFlags.Instance | BindingFlags.NonPublic);
        saveGameWorldMethod.Invoke(system, [false]);
        return;
      }
    }
    throw new ArgumentException(
        "Server is missing the ServerSystemLoadAndSaveGame", nameof(server));
  }
}
