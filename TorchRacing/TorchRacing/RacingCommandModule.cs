using Torch.Commands;
using Torch.Commands.Permissions;
using TorchRacing.Core;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchRacing
{
    [Category("race")]
    public sealed class RacingCommandModule : CommandModule
    {
        RacingPlugin Plugin => (RacingPlugin) Context.Plugin;
        RacingServer Server => Plugin.Server;

        [Command("configs", "Get or set config properties")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Configs() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config);
        });

        [Command("commands", "Show available commands")]
        [Permission(MyPromoteLevel.None)]
        public void Commands() => this.CatchAndReport(() =>
        {
            this.ShowCommands();
        });

        [Command("state", "Show status of the race")]
        [Permission(MyPromoteLevel.None)]
        public void ShowState() => this.CatchAndReport(() =>
        {
            var steamId = Context.Player?.SteamUserId ?? 0;

            if (Server.TryGetLobbyOfPlayer(steamId, out var lobby))
            {
                Context.Respond($"\n{lobby}");
            }
            else
            {
                Context.Respond($"\n{Server}");
            }
        });

        [Command("createtrack", "Create new race track")]
        [Permission(MyPromoteLevel.None)]
        public void CreateRaceTrack(string raceId) => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.AddTrack(Context.Player, raceId);
        });

        [Command("deletetrack", "Create new race track")]
        [Permission(MyPromoteLevel.None)]
        public void DeleteRaceTrack() => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.DeleteTrack(Context.Player);
        });

        [Command("addcp", "Add new checkpoint at the player position")]
        [Permission(MyPromoteLevel.None)]
        public void AddCheckpoint(float radius, bool useSafezone) => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.AddCheckpoint(Context.Player, radius, useSafezone);
        });

        [Command("replacecp", "Replace the checkpoint identified by the index")]
        [Permission(MyPromoteLevel.None)]
        public void ReplaceCheckpoint(int index, float radius, bool useSafezone) => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.ReplaceCheckpoint(Context.Player, index - 1, radius, useSafezone);
        });

        [Command("deletecp", "Remove the checkpoint closest to the player")]
        [Permission(MyPromoteLevel.None)]
        public void RemoveCheckpoint() => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.DeleteCheckpoint(Context.Player);
        });

        [Command("deleteallcp", "Remove the checkpoint closest to the player")]
        [Permission(MyPromoteLevel.None)]
        public void RemoveAllCheckpoints() => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.DeleteAllCheckpoints(Context.Player);
        });

        [Command("showallcp", "Show all checkpoints")]
        [Permission(MyPromoteLevel.None)]
        public void ShowAllCheckpoints() => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.ShowAllCheckpoints(Context.Player);
        });

        [Command("join", "Join the race if any")]
        [Permission(MyPromoteLevel.None)]
        public void JoinRace(string raceId) => this.CatchAndReport(() =>
        {
            Server.JoinRace(Context.Player, raceId);
        });

        [Command("exit", "Exit the race if any")]
        [Permission(MyPromoteLevel.None)]
        public void ExitRace() => this.CatchAndReport(() =>
        {
            Server.ExitRace(Context.Player);
        });

        [Command("start", "Start the race")]
        [Permission(MyPromoteLevel.None)]
        public void StartRace(int lapCount = 3) => this.CatchAndReportAsync(async () =>
        {
            await Server.StartRace(Context.Player, lapCount);
        });

        [Command("reset", "Reset the current race state")]
        [Permission(MyPromoteLevel.None)]
        public void ResetRace() => this.CatchAndReport(() =>
        {
            Server.ResetRace(Context.Player);
        });
    }
}