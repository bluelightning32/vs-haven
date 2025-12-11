using System.Collections.Generic;
using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
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
        .RequiresPrivilege(Privilege.chat)
        .BeginSub("create")
        .RequiresPrivilege("worldedit")
        .WithDesc("Create a haven centered at the player's block selection.")
        .HandleWith(CreateHaven)
        .EndSub()
        .BeginSub("undo")
        .RequiresPrivilege("worldedit")
        .WithDesc("Undo the last manual haven generation.")
        .HandleWith(UndoHaven)
        .EndSub()
        .BeginSub("info")
        .RequiresPrivilege("worldedit")
        .WithDesc("Shows information about all loaded havens.")
        .HandleWith(ShowHavens)
        .EndSub()
        .BeginSub("register")
        .RequiresPrivilege("worldedit")
        .WithDesc("Register an already created haven centered at the " +
                  "player's block selection.")
        .WithArgs(parsers.Int("resourceZoneRadius"), parsers.Int("radius"))
        .HandleWith(RegisterHaven)
        .EndSub()
        .BeginSub("unregister")
        .RequiresPrivilege("worldedit")
        .WithDesc("Unregister an already created haven centered at the " +
                  "player's block selection, but do not remove the blocks.")
        .WithArgs(parsers.OptionalWord("confirmation"))
        .HandleWith(UnregisterHaven)
        .EndSub()
        .BeginSub("claim")
        .WithDesc("Claim the plot at the block selection.")
        .RequiresPrivilege(Privilege.claimland)
        .HandleWith(ClaimPlot)
        .EndSub()
        .BeginSub("unclaim")
        .WithDesc("Unclaim the plot at the block selection, or all plots in " +
                  "the haven if all is passed.")
        .HandleWith(UnclaimPlot)
        .WithArgs(parsers.OptionalWord("all"))
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
      Haven haven = _system.GetHaven(selection);
      if (haven != null) {
        builder.AppendFormat("  {0}\n", haven.ToString(2));
      } else {
        HavenRegionIntersection intersection =
            _system.GetHavenIntersection(selection);
        if (intersection != null) {
          builder.AppendFormat("  {0}\n", intersection);
        } else {
          builder.AppendFormat("  none\n");
        }
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
                SafeZoneRadius = resourceZoneRadius, Radius = radius };
    Haven haven = new(intersection, _system.ServerConfig.PlotBorderWidth,
                      _system.ServerConfig.BlocksPerPlot);
    _system.RegisterHaven(haven);
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

    _system.UnregisterHaven(intersection);
    return TextCommandResult.Success(
        $"Unregistered haven at center {intersection.Center}");
  }

  private TextCommandResult ClaimPlot(TextCommandCallingArgs args) {
    BlockPos pos = args.Caller.Player?.CurrentBlockSelection.Position;
    string langCode = (args.Caller.Player as IServerPlayer)?.LanguageCode ?? "";

    if (pos == null) {
      return TextCommandResult.Error("Cannot read block selection.");
    }
    Haven haven = _system.GetHaven(pos);
    if (haven == null) {
      return TextCommandResult.Error("There is no haven at that location.");
    }

    int alreadyOwned = haven.GetOwnedPlots(args.Caller.Player.PlayerUID);
    if (alreadyOwned >= _system.ServerConfig.PlotsPerPlayer) {
      return TextCommandResult.Error(
          $"You already own the max plots per player per haven of {_system.ServerConfig.PlotsPerPlayer}");
    }

    (PlotRing ring, double radians) =
        haven.GetPlotRing(pos, _system.ServerConfig.HavenBelowHeight,
                          _system.ServerConfig.HavenAboveHeight);
    if (ring == null) {
      return TextCommandResult.Error("That location is not in the plot zone.");
    }

    string error = ring.ClaimPlot(radians, args.Caller.Player.PlayerUID,
                                  args.Caller.Player.PlayerName);
    if (error != null) {
      return TextCommandResult.Error(Lang.GetL(langCode, error));
    }

    _system.UpdateHaven(haven.GetIntersection().Center,
                        haven.GetIntersection().Radius, haven);
    return TextCommandResult.Success("Claimed");
  }

  private TextCommandResult UnclaimPlot(TextCommandCallingArgs args) {
    BlockPos pos = args.Caller.Player?.CurrentBlockSelection.Position;
    string langCode = (args.Caller.Player as IServerPlayer)?.LanguageCode ?? "";

    if (pos == null) {
      return TextCommandResult.Error("Cannot read block selection.");
    }

    Haven haven = _system.GetHaven(pos);
    if (haven == null) {
      return TextCommandResult.Error("There is no haven at that location.");
    }

    bool all = (args.Parsers[0].IsMissing ? "" : args[0] as string) == "all";

    if (all) {
      int unclaimed = haven.UnclaimAllPlots(args.Caller.Player.PlayerUID);
      if (unclaimed > 0) {
        _system.UpdateHaven(haven.GetIntersection().Center,
                            haven.GetIntersection().Radius, haven);
        return TextCommandResult.Success($"Claimed {unclaimed} plot(s).");
      } else {
        return TextCommandResult.Error(
            "You have no claimed plots in the haven.");
      }
    } else {
      (PlotRing ring, double radians) =
          haven.GetPlotRing(pos, _system.ServerConfig.HavenBelowHeight,
                            _system.ServerConfig.HavenAboveHeight);
      if (ring == null) {
        return TextCommandResult.Error(
            "That location is not in the plot zone.");
      }
      string error = ring.UnclaimPlot(radians, args.Caller.Player.PlayerUID);
      if (error != null) {
        return TextCommandResult.Error(Lang.GetL(langCode, error));
      }
      _system.UpdateHaven(haven.GetIntersection().Center,
                          haven.GetIntersection().Radius, haven);
      return TextCommandResult.Success("Unclaimed");
    }
  }
}
