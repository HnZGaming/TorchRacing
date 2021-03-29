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
        [Permission(MyPromoteLevel.Moderator)]
        public void ShowState() => this.CatchAndReport(() =>
        {
            var promoteLevel = Context.Player?.PromoteLevel ?? MyPromoteLevel.Admin;
            var debug = promoteLevel >= MyPromoteLevel.Moderator;
            Context.Respond($"\n{Server.ToString(debug)}");
        });

        [Command("cpadd", "Add new checkpoint at the player position")]
        [Permission(MyPromoteLevel.Moderator)]
        public void AddCheckpoint(float radius, bool useSafezone) => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.AddCheckpoint(Context.Player, radius, useSafezone);
        });

        [Command("cpdel", "Remove the checkpoint closest to the player")]
        [Permission(MyPromoteLevel.Moderator)]
        public void RemoveCheckpoint() => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.RemoveCheckpoint(Context.Player);
        });

        [Command("cpdelall", "Remove the checkpoint closest to the player")]
        [Permission(MyPromoteLevel.Moderator)]
        public void RemoveAllCheckpoints() => this.CatchAndReport(() =>
        {
            this.EnsureInvokedByPlayer();
            Server.RemoveAllCheckpoints();
        });

        [Command("join", "Join the race if any")]
        [Permission(MyPromoteLevel.None)]
        public void JoinRace() => this.CatchAndReport(() =>
        {
            Server.JoinRace(Context.Player);
        });

        [Command("exit", "Exit the race if any")]
        [Permission(MyPromoteLevel.None)]
        public void ExitRace() => this.CatchAndReport(() =>
        {
            Server.ExitRace(Context.Player);
        });

        [Command("start", "Start the race in N seconds")]
        [Permission(MyPromoteLevel.None)]
        public void StartRace(int countdown = 5) => this.CatchAndReport(async () =>
        {
            await Server.StartRace(Context.Player, countdown);
        });

        [Command("reset", "Reset the current race state")]
        [Permission(MyPromoteLevel.None)]
        public void ResetRace() => this.CatchAndReport(() =>
        {
            Server.ResetRace(Context.Player);
        });
    }
}