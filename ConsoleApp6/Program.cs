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
        purchaseOrders = new List<PurchaseOrder>(;
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

   