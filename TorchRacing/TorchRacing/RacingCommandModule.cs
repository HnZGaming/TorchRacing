using Torch.Commands;
using Torch.Commands.Permissions;
using TorchRacing.Core;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchRacing
{
    [Category("race")]
    public class RacingCommandModule : CommandModule
    {
        RacingPlugin Plugin => (RacingPlugin) Context.Plugin;
        RacingServer Server => Plugin.Server;

        [Command("configs", "Get or set config properties")]
        [Permission(MyPromoteLevel.None)]
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

        [Command("cpadd", "Add new checkpoint at the player position")]
        [Permission(MyPromoteLevel.Moderator)]
        public void AddCheckpoint() => this.CatchAndReport(() =>
        {
            Server.AddCheckpoint(Context.Player);
        });

        [Command("cpdel", "Remove the checkpoint closest to the player")]
        [Permission(MyPromoteLevel.Moderator)]
        public void RemoveCheckpoint() => this.CatchAndReport(() =>
        {
            Server.RemoveCheckpoint(Context.Player);
        });

        [Command("init", "Initialize a new race")]
        [Permission(MyPromoteLevel.None)]
        public void InitializeRace() => this.CatchAndReport(() =>
        {
            Server.InitializeRace(Context.Player);
        });

        [Command("join", "Join the race if any")]
        [Permission(MyPromoteLevel.None)]
        public void JoinRace() => this.CatchAndReport(() =>
        {
            Server.JoinRace(Context.Player);
        });

        [Command("start", "Start the race in N seconds")]
        [Permission(MyPromoteLevel.None)]
        public void StartRace(int countdown = 5) => this.CatchAndReport(() =>
        {
            Server.StartRace(Context.Player, countdown);
        });

        [Command("end", "End the race if any")]
        [Permission(MyPromoteLevel.None)]
        public void EndRace() => this.CatchAndReport(() =>
        {
            Server.EndRace(Context.Player);
        });

        [Command("cancel", "Cancel the race if any")]
        [Permission(MyPromoteLevel.None)]
        public void CancelRace() => this.CatchAndReport(() =>
        {
            Server.CancelRace(Context.Player);
        });
    }
}