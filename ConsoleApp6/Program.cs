using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

class AutoServiceGame
{
    private int money;
    private Dictionary<string, int> warehouse;
    private List<PurchaseOrder> purchaseOrders;
    private Random random;
    private string connectionString;
    private int gameId;

    public AutoServiceGame(int startMoney, string dbConnectionString)
    {
        connectionString = dbConnectionString;
        money = startMoney;
        warehouse = new Dictionary<string, int>();
        purchaseOrders = new List<PurchaseOrder>();
        random = new Random();

        InitializeGame();
    }

    private void InitializeGame()
    {
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // Создаем новую игровую сессию
            string sql = "INSERT INTO GameSessions (StartMoney, CurrentMoney, CreatedDate) OUTPUT INSERTED.Id VALUES (@StartMoney, @CurrentMoney, GETDATE())";
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@StartMoney", money);
                cmd.Parameters.AddWithValue("@CurrentMoney", money);
                gameId = (int)cmd.ExecuteScalar();
            }

            // Загружаем начальные детали
            sql = "SELECT * FROM Parts WHERE IsActive = 1";
            using (var cmd = new SqlCommand(sql, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string partName = reader["Name"].ToString();
                    int initialQuantity = Convert.ToInt32(reader["InitialQuantity"]);

                    if (initialQuantity > 0)
                    {
                        warehouse[partName] = initialQuantity;

                        // Сохраняем в инвентарь
                        SaveInventory(partName, initialQuantity);
                    }
                }
            }
        }
    }

    public void RunGame()
    {
        Console.WriteLine("=== АВТОСЕРВИС ===");
        Console.WriteLine($"Начальный баланс: {money} руб.");
        Console.WriteLine("Нажмите любую клавишу для начала обслуживания клиентов...");
        Console.ReadKey();

        int clientNumber = 1;

        while (true)
        {
            Console.Clear();
            Console.WriteLine($"=== КЛИЕНТ №{clientNumber} ===");

            // Обрабатываем доставки запчастей
            ProcessDeliveries();

            // Показываем текущее состояние
            ShowStatus();

            // Генерируем поломку
            string brokenPart = GenerateBrokenPart();
            int repairCost = GetPartPrice(brokenPart) + random.Next(200, 800);
            Console.WriteLine($"\nПоломка: {brokenPart}");
            Console.WriteLine($"Стоимость ремонта: {repairCost} руб.");

            // Предлагаем варианты действий
            Console.WriteLine("\nВаши действия:");
            Console.WriteLine("1 - Взять заказ (если есть деталь на складе)");
            Console.WriteLine("2 - Отказать клиенту (штраф 300 руб.)");
            Console.WriteLine("3 - Закупить запчасти");
            Console.WriteLine("4 - Выйти из игры");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    AcceptOrder(brokenPart, repairCost, clientNumber);
                    break;
                case "2":
                    RefuseOrder(clientNumber);
                    break;
                case "3":
                    ShowPurchaseMenu();
                    break;
                case "4":
                    SaveGameState();
                    Console.WriteLine($"Игра завершена! Итоговый баланс: {money} руб.");
                    return;
                default:
                    Console.WriteLine("Неверный выбор! Нажмите любую клавишу для продолжения...");
                    Console.ReadKey();
                    continue;
            }

            clientNumber++;
            Console.WriteLine("Нажмите любую клавишу для следующего клиента...");
            Console.ReadKey();
        }
    }

    private void ProcessDeliveries()
    {
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // Получаем заказы, готовые к доставке
            string sql = "SELECT * FROM PurchaseOrders WHERE GameId = @GameId AND DeliveryCounter <= 0";
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@GameId", gameId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string partName = reader["PartName"].ToString();
                        int quantity = Convert.ToInt32(reader["Quantity"]);
                        int orderId = Convert.ToInt32(reader["Id"]);

                        if (warehouse.ContainsKey(partName))
                            warehouse[partName] += quantity;
                        else
                            warehouse[partName] = quantity;

                        Console.WriteLine($"✓ Доставлены {quantity} {partName}");

                        // Обновляем инвентарь в БД
                        SaveInventory(partName, warehouse[partName]);

                        // Удаляем выполненный заказ
                        DeletePurchaseOrder(orderId);
                    }
                }
            }

            // Уменьшаем счетчик доставки для остальных заказов
            sql = "UPDATE PurchaseOrders SET DeliveryCounter = DeliveryCounter - 1 WHERE GameId = @GameId AND DeliveryCounter > 0";
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@GameId", gameId);
                cmd.ExecuteNonQuery();
            }
        }

        // Обновляем локальный список заказов
        UpdateLocalPurchaseOrders();
    }

    private void ShowStatus()
    {
        Console.WriteLine($"\nБаланс: {money} руб.");
        Console.WriteLine("Склад:");

        if (warehouse.Count == 0)
        {
            Console.WriteLine("  (пусто)");
        }
        else
        {
            foreach (var part in warehouse)
            {
                Console.WriteLine($"  {part.Key}: {part.Value} шт.");
            }
        }

        // Показываем ожидаемые поставки
        ShowPendingOrders();
    }

   