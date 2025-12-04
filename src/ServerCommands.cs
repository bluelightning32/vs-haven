using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Haven;

public class ServerCommands {
  private readonly ICoreServerAPI _sapi;
  private readonly HavenSystem _system;
  public ServerCommands(ICoreServerAPI sapi, HavenSystem system) {
    _sapi = sapi;
    _system = system;
    Register();
  }

  private void Register() {
    _sapi.ChatCommands.GetOrCreate("haven")
        .WithDesc("Read or write haven information")
        .RequiresPrivilege("worldedit")
        .BeginSub("create")
        .WithDesc("Create a haven centered at the player's block selection.")
        .HandleWith(CreateHaven)
        .EndSub()
        .BeginSub("undo")
        .WithDesc("Undo the last manual haven generation.")
        .HandleWith(UndoHaven)
        .EndSub();
  }

  private TextCommandResult CreateHaven(TextCommandCallingArgs args) {
    BlockPos center = args.Caller.Player?.CurrentBlockSelection.Position;
    if (center == null) {
      return TextCommandResult.Error("Cannot read block selection.");
    }
    if (_system.GenerateHaven(center)) {
      return TextCommandResult.Success(
          "Generation started. Use '/haven undo' to undo.");
    } else {
      return TextCommandResult.Error("A haven generator is already active.");
    }
  }

  private TextCommandResult UndoHaven(TextCommandCallingArgs args) {
    if (_system.UndoHaven()) {
      return TextCommandResult.Success("Haven undone.");
    } else {
      return TextCommandResult.Error(
          "No more haven generations are available to undo since the server " +
          "was started.");
    }
  }
}
