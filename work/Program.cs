using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using SeleniumUndetectedChromeDriver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

class Program
{
	static readonly string DriverPath = @"C:\chromedriver.exe";
	static readonly string CookiesFolder = "cookies";

	static void Main()
	{
		Console.Write("Имя файла cookies (без _marktplaats_cookies.json): ");
		string loginName = Console.ReadLine();
		string cookiePath = Path.Combine(CookiesFolder, $"{loginName}_marktplaats_cookies.json");

		Console.Write("Введите ссылку на объявление: ");
		string advertUrl = Console.ReadLine();

		Console.Write("Введите текст сообщения: ");
		string messageText = Console.ReadLine();

		if (!File.Exists(cookiePath))
		{
			Console.WriteLine("❌ Файл cookies не найден.");
			return;
		}

		var chromeArgs = new ChromeOptions();
		chromeArgs.AddArguments("--disable-blink-features=AutomationControlled");
		chromeArgs.AddArguments("--start-maximized");

		using var driver = UndetectedChromeDriver.Create(driverExecutablePath: DriverPath, options: chromeArgs);

		// 1. Перейти на Marktplaats и загрузить cookies
		driver.GoToUrl("https://www.marktplaats.nl/");
		driver.Manage().Cookies.DeleteAllCookies();

		var cookies = LoadCookiesFromFile(cookiePath);
		foreach (var cookie in cookies)
		{
			try
			{
				driver.Manage().Cookies.AddCookie(new Cookie(
					cookie.Name,
					cookie.Value,
					cookie.Domain.StartsWith(".") ? cookie.Domain.Substring(1) : cookie.Domain,
					cookie.Path,
					cookie.Expiry
				));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Ошибка добавления куки {cookie.Name}: {ex.Message}");
			}
		}

		// 2. Переходим на страницу объявления
		driver.Navigate().GoToUrl(advertUrl);

		var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));

		// 3. Нажать на кнопку "Bericht"
		try
		{
			var berichtBtn = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//button[contains(@class, 'SellerContactOptions-button')]//span[contains(text(), 'Bericht')]/..")));
			berichtBtn.Click();
			Console.WriteLine("✅ Кнопка 'Bericht' нажата.");
		}
		catch (Exception ex)
		{
			Console.WriteLine("❌ Не удалось нажать кнопку 'Bericht': " + ex.Message);
			return;
		}
		// 4. Вводим текст сообщения
		try
		{
			var textarea = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//textarea")));
			textarea.Clear();
			textarea.SendKeys(messageText);
			Console.WriteLine("✍️ Сообщение введено.");
		}
		catch (Exception ex)
		{
			Console.WriteLine("❌ Не удалось ввести сообщение: " + ex.Message);
			return;
		}

		try
		{
			// Подождём, пока кнопка появится
			var sendBtn = wait.Until(ExpectedConditions.ElementExists(
				By.XPath("//button[contains(@class, 'hz-Button--primary') and normalize-space()='Stuur bericht']")));

			// Убедимся, что она видима и кликабельна
			wait.Until(ExpectedConditions.ElementToBeClickable(sendBtn));

			// Прокрутка до кнопки
			((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", sendBtn);
			Thread.Sleep(500);

			// Попробуем нажать через JavaScript (надёжнее)
			((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", sendBtn);
			Console.WriteLine("✅ Сообщение отправлено (через JS).");
		}
		catch (Exception ex)
		{
			Console.WriteLine("❌ Не удалось нажать 'Stuur bericht': " + ex.Message);
		}

		Console.WriteLine("Нажмите Enter для выхода...");
		Console.ReadLine();
	}

	class SerializableCookie
	{
		public string Name { get; set; }
		public string Value { get; set; }
		public string Domain { get; set; }
		public string Path { get; set; }
		public DateTime? Expiry { get; set; }
		public bool Secure { get; set; }
		public bool IsHttpOnly { get; set; }
	}

	static List<SerializableCookie> LoadCookiesFromFile(string path)
	{
		var json = File.ReadAllText(path);
		return JsonConvert.DeserializeObject<List<SerializableCookie>>(json);
	}
}
