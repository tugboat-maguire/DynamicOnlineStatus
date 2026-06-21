using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Plugins.Interfaces;
using JetBrains.Annotations;
using SteamKit2;

namespace DynamicOnlineStatus;

#pragma warning disable CA1812 // ASF uses this class during runtime
[UsedImplicitly]
internal sealed class DynamicOnlineStatus : IGitHubPluginUpdates, IBotCardsFarmerInfo, IBotModules {
	public string Name => nameof(DynamicOnlineStatus);
	public string RepositoryName => "tugboat-maguire/DynamicOnlineStatus";
	public Version Version => typeof(DynamicOnlineStatus).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	private static readonly ConcurrentDictionary<string, bool> EnabledBots = new();

	public Task OnLoaded() {
		ASF.ArchiLogger.LogGenericInfo($"Hello {Name}!");
		return Task.CompletedTask;
	}

	public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
		if (additionalConfigProperties == null) {
			EnabledBots[bot.BotName] = false;
			return Task.CompletedTask;
		}

		if (additionalConfigProperties.TryGetValue("EnableDynamicStatus", out JsonElement jsonElement) && (jsonElement.ValueKind == JsonValueKind.True)) {
			EnabledBots[bot.BotName] = true;
			bot.ArchiLogger.LogGenericInfo("Dynamic Online Status initialized and ENABLED.");
		} else {
			EnabledBots[bot.BotName] = false;
		}

		return Task.CompletedTask;
	}

	public static Task OnBotFarmingSurpassed(Bot bot) {
		return Task.CompletedTask;
	}

	public Task OnBotFarmingStarted(Bot bot) {
		if (!EnabledBots.GetValueOrDefault(bot.BotName, false)) {
			return Task.CompletedTask;
		}

		bot.ArchiLogger.LogGenericInfo("Started farming. Setting status to Online.");

		_ = Task.Run(() => {
			if (bot.IsConnectedAndLoggedOn) {
				SetPersonaStateSafe(bot, EPersonaState.Online);
			}
		});

		return Task.CompletedTask;
	}

	public Task OnBotFarmingFinished(Bot bot, bool farmedSomething) {
		// Silenced. We rely purely on OnBotFarmingStopped to prevent duplicate network calls.
		return Task.CompletedTask;
	}

	public Task OnBotFarmingStopped(Bot bot) {
		return HandleIdleState(bot, "Stopped");
	}

	private static Task HandleIdleState(Bot bot, string reason) {
		if (!EnabledBots.GetValueOrDefault(bot.BotName, false)) {
			return Task.CompletedTask;
		}

		bot.ArchiLogger.LogGenericInfo($"{reason} farming. Waiting 10 seconds before going Invisible...");

		_ = Task.Run(async () => {
			await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

			bool isFarming = bot.CardsFarmer.NowFarming;

			if (bot.IsConnectedAndLoggedOn && !isFarming) {
				bot.ArchiLogger.LogGenericInfo("Remained idle. Setting status to Invisible.");
				SetPersonaStateSafe(bot, EPersonaState.Invisible);
			} else {
				bot.ArchiLogger.LogGenericInfo("Resumed farming or disconnected during the delay. Aborting state change.");
			}
		});

		return Task.CompletedTask;
	}

	// Safely injects the state change using Reflection to bypass runtime accessibility limits
	private static void SetPersonaStateSafe(Bot bot, EPersonaState state) {
		try {
			// Extract SteamFriends directly from the Bot instance, ignoring visibility limits
			PropertyInfo? steamFriendsProp = typeof(Bot).GetProperty("SteamFriends", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			if (steamFriendsProp?.GetValue(bot) is SteamFriends steamFriends) {
				steamFriends.SetPersonaState(state);
				return;
			}

			// Fallback: Access the SteamClient and extract the handler
			PropertyInfo? steamClientProp = typeof(Bot).GetProperty("SteamClient", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			if (steamClientProp?.GetValue(bot) is SteamClient steamClient) {
				steamClient.GetHandler<SteamFriends>()?.SetPersonaState(state);
			} else {
				bot.ArchiLogger.LogGenericError("DynamicOnlineStatus: Failed to locate SteamKit network handlers via Reflection.");
			}
		} catch (Exception ex) {
			bot.ArchiLogger.LogGenericError($"DynamicOnlineStatus Exception: {ex.Message}");
		}
	}
}
#pragma warning restore CA1812 // ASF uses this class during runtime
