using GetText;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace YourPluginNamespace
{
    [ApiVersion(2, 1)]
    public class InactivePlayerChatKicke : TerrariaPlugin
    {
        public override string Name => "InactivePlayerChatKicke";
        public override string Author => "肝帝熙恩";
        public override string Description => "踢掉未发active就发消息的玩家，临时插件，之后会被恋恋插件涵盖";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public InactivePlayerChatKicke(Main game) : base(game)
        {
            // 服务器启动时执行的代码可以放在这里
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerChat.Register(this, OnPlayerChat);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 取消注册OnInitialize和OnPlayerChat事件处理器
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerChat.Deregister(this, OnPlayerChat);

                // 插件卸载时需要执行的其他清理工作可以放在这里
                // ...
            }
            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs args)
        {
            // 注册命令 
            Commands.ChatCommands.Add(new Command("listconnected", ListConnected, "listconnected", "ld")
            {
                HelpText = "Lists all connected players."
            });

            Commands.ChatCommands.Add(new Command("yourplugin.kick", Kick2, "kick2")
            {
                HelpText = "Kicks a player from the server."
            });
        }
        public static class I18n
        {
            private static Func<FormattableString, string> GetStringFunc = typeof(TShock).Assembly.GetType("TShockAPI.I18n")!.GetMethod(nameof(Catalog.GetString), new Type[] { typeof(FormattableString) })!.CreateDelegate<Func<FormattableString, string>>();
            private static Func<FormattableStringAdapter, object[], string> GetStringParamFunc = typeof(TShock).Assembly.GetType("TShockAPI.I18n")!.GetMethod(nameof(Catalog.GetString), new Type[] { typeof(FormattableStringAdapter), typeof(object[]) })!.CreateDelegate<Func<FormattableStringAdapter, object[], string>>();
            public static string GetString(FormattableString text) => GetStringFunc(text);
            public static string GetString(FormattableStringAdapter text, params object[] args) => GetStringParamFunc(text, args);
        }
        private void OnPlayerChat(ServerChatEventArgs e)
        {
            // 获取发送消息的玩家索引
            int sender = e.Who;

            // 使用 FindByNameOrID 方法获取发送消息的 TSPlayer 对象
            var players = FindByNameOrID(sender.ToString());
            if (players.Count == 0)
            {
                return;
            }
            var player = players[0];

            if (!player.Active && player.ConnectionAlive)
            {
                // 构造模拟的命令字符串和参数数组
                string kickCommand = $"/kick2 {player.Index}";
                string[] kickParametersArray = { $"tsi:{player.Index}" };

                // 将参数数组转换为 List<string>
                List<string> kickParameters = kickParametersArray.ToList();

                // 创建 CommandArgs 对象
                CommandArgs kickArgs = new CommandArgs(kickCommand, player, kickParameters);

                // 调用 Kick2 方法
                Kick2(kickArgs);
            }
        }
        public static List<TSPlayer> FindByNameOrID(string search)
        {
            var result = new List<TSPlayer>();
            search = search.Trim();
            var isTsi = search.StartsWith("tsi:");
            var isTsn = search.StartsWith("tsn:");
            if (isTsn || isTsi)
            {
                search = search.Remove(0, 4);
            }
            if (string.IsNullOrEmpty(search))
            {
                return result;
            }
            if (byte.TryParse(search, out var index) && index < byte.MaxValue)
            {
                var tsPlayer = TShock.Players[index];
                if (tsPlayer is { Active: true } or { ConnectionAlive: true })
                {
                    if (isTsi)
                    {
                        result.Add(tsPlayer);
                        return result;
                    }
                    result.Add(tsPlayer);
                }
            }
            var players = TShock.Players;
            if (isTsn)
            {
                var query = players.Where(x => x?.Name == search);
                if (query.Any())
                {
                    if (result.Any())
                    {
                        return new() { query.First() };
                    }
                    result.Add(query.First());
                }
            }
            else
            {
                var name = search.ToLower();
                foreach (var tsPlayer in players)
                {
                    if (tsPlayer is not null)
                    {
                        if (tsPlayer.Name.ToLower().StartsWith(name))
                        {
                            result.Add(tsPlayer);
                        }
                    }
                }
            }
            return result;
        }
        public static int GetActiveConnectionCount() => Netplay.Clients.Where(x => x.IsActive).Count();
        // 下面是您提供的方法
        public static void ListConnected(CommandArgs args)
        {
            if (GetActiveConnectionCount() == 0)
            {
                args.Player.SendMessage("当前没有活动连接", Color.White);
                return;
            }
            var players = new List<string>();
            foreach (TSPlayer ply in TShock.Players)
            {
                if (ply is { Active: true } or { ConnectionAlive: true })
                {
                    if (ply.Active)
                    {
                        if (ply.Account is null)
                        {
                            players.Add(I18n.GetString($"{ply.Name} (Index: {ply.Index})"));
                        }
                        else
                        {
                            players.Add(I18n.GetString($"{ply.Name} (Index: {ply.Index}, Account ID: {ply.Account.ID})"));
                        }
                    }
                    else
                    {
                        if (ply.Account is null)
                        {
                            players.Add(I18n.GetString($"{ply.Name} (Index: {ply.Index})") + "(Connected Only)");
                        }
                        else
                        {
                            players.Add(I18n.GetString($"{ply.Name} (Index: {ply.Index}, Account ID: {ply.Account.ID})") + "(Connected Only)");
                        }
                    }
                }
            }
            PaginationTools.SendPage(args.Player, 1, PaginationTools.BuildLinesFromTerms(players), new PaginationTools.Settings() { IncludeHeader = false });
        }
        private static void Kick2(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage(I18n.GetString("Invalid syntax. Proper syntax: {0}kick <player> [reason].", Commands.Specifier));
                return;
            }
            if (args.Parameters[0].Length == 0)
            {
                args.Player.SendErrorMessage(I18n.GetString("A player name must be provided to kick a player. Please provide one."));
                return;
            }
            var list = FindByNameOrID(args.Parameters[0]);
            if (list.Count == 0)
            {
                args.Player.SendErrorMessage(I18n.GetString("Player not found. Unable to kick the player."));
                return;
            }
            if (list.Count > 1)
            {
                args.Player.SendMultipleMatchError(list.Select((TSPlayer p) => p.Name));
                return;
            }
            string reason = ((args.Parameters.Count > 1) ? string.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1)) : I18n.GetString("Misbehaviour."));
            if (list[0].Kick(reason, !args.Player.RealPlayer, silent: false, args.Player.Name))
            {
                Netplay.Clients[list[0].Index].Socket.Close();
            }
            else
            {
                args.Player.SendErrorMessage(I18n.GetString("You can't kick another admin."));
            }
        }

    }
}
