using System;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Data.SqlClient;
using ConsumerPoc.Models;
using ConsumerPoc;

public class Program
{
    // Ajuste aqui sua string de conexão:
    private static string _connectionString = 
        "Server=localhost,1433;Database=PoC_DB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;";

    public static void Main(string[] args)
    {
        // Iniciamos o worker de consolidação
        var worker = new SummarizationWorker(_connectionString);
        worker.Start(); 
        // Ele rodará a cada 5 minutos em background.
        
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: "industria_eventos_queue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Desserializa JSON para o objeto
                var evento = JsonSerializer.Deserialize<IndustriaEvento>(message);

                if (evento != null)
                {
                    // Grava no banco
                    SalvarEventoNoBanco(evento);
                    Console.WriteLine($"[x] Evento salvo: {evento.Id} - {evento.Etapa}");
                }
            }
            catch (Exception ex)
            {
                // Log de erro e possivelmente requeue ou dead-letter
                Console.WriteLine($"[!] Erro ao processar mensagem: {ex.Message}");
            }
        };

        // Inicia consumo
        channel.BasicConsume(
            queue: "industria_eventos_queue",
            autoAck: true,   // para simplificar, sem ack manual
            consumer: consumer);

        Console.WriteLine("Consumer aguardando mensagens. Pressione [enter] para sair.");
        Console.ReadLine();
    }

    private static void SalvarEventoNoBanco(IndustriaEvento evento)
    {
        // Monta colunas desnormalizadas
        var data = evento.DataHoraEvento.Date;
        var hora = evento.DataHoraEvento.Hour;
        var mes = evento.DataHoraEvento.Month;
        var ano = evento.DataHoraEvento.Year;

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // Exemplo de insert simples
        var sql = @"
            INSERT INTO industria_evento 
                (id, origem, processo, canal, etapa, identificadorExterno, status, 
                 operador_id, operador_nome, quantidade_itens, tipo_etapa_no_ciclo, data_hora_evento,
                 data_evento, hora_evento, mes_evento, ano_evento)
            VALUES
                (@id, @origem, @processo, @canal, @etapa, @identificadorExterno, @status,
                 @operador_id, @operador_nome, @quantidade_itens, @tipo_etapa_no_ciclo, @data_hora_evento,
                 @data_evento, @hora_evento, @mes_evento, @ano_evento);";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", evento.Id);
        cmd.Parameters.AddWithValue("@origem", evento.Origem);
        cmd.Parameters.AddWithValue("@processo", evento.Processo);
        cmd.Parameters.AddWithValue("@canal", evento.Canal);
        cmd.Parameters.AddWithValue("@etapa", evento.Etapa);
        cmd.Parameters.AddWithValue("@identificadorExterno", evento.IdentificadorExterno);
        cmd.Parameters.AddWithValue("@status", evento.Status);
        cmd.Parameters.AddWithValue("@operador_id", evento.Operador.Id);
        cmd.Parameters.AddWithValue("@operador_nome", evento.Operador.Nome);
        cmd.Parameters.AddWithValue("@quantidade_itens", evento.QuantidadeItens);
        cmd.Parameters.AddWithValue("@tipo_etapa_no_ciclo", evento.TipoEtapaNoCliclo);
        cmd.Parameters.AddWithValue("@data_hora_evento", evento.DataHoraEvento);

        cmd.Parameters.AddWithValue("@data_evento", data);
        cmd.Parameters.AddWithValue("@hora_evento", hora);
        cmd.Parameters.AddWithValue("@mes_evento", mes);
        cmd.Parameters.AddWithValue("@ano_evento", ano);

        cmd.ExecuteNonQuery();
    }
}
