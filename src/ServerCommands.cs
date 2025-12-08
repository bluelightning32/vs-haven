using System.Collections.Generic;
using System.Text;

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
    CommandArgumentParsers parsers = _sapi.ChatCommands.Parsers;
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
        .EndSub()
        .BeginSub("info")
        .WithDesc("Shows information about all loaded havens.")
        .HandleWith(ShowHavens)
        .EndSub()
        .BeginSub("register")
        .WithDesc("Register an already created haven centered at the " +
                  "player's block selection.")
        .WithArgs(parsers.Int("resourceZoneRadius"), parsers.Int("radius"))
        .HandleWith(RegisterHaven)
        .EndSub()
        .BeginSub("unregister")
        .WithDesc("Unregister an already created haven centered at the " +
                  "player's block selection, but do not remove the blocks.")
        .WithArgs(parsers.OptionalWord("confirmation"))
        .HandleWith(UnregisterHaven)
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

  private TextCommandResult ShowHavens(TextCommandCallingArgs args) {
    StringBuilder builder = new();
    BlockPos selection = args.Caller.Player?.CurrentBlockSelection.Position;
    if (selection != null) {
      builder.AppendLine("Haven at block selection:");
      HavenRegionIntersection intersection =
          _system.GetHavenIntersection(selection);
      if (intersection != null) {
        builder.AppendFormat("  {0}\n", intersection);
      } else {
        builder.AppendFormat("  none\n");
      }
    }
    builder.AppendLine("Loaded haven intersections by map region coords:");
    foreach ((Vec2i pos, List<HavenRegionIntersection> intersections)
                 in _system.GetLoadedIntersections()) {
      builder.AppendFormat("  {0}:\n", pos);
      foreach (HavenRegionIntersection intersection in intersections) {
        builder.AppendFormat("    {0}\n", intersection);
      }
    }
    return TextCommandResult.Success(builder.ToString());
  }

  private TextCommandResult RegisterHaven(TextCommandCallingArgs args) {
    BlockPos center = args.Caller.Player?.CurrentBlockSelection.Position;
    if (center == null) {
      return TextCommandResult.Error("Cannot read block selection.");
    }
    int resourceZoneRadius = (int)args[0];
    int radius = (int)args[1];
    HavenRegionIntersection intersection =
        new() { Center = center, ResourceZoneRadius = resourceZoneRadius,
                Radius = radius };
    _system.RegisterHavenIntersection(intersection);
    return TextCommandResult.Success("Registered");
  }

  private TextCommandResult UnregisterHaven(TextCommandCallingArgs args) {
    BlockPos center = args.Caller.Player?.CurrentBlockSelection.Position;
    if (center == null) {
      return TextCommandResult.Error("Cannot read block selection.");
    }

    HavenRegionIntersection intersection = _system.GetHavenIntersection(center);
    if (intersection == null) {
      return TextCommandResult.Error("There is no haven at that location.");
    }
    string confirmation = args.Parsers[0].IsMissing ? "" : args[0] as string;
    if (confirmation != "confirm") {
      return TextCommandResult.Error(
          "Unregistering a haven is dangerous. Add 'confirm' to verify you " +
          "want to that.");
    }

    _system.UnregisterHavenIntersection(intersection);
    return TextCommandResult.Success(
        $"Unregistered haven at center {intersection.Center}");
  }
}
